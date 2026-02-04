using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SeifDigital.Filters
{
    public class Require2FAAttribute : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var http = context.HttpContext;

            // Permitem acces liber la Auth (unde e pagina de 2FA) + resurse statice
            var path = http.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.StartsWith("/auth") || path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib"))
                return;

            // Dacă nu e autentificat (Windows auth n-ar trebui să fie null, dar e ok ca safety)
            if (http.User?.Identity?.IsAuthenticated != true)
            {
                context.Result = new RedirectToRouteResult(new { controller = "Auth", action = "Verificare2FA" });
                return;
            }

            // REGULA IMPORTANTĂ: dacă 2FA nu e validat, blocăm orice pagină
            var ok2fa = http.Session.GetString("Status2FA");
            if (ok2fa != "Validat")
            {
                context.Result = new RedirectToRouteResult(new { controller = "Auth", action = "Verificare2FA" });
                return;
            }
        }
    }
}
