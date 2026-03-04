using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Users> _signInManager;
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(SignInManager<Users> signInManager, UserManager<Users> userManager, RoleManager<IdentityRole> roleManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult SignIn()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToAction("Index", "Profile");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignIn(SignInViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Profile");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Wrong email or password.");
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToAction("Index", "Profile");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(SignUpViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Trim whitespace from DisplayName
                model.DisplayName = model.DisplayName?.Trim() ?? string.Empty;

                // Validate DisplayName is not empty after trimming
                if (string.IsNullOrWhiteSpace(model.DisplayName))
                {
                    ModelState.AddModelError(nameof(model.DisplayName), "Username cannot be empty or contain only spaces.");
                    return View(model);
                }

                var displayNameExists = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.NormalizedDisplayName == model.DisplayName.ToUpper());

                if (displayNameExists != null)
                {
                    ModelState.AddModelError(nameof(model.DisplayName), "This username is already in use.");
                    return View(model);
                }

                var user = new Users
                {
                    DisplayName = model.DisplayName,
                    NormalizedDisplayName = model.DisplayName.ToUpper(),
                    UserName = model.Email,
                    NormalizedUserName = model.Email.ToUpper(),
                    Email = model.Email,
                    NormalizedEmail = model.Email.ToUpper(),
                    EmailConfirmed = false,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.Now,
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    var roleExists = await _roleManager.RoleExistsAsync("User");
                    if (roleExists) {
                        await _userManager.AddToRoleAsync(user, "User");
                    }
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Onboarding");
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
        public async Task<IActionResult> Onboarding()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn");
            }

            if (user.HasCompletedOnboarding)
            {
                return RedirectToAction("Index", "Profile");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onboarding(OnboardingViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("SignIn");
            }

            if (model.SkipOnboarding)
            {
                user.HasCompletedOnboarding = true;
                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Profile");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            else if (ModelState.IsValid)
            {
                user.DateOfBirth = model.DateOfBirth;
                user.UserGender = model.UserGender;

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

                user.HasCompletedOnboarding = true;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Profile");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    if (!string.IsNullOrEmpty(error.ErrorMessage))
                    {
                        ModelState.AddModelError(string.Empty, error.ErrorMessage);
                    }
                }
            }

            return View(model);
        }

        // Placeholder for forgot password functionality, if needed in the future.
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public new async Task<IActionResult> SignOut()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("SignIn");
        }

        // API endpoint to check if a display name already exists for AJAX calls
        [HttpGet]
        public async Task<IActionResult> CheckDisplayNameExist(string displayname)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.DisplayName == displayname);
            return Json(user != null);
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailExist(string email)
        {
            // Not a mistake here, because the email is also stored in username.
            var user = await _userManager.FindByNameAsync(email);
            return Json(user != null);
        }
    }
}
