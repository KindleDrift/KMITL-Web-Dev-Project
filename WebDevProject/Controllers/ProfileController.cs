using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Models;
using WebDevProject.Filters;
using WebDevProject.Services;

namespace WebDevProject.Controllers
{
    [RequireOnboarding]
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly SignInManager<Users> _signInManager;
        private readonly ProfileImageService _profileImageService;

        public ProfileController(
            UserManager<Users> userManager, 
            SignInManager<Users> signInManager,
            ProfileImageService profileImageService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _profileImageService = profileImageService;
        }

        [HttpGet("/Profile")]
        public async Task<IActionResult> Index()
        {
            var signedInUser = await _userManager.GetUserAsync(User);
            if (signedInUser == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            return RedirectToAction(nameof(Details), new { displayName = signedInUser.DisplayName });
        }

        [HttpGet("/Profile/u/{displayName}")]
        public async Task<IActionResult> Details(string displayName)
        {
            var normalizedDisplayName = displayName.Trim().ToUpperInvariant();

            var userToView = await _userManager.Users
                .Include(u => u.AuthoredBoards)
                .Include(u => u.BoardParticipations)
                .FirstOrDefaultAsync(u => u.NormalizedDisplayName == normalizedDisplayName);

            if (userToView == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            bool isOwnProfile = currentUser != null && currentUser.Id == userToView.Id;

            var model = new UserProfileViewModel
            {
                UserId = userToView.Id,
                DisplayName = userToView.DisplayName,
                Bio = userToView.Bio,
                ProfilePictureUrl = userToView.ProfilePictureUrl,
                CreatedAt = userToView.CreatedAt,
                IsOwnProfile = isOwnProfile,
                BoardsCreatedCount = userToView.AuthoredBoards?.Count ?? 0,
                BoardParticipationsCount = userToView.BoardParticipations?.Count ?? 0,
                UserGender = userToView.UserGender,
                Email = isOwnProfile ? userToView.Email : null,
                DateOfBirth = isOwnProfile ? userToView.DateOfBirth : null,
            };

            return View("Index", model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            var model = new EditProfileViewModel
            {
                CurrentProfilePictureUrl = user.ProfilePictureUrl,
                UserName = user.DisplayName,
                DateOfBirth = user.DateOfBirth,
                UserGender = user.UserGender,
                Bio = user.Bio
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return RedirectToAction("SignIn", "Account");
                }

                // Trim whitespace from DisplayName
                model.UserName = model.UserName?.Trim() ?? string.Empty;

                // Validate DisplayName is not empty after trimming
                if (string.IsNullOrWhiteSpace(model.UserName))
                {
                    ModelState.AddModelError(nameof(model.UserName), "Username cannot be empty or contain only spaces.");
                    return View(model);
                }

                var displayNameExists = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.NormalizedDisplayName == model.UserName.ToUpper());

                if (displayNameExists != null && displayNameExists.Id != user.Id)
                {
                    ModelState.AddModelError(nameof(model.UserName), "This username is already in use.");
                    return View(model);
                }

                // Update user fields
                user.DisplayName = model.UserName;
                user.NormalizedDisplayName = model.UserName.ToUpper();
                user.DateOfBirth = model.DateOfBirth;
                user.UserGender = model.UserGender;
                user.Bio = model.Bio;

                // Update user profile picture
                var profileImageResult = await _profileImageService.SaveProfileImageAsync(model.ProfileImage, user.Id, user.ProfilePictureUrl);
                user.ProfilePictureUrl = profileImageResult.ImageUrl;
                if (!profileImageResult.Success)
                {
                    ModelState.AddModelError(nameof(model.ProfileImage), profileImageResult.ErrorMessage!);
                    return View(model);
                }

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Profile");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        // if the email is in use it will return both "Email '...' is already taken." and "Username '...' is already taken.", so if-else catches is needed.
                        if (error.Code == "DuplicateUserName" && error.Description.Contains("is already taken."))
                        {
                            // skip
                        }
                        else if (error.Code != "DuplicateUserName")
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Your password has been changed successfully.";
                return RedirectToAction("Index", "Profile");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
