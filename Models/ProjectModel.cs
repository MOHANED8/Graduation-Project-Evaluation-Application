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

        // 🔹 Indicates if the project is archived
        public bool IsArchived { get; set; }

        // 🔹 Indicates if the project is approved
        public bool IsApproved { get; set; }

        // 🔹 Date and time when the project was created
        public DateTime CreatedAt { get; set; }

        // 🔹 Date and time when the project was last updated
        public DateTime? UpdatedAt { get; set; }

        // 🔹 Status of the project (e.g., Pending, Active, Completed)
        public string Status { get; set; }

        // 🔹 Progress of the project (e.g., percent complete)
        public int Progress { get; set; }

        // 🔹 Name (for compatibility with some admin code)
        public string Name { get; set; }
    }
}
