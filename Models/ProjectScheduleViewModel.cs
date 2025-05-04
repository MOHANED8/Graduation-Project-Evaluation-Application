namespace SoftwareProject.Models
{
    public class ProjectScheduleViewModel
    {
        public string ProjectId { get; set; }
        public string ProjectTitle { get; set; }  // Match Razor view
        public string Description { get; set; }
        public List<string> EnrolledStudents { get; set; }
    }
}
