using System;

namespace SoftwareProject.Models
{
    public class MessageModel
    {
        public string Id { get; set; }
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public DateTime SentAt { get; set; }
    }
} 