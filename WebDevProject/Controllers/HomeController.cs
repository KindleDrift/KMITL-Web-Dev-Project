using Microsoft.AspNetCore.Diagnostics;
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
            var originalPath = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
                ?? HttpContext.Request.Path.Value
                ?? string.Empty;

            if (statusCode == 404 || ((statusCode == 401 || statusCode == 403) && originalPath.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase)))
            {
                Response.StatusCode = 404;
                return View("Error404", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
            }

            Response.StatusCode = statusCode;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
