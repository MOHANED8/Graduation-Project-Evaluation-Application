using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using SoftwareProject.Models;

namespace SoftwareProject.Controllers
{
    public class ProjectController : Controller
    {
        private readonly FirestoreDb db;

        public ProjectController(FirestoreDb firestoreDb)
        {
            db = firestoreDb;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId)) return RedirectToAction("Login", "Login");

            var projectsSnapshot = await db
                .Collection("evaluation-project")
                .Document("ProjectList")
                .Collection("Projects")
                .GetSnapshotAsync();

            var projects = new List<ProjectModel>();
            bool isAlreadyEnrolled = false;

            foreach (var doc in projectsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                var status = data.ContainsKey("Status") ? data["Status"]?.ToString() : "Pending";
                if (status != "Approved") continue;
                var enrolledList = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();
                var requestedList = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>();

                if (enrolledList.Contains(studentId))
                    isAlreadyEnrolled = true;

                var model = new ProjectModel
                {
                    Id = doc.Id,
                    Title = data["title"]?.ToString() ?? "",
                    Description = data["description"]?.ToString() ?? "",
                    ProfessorEmail = data["professorEmail"]?.ToString() ?? "",
                    TotalSlots = Convert.ToInt32(data["totalSlots"]),
                    Enrolled = enrolledList,
                    Requested = requestedList
                };
                projects.Add(model);
            }

            ViewBag.StudentId = studentId;
            ViewBag.IsAlreadyEnrolled = isAlreadyEnrolled;
            return View(projects);
        }

        [HttpPost]
        public async Task<IActionResult> Apply(string projectId)
        {
            var studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId)) return RedirectToAction("Login", "Login");

            try
            {
                // Check all projects for existing enrollment
                var projectsRef = db.Collection("evaluation-project").Document("ProjectList").Collection("Projects");
                var projectsSnapshot = await projectsRef.GetSnapshotAsync();

                foreach (var doc in projectsSnapshot.Documents)
                {
                    var data = doc.ToDictionary();
                    if (data.ContainsKey("enrolled") && ((List<object>)data["enrolled"]).Select(x => x.ToString()).Contains(studentId))
                    {
                        TempData["Error"] = "You are already enrolled in a project.";
                        return RedirectToAction("Index");
                    }
                }

                // Check and update the target project
                var projectRef = projectsRef.Document(projectId);
                var snapshot = await projectRef.GetSnapshotAsync();
                var projectData = snapshot.ToDictionary();

                var requested = projectData.ContainsKey("requested")
                    ? ((List<object>)projectData["requested"]).Select(x => x.ToString()).ToList()
                    : new List<string>();

                if (requested.Contains(studentId))
                {
                    TempData["Error"] = "You have already requested this project.";
                }
                else
                {
                    requested.Add(studentId);
                    await projectRef.UpdateAsync("requested", requested);
                    TempData["Success"] = "Request sent to the teacher.";
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred. Please try again.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Create()
        {
            var teacherEmail = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(teacherEmail)) return RedirectToAction("Login", "Login");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProjectModel model)
        {
            var teacherEmail = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(teacherEmail)) return RedirectToAction("Login", "Login");

            var newProject = new Dictionary<string, object>
            {
                { "title", model.Title },
                { "description", model.Description },
                { "professorEmail", teacherEmail },
                { "totalSlots", model.TotalSlots },
                { "enrolled", new List<string>() },
                { "requested", new List<string>() }
            };

            await db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").AddAsync(newProject);
            TempData["Success"] = "Project created!";
            return RedirectToAction("Dashboard", "Teacher");
        }

        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            var teacherEmail = HttpContext.Session.GetString("TeacherId");
            if (string.IsNullOrEmpty(teacherEmail)) return RedirectToAction("Login", "Login");

            var snapshot = await db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();
            var projects = new List<ProjectModel>();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                if (data.ContainsKey("professorEmail") && data["professorEmail"].ToString() == teacherEmail)
                {
                    projects.Add(new ProjectModel
                    {
                        Id = doc.Id,
                        Title = data["title"]?.ToString() ?? "",
                        Description = data["description"]?.ToString() ?? "",
                        ProfessorEmail = teacherEmail,
                        TotalSlots = Convert.ToInt32(data["totalSlots"]),
                        Enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>(),
                        Requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>()
                    });
                }
            }

            return View(projects);
        }

        [HttpPost]
        public async Task<IActionResult> EditProject(string projectId, string newDescription, int newSlots)
        {
            var projectRef = db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(projectId);
            var updates = new Dictionary<string, object>
            {
                { "description", newDescription },
                { "totalSlots", newSlots }
            };

            await projectRef.UpdateAsync(updates);
            TempData["Success"] = "Project updated.";
            return RedirectToAction("Manage");
        }

        [HttpPost]
        public async Task<IActionResult> ApproveStudent(string projectId, string studentId)
        {
            var projectRef = db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(projectId);
            var snapshot = await projectRef.GetSnapshotAsync();
            if (!snapshot.Exists)
            {
                TempData["Error"] = "Project not found. It may have been deleted.";
                return RedirectToAction("Manage");
            }
            var data = snapshot.ToDictionary();

            var enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();
            var requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>();

            // Check if student already enrolled elsewhere
            var allProjects = await db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();
            bool isAlreadyEnrolled = allProjects.Documents.Any(doc =>
            {
                var docData = doc.ToDictionary();
                var enrolledList = docData.ContainsKey("enrolled") ? ((List<object>)docData["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();
                return enrolledList.Contains(studentId);
            });

            if (isAlreadyEnrolled)
            {
                // Auto-remove the student from the requested list
                if (requested.Contains(studentId))
                {
                    requested.Remove(studentId);
                    await projectRef.UpdateAsync("requested", requested);
                }

                TempData["Error"] = "Student already enrolled in another project. Automatically removed from this request.";
                return RedirectToAction("Manage");
            }

            // Check if slots are available
            int totalSlots = data.ContainsKey("totalSlots") ? Convert.ToInt32(data["totalSlots"]) : 0;
            if (enrolled.Count >= totalSlots)
            {
                TempData["Error"] = "This project has no available slots.";
                return RedirectToAction("Manage");
            }

            // Approve the student
            enrolled.Add(studentId);
            requested.Remove(studentId);

            await projectRef.UpdateAsync("enrolled", enrolled);
            await projectRef.UpdateAsync("requested", requested);

            TempData["Success"] = "Student approved successfully.";
            return RedirectToAction("Manage");
        }

        [HttpPost]
        public async Task<IActionResult> RejectStudent(string projectId, string studentId)
        {
            var projectRef = db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(projectId);
            var snapshot = await projectRef.GetSnapshotAsync();

            var data = snapshot.ToDictionary();
            var requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>();

            if (requested.Contains(studentId))
            {
                requested.Remove(studentId);
                await projectRef.UpdateAsync("requested", requested);
                TempData["Success"] = "Student request removed.";
            }

            return RedirectToAction("Manage");
        }
    }
}
