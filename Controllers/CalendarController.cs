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

            foreach (var date in newDates)
            {
                if (!existingDates.Contains(date))
                    existingDates.Add(date);
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
        public async Task<IActionResult> RemoveDate([FromBody] string dateToRemove)
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

            // 🔐 In future, check if dateToRemove is assigned, skip deletion if so
            currentDates.Remove(dateToRemove);

            var data = new Dictionary<string, object>
            {
                { "availableDates", currentDates }
            };

            await docRef.SetAsync(data, SetOptions.MergeAll);
            return Ok();
        }
    }
}
