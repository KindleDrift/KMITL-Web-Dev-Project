using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using WebDevProject.Models;

namespace WebDevProject.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireOnboardingAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<Users>>();
            var user = await userManager.GetUserAsync(context.HttpContext.User);

            if (user != null && !user.HasCompletedOnboarding)
            {
                var controller = context.RouteData.Values["controller"]?.ToString();
                var action = context.RouteData.Values["action"]?.ToString();

                if (controller != "Account" || action != "Onboarding")
                {
                    context.Result = new RedirectToActionResult("Onboarding", "Account", null);
                    return;
                }
            }

            await next();
        }
    }
}
