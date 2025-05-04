using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using SoftwareProject.Models;

namespace SoftwareProject.Controllers
{
    public class TeacherController : Controller
    {
        private readonly FirestoreDb db;

        public TeacherController(FirestoreDb firestoreDb)
        {
            db = firestoreDb;
        }

        public IActionResult Dashboard()
        {
            var id = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(id))
                return RedirectToAction("Login", "Login");

            ViewBag.TeacherId = id;
            return View();
        }

        [HttpGet]
        public IActionResult CreateProject() => View();

        [HttpPost]
        public async Task<IActionResult> CreateProject(ProjectModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var professorEmail = HttpContext.Session.GetString("TeacherId");

            var newDoc = db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document();

            var data = new Dictionary<string, object>
            {
                { "title", model.Title },
                { "description", model.Description },
                { "totalSlots", model.TotalSlots },
                { "professorEmail", professorEmail },
                { "enrolled", new List<string>() }
            };

            await newDoc.SetAsync(data);
            TempData["Success"] = "Project created successfully!";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> ManageProjects()
        {
            var email = HttpContext.Session.GetString("TeacherId");
            var snapshot = await db.Collection("evaluation-project").Document("ProjectList")
                .Collection("Projects").WhereEqualTo("professorEmail", email).GetSnapshotAsync();

            var projects = new List<ProjectModel>();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                projects.Add(new ProjectModel
                {
                    Id = doc.Id,
                    Title = data["title"].ToString(),
                    Description = data["description"].ToString(),
                    ProfessorEmail = data["professorEmail"].ToString(),
                    TotalSlots = Convert.ToInt32(data["totalSlots"]),
                    Enrolled = ((List<object>)data["enrolled"]).Select(e => e.ToString()).ToList()
                });
            }

            return View(projects);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveStudent(string projectId, string studentId)
        {
            var docRef = db.Collection("evaluation-project").Document("ProjectList")
                .Collection("Projects").Document(projectId);

            var snapshot = await docRef.GetSnapshotAsync();
            if (!snapshot.Exists) return NotFound();

            var data = snapshot.ToDictionary();
            var enrolled = ((List<object>)data["enrolled"]).Select(e => e.ToString()).ToList();

            enrolled.Remove(studentId);
            await docRef.UpdateAsync(new Dictionary<string, object> { { "enrolled", enrolled } });

            return RedirectToAction("ManageProjects");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProject(string projectId)
        {
            await db.Collection("evaluation-project").Document("ProjectList")
                .Collection("Projects").Document(projectId).DeleteAsync();

            return RedirectToAction("ManageProjects");
        }

        // In TeacherController.cs
        [HttpGet]
        public async Task<IActionResult> ReviewProjects()
        {
            var snapshot = await db.Collection("evaluation-project")
                                   .Document("Evaluation")
                                   .Collection("Projects")
                                   .GetSnapshotAsync();

            var reviews = new List<dynamic>();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                reviews.Add(new
                {
                    StudentId = doc.Id,
                    Title = data.GetValueOrDefault("Title", ""),
                    Description = data.GetValueOrDefault("Description", ""),
                    FileUrl = data.GetValueOrDefault("FileUrl", ""),
                    ProfessorEmail = data.GetValueOrDefault("ProfessorEmail", "")
                });
            }

            ViewBag.Reviews = reviews;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> SubmitReview(string studentId, string professorEmail, string feedback)
        {
            var docRef = db.Collection("evaluation-project")
                           .Document("Evaluation")
                           .Collection("results")
                           .Document(studentId);

            var update = new Dictionary<string, object>
    {
        { "text22", feedback },
        { "prefossorEmail", professorEmail }
    };

            await docRef.SetAsync(update, SetOptions.MergeAll);
            return RedirectToAction("ReviewProjects");
        }

    }
}
