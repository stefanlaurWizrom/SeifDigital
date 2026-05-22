using SeifDigital.Data;

namespace SeifDigital.Services
{
    /// <summary>
    /// Service pentru gestiunea setarilor certificate alert
    /// </summary>
    public class CertificateSettingsService
    {
        private readonly SettingsService _settings;

        public CertificateSettingsService(SettingsService settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Obtine ora verificarii (0-23)
        /// </summary>
        public async Task<int> GetVerificationHourAsync()
        {
            return await _settings.GetIntAsync("CertVerificationHour", 8);
        }

        /// <summary>
        /// Seteaza ora verificarii
        /// </summary>
        public async Task SetVerificationHourAsync(int hour)
        {
            if (hour < 0 || hour > 23)
                throw new ArgumentException("Hour must be between 0 and 23");

            await _settings.SetIntAsync("CertVerificationHour", hour);
        }

        /// <summary>
        /// Obtine minuta verificarii (0-59)
        /// </summary>
        public async Task<int> GetVerificationMinuteAsync()
        {
            return await _settings.GetIntAsync("CertVerificationMinute", 0);
        }

        /// <summary>
        /// Seteaza minuta verificarii
        /// </summary>
        public async Task SetVerificationMinuteAsync(int minute)
        {
            if (minute < 0 || minute > 59)
                throw new ArgumentException("Minute must be between 0 and 59");

            await _settings.SetIntAsync("CertVerificationMinute", minute);
        }

        /// <summary>
        /// Obtine timezone-ul (IANA format)
        /// </summary>
        public async Task<string> GetTimezoneAsync()
        {
            var tz = await _settings.GetStringAsync("CertVerificationTimezone", "Europe/Bucharest");
            return tz ?? "Europe/Bucharest";
        }

        /// <summary>
        /// Seteaza timezone-ul
        /// </summary>
        public async Task SetTimezoneAsync(string timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
                throw new ArgumentException("Timezone cannot be empty");

            // Validate timezone exists
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch
            {
                throw new ArgumentException($"Invalid timezone: {timezone}");
            }

            await _settings.SetStringAsync("CertVerificationTimezone", timezone);
        }

        /// <summary>
        /// Obtine pragul de alerta (zile)
        /// </summary>
        public async Task<int> GetAlertDaysThresholdAsync()
        {
            return await _settings.GetIntAsync("CertAlertDaysThreshold", 15);
        }

        /// <summary>
        /// Seteaza pragul de alerta
        /// </summary>
        public async Task SetAlertDaysThresholdAsync(int days)
        {
            if (days < 1)
                throw new ArgumentException("Alert threshold must be at least 1 day");

            await _settings.SetIntAsync("CertAlertDaysThreshold", days);
        }

        /// <summary>
        /// Obtine email-ul pentru alerte
        /// </summary>
        public async Task<string> GetAlertEmailAsync()
        {
            var email = await _settings.GetStringAsync("CertAlertEmail", "admin@wizrom.ro");
            return email ?? "admin@wizrom.ro";
        }

        /// <summary>
        /// Seteaza email-ul pentru alerte
        /// </summary>
        public async Task SetAlertEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty");

            // Simple email validation
            if (!email.Contains("@") || !email.Contains("."))
                throw new ArgumentException("Invalid email format");

            await _settings.SetStringAsync("CertAlertEmail", email);
        }

        /// <summary>
        /// Obtine toate setarile intr-o clasa DTO
        /// </summary>
        public async Task<CertificateSettingsViewModel> GetAllSettingsAsync()
        {
            return new CertificateSettingsViewModel
            {
                VerificationHour = await GetVerificationHourAsync(),
                VerificationMinute = await GetVerificationMinuteAsync(),
                Timezone = await GetTimezoneAsync(),
                AlertDaysThreshold = await GetAlertDaysThresholdAsync(),
                AlertEmail = await GetAlertEmailAsync()
            };
        }

        /// <summary>
        /// Seteaza toate setarile
        /// </summary>
        public async Task SaveAllSettingsAsync(CertificateSettingsViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            await SetVerificationHourAsync(model.VerificationHour);
            await SetVerificationMinuteAsync(model.VerificationMinute);
            await SetTimezoneAsync(model.Timezone);
            await SetAlertDaysThresholdAsync(model.AlertDaysThreshold);
            await SetAlertEmailAsync(model.AlertEmail);
        }

        /// <summary>
        /// Returneaza lista cu timezone-uri suportate
        /// </summary>
        public static List<string> GetAvailableTimezones()
        {
            return TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => tz.Id)
                .ToList();
        }
    }

    /// <summary>
    /// DTO pentru Certificate Settings
    /// </summary>
    public class CertificateSettingsViewModel
    {
        /// <summary>
        /// Ora verificarii (0-23, ex: 8 = 8 AM)
        /// </summary>
        public int VerificationHour { get; set; } = 8;

        /// <summary>
        /// Minuta verificarii (0-59, ex: 0, 15, 30, 45)
        /// </summary>
        public int VerificationMinute { get; set; } = 0;

        /// <summary>
        /// Timezone IANA (ex: Europe/Bucharest)
        /// </summary>
        public string Timezone { get; set; } = "Europe/Bucharest";

        /// <summary>
        /// Pragul de alerta in zile (ex: 15)
        /// </summary>
        public int AlertDaysThreshold { get; set; } = 15;

        /// <summary>
        /// Email-ul pentru alerte
        /// </summary>
        public string AlertEmail { get; set; } = string.Empty;
    }
}
