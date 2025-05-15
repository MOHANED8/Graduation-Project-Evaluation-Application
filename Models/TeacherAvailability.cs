namespace SoftwareProject.Models
{
    public class TeacherAvailability
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string NextAvailableDate { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public List<string> AllAvailableTimes { get; set; }
        public string Id { get; set; } // Teacher's unique ID
    }
} 