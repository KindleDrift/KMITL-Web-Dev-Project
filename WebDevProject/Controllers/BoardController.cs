using Microsoft.AspNetCore.Mvc;

namespace WebDevProject.Controllers
{
    public class BoardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Search() { 
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }
    }
}
