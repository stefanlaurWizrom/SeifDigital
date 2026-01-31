using System.DirectoryServices.AccountManagement;

namespace SeifDigital.Utils
{
    public static class AdUserHelper
    {
        // returnează emailul din AD pentru userul Windows (DOMAIN\user)
        public static string? GetEmailFromDomainUser(string? domainUser)
        {
            if (string.IsNullOrWhiteSpace(domainUser))
                return null;

            // Acceptă atât DOMAIN\user cât și user@domain
            string identity = domainUser;

            try
            {
                using var context = new PrincipalContext(ContextType.Domain);
                var userPrincipal = UserPrincipal.FindByIdentity(context, identity);

                // Asta corespunde câmpului "E-mail" din AD Users and Computers (de obicei atributul "mail")
                return userPrincipal?.EmailAddress;
            }
            catch
            {
                return null;
            }
        }
    }
}
