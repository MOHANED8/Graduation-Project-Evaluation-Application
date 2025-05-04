namespace SoftwareProject.Models
{
    public class ProjectModel
    {
        // 🔹 Unique identifier for the project
        public string Id { get; set; }

        // 🔹 Title of the project
        public string Title { get; set; }

        // 🔹 Description of the project
        public string Description { get; set; }

        // 🔹 Maximum number of students who can enroll
        public int TotalSlots { get; set; }

        // 🔹 List of student IDs who are already enrolled
        public List<string> Enrolled { get; set; } = new();

        // 🔹 List of student IDs who have requested enrollment
        public List<string> Requested { get; set; } = new();

        // 🔹 Email of the professor who created the project
        public string ProfessorEmail { get; set; }
    }
}
