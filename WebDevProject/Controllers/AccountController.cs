using Microsoft.AspNetCore.Mvc;

namespace WebDevProject.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Signin()
        {
            return View();
        }
    }
}
