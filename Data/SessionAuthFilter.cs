using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GathalaMFS.Data
{
    public class SessionAuthFilter: IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            if (controller == "Login")
                return;

            if (controller == "Student" && action == "Detail")
                return;

            var user = context.HttpContext.Session.GetString("User");

            if (string.IsNullOrEmpty(user))
            {
                context.Result = new RedirectToActionResult("Index", "Login", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
