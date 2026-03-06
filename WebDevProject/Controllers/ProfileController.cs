using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<Users> _userManager;

        public ProfileController(UserManager<Users> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn", "Account");
            }

            // Populate the view model with current user data (this creates the placeholders!)
            var model = new EditProfileViewModel
            {
                CurrentProfilePictureUrl = user.ProfilePictureUrl,
                UserName = user.DisplayName,
                Email = user.Email ?? string.Empty,
                DateOfBirth = user.DateOfBirth,
                UserGender = user.UserGender
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

                if (displayNameExists != null)
                {
                    ModelState.AddModelError(nameof(model.UserName), "This username is already in use.");
                    return View(model);
                }

                // Update user fields
                user.DisplayName = model.UserName;
                user.NormalizedDisplayName = model.UserName.ToUpper();
                user.DateOfBirth = model.DateOfBirth;
                user.UserGender = model.UserGender;
                user.Email = model.Email;
                user.NormalizedEmail = model.Email.ToUpper();
                user.UserName = model.Email;
                user.NormalizedUserName = model.Email.ToUpper();

                // Update user profile picture
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var sanitizedFileName = Path.GetFileName(model.ProfileImage.FileName);
                    var uniqueFileName = $"{user.Id}_{Guid.NewGuid()}_{sanitizedFileName}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(fileStream);
                    }

                    user.ProfilePictureUrl = $"/uploads/profiles/{uniqueFileName}";
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
    }
}
