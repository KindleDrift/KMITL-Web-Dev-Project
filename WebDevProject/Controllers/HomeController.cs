using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebDevProject.Models;

namespace WebDevProject.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? id)
        {
            var statusCode = id ?? 500;
            var originalPath = HttpContext.Request.Path.Value ?? "";

            // Return 404 view for unauthorized/unauthenticated access to admin routes
            if ((statusCode == 401 || statusCode == 403) && originalPath.StartsWith("/Admin"))
            {
                Response.StatusCode = 404;
                return View("Error404", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }

            // Return normal error page for other cases
            Response.StatusCode = statusCode;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
