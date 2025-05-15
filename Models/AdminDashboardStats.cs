namespace SoftwareProject.Models
{
    public class AdminDashboardStats
    {
        public AdminDashboardStats()
        {
            SystemVersion = "1.0.0"; // Default version
            TotalStudents = 0;
            TotalTeachers = 0;
            TotalProjects = 0;
            ActiveProjects = 0;
            ActiveStudents = 0;
            ActiveTeachers = 0;
            PendingProjects = 0;
            CompletedProjects = 0;
            LastBackup = DateTime.UtcNow;
            LastUpdated = DateTime.UtcNow;
            DatabaseStatus = false;
            FirebaseConnection = false;
        }

        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int ActiveStudents { get; set; }
        public int ActiveTeachers { get; set; }
        public int PendingProjects { get; set; }
        public int CompletedProjects { get; set; }
        public DateTime LastBackup { get; set; }
        public required string SystemVersion { get; set; }
        public bool DatabaseStatus { get; set; }
        public bool FirebaseConnection { get; set; }
        public DateTime LastUpdated { get; set; }
    }
} 