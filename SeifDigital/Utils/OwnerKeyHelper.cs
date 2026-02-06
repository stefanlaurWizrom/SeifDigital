using Microsoft.AspNetCore.Http;

namespace SeifDigital.Utils
{
    public static class OwnerKeyHelper
    {
        public static string GetOwnerKey(HttpContext http, string? ignoredWindowsUser = null)
        {
            var k = http.Session.GetString("OwnerKey");
            if (!string.IsNullOrWhiteSpace(k))
                return k.Trim().ToLowerInvariant();

            return "unknown";
        }
    }
}
