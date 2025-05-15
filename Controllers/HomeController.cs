using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Google.Apis.Storage.v1.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using SoftwareProject.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SoftwareProject.Controllers
{
    public class HomeController : Controller
    {
        private readonly FirestoreDb db;
        private readonly StorageClient _storageClient;
        private readonly string Bucket;

        public HomeController(FirestoreDb firestoreDb, StorageClient storageClient, IConfiguration configuration)
        {
            db = firestoreDb ?? throw new ArgumentNullException(nameof(firestoreDb));
            _storageClient = storageClient ?? throw new ArgumentNullException(nameof(storageClient));
            Bucket = configuration["Firebase:StorageBucket"];

            if (string.IsNullOrEmpty(Bucket))
                throw new ArgumentException("Firebase storage bucket name must be provided in the configuration.");
        }

        public async Task<IActionResult> File()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Login");

            ViewBag.Username = username;

            var projectsSnapshot = await db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();

            foreach (var doc in projectsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                var enrolledList = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();

                if (enrolledList.Contains(username))
                {
                    ViewBag.ProjectId = doc.Id;
                    ViewBag.ProjectTitle = data.ContainsKey("title") ? data["title"]?.ToString() : "N/A";
                    ViewBag.ProfessorEmail = data.ContainsKey("professorEmail") ? data["professorEmail"]?.ToString() : "N/A";
                    return View();
                }
            }

            TempData["Error"] = "You are not enrolled in any project.";
            return RedirectToAction("Index", "Project");
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string description)
        {
            var studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId)) return RedirectToAction("Login", "Login");

            string title = "";
            string professor = "";
            string downloadUrl = "";

            var projectsSnapshot = await db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();

            bool found = false;
            foreach (var doc in projectsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                var enrolledList = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();

                if (enrolledList.Contains(studentId))
                {
                    title = data.ContainsKey("title") ? data["title"]?.ToString() : "";
                    professor = data.ContainsKey("professorEmail") ? data["professorEmail"]?.ToString() : "";
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                TempData["Error"] = "You are not enrolled in any project.";
                return RedirectToAction("File");
            }

            if (file != null && file.Length > 0)
            {
                var fileName = $"{studentId}_{file.FileName}";
                using var fileStream = file.OpenReadStream();

                var storageObject = await _storageClient.UploadObjectAsync(Bucket, fileName, file.ContentType, fileStream);
                storageObject.Acl = new List<ObjectAccessControl>
                {
                    new ObjectAccessControl { Entity = "allUsers", Role = "READER" }
                };
                await _storageClient.UpdateObjectAsync(storageObject);

                downloadUrl = $"https://storage.googleapis.com/{Bucket}/{fileName}";
            }

            var docRef = db.Collection("evaluation-project").Document("Evaluation").Collection("Projects").Document(studentId);

            await docRef.SetAsync(new
            {
                Title = title,
                Description = description,
                FileUrl = downloadUrl,
                ProfessorEmail = professor,
                User = studentId
            }, SetOptions.MergeAll);

            TempData["UploadSuccess"] = "true";
            TempData.Keep("UploadSuccess");

            return RedirectToAction("File");
        }

        public async Task<IActionResult> Results()
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                TempData["ErrorMessage"] = "Session expired. Please log in again.";
                return RedirectToAction("Login", "Login");
            }

            var docRef = db.Collection("evaluation-project").Document("Evaluation").Collection("results").Document(username);
            var snapshot = await docRef.GetSnapshotAsync();

            string professorId = null;
            string professorEmail = null;
            double totalPercent = 0;
            string comment = "";
            if (snapshot.Exists)
            {
                var data = snapshot.ToDictionary();
                // Get projectId from results document
                string projectId = data.ContainsKey("projectId") ? data["projectId"]?.ToString() : null;
                if (!string.IsNullOrEmpty(projectId))
                {
                    // Fetch the project document to get the professor ID
                    var projectDoc = await db.Collection("evaluation-project")
                        .Document("ProjectList")
                        .Collection("Projects")
                        .Document(projectId)
                        .GetSnapshotAsync();
                    if (projectDoc.Exists)
                    {
                        var projectData = projectDoc.ToDictionary();
                        professorId = projectData.ContainsKey("professorEmail") ? projectData["professorEmail"]?.ToString() : null;
                    }
                }
                // Fallback to primaryTeacherEmail if project lookup fails
                if (string.IsNullOrEmpty(professorId))
                    professorId = data.ContainsKey("primaryTeacherEmail") ? data["primaryTeacherEmail"]?.ToString() : null;
                totalPercent = data.ContainsKey("totalGrateToPercent") ? Convert.ToDouble(data["totalGrateToPercent"]) : 0;
                comment = data.ContainsKey("text22") ? data["text22"]?.ToString() : "";

                // Fetch professor email from Academician collection
                if (!string.IsNullOrEmpty(professorId))
                {
                    var profDoc = await db.Collection("evaluation-project")
                                          .Document("Professor")
                                          .Collection("Academician")
                                          .Document(professorId)
                                          .GetSnapshotAsync();
                    if (profDoc.Exists)
                    {
                        var profData = profDoc.ToDictionary();
                        professorEmail = profData.ContainsKey("Mail") && !string.IsNullOrWhiteSpace(profData["Mail"]?.ToString())
                            ? profData["Mail"]?.ToString()
                            : "Not available";
                    }
                    else
                    {
                        professorEmail = "Not available";
                    }
                }
            }

            ViewBag.ProfEm = professorEmail;
            ViewBag.TotalGrateToPercent = totalPercent;
            ViewBag.GenelYorum = comment;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MyProject()
        {
            string studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId))
                return RedirectToAction("Login", "Login");

            var projectSnapshot = await db.Collection("evaluation-project")
                                          .Document("ProjectList")
                                          .Collection("Projects")
                                          .GetSnapshotAsync();

            dynamic projectData = null;
            string professorName = null;
            string professorEmail = null;
            foreach (var doc in projectSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                if (data.ContainsKey("enrolled") && ((List<object>)data["enrolled"]).Contains(studentId))
                {
                    string profId = data.GetValueOrDefault("professorEmail", "N/A").ToString();
                    // Fetch professor info
                    var profDoc = await db.Collection("evaluation-project")
                        .Document("Professor")
                        .Collection("Academician")
                        .Document(profId)
                        .GetSnapshotAsync();
                    if (profDoc.Exists)
                    {
                        var profData = profDoc.ToDictionary();
                        professorName = (profData.ContainsKey("First Name") ? profData["First Name"]?.ToString() : "") +
                                        " " + (profData.ContainsKey("Last Name") ? profData["Last Name"]?.ToString() : "");
                        // Try all possible keys for the email field
                        string email = null;
                        if (profData.ContainsKey("Mail") && !string.IsNullOrWhiteSpace(profData["Mail"]?.ToString()))
                            email = profData["Mail"]?.ToString();
                        else if (profData.ContainsKey("mail") && !string.IsNullOrWhiteSpace(profData["mail"]?.ToString()))
                            email = profData["mail"]?.ToString();
                        else if (profData.ContainsKey("email") && !string.IsNullOrWhiteSpace(profData["email"]?.ToString()))
                            email = profData["email"]?.ToString();
                        professorEmail = !string.IsNullOrWhiteSpace(email) ? email : "Not available";
                    }
                    else
                    {
                        professorName = profId;
                        professorEmail = "Not available";
                    }
                    projectData = new
                    {
                        Title = data.GetValueOrDefault("title", "N/A").ToString(),
                        Professor = profId,
                        ProjectId = doc.Id
                    };
                    break;
                }
            }

            if (projectData == null)
            {
                ViewBag.NotEnrolled = true;
                return View();
            }

            var fileSnapshot = await db.Collection("evaluation-project")
                                       .Document("Evaluation")
                                       .Collection("Projects")
                                       .Document(studentId)
                                       .GetSnapshotAsync();

            var fileData = fileSnapshot.Exists ? fileSnapshot.ToDictionary() : null;

            var scheduleSnapshot = await db.Collection("evaluation-project")
                                           .Document("Evaluation")
                                           .Collection("results")
                                           .Document(studentId)
                                           .GetSnapshotAsync();

            string presentationDate = scheduleSnapshot.Exists && scheduleSnapshot.ContainsField("presentationDate")
                ? scheduleSnapshot.GetValue<string>("presentationDate")
                : null;

            ViewBag.Project = projectData;
            ViewBag.File = fileData;
            ViewBag.PresentationDate = presentationDate;
            ViewBag.ProfessorName = professorName;
            ViewBag.ProfessorEmail = professorEmail;

            return View();
        }

        public IActionResult NecessaryDocument() => View();
        public IActionResult DownloadCriteria() => ServeFile("evaluationCriteria.pdf", "application/pdf");
        public IActionResult DownloadPoster() => ServeFile("Poster.pptx", "application/pptx");
        public IActionResult DownloadStages() => ServeFile("stages.pdf", "application/pdf");
        public IActionResult DownloadObserved() => ServeFile("tobeobserved.pdf", "application/pdf");
        public IActionResult DownloadReport() => ServeFile("ProjectR.docx", "application/docx");
        public IActionResult DownloadIEEE() => ServeFile("IEEE.docx", "application/docx");

        private IActionResult ServeFile(string filename, string mime)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/files", filename);
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, mime, filename);
        }

        public IActionResult Index() => View();
        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
