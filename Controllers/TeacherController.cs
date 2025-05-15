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
            var role = HttpContext.Session.GetString("Role");
            var id = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(id) || role != "Teacher")
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
                    Enrolled = ((List<object>)data["enrolled"]).Select(e => e.ToString()).ToList(),
                    Status = data.ContainsKey("Status") ? data["Status"].ToString() : "Pending"
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

            // Delete the student's results document
            var resultsDocRef = db.Collection("evaluation-project")
                .Document("Evaluation")
                .Collection("results")
                .Document(studentId);
            await resultsDocRef.DeleteAsync();

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
        public async Task<IActionResult> SubmitReview(string studentId, string professorEmail, string feedback, int marks)
        {
            var docRef = db.Collection("evaluation-project")
                           .Document("Evaluation")
                           .Collection("results")
                           .Document(studentId);

            var update = new Dictionary<string, object>
    {
        { "text22", feedback },
        { "prefossorEmail", professorEmail },
        { "totalGrateToPercent", marks }
    };

            await docRef.SetAsync(update, SetOptions.MergeAll);
            return RedirectToAction("ReviewProjects");
        }

        [HttpPost]
        public async Task<IActionResult> HandleStudentRequest(string projectId, string studentId, string action, string feedback = "")
        {
            var teacherEmail = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(teacherEmail)) return RedirectToAction("Login", "Login");

            var projectRef = db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(projectId);
            var snapshot = await projectRef.GetSnapshotAsync();
            
            if (!snapshot.Exists) return NotFound();

            var data = snapshot.ToDictionary();
            if (data["professorEmail"].ToString() != teacherEmail)
            {
                TempData["Error"] = "You are not authorized to manage this project.";
                return RedirectToAction("ManageProjects");
            }

            var requested = data.ContainsKey("requested") 
                ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() 
                : new List<string>();

            if (!requested.Contains(studentId))
            {
                TempData["Error"] = "Student request not found.";
                return RedirectToAction("ManageProjects");
            }

            if (action.ToLower() == "approve")
            {
                var enrolled = data.ContainsKey("enrolled") 
                    ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() 
                    : new List<string>();

                if (enrolled.Count >= Convert.ToInt32(data["totalSlots"]))
                {
                    TempData["Error"] = "Project is full.";
                    return RedirectToAction("ManageProjects");
                }

                enrolled.Add(studentId);
                requested.Remove(studentId);

                await projectRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "enrolled", enrolled },
                    { "requested", requested }
                });

                // Store feedback in a separate collection
                await db.Collection("evaluation-project")
                    .Document("Feedback")
                    .Collection("ProjectFeedback")
                    .AddAsync(new Dictionary<string, object>
                    {
                        { "projectId", projectId },
                        { "studentId", studentId },
                        { "teacherId", teacherEmail },
                        { "feedback", feedback },
                        { "timestamp", DateTime.UtcNow }
                    });

                TempData["Success"] = "Student approved successfully.";
            }
            else if (action.ToLower() == "reject")
            {
                requested.Remove(studentId);
                await projectRef.UpdateAsync("requested", requested);

                // Store rejection feedback
                await db.Collection("evaluation-project")
                    .Document("Feedback")
                    .Collection("ProjectFeedback")
                    .AddAsync(new Dictionary<string, object>
                    {
                        { "projectId", projectId },
                        { "studentId", studentId },
                        { "teacherId", teacherEmail },
                        { "feedback", feedback },
                        { "timestamp", DateTime.UtcNow }
                    });

                TempData["Success"] = "Student request rejected.";
            }

            return RedirectToAction("ManageProjects");
        }
    }
}
