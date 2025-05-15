namespace SoftwareProject.Models
{
    public class SettingsViewModel
    {
        public string SystemName { get; set; }
        public string AcademicYear { get; set; }
        public string Semester { get; set; }
        public string SmtpServer { get; set; }
        public string SmtpPort { get; set; }
        public string SenderEmail { get; set; }
        public string SessionTimeout { get; set; }
        public string BackupFrequency { get; set; }
        public string BackupTime { get; set; }
        public bool EnableAutomaticBackup { get; set; }
        public bool RequireStrongPasswords { get; set; }
        public bool EnableTwoFactorAuthentication { get; set; }
    }
} 