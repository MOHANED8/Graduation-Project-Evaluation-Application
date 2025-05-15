using System;

namespace SoftwareProject.Models
{
    public class ActivityLog
    {
        public string Id { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserId { get; set; }
        public string UserEmail { get; set; }

        // 🔹 Admin username or email for admin logs
        public string Admin { get; set; }
    }
} 