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
                var acl = storageObject.Acl ?? new List<ObjectAccessControl>();
                acl.Add(new ObjectAccessControl { Entity = "allUsers", Role = "READER" });
                storageObject.Acl = acl;
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

            if (snapshot.Exists)
            {
                ViewBag.TotalGrateToPercent = snapshot.GetValue<int>("totalGrateToPercent");
                ViewBag.ProfEm = snapshot.GetValue<string>("prefossorEmail");
                ViewBag.GenelYorum = snapshot.GetValue<string>("text22");
            }
            else
            {
                ViewBag.TotalGrateToPercent = "Document not found";
            }

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> MyProject()
        {
            string studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId))
                return RedirectToAction("Login", "Login");

            // Get approved project info
            var projectSnapshot = await db.Collection("evaluation-project")
                                          .Document("ProjectList")
                                          .Collection("Projects")
                                          .GetSnapshotAsync();

            dynamic projectData = null;
            foreach (var doc in projectSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                if (data.ContainsKey("enrolled") && ((List<object>)data["enrolled"]).Contains(studentId))
                {
                    projectData = new
                    {
                        Title = data.GetValueOrDefault("title", "N/A").ToString(),
                        Professor = data.GetValueOrDefault("professorEmail", "N/A").ToString(),
                        ProjectId = doc.Id
                    };
                    break;
                }
            }

            if (projectData == null)
            {
                TempData["Error"] = "You are not enrolled in any project.";
                return RedirectToAction("Index");
            }

            // Get uploaded file info
            var fileSnapshot = await db.Collection("evaluation-project")
                                       .Document("Evaluation")
                                       .Collection("Projects")
                                       .Document(studentId)
                                       .GetSnapshotAsync();

            var fileData = fileSnapshot.Exists ? fileSnapshot.ToDictionary() : null;

            // Get presentation schedule
            var scheduleSnapshot = await db.Collection("evaluation-project")
                                           .Document("Evaluation")
                                           .Collection("results")
                                           .Document(studentId)
                                           .GetSnapshotAsync();

            string presentationDate = scheduleSnapshot.Exists && scheduleSnapshot.ContainsField("presentationDate")
                ? scheduleSnapshot.GetValue<string>("presentationDate")
                : "Not Assigned";

            ViewBag.Project = projectData;
            ViewBag.File = fileData;
            ViewBag.PresentationDate = presentationDate;

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
