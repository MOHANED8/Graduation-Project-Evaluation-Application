using System;

namespace SoftwareProject.Models
{
    public class AdminUserModel
    {
        public required string Id { get; set; }
        public required string Email { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public required string Role { get; set; }
        public bool IsActive { get; set; } = true;
        public required string Department { get; set; }
        public required string Phone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 