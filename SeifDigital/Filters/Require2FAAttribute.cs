using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SeifDigital.Filters
{
    public class Require2FAAttribute : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;
            var path = (http.Request.Path.Value ?? "").ToLowerInvariant();

            // Root
            if (path == "/" || string.IsNullOrWhiteSpace(path))
                return;

            // Static & system
            if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") ||
                path.StartsWith("/favicon") || path.StartsWith("/images") ||
                path == "/robots.txt" || path.StartsWith("/.well-known"))
                return;

            // Error pages (IMPORTANT)
            if (path.StartsWith("/error") || path.StartsWith("/home/error"))
                return;

            // Login / 2FA / Logout
            if (path.StartsWith("/account"))
                return;

            // 2FA ok?
            if (http.Session.GetString("Status2FA") == "Validat")
                return;

            // Redirect to login
            context.Result = new RedirectToRouteResult(
                new { controller = "Account", action = "Login" }
            );
        }
    }
}
