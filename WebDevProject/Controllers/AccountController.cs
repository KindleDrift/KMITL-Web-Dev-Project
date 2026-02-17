using Microsoft.AspNetCore.Mvc;

namespace WebDevProject.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Signin()
        {
            return View();
        }

        public IActionResult Signup()
        {
            return View();
        }

        public IActionResult Onboarding()
        {
            return View();
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }
    }
}
