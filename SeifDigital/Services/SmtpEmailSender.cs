using System.Net;
using System.Net.Mail;

namespace SeifDigital.Services
{
    public class SmtpEmailSender
    {
        private readonly IConfiguration _config;

        public SmtpEmailSender(IConfiguration config)
        {
            _config = config;
        }

        public void Send(string to, string subject, string body)
        {
            var host = _config["Smtp:Host"];
            var port = int.Parse(_config["Smtp:Port"] ?? "25");
            var enableSsl = bool.Parse(_config["Smtp:EnableSsl"] ?? "false");
            var from = _config["Smtp:From"];

            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];

            using var msg = new MailMessage(from!, to, subject, body);

            using var smtp = new SmtpClient(host!)
            {
                Port = port,
                EnableSsl = enableSsl
            };

            // dacă Username este setat → folosim autentificare
            if (!string.IsNullOrWhiteSpace(username))
            {
                smtp.Credentials = new NetworkCredential(username, password);
            }
            else
            {
                // relay intern (Windows credentials)
                smtp.UseDefaultCredentials = true;
            }

            smtp.Send(msg);
        }
    }
}
