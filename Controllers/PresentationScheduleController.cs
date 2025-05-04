using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using SoftwareProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SoftwareProject.Controllers
{
    public class PresentationScheduleController : Controller
    {
        private readonly FirestoreDb _db;

        public PresentationScheduleController(FirestoreDb db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            string professorEmail = HttpContext.Session.GetString("TeacherId")
                                  ?? HttpContext.Session.GetString("Username")
                                  ?? HttpContext.Session.GetString("Email");

            if (string.IsNullOrEmpty(professorEmail))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            var projectsSnapshot = await _db.Collection("evaluation-project")
                                            .Document("ProjectList")
                                            .Collection("Projects")
                                            .WhereEqualTo("professorEmail", professorEmail)
                                            .GetSnapshotAsync();

            var projectList = new List<ProjectScheduleViewModel>();

            foreach (var doc in projectsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                var enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();
                var title = data.ContainsKey("title") ? data["title"]?.ToString() : "";
                var description = data.ContainsKey("description") ? data["description"]?.ToString() : "";

                if (enrolled.Count > 0)
                {
                    projectList.Add(new ProjectScheduleViewModel
                    {
                        ProjectId = doc.Id,
                        ProjectTitle = title,
                        Description = description,
                        EnrolledStudents = enrolled
                    });
                }
            }

            return View(projectList);
        }

        public async Task<IActionResult> Schedule(string projectId)
        {
            string professorEmail = HttpContext.Session.GetString("TeacherId")
                                  ?? HttpContext.Session.GetString("Username")
                                  ?? HttpContext.Session.GetString("Email");

            if (string.IsNullOrEmpty(professorEmail))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Login");
            }

            var projectDoc = await _db.Collection("evaluation-project")
                                      .Document("ProjectList")
                                      .Collection("Projects")
                                      .Document(projectId)
                                      .GetSnapshotAsync();

            if (!projectDoc.Exists)
                return NotFound("Project not found.");

            var projectData = projectDoc.ToDictionary();
            string title = projectData.ContainsKey("title") ? projectData["title"].ToString() : "";
            string description = projectData.ContainsKey("description") ? projectData["description"].ToString() : "";
            var enrolledStudents = projectData.ContainsKey("enrolled")
                ? ((List<object>)projectData["enrolled"]).Select(x => x.ToString()).ToList()
                : new List<string>();

            // Fetch teacher availability
            var teacherDoc = await _db.Collection("evaluation-project")
                                      .Document("BookedDates")
                                      .Collection("DatesThatBooked")
                                      .Document(professorEmail)
                                      .GetSnapshotAsync();

            if (!teacherDoc.Exists || !teacherDoc.ToDictionary().ContainsKey("availableDates"))
            {
                TempData["ScheduleMessage"] = "Teacher has no available dates.";
                return RedirectToAction("Index");
            }

            var teacherDates = teacherDoc.GetValue<List<string>>("availableDates");

            string selectedDate = null;
            string matchedStudent = null;

            foreach (var studentId in enrolledStudents)
            {
                var studentDoc = await _db.Collection("evaluation-project")
                                          .Document("BookedDates")
                                          .Collection("DatesThatBooked")
                                          .Document(studentId)
                                          .GetSnapshotAsync();

                if (!studentDoc.Exists || !studentDoc.ToDictionary().ContainsKey("availableDates"))
                    continue;

                var studentDates = studentDoc.GetValue<List<string>>("availableDates");
                selectedDate = teacherDates.Intersect(studentDates).FirstOrDefault();

                if (!string.IsNullOrEmpty(selectedDate))
                {
                    matchedStudent = studentId;
                    break;
                }
            }

            if (string.IsNullOrEmpty(selectedDate))
            {
                // Get teacher full name
                var teacherInfo = await _db.Collection("evaluation-project")
                                           .Document("Professor")
                                           .Collection("Academician")
                                           .Document(professorEmail)
                                           .GetSnapshotAsync();

                string firstName = teacherInfo.ContainsField("First Name") ? teacherInfo.GetValue<string>("First Name") : "";
                string lastName = teacherInfo.ContainsField("Last Name") ? teacherInfo.GetValue<string>("Last Name") : "";

                string studentId = enrolledStudents.FirstOrDefault();
                string studentEmail = $"{studentId}@stu.iku.edu.tr";
                string subject = Uri.EscapeDataString("Entry Available date for the project presentation");

                string teacherDatesStr = string.Join(", ", teacherDates);
                string body = Uri.EscapeDataString(
                    $"Dear Student,\n\nPlease select the available date on your portal for the project presentation:\n      {teacherDatesStr}\n\nBest wishes,\n{firstName} {lastName}"
                );

                string mailtoLink = $"mailto:{studentEmail}?subject={subject}&body={body}";

                ViewBag.MailtoLink = mailtoLink;
                return View("NoMatch");
            }


            // Save scheduled date
            var resultRef = _db.Collection("evaluation-project")
                               .Document("Evaluation")
                               .Collection("results")
                               .Document(matchedStudent);

            var resultData = new Dictionary<string, object>
            {
                { "projectId", projectId },
                { "professorEmail", professorEmail },
                { "presentationDate", selectedDate },
                { "text22", "" },
                { "totalGrateToPercent", 0 }
            };

            await resultRef.SetAsync(resultData, SetOptions.MergeAll);

            // Pass data to view
            ViewBag.ProjectTitle = title;
            ViewBag.Description = description;
            ViewBag.ScheduledDate = selectedDate;
            ViewBag.EnrolledStudents = enrolledStudents;
            ViewBag.ProfessorId = professorEmail;

            return View("ScheduleResult");
        }
    }
}
