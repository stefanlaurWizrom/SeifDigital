using Hangfire.Dashboard;

namespace SeifDigital.Filters
{
    /// <summary>
    /// Restricts Hangfire Dashboard access to admin users only
    /// </summary>
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            
            // Check if user is authenticated and is admin
            var isAdmin = httpContext.Session.GetString("IsAdmin") == "1";
            
            return isAdmin;
        }
    }
}
