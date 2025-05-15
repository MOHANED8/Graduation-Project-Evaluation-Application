using System;

namespace SoftwareProject.Models
{
    public class StudentModel
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public string Department { get; set; }
        public string Phone { get; set; }

        // 🔹 Date and time when the student was created
        public DateTime? CreatedAt { get; set; }
    }
} 