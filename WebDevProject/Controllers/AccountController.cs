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
                    return RedirectToAction("Index", "Home");
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
                    SecurityStamp = Guid.NewGuid().ToString()
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

        public IActionResult Onboarding()
        {
            return View();
        }

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
