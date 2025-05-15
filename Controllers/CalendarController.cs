using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;

namespace SoftwareProject.Controllers
{
    public class CalendarController : Controller
    {
        private readonly FirestoreDb db;

        public CalendarController(FirestoreDb firestoreDb)
        {
            db = firestoreDb;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string role = HttpContext.Session.GetString("Role");
            string id = role == "Teacher"
                ? HttpContext.Session.GetString("TeacherId")
                : HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            var docRef = db.Collection("evaluation-project")
                           .Document("BookedDates")
                           .Collection("DatesThatBooked")
                           .Document(id);

            var snapshot = await docRef.GetSnapshotAsync();
            List<string> bookedDates = snapshot.Exists && snapshot.ContainsField("availableDates")
                ? snapshot.GetValue<List<string>>("availableDates")
                : new List<string>();

            ViewBag.ExistingDates = bookedDates;
            ViewBag.UserId = id;
            ViewBag.Role = role;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SaveDates([FromBody] List<string> newDates)
        {
            string role = HttpContext.Session.GetString("Role");
            string id = role == "Teacher" ? HttpContext.Session.GetString("TeacherId") : HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(id))
                return Unauthorized();

            var docRef = db.Collection("evaluation-project")
                           .Document("BookedDates")
                           .Collection("DatesThatBooked")
                           .Document(id);

            var snapshot = await docRef.GetSnapshotAsync();
            List<string> existingDates = snapshot.Exists && snapshot.ContainsField("availableDates")
                ? snapshot.GetValue<List<string>>("availableDates")
                : new List<string>();

            foreach (var dateTimeKey in newDates)
            {
                if (!existingDates.Contains(dateTimeKey))
                    existingDates.Add(dateTimeKey);
            }

            var data = new Dictionary<string, object>
            {
                { "availableDates", existingDates },
                { "role", role },
                { "id", id }
            };

            await docRef.SetAsync(data, SetOptions.MergeAll);
            return Ok(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDate([FromBody] string dateTimeKey)
        {
            string role = HttpContext.Session.GetString("Role");
            string id = role == "Teacher" ? HttpContext.Session.GetString("TeacherId") : HttpContext.Session.GetString("Username");

            if (string.IsNullOrEmpty(id)) return Unauthorized();

            var docRef = db.Collection("evaluation-project")
                           .Document("BookedDates")
                           .Collection("DatesThatBooked")
                           .Document(id);

            var snapshot = await docRef.GetSnapshotAsync();

            if (!snapshot.Exists || !snapshot.ContainsField("availableDates"))
                return BadRequest("No available dates found.");

            var currentDates = snapshot.GetValue<List<string>>("availableDates");
            currentDates.Remove(dateTimeKey);

            var data = new Dictionary<string, object>
            {
                { "availableDates", currentDates }
            };

            await docRef.SetAsync(data, SetOptions.MergeAll);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AddAvailability([FromBody] AddAvailabilityRequest req)
        {
            // Get teacher ID from session
            string? teacherId = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(teacherId))
                return Unauthorized();
            if (string.IsNullOrEmpty(req.date) || string.IsNullOrEmpty(req.startTime) || string.IsNullOrEmpty(req.endTime))
                return BadRequest("Missing data");

            // Find the second teacher with closest availability (excluding this teacher)
            var allTeachersSnapshot = await db.Collection("evaluation-project")
                .Document("Professor")
                .Collection("Academician")
                .GetSnapshotAsync();
            string secondTeacherId = null;
            foreach (var doc in allTeachersSnapshot.Documents)
            {
                if (doc.Id != teacherId)
                {
                    secondTeacherId = doc.Id;
                    break;
                }
            }
            bool contained = false;
            if (secondTeacherId != null)
            {
                var secondTeacherDatesDoc = await db.Collection("evaluation-project")
                    .Document("BookedDates")
                    .Collection("DatesThatBooked")
                    .Document(secondTeacherId)
                    .GetSnapshotAsync();
                if (secondTeacherDatesDoc.Exists && secondTeacherDatesDoc.ContainsField("availableDates"))
                {
                    var secondTeacherDates = secondTeacherDatesDoc.GetValue<List<string>>("availableDates") ?? new List<string>();
                    foreach (var slot in secondTeacherDates)
                    {
                        var parts = slot.Split('|');
                        if (parts.Length == 3 && parts[0] == (req.date ?? string.Empty))
                        {
                            // Check for full containment
                            var s1 = TimeSpan.Parse(req.startTime ?? "00:00");
                            var e1 = TimeSpan.Parse(req.endTime ?? "00:00");
                            var s2 = TimeSpan.Parse(parts[1]);
                            var e2 = TimeSpan.Parse(parts[2]);
                            if (s1 >= s2 && e1 <= e2)
                            {
                                contained = true;
                                break;
                            }
                        }
                    }
                }
            }

            var docRef = db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(teacherId);

            var snapshot = await docRef.GetSnapshotAsync();
            List<string> existingDates = snapshot.Exists && snapshot.ContainsField("availableDates")
                ? snapshot.GetValue<List<string>>("availableDates") ?? new List<string>()
                : new List<string>();
            List<string> bookedDates = snapshot.Exists && snapshot.ContainsField("bookedDates")
                ? snapshot.GetValue<List<string>>("bookedDates") ?? new List<string>()
                : new List<string>();

            string newSlot = $"{req.date ?? string.Empty}|{req.startTime ?? string.Empty}|{req.endTime ?? string.Empty}";
            
            // Check if the new slot overlaps with any booked dates
            foreach (var bookedSlot in bookedDates)
            {
                var bookedParts = bookedSlot.Split('|');
                if (bookedParts.Length == 3 && bookedParts[0] == (req.date ?? string.Empty))
                {
                    var newStart = TimeSpan.Parse(req.startTime ?? "00:00");
                    var newEnd = TimeSpan.Parse(req.endTime ?? "00:00");
                    var bookedStart = TimeSpan.Parse(bookedParts[1]);
                    var bookedEnd = TimeSpan.Parse(bookedParts[2]);

                    // Check for any overlap
                    if ((newStart >= bookedStart && newStart < bookedEnd) ||
                        (newEnd > bookedStart && newEnd <= bookedEnd) ||
                        (newStart <= bookedStart && newEnd >= bookedEnd))
                    {
                        return Json(new { error = "This time slot overlaps with a booked meeting." });
                    }
                }
            }

            // Remove any existing slot for the same date
            existingDates = existingDates
                .Where(slot => slot.Split('|')[0] != (req.date ?? string.Empty))
                .ToList();
            if (!existingDates.Contains(newSlot))
                existingDates.Add(newSlot);

            var data = new Dictionary<string, object>
            {
                { "availableDates", existingDates },
                { "bookedDates", bookedDates },
                { "id", teacherId },
                { "role", "Teacher" }
            };
            await docRef.SetAsync(data, SetOptions.MergeAll);
            return Json(new { match = contained });
        }

        public class AddAvailabilityRequest
        {
            public string? date { get; set; }
            public string? startTime { get; set; }
            public string? endTime { get; set; }
        }
    }
}
