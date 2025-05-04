namespace SoftwareProject.Models
{
    public class CalendarViewModel
    {
        public List<Event> Events { get; set; }
    }

    public class Event
    {
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
    }
    public class MyBookedDates
    {
        public string ProfessorId { get; set; }
        public string ProjectId { get; set; }
        public DateTime BookedDate { get; set; }
        public string professoremail { get; set; }
    }

}
