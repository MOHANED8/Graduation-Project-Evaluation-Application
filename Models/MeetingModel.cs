using System;

namespace SoftwareProject.Models
{
    public class MeetingModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ProfessorEmail { get; set; }
        public string StudentEmail { get; set; }
        public DateTime Date { get; set; }
        public string Time { get; set; }
        public string Recurrence { get; set; }
        public DateTime CreatedAt { get; set; }

        // 🔹 Status of the meeting (e.g., Scheduled, Completed, Cancelled)
        public string Status { get; set; }
    }
} 