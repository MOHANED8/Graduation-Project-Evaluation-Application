namespace SoftwareProject.Models
{
    public class ProjectScheduleViewModel
    {
        public string ProjectId { get; set; }
        public string ProjectTitle { get; set; }  // Match Razor view
        public string Description { get; set; }
        public List<string> EnrolledStudents { get; set; }
        public bool IsScheduled { get; set; }
        public string ScheduledDate { get; set; }
        public string SecondaryTeacherId { get; set; }
        public string SecondaryTeacherName { get; set; }
        public string SecondaryTeacherEmail { get; set; }
        public string Status { get; set; } // Meeting status: Scheduled, Completed, Cancelled, etc.
    }
}
