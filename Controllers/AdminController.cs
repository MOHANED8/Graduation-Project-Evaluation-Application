using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using SoftwareProject.Models;
using SoftwareProject.Services;
using System.Security.Claims;
using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SoftwareProject.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly FirebaseService _firebaseService;
        private readonly FirestoreDb _db;
        private readonly ILogger<AdminController> _logger;

        public AdminController(FirebaseService firebaseService, FirestoreDb db, ILogger<AdminController> logger)
        {
            _firebaseService = firebaseService;
            _db = db;
            _logger = logger;
        }

        // Admin Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            var stats = await GetSystemStats();
            return View(stats);
        }

        // Student Control Portal
        public async Task<IActionResult> StudentControl()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            // Fetch all projects
            var projects = await _firebaseService.GetAllProjects();
            // Aggregate all unique student IDs from enrolled lists
            var enrolledStudentIds = projects.SelectMany(p => p.Enrolled ?? new List<string>()).Distinct().ToList();

            // Fetch only those students from the students collection
            var allStudents = await _firebaseService.GetAllStudents();
            var enrolledStudents = allStudents.Where(s => enrolledStudentIds.Contains(s.Id)).ToList();

            var adminUsers = enrolledStudents.Select(s => new AdminUserModel
            {
                Id = s.Id,
                Email = s.Email,
                FirstName = s.FirstName,
                LastName = s.LastName,
                Role = "Student",
                IsActive = s.IsActive,
                Department = s.Department,
                Phone = s.Phone
            }).ToList();

            return View(adminUsers);
        }

        [HttpPost]
        public async Task<IActionResult> AddStudent(string FirstName, string LastName, string Email, string Password)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var student = new StudentModel
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = Email,
                    Password = Password,
                    IsActive = true,
                    Role = "Student",
                    Department = "Computer Science", // Default department
                    Phone = "N/A" // Default phone
                };

                await _firebaseService.AddStudent(student);
                await LogActivity("AddStudent", $"Added student {Email}");
                TempData["Success"] = "Student added successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error adding student: " + ex.Message;
            }

            return RedirectToAction("StudentControl");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStudentStatus(string studentId, bool isActive)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var docRef = _db.Collection("evaluation-project")
                    .Document("evaluation-project")
                    .Collection("Students")
                    .Document(studentId);

                await docRef.UpdateAsync("IsActive", isActive);
                await LogAdminAction("UpdateStudentStatus", $"Updated student {studentId} to IsActive: {isActive}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Teacher Control Portal
        [HttpGet]
        public async Task<IActionResult> TeacherControl()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var teachers = await GetAllTeachersList();
                return View(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teachers");
                TempData["Error"] = "Error loading teachers. Please try again.";
                return View(new List<AdminUserModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTeachers()
        {
            try
            {
                var teachers = await GetAllTeachersList();
                return Json(teachers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all teachers");
                return Json(new List<AdminUserModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddTeacher(string TeacherId, string FirstName, string LastName, string Email, string Password)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var docRef = _db.Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .Document(TeacherId);

                await docRef.SetAsync(new Dictionary<string, object>
                {
                    ["First Name"] = FirstName,
                    ["Last Name"] = LastName,
                    ["Mail"] = Email,
                    ["pass"] = Password,
                    ["IsActive"] = true
                });

                await LogAdminAction("AddTeacher", $"Added teacher {Email} with ID {TeacherId}");
                TempData["Success"] = "Teacher added successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error adding teacher: " + ex.Message;
            }

            return RedirectToAction("TeacherControl");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTeacherStatus(string teacherId, bool isActive)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var docRef = _db.Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .Document(teacherId);

                await docRef.UpdateAsync("IsActive", isActive);
                await LogAdminAction("UpdateTeacherStatus", $"Updated teacher {teacherId} to IsActive: {isActive}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Project Management
        public async Task<IActionResult> ProjectManagement(bool archived = false)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var projects = await _firebaseService.GetAllProjects();
                ViewBag.ShowArchived = archived;
                return View(projects.Where(p => p.IsArchived == archived).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting projects for management");
                TempData["Error"] = "Error loading projects. Please try again.";
                return View(new List<ProjectModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddProject(string Title, string Description, string ProfessorId, int TotalSlots)
        {
            try
            {
                if (!IsAdmin())
                {
                    return Unauthorized();
                }
                var project = new Dictionary<string, object>
                {
                    ["title"] = Title,
                    ["description"] = Description,
                    ["professorEmail"] = ProfessorId,
                    ["totalSlots"] = TotalSlots,
                    ["enrolled"] = new List<string>(),
                    ["requested"] = new List<string>(),
                    ["Status"] = "Pending",
                    ["Progress"] = 0,
                    ["IsArchived"] = false
                };
                await _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").AddAsync(project);
                await LogActivity("Add Project", $"Added new project: {Title}");
                TempData["Success"] = "Project added successfully.";
                return RedirectToAction(nameof(ProjectManagement));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding project");
                TempData["Error"] = "Error adding project. Please try again.";
                return RedirectToAction(nameof(ProjectManagement));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetProject(string id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized" });
            }
            try
            {
                var docRef = _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(id);
                var snapshot = await docRef.GetSnapshotAsync();
                if (!snapshot.Exists)
                {
                return Json(new { success = false, message = "Project not found" });
                }
                var data = snapshot.ToDictionary();
                int totalSlots = 0;
                if (data.ContainsKey("totalSlots"))
                {
                    int.TryParse(data["totalSlots"]?.ToString(), out totalSlots);
                }
                return Json(new {
                    id = snapshot.Id,
                    title = data.ContainsKey("title") ? data["title"]?.ToString() ?? "" : "",
                    description = data.ContainsKey("description") ? data["description"]?.ToString() ?? "" : "",
                    professorEmail = data.ContainsKey("professorEmail") ? data["professorEmail"]?.ToString() ?? "" : "",
                    enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>(),
                    requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>(),
                    totalSlots = totalSlots
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProject([FromForm] ProjectModel project)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                project.UpdatedAt = DateTime.UtcNow;
                var docRef = _db.Collection("evaluation-project")
                    .Document("Projects")
                    .Collection("ProjectList")
                    .Document(project.Id);

                await docRef.SetAsync(project, SetOptions.MergeAll);
                await LogAdminAction("UpdateProject", $"Updated project: {project.Name}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProject(string id)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            try
            {
                var docRef = _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(id);
                await docRef.DeleteAsync();
                await LogAdminAction("DeleteProject", $"Deleted project with ID: {id}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveProject(string id)
        {
            if (!IsAdmin())
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            try
            {
                var docRef = _db.Collection("evaluation-project")
                    .Document("ProjectList")
                    .Collection("Projects")
                    .Document(id);

                var snapshot = await docRef.GetSnapshotAsync();
                if (!snapshot.Exists)
                {
                    return Json(new { success = false, message = "Project not found. It may have been deleted." });
                }

                await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "Status", "Approved" },
                    { "UpdatedAt", DateTime.UtcNow }
                });

                await LogAdminAction("ApproveProject", $"Approved project with ID: {id}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ArchiveProject(string id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var docRef = _db.Collection("evaluation-project")
                    .Document("Projects")
                    .Collection("ProjectList")
                    .Document(id);

                await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "IsArchived", true },
                    { "UpdatedAt", DateTime.UtcNow }
                });

                await LogAdminAction("ArchiveProject", $"Archived project with ID: {id}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Meeting Rescheduling
        public async Task<IActionResult> MeetingRescheduling()
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            var meetings = await GetAllMeetings();

            // Fetch scheduled projects from results
            var resultsSnapshot = await _db.Collection("evaluation-project").Document("Evaluation").Collection("results").GetSnapshotAsync();
            var scheduledProjects = new List<Dictionary<string, object>>();
            var uniqueProjectIds = new HashSet<string>();
            foreach (var doc in resultsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                data["studentId"] = doc.Id; // Always set studentId for every result
                if (data.ContainsKey("presentationDate") && !string.IsNullOrEmpty(data["presentationDate"]?.ToString()))
                {
                    if (data.ContainsKey("projectId") && !string.IsNullOrEmpty(data["projectId"]?.ToString()))
                    {
                        var projectId = data["projectId"].ToString();
                        if (!uniqueProjectIds.Contains(projectId))
                        {
                            uniqueProjectIds.Add(projectId);
                            // Fetch project details
                            var projectDoc = await _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(projectId).GetSnapshotAsync();
                            if (projectDoc.Exists)
                            {
                                var projectData = projectDoc.ToDictionary();
                                data["projectTitle"] = projectData.ContainsKey("title") ? projectData["title"] : "-";
                                data["enrolled"] = projectData.ContainsKey("enrolled") ? projectData["enrolled"] : new List<string>();
                            }
                            else
                            {
                                data["projectTitle"] = "-";
                                data["enrolled"] = new List<string>();
                            }
                            // Map teacher IDs/emails for the view
                            data["primaryTeacherID"] = data.ContainsKey("primaryTeacherEmail") ? data["primaryTeacherEmail"] : "-";
                            data["secondaryTeacherID"] = data.ContainsKey("secondaryTeacherEmail") ? data["secondaryTeacherEmail"] : "-";
                            scheduledProjects.Add(data);
                        }
                    }
                }
            }
            ViewBag.MeetingProjectCount = uniqueProjectIds.Count;
            ViewBag.ScheduledProjects = scheduledProjects;
            return View(meetings);
        }

        [HttpPost]
        public async Task<IActionResult> AddMeeting(string Title, string ProfessorEmail, string StudentEmail, DateTime Date, string Time, string Recurrence)
        {
            if (!IsAdmin())
                return RedirectToAction("MeetingRescheduling");
            try
            {
                _logger.LogInformation($"AddMeeting called with Title={Title}, ProfessorEmail={ProfessorEmail}, StudentEmail={StudentEmail}, Date={Date}, Time={Time}, Recurrence={Recurrence}");
                if (string.IsNullOrEmpty(StudentEmail) || string.IsNullOrEmpty(Title) || Date == default)
                {
                    TempData["Error"] = "Missing required meeting information (student, title, or date).";
                    _logger.LogError($"AddMeeting: Missing required info. StudentEmail={StudentEmail}, Title={Title}, Date={Date}");
                    return RedirectToAction("MeetingRescheduling");
                }
                // 1. Check every project for the student ID in enrolled array
                var projectsSnapshot = await _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();
                string enrolledStudentId = null;
                foreach (var projectDoc in projectsSnapshot.Documents)
                {
                    var pdata = projectDoc.ToDictionary();
                    if (pdata.ContainsKey("enrolled"))
                    {
                        var enrolledList = ((List<object>)pdata["enrolled"]).Select(x => x.ToString()).ToList();
                        if (enrolledList.Contains(StudentEmail))
                        {
                            enrolledStudentId = StudentEmail;
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(enrolledStudentId))
                {
                    TempData["Error"] = "Student is not enrolled in any project.";
                    _logger.LogWarning($"AddMeeting: Student {StudentEmail} not enrolled in any project.");
                    return RedirectToAction("MeetingRescheduling");
                }
                // 2. Check /evaluation-project/Evaluation/results/{studentId}/presentationDate
                var resultDocRef = _db.Collection("evaluation-project").Document("Evaluation").Collection("results").Document(enrolledStudentId);
                var resultSnap = await resultDocRef.GetSnapshotAsync();
                if (resultSnap.Exists)
                {
                    var resultData = resultSnap.ToDictionary();
                    if (resultData.ContainsKey("presentationDate") && !string.IsNullOrEmpty(resultData["presentationDate"]?.ToString()))
                    {
                        TempData["Error"] = "Student already has a presentation scheduled.";
                        _logger.LogWarning($"AddMeeting: Student {enrolledStudentId} already has a presentation.");
                        return RedirectToAction("MeetingRescheduling");
                    }
                }
                int repeatCount = 1;
                TimeSpan interval = TimeSpan.Zero;
                if (Recurrence == "Daily") { repeatCount = 7; interval = TimeSpan.FromDays(1); }
                else if (Recurrence == "Weekly") { repeatCount = 4; interval = TimeSpan.FromDays(7); }
                else if (Recurrence == "Monthly") { repeatCount = 3; interval = TimeSpan.FromDays(30); }
                for (int i = 0; i < repeatCount; i++)
                {
                    var localDate = Date.AddDays(interval.TotalDays * i);
                    var utcDate = DateTime.SpecifyKind(localDate, DateTimeKind.Local).ToUniversalTime();
                    if (string.IsNullOrEmpty(enrolledStudentId) || string.IsNullOrEmpty(Title) || utcDate == default)
                    {
                        TempData["Error"] = "Invalid meeting data (student, title, or date).";
                        _logger.LogError($"AddMeeting: Invalid data. enrolledStudentId={enrolledStudentId}, Title={Title}, utcDate={utcDate}");
                        return RedirectToAction("MeetingRescheduling");
                    }
                    var docRef = _db.Collection("evaluation-project")
                        .Document("Meetings")
                        .Collection("ScheduledMeetings")
                        .Document();
                    await docRef.SetAsync(new
                    {
                        Title = Title,
                        ProfessorEmail = ProfessorEmail,
                        StudentEmail = enrolledStudentId,
                        Date = utcDate,
                        Time = Time,
                        Status = "Scheduled",
                        Recurrence = Recurrence
                    });

                    // Compose presentationDate string as yyyy-MM-dd|HH:mm|HH:mm+1hr
                    string startTime = Time;
                    string endTime = "";
                    if (TimeSpan.TryParse(Time, out var startTs))
                    {
                        endTime = startTs.Add(TimeSpan.FromHours(1)).ToString(@"hh\:mm");
                    }
                    string presentationDate = $"{Date:yyyy-MM-dd}|{startTime}|{endTime}";

                    // Update the student's result document with all relevant fields
                    var secondaryTeacherIdStr = Request.Form["SecondaryTeacherId"].ToString();
                    var resultDataToUpdate = new Dictionary<string, object>
                    {
                        ["enrolled"] = new List<string> { enrolledStudentId },
                        ["presentationDate"] = presentationDate,
                        ["primaryTeacherEmail"] = ProfessorEmail,
                        ["projectId"] = projectsSnapshot.Documents.FirstOrDefault(d => ((List<object>)d.ToDictionary()["enrolled"]).Select(x => x.ToString()).Contains(enrolledStudentId))?.Id ?? "",
                        ["secondaryTeacherEmail"] = secondaryTeacherIdStr,
                        ["secondaryTeacherID"] = secondaryTeacherIdStr,
                        ["status"] = "Scheduled"
                    };
                    await resultDocRef.SetAsync(resultDataToUpdate, Google.Cloud.Firestore.SetOptions.MergeAll);

                    // Add the scheduled time to bookedDates for both teachers
                    var primaryTeacherId = ProfessorEmail;
                    var secondaryTeacherId = Request.Form["SecondaryTeacherId"];
                    var bookedDateField = "bookedDates";
                    // Primary teacher
                    if (!string.IsNullOrEmpty(primaryTeacherId))
                    {
                        var primBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(primaryTeacherId);
                        var primSnap = await primBookedRef.GetSnapshotAsync();
                        if (!primSnap.Exists) { await primBookedRef.SetAsync(new Dictionary<string, object> { { bookedDateField, new List<string>() } }); }
                        await primBookedRef.UpdateAsync(bookedDateField, Google.Cloud.Firestore.FieldValue.ArrayUnion(presentationDate));
                    }
                    // Secondary teacher
                    if (!string.IsNullOrEmpty(secondaryTeacherId))
                    {
                        var secBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(secondaryTeacherId);
                        var secSnap = await secBookedRef.GetSnapshotAsync();
                        if (!secSnap.Exists) { await secBookedRef.SetAsync(new Dictionary<string, object> { { bookedDateField, new List<string>() } }); }
                        await secBookedRef.UpdateAsync(bookedDateField, Google.Cloud.Firestore.FieldValue.ArrayUnion(presentationDate));
                    }

                    // Also update the student's result status to 'Scheduled'
                    if (resultSnap.Exists)
                    {
                        await resultDocRef.UpdateAsync("status", "Scheduled");
                    }
                }
                await LogAdminAction("AddMeeting", $"Added meeting with Title: {Title}, ProfessorEmail: {ProfessorEmail}, StudentEmail: {enrolledStudentId}, Date: {Date}, Time: {Time}, Recurrence: {Recurrence}");
                TempData["Success"] = "Meeting(s) scheduled successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error scheduling meeting: " + ex.Message;
                _logger.LogError(ex, "AddMeeting: Exception occurred");
            }
            return RedirectToAction("MeetingRescheduling");
        }

        [HttpPost]
        public async Task<IActionResult> RescheduleMeeting(string meetingId, DateTime newDate, string newTime)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                // Ensure newDate is UTC
                var utcNewDate = DateTime.SpecifyKind(newDate, DateTimeKind.Local).ToUniversalTime();
                var docRef = _db.Collection("evaluation-project")
                    .Document("Meetings")
                    .Collection("ScheduledMeetings")
                    .Document(meetingId);

                await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "Date", utcNewDate },
                    { "Time", newTime },
                    { "Status", "Rescheduled" }
                });

                await LogAdminAction("RescheduleMeeting", $"Rescheduled meeting {meetingId} to Date: {utcNewDate}, Time: {newTime}");
                TempData["Success"] = "Meeting rescheduled successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error rescheduling meeting: " + ex.Message;
            }

            return RedirectToAction("MeetingRescheduling");
        }

        [HttpPost]
        public async Task<IActionResult> CancelMeeting(string meetingId)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            try
            {
                var docRef = _db.Collection("evaluation-project")
                    .Document("Meetings")
                    .Collection("ScheduledMeetings")
                    .Document(meetingId);

                await docRef.UpdateAsync("Status", "Cancelled");
                await LogAdminAction("CancelMeeting", $"Cancelled meeting {meetingId}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewProject(string id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var doc = await _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound();
            var data = doc.ToDictionary();
            var project = new ProjectModel
            {
                Id = doc.Id,
                Title = data.GetValueOrDefault("title", "").ToString(),
                Description = data.GetValueOrDefault("description", "").ToString(),
                ProfessorEmail = data.GetValueOrDefault("professorEmail", "").ToString(),
                TotalSlots = Convert.ToInt32(data.GetValueOrDefault("totalSlots", 0)),
                Enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>(),
                Requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>()
            };
            return View("ViewProject", project);
        }

        [HttpPost]
        public async Task<IActionResult> EditProject(string id, string title, string description, string professorId, string enrolled, string totalSlots)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            try
            {
            var docRef = _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(id);
                var snapshot = await docRef.GetSnapshotAsync();
                var data = snapshot.Exists ? snapshot.ToDictionary() : new Dictionary<string, object>();
                var oldEnrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>();
                var enrolledList = string.IsNullOrEmpty(enrolled) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(enrolled);
                // Find removed students
                var removedStudents = oldEnrolled.Except(enrolledList).ToList();
                // Remove their results and project docs
                foreach (var studentId in removedStudents)
                {
                    var resultsDocRef = _db.Collection("evaluation-project").Document("Evaluation").Collection("results").Document(studentId);
                    await resultsDocRef.DeleteAsync();
                    var projectDocRef = _db.Collection("evaluation-project").Document("Evaluation").Collection("Projects").Document(studentId);
                    await projectDocRef.DeleteAsync();
                }
                int totalSlotsInt = 0;
                int.TryParse(totalSlots, out totalSlotsInt);
                await docRef.SetAsync(new Dictionary<string, object>
                {
                    ["title"] = title,
                    ["description"] = description,
                    ["professorEmail"] = professorId,
                    ["enrolled"] = enrolledList,
                    ["totalSlots"] = totalSlotsInt
                }, Google.Cloud.Firestore.SetOptions.MergeAll);
                await LogAdminAction("EditProject", $"Updated project {id} with Title: {title}, Description: {description}, ProfessorId: {professorId}, Enrolled: {string.Join(",", enrolledList)}, TotalSlots: {totalSlotsInt}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportProjects()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var projects = await GetAllProjects();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Title,Description,ProfessorEmail,TotalSlots,Enrolled,Requested");
            foreach (var p in projects)
            {
                var enrolled = string.Join(";", p.Enrolled);
                var requested = string.Join(";", p.Requested);
                sb.AppendLine($"\"{p.Title}\",\"{p.Description}\",\"{p.ProfessorEmail}\",{p.TotalSlots},\"{enrolled}\",\"{requested}\"");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "projects_export.csv");
        }

        [HttpPost]
        public async Task<IActionResult> EditStudent(string id, string firstName, string lastName, string email, string department, string phone)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var docRef = _db.Collection("evaluation-project").Document("Student").Collection("Students").Document(id);
            await docRef.UpdateAsync(new Dictionary<string, object>
            {
                { "FirstName", firstName },
                { "LastName", lastName },
                { "Email", email },
                { "Department", department },
                { "Phone", phone }
            });
            await LogAdminAction("EditStudent", $"Updated student {id} with FirstName: {firstName}, LastName: {lastName}, Email: {email}, Department: {department}, Phone: {phone}");
            TempData["Success"] = "Student updated successfully.";
            return RedirectToAction("StudentControl");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteStudent(string id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var docRef = _db.Collection("evaluation-project").Document("Student").Collection("Students").Document(id);
            await docRef.DeleteAsync();
            await LogAdminAction("DeleteStudent", $"Deleted student {id}");
            TempData["Success"] = "Student deleted successfully.";
            return RedirectToAction("StudentControl");
        }

        [HttpGet]
        public async Task<IActionResult> GetStudentDetails(string id)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var doc = await _db.Collection("evaluation-project").Document("Student").Collection("Students").Document(id).GetSnapshotAsync();
            if (!doc.Exists) return NotFound();
            var data = doc.ToDictionary();
            // Fetch assigned teacher and projects
            var teacher = data.ContainsKey("AssignedTeacher") ? data["AssignedTeacher"].ToString() : "";
            var projects = new List<string>();
            var projectsSnapshot = await _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();
            foreach (var p in projectsSnapshot.Documents)
            {
                var pdata = p.ToDictionary();
                if (pdata.ContainsKey("enrolled") && ((List<object>)pdata["enrolled"]).Contains(id))
                    projects.Add(pdata["title"].ToString());
            }
            var details = new {
                Id = doc.Id,
                FirstName = data.GetValueOrDefault("FirstName", "").ToString(),
                LastName = data.GetValueOrDefault("LastName", "").ToString(),
                Email = data.GetValueOrDefault("Email", "").ToString(),
                Department = data.GetValueOrDefault("Department", "").ToString(),
                Phone = data.GetValueOrDefault("Phone", "").ToString(),
                AssignedTeacher = teacher,
                Projects = projects
            };
            return Json(details);
        }

        [HttpGet]
        public async Task<IActionResult> ExportStudents()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var students = await _firebaseService.GetAllStudents();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,FirstName,LastName,Email,Department,Phone,Role,IsActive");
            foreach (var s in students)
            {
                sb.AppendLine($"{s.Id},\"{s.FirstName}\",\"{s.LastName}\",\"{s.Email}\",\"{s.Department}\",\"{s.Phone}\",{s.Role},{s.IsActive}");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "students_export.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ExportTeachers()
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var teachers = await GetAllTeachersList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,FirstName,LastName,Email,Department,Phone,Role,IsActive");
            foreach (var t in teachers)
            {
                sb.AppendLine($"{t.Id},\"{t.FirstName}\",\"{t.LastName}\",\"{t.Email}\",\"{t.Department}\",\"{t.Phone}\",{t.Role},{t.IsActive}");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "teachers_export.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ImportStudents(IFormFile csvFile)
        {
            if (!IsAdmin() || csvFile == null || csvFile.Length == 0)
                return RedirectToAction("StudentControl");
            using (var reader = new StreamReader(csvFile.OpenReadStream()))
            {
                string headerLine = await reader.ReadLineAsync();
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    var values = line.Split(',');
                    if (values.Length < 8) continue;
                    var docRef = _db.Collection("evaluation-project").Document("Student").Collection("Students").Document(values[0]);
                    await docRef.SetAsync(new
                    {
                        FirstName = values[1].Trim('"'),
                        LastName = values[2].Trim('"'),
                        Email = values[3].Trim('"'),
                        Department = values[4].Trim('"'),
                        Phone = values[5].Trim('"'),
                        Role = values[6],
                        IsActive = bool.Parse(values[7])
                    });
                }
            }
            await LogAdminAction("ImportStudents", $"Imported students from CSV");
            TempData["Success"] = "Students imported successfully.";
            return RedirectToAction("StudentControl");
        }

        [HttpPost]
        public async Task<IActionResult> ImportTeachers(IFormFile csvFile)
        {
            if (!IsAdmin() || csvFile == null || csvFile.Length == 0)
                return RedirectToAction("TeacherControl");
            using (var reader = new StreamReader(csvFile.OpenReadStream()))
            {
                string headerLine = await reader.ReadLineAsync();
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    var values = line.Split(',');
                    if (values.Length < 8) continue;
                    var docRef = _db.Collection("evaluation-project").Document("Professor").Collection("Academician").Document(values[0]);
                    await docRef.SetAsync(new
                    {
                        FirstName = values[1].Trim('"'),
                        LastName = values[2].Trim('"'),
                        Email = values[3].Trim('"'),
                        Department = values[4].Trim('"'),
                        Phone = values[5].Trim('"'),
                        Role = values[6],
                        IsActive = bool.Parse(values[7])
                    });
                }
            }
            await LogAdminAction("ImportTeachers", $"Imported teachers from CSV");
            TempData["Success"] = "Teachers imported successfully.";
            return RedirectToAction("TeacherControl");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string id, string userType, string newPassword)
        {
            if (!IsAdmin())
                return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
            var collection = userType == "Teacher" ? "Academician" : "Students";
            var docRef = _db.Collection("evaluation-project").Document(userType == "Teacher" ? "Professor" : "Student").Collection(collection).Document(id);
            await docRef.UpdateAsync("Password", newPassword);
            await LogAdminAction(userType == "Teacher" ? "ResetTeacherPassword" : "ResetStudentPassword", $"Reset password for {id}");
            TempData["Success"] = $"{userType} password reset successfully.";
            return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserRole(string id, string userType, string newRole)
        {
            if (!IsAdmin())
                return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
            var collection = userType == "Teacher" ? "Academician" : "Students";
            var docRef = _db.Collection("evaluation-project").Document(userType == "Teacher" ? "Professor" : "Student").Collection(collection).Document(id);
            await docRef.UpdateAsync("Role", newRole);
            await LogAdminAction(userType == "Teacher" ? "ChangeTeacherRole" : "ChangeStudentRole", $"Changed role for {id} to {newRole}");
            TempData["Success"] = $"{userType} role updated successfully.";
            return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAccountStatus(string id, string userType, bool isActive)
        {
            if (!IsAdmin())
                return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
            var collection = userType == "Teacher" ? "Academician" : "Students";
            var docRef = _db.Collection("evaluation-project").Document(userType == "Teacher" ? "Professor" : "Student").Collection(collection).Document(id);
            await docRef.UpdateAsync("IsActive", isActive);
            await LogAdminAction(userType == "Teacher" ? "ToggleTeacherAccount" : "ToggleStudentAccount", $"Toggled account for {id} to IsActive: {isActive}");
            TempData["Success"] = $"{userType} account status updated.";
            return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
        }

        [HttpPost]
        public async Task<IActionResult> SendNotification(string userType, List<string> userIds, string subject, string message)
        {
            if (!IsAdmin())
                return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
            // For demo: just store notifications in Firestore (could be extended to send emails)
            foreach (var id in userIds)
            {
                var notifRef = _db.Collection("notifications").Document();
                await notifRef.SetAsync(new
                {
                    UserId = id,
                    UserType = userType,
                    Subject = subject,
                    Message = message,
                    SentAt = DateTime.UtcNow
                });
            }
            await LogAdminAction("SendNotification", $"Sent notification to {string.Join(", ", userIds)}");
            TempData["Success"] = "Notification(s) sent.";
            return RedirectToAction(userType == "Teacher" ? "TeacherControl" : "StudentControl");
        }

        [HttpGet]
        public async Task<IActionResult> MessageCenter()
        {
            if (!IsAdmin())
                return RedirectToAction("Dashboard");
            var messages = new List<dynamic>();
            var snapshot = await _db.Collection("notifications").OrderByDescending("SentAt").GetSnapshotAsync();
            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                messages.Add(new
                {
                    Id = doc.Id,
                    UserId = data.GetValueOrDefault("UserId", "").ToString(),
                    UserType = data.GetValueOrDefault("UserType", "").ToString(),
                    Subject = data.GetValueOrDefault("Subject", "").ToString(),
                    Message = data.GetValueOrDefault("Message", "").ToString(),
                    SentAt = data.GetValueOrDefault("SentAt", null)
                });
            }
            ViewBag.Messages = messages;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMessage(string id)
        {
            if (!IsAdmin())
                return RedirectToAction("MessageCenter");
            await _db.Collection("notifications").Document(id).DeleteAsync();
            await LogAdminAction("DeleteMessage", $"Deleted message {id}");
            TempData["Success"] = "Message deleted.";
            return RedirectToAction("MessageCenter");
        }

        [HttpPost]
        public async Task<IActionResult> SendBroadcast(string subject, string message, string userType)
        {
            if (!IsAdmin())
                return RedirectToAction("MessageCenter");

            try
            {
                List<AdminUserModel> users = new List<AdminUserModel>();
                
                if (userType == "Teacher")
                {
                    var teachers = await GetAllTeachersList();
                    if (teachers != null)
                    {
                        users.AddRange(teachers);
                    }
                }
                else
                {
                    var students = await _firebaseService.GetAllStudents();
                    foreach (var student in students)
                    {
                        users.Add(new AdminUserModel
                        {
                            Id = student.Id,
                            Email = student.Email,
                            FirstName = student.FirstName,
                            LastName = student.LastName,
                            Role = "Student",
                            Department = student.Department,
                            Phone = student.Phone,
                            IsActive = student.IsActive
                        });
                    }
                }

                // Send notifications to each user
                foreach (var user in users)
                {
                    var notifRef = _db.Collection("notifications").Document();
                    await notifRef.SetAsync(new
                    {
                        UserId = user.Id,
                        UserType = userType,
                        Subject = subject,
                        Message = message,
                        SentAt = DateTime.UtcNow
                    });
                }

                await LogAdminAction("SendBroadcast", $"Sent broadcast to {userType}");
                TempData["Success"] = "Broadcast sent.";
                return RedirectToAction("MessageCenter");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending broadcast");
                TempData["Error"] = "Error sending broadcast. Please try again.";
                return RedirectToAction("MessageCenter");
            }
        }

        [HttpGet]
        public async Task<IActionResult> MeetingAnalytics()
        {
            if (!IsAdmin())
                return RedirectToAction("Dashboard");
            var meetingsSnapshot = await _db.Collection("evaluation-project").Document("Meetings").Collection("ScheduledMeetings").GetSnapshotAsync();
            var totalMeetings = meetingsSnapshot.Documents.Count;
            var attendance = new Dictionary<string, int>(); // userId -> count
            foreach (var doc in meetingsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                var professor = data.GetValueOrDefault("ProfessorEmail", "").ToString();
                var student = data.GetValueOrDefault("StudentEmail", "").ToString();
                if (!string.IsNullOrEmpty(professor))
                    attendance[professor] = attendance.ContainsKey(professor) ? attendance[professor] + 1 : 1;
                if (!string.IsNullOrEmpty(student))
                    attendance[student] = attendance.ContainsKey(student) ? attendance[student] + 1 : 1;
            }
            var mostActive = attendance.OrderByDescending(x => x.Value).Take(5).ToList();
            ViewBag.TotalMeetings = totalMeetings;
            ViewBag.MostActive = mostActive;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ExportMeetingsToICS()
        {
            if (!IsAdmin())
                return RedirectToAction("MeetingAnalytics");
            var meetingsSnapshot = await _db.Collection("evaluation-project").Document("Meetings").Collection("ScheduledMeetings").GetSnapshotAsync();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//YourApp//Meeting Export//EN");
            foreach (var doc in meetingsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                var title = data.GetValueOrDefault("Title", "Meeting").ToString();
                var date = data.GetValueOrDefault("Date", null);
                var time = data.GetValueOrDefault("Time", "").ToString();
                var professor = data.GetValueOrDefault("ProfessorEmail", "").ToString();
                var student = data.GetValueOrDefault("StudentEmail", "").ToString();
                if (date == null) continue;
                DateTime dt = Convert.ToDateTime(date + " " + time);
                string dtStart = dt.ToUniversalTime().ToString("yyyyMMdd\'T\'HHmmss\'Z\'");
                string dtEnd = dt.AddHours(1).ToUniversalTime().ToString("yyyyMMdd\'T\'HHmmss\'Z\'");
                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"SUMMARY:{title}");
                sb.AppendLine($"DTSTART:{dtStart}");
                sb.AppendLine($"DTEND:{dtEnd}");
                sb.AppendLine($"DESCRIPTION:Professor: {professor} | Student: {student}");
                sb.AppendLine("END:VEVENT");
            }
            sb.AppendLine("END:VCALENDAR");
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/calendar", "meetings_export.ics");
        }

        [HttpGet]
        public async Task<IActionResult> ActivityLog()
        {
            if (!IsAdmin())
                return RedirectToAction("Dashboard");
            var logs = new List<dynamic>();
            var snapshot = await _db.Collection("admin_logs").OrderByDescending("Timestamp").GetSnapshotAsync();
            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                object tsObj = data.GetValueOrDefault("Timestamp", null);
                DateTime? ts = null;
                if (tsObj is Google.Cloud.Firestore.Timestamp gcts)
                    ts = gcts.ToDateTime();
                else if (tsObj is DateTime dt)
                    ts = dt;
                else if (tsObj != null && DateTime.TryParse(tsObj.ToString(), out var parsed))
                    ts = parsed;
                logs.Add(new
                {
                    Admin = data.GetValueOrDefault("Admin", "").ToString(),
                    Action = data.GetValueOrDefault("Action", "").ToString(),
                    Details = data.GetValueOrDefault("Details", "").ToString(),
                    Timestamp = ts
                });
            }
            ViewBag.Logs = logs;
            return View();
        }

        // GET: Admin/ManageProjects
        public async Task<IActionResult> ManageProjects()
        {
            try
            {
                var projects = await _firebaseService.GetAllProjects();
                ViewBag.Projects = projects;
                return View();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error loading projects: " + ex.Message);
            }
        }

        // GET: Admin/GetAllStudents
        [HttpGet]
        public async Task<IActionResult> GetAllStudents()
        {
            try
            {
                var students = await _firebaseService.GetAllStudents();
                return Json(students);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all students");
                return Json(new List<StudentModel>());
            }
        }

        // GET: Admin/GetAssignedStudents/{projectId}
        [HttpGet]
        public async Task<IActionResult> GetAssignedStudents(string projectId)
        {
            try
            {
                var project = await _firebaseService.GetProject(projectId);
                if (project == null)
                    return Json(new List<StudentModel>());

                var assignedStudents = new List<StudentModel>();
                foreach (var studentId in project.Enrolled)
                {
                    var docRef = _db.Collection("evaluation-project")
                        .Document("Student")
                        .Collection("Students")
                        .Document(studentId);

                    var snapshot = await docRef.GetSnapshotAsync();
                    if (snapshot.Exists)
                    {
                        var data = snapshot.ToDictionary();
                        assignedStudents.Add(new StudentModel
                        {
                            Id = snapshot.Id,
                            FirstName = data.GetValueOrDefault("FirstName", "").ToString(),
                            LastName = data.GetValueOrDefault("LastName", "").ToString(),
                            Email = data.GetValueOrDefault("Email", "").ToString(),
                            Department = data.GetValueOrDefault("Department", "").ToString(),
                            Phone = data.GetValueOrDefault("Phone", "").ToString(),
                            Role = "Student",
                            IsActive = Convert.ToBoolean(data.GetValueOrDefault("IsActive", true)),
                            Password = data.GetValueOrDefault("Password", "").ToString()
                        });
                    }
                }
                return Json(assignedStudents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assigned students");
                return Json(new List<StudentModel>());
            }
        }

        // POST: Admin/AssignStudents
        [HttpPost]
        public async Task<IActionResult> AssignStudents(string projectId, List<string> studentIds)
        {
            try
            {
                var project = await _firebaseService.GetProject(projectId);
                if (project == null)
                    return Json(new { success = false, message = "Project not found" });

                var docRef = _db.Collection("evaluation-project")
                    .Document("ProjectList")
                    .Collection("Projects")
                    .Document(projectId);

                await docRef.UpdateAsync("Enrolled", studentIds);
                await LogAdminAction("AssignStudents", $"Assigned students to project {projectId}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning students");
                return Json(new { success = false, message = "Error assigning students" });
            }
        }

        private async Task<List<AdminUserModel>> GetAllTeachersList()
        {
            var teachers = new List<AdminUserModel>();
            try
            {
                var snapshot = await _db.Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();
                    teachers.Add(new AdminUserModel
                    {
                        Id = doc.Id,
                        Email = data.ContainsKey("Mail") ? data["Mail"].ToString() : "",
                        FirstName = data.ContainsKey("First Name") ? data["First Name"].ToString() : "",
                        LastName = data.ContainsKey("Last Name") ? data["Last Name"].ToString() : "",
                        Role = "Teacher",
                        IsActive = data.ContainsKey("IsActive") && Convert.ToBoolean(data["IsActive"]),
                        Department = data.ContainsKey("Department") ? data["Department"].ToString() : "Computer Science",
                        Phone = data.ContainsKey("Phone") ? data["Phone"].ToString() : "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teachers list");
            }
            return teachers;
        }

        private async Task<List<ProjectModel>> GetAllProjects()
        {
            var projects = new List<ProjectModel>();
            var snapshot = await _db.Collection("evaluation-project")
                .Document("ProjectList")
                .Collection("Projects")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                projects.Add(new ProjectModel
                {
                    Id = doc.Id,
                    Title = data.GetValueOrDefault("title", "").ToString(),
                    Description = data.GetValueOrDefault("description", "").ToString(),
                    ProfessorEmail = data.GetValueOrDefault("professorEmail", "").ToString(),
                    TotalSlots = Convert.ToInt32(data.GetValueOrDefault("totalSlots", 0)),
                    Enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>(),
                    Requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>(),
                    Status = data.GetValueOrDefault("Status", "Pending").ToString(),
                    IsArchived = data.ContainsKey("IsArchived") && Convert.ToBoolean(data["IsArchived"]),
                    Progress = Convert.ToInt32(data.GetValueOrDefault("Progress", 0))
                });
            }

            return projects;
        }

        private async Task<List<MeetingModel>> GetAllMeetings()
        {
            var meetings = new List<MeetingModel>();
            var snapshot = await _db.Collection("evaluation-project")
                .Document("Meetings")
                .Collection("ScheduledMeetings")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                // Defensive: handle missing/null fields
                string title = data.ContainsKey("Title") && data["Title"] != null ? data["Title"].ToString() : string.Empty;
                string professor = data.ContainsKey("ProfessorEmail") && data["ProfessorEmail"] != null ? data["ProfessorEmail"].ToString() : string.Empty;
                string student = data.ContainsKey("StudentEmail") && data["StudentEmail"] != null ? data["StudentEmail"].ToString() : string.Empty;
                string status = data.ContainsKey("Status") && data["Status"] != null ? data["Status"].ToString() : "Scheduled";
                string time = data.ContainsKey("Time") && data["Time"] != null ? data["Time"].ToString() : string.Empty;
                string recurrence = data.ContainsKey("Recurrence") && data["Recurrence"] != null ? data["Recurrence"].ToString() : string.Empty;
                DateTime date = DateTime.Now;
                if (data.ContainsKey("Date") && data["Date"] != null)
                {
                    DateTime parsed;
                    if (DateTime.TryParse(data["Date"].ToString(), out parsed))
                        date = parsed;
                }
                meetings.Add(new MeetingModel
                {
                    Id = doc.Id,
                    Title = title,
                    Date = date,
                    Time = time,
                    ProfessorEmail = professor,
                    StudentEmail = student,
                    Status = status,
                    Recurrence = recurrence
                });
            }

            return meetings;
        }

        private async Task LogActivity(string action, string details)
        {
            var log = new ActivityLog
            {
                Id = Guid.NewGuid().ToString(),
                Admin = User.Identity.Name,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            };
            
            await _firebaseService.AddActivityLog(log);
        }

        // Helper Methods
        private bool IsAdmin()
        {
            return User.Identity.IsAuthenticated && User.IsInRole("Admin");
        }

        private async Task<AdminDashboardStats> GetSystemStats()
        {
            var stats = new AdminDashboardStats { SystemVersion = "1.0.0" };

            // Get all projects
            var projectsSnapshot = await _db.Collection("evaluation-project")
                .Document("ProjectList")
                .Collection("Projects")
                .GetSnapshotAsync();

            // Count total enrolled students (sum of all enrolled arrays)
            int totalEnrolled = 0;
            foreach (var doc in projectsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                if (data.ContainsKey("enrolled"))
                {
                    totalEnrolled += ((List<object>)data["enrolled"]).Count;
                }
            }
            stats.TotalStudents = totalEnrolled;

            // Get total teachers
            var teachersSnapshot = await _db.Collection("evaluation-project")
                .Document("Professor")
                .Collection("Academician")
                .GetSnapshotAsync();
            stats.TotalTeachers = teachersSnapshot.Documents.Count;

            // Get total projects
            stats.TotalProjects = projectsSnapshot.Documents.Count;

            // Get active projects
            stats.ActiveProjects = projectsSnapshot.Documents
                .Count(d => d.ToDictionary().ContainsKey("IsArchived") && !(bool)d.ToDictionary()["IsArchived"] &&
                            d.ToDictionary().ContainsKey("Status") &&
                            (d.ToDictionary()["Status"].ToString() == "Scheduled" || d.ToDictionary()["Status"].ToString() == "Active"));

            return stats;
        }

        private async Task LogAdminAction(string action, string details)
        {
            var admin = HttpContext.Session.GetString("Username") ?? "Unknown";
            var logRef = _db.Collection("admin_logs").Document();
            await logRef.SetAsync(new
            {
                Admin = admin,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            });
        }

        public IActionResult ManageUsers() => RedirectToAction("StudentControl");
        public IActionResult Settings() => View();

        // GET: Admin/GetDashboardStats
        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var stats = await GetSystemStats();
                
                // Get active students count
                var activeStudents = await _db.Collection("evaluation-project")
                    .Document("evaluation-project")
                    .Collection("Students")
                    .WhereEqualTo("IsActive", true)
                    .GetSnapshotAsync();
                stats.ActiveStudents = activeStudents.Documents.Count;

                // Get active teachers count
                var activeTeachers = await _db.Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .WhereEqualTo("IsActive", true)
                    .GetSnapshotAsync();
                stats.ActiveTeachers = activeTeachers.Documents.Count;

                // Get pending projects count
                var pendingProjects = await _db.Collection("evaluation-project")
                    .Document("ProjectList")
                    .Collection("Projects")
                    .WhereEqualTo("Status", "Pending")
                    .GetSnapshotAsync();
                stats.PendingProjects = pendingProjects.Documents.Count;

                // Get completed projects count
                var completedProjects = await _db.Collection("evaluation-project")
                    .Document("ProjectList")
                    .Collection("Projects")
                    .WhereEqualTo("Status", "Completed")
                    .GetSnapshotAsync();
                stats.CompletedProjects = completedProjects.Documents.Count;

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return Json(new { error = "Error getting dashboard stats" });
            }
        }

        // GET: Admin/Search
        [HttpGet]
        public async Task<IActionResult> Search(string term, string type)
        {
            try
            {
                var results = new List<dynamic>();

                switch (type.ToLower())
                {
                    case "students":
                        var students = await _firebaseService.GetAllStudents();
                        results = students
                            .Where(s => s.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      s.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      s.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Email, Type = "Student" })
                            .ToList<dynamic>();
                        break;

                    case "teachers":
                        var teachers = await GetAllTeachersList();
                        results = teachers
                            .Where(t => t.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      t.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      t.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .Select(t => new { t.Id, t.FirstName, t.LastName, t.Email, Type = "Teacher" })
                            .ToList<dynamic>();
                        break;

                    case "projects":
                        var projects = await GetAllProjects();
                        results = projects
                            .Where(p => p.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      p.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .Select(p => new { p.Id, Title = p.Title, Description = p.Description, Type = "Project" })
                            .ToList<dynamic>();
                        break;

                    default:
                        // Search all types
                        var allStudents = await _firebaseService.GetAllStudents();
                        var allTeachers = await GetAllTeachersList();
                        var allProjects = await GetAllProjects();

                        results.AddRange(allStudents
                            .Where(s => s.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      s.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      s.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Email, Type = "Student" }));

                        results.AddRange(allTeachers
                            .Where(t => t.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      t.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      t.Email.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .Select(t => new { t.Id, t.FirstName, t.LastName, t.Email, Type = "Teacher" }));

                        results.AddRange(allProjects
                            .Where(p => p.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                      p.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
                            .Select(p => new { p.Id, Title = p.Title, Description = p.Description, Type = "Project" }));
                        break;
                }

                return Json(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search");
                return Json(new { error = "Error performing search" });
            }
        }

        // GET: Admin/GetSortedData
        [HttpGet]
        public async Task<IActionResult> GetSortedData(string type, string sortBy)
        {
            try
            {
                var results = new List<dynamic>();

                switch (type.ToLower())
                {
                    case "students":
                        var students = await _firebaseService.GetAllStudents();
                        results = students
                            .OrderBy(s => sortBy == "name" ? s.FirstName :
                                        sortBy == "date" ? s.CreatedAt.ToString() :
                                        s.IsActive.ToString())
                            .Select(s => new { s.Id, s.FirstName, s.LastName, s.Email, s.IsActive })
                            .ToList<dynamic>();
                        break;

                    case "teachers":
                        var teachers = await GetAllTeachersList();
                        results = teachers
                            .OrderBy(t => sortBy == "name" ? t.FirstName :
                                        sortBy == "date" ? t.CreatedAt.ToString() :
                                        t.IsActive.ToString())
                            .Select(t => new { t.Id, t.FirstName, t.LastName, t.Email, t.IsActive })
                            .ToList<dynamic>();
                        break;

                    case "projects":
                        var projects = await GetAllProjects();
                        results = projects
                            .OrderBy(p => sortBy == "name" ? p.Title :
                                        sortBy == "date" ? p.CreatedAt.ToString() :
                                        p.Status)
                            .Select(p => new { p.Id, p.Title, p.Description, p.Status })
                            .ToList<dynamic>();
                        break;
                }

                return Json(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sorted data");
                return Json(new { error = "Error getting sorted data" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> HandleProjectOversight(string projectId, string action, string feedback)
        {
            if (!IsAdmin())
            {
                return RedirectToAction("Login", "Login");
            }

            var projectRef = _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(projectId);
            var snapshot = await projectRef.GetSnapshotAsync();
            
            if (!snapshot.Exists) return NotFound();

            var data = snapshot.ToDictionary();
            var teacherEmail = data["professorEmail"].ToString();

            // Log the admin action
            await LogAdminAction("ProjectOversight", $"Admin {action} for project {projectId}");

            // Store admin feedback
            await _db.Collection("evaluation-project")
                .Document("Feedback")
                .Collection("AdminFeedback")
                .AddAsync(new Dictionary<string, object>
                {
                    { "projectId", projectId },
                    { "teacherId", teacherEmail },
                    { "action", action },
                    { "feedback", feedback },
                    { "timestamp", DateTime.UtcNow }
                });

            // Notify teacher
            await _db.Collection("evaluation-project")
                .Document("Notifications")
                .Collection("TeacherNotifications")
                .AddAsync(new Dictionary<string, object>
                {
                    { "teacherId", teacherEmail },
                    { "message", $"Admin {action}: {feedback}" },
                    { "projectId", projectId },
                    { "timestamp", DateTime.UtcNow },
                    { "isRead", false }
                });

            TempData["Success"] = $"Project {action} successfully.";
            return RedirectToAction("ProjectManagement");
        }

        [HttpPost]
        public async Task<IActionResult> EditTeacher(string id, string firstName, string lastName, string email, string FirstName, string LastName, string Email)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            // Prefer camelCase if present, fallback to PascalCase
            var fName = !string.IsNullOrEmpty(firstName) ? firstName : FirstName;
            var lName = !string.IsNullOrEmpty(lastName) ? lastName : LastName;
            var mail = !string.IsNullOrEmpty(email) ? email : Email;
            var docRef = _db.Collection("evaluation-project").Document("Professor").Collection("Academician").Document(id);
            await docRef.SetAsync(new Dictionary<string, object>
            {
                ["First Name"] = fName,
                ["Last Name"] = lName,
                ["Mail"] = mail
            }, SetOptions.MergeAll);
            await LogAdminAction("EditTeacher", $"Updated teacher {id} with FirstName: {fName}, LastName: {lName}, Email: {mail}");
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTeacher(string id)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            var docRef = _db.Collection("evaluation-project").Document("Professor").Collection("Academician").Document(id);
            await docRef.DeleteAsync();
            await LogAdminAction("DeleteTeacher", $"Deleted teacher {id}");
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetTeacherDetails(string id)
        {
            _logger.LogInformation($"GetTeacherDetails called with id: {id}");
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            var doc = await _db.Collection("evaluation-project").Document("Professor").Collection("Academician").Document(id).GetSnapshotAsync();
            if (!doc.Exists)
            {
                _logger.LogWarning($"No teacher found for id: {id}");
                return Json(new { success = false, message = "Not found" });
            }
            var data = doc.ToDictionary();
            _logger.LogInformation($"Teacher data for id {id}: {JsonSerializer.Serialize(data)}");
            return Json(new {
                success = true,
                Id = doc.Id,
                FirstName = data.ContainsKey("First Name") ? data["First Name"]?.ToString() ?? "" : "",
                LastName = data.ContainsKey("Last Name") ? data["Last Name"]?.ToString() ?? "" : "",
                Email = data.ContainsKey("Mail") ? data["Mail"]?.ToString() ?? "" : "",
                IsActive = data.ContainsKey("IsActive") && Convert.ToBoolean(data["IsActive"])
            });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeTeacherPassword(string id, string newPassword)
        {
            if (!IsAdmin())
                return RedirectToAction("Login", "Login");
            var docRef = _db.Collection("evaluation-project").Document("Professor").Collection("Academician").Document(id);
            await docRef.UpdateAsync("pass", newPassword);
            await LogAdminAction("ChangeTeacherPassword", $"Changed password for teacher {id}");
            TempData["Success"] = "Teacher password updated successfully.";
            return RedirectToAction("TeacherControl");
        }

        [HttpGet]
        public async Task<IActionResult> GetScheduledProjectsCount()
        {
            var resultsSnapshot = await _db.Collection("evaluation-project").Document("Evaluation").Collection("results").GetSnapshotAsync();
            var uniqueProjectIds = new HashSet<string>();
            foreach (var doc in resultsSnapshot.Documents)
            {
                var data = doc.ToDictionary();
                data["studentId"] = doc.Id; // Always set studentId for every result
                if (data.ContainsKey("presentationDate") && !string.IsNullOrEmpty(data["presentationDate"]?.ToString()))
                {
                    if (data.ContainsKey("projectId") && !string.IsNullOrEmpty(data["projectId"]?.ToString()))
                        uniqueProjectIds.Add(data["projectId"].ToString());
                }
            }
            return Json(new { count = uniqueProjectIds.Count });
        }

        [HttpPost]
        public async Task<IActionResult> ChangeProjectStatus(string id, string status)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            try
            {
                var docRef = _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").Document(id);
                await docRef.UpdateAsync("Status", status);
                await LogAdminAction("ChangeProjectStatus", $"Changed status for project {id} to {status}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditPresentation(string studentId, string projectId, string secondaryTeacherID, string presentationDate)
        {
            try
            {
                var resultDocRef = _db.Collection("evaluation-project").Document("Evaluation").Collection("results").Document(studentId);
                var resultSnapshot = await resultDocRef.GetSnapshotAsync();
                if (!resultSnapshot.Exists)
                    return Json(new { success = false, message = "Result document not found." });
                var resultData = resultSnapshot.ToDictionary();
                var oldSecondaryTeacherID = resultData.ContainsKey("secondaryTeacherID") ? resultData["secondaryTeacherID"].ToString() : "";
                var primaryTeacherID = resultData.ContainsKey("primaryTeacherID") ? resultData["primaryTeacherID"].ToString() : "";
                var oldPresentationDate = resultData.ContainsKey("presentationDate") ? resultData["presentationDate"].ToString() : "";
                // Remove from old secondary teacher if changed
                if (!string.IsNullOrEmpty(secondaryTeacherID) && secondaryTeacherID != oldSecondaryTeacherID && !string.IsNullOrEmpty(oldSecondaryTeacherID))
                {
                    var oldSecBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(oldSecondaryTeacherID);
                    var docSnap = await oldSecBookedRef.GetSnapshotAsync();
                    if (!docSnap.Exists) { await oldSecBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                    await oldSecBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayRemove(oldPresentationDate));
                }
                // Add to new secondary teacher
                if (!string.IsNullOrEmpty(secondaryTeacherID))
                {
                    var newSecBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(secondaryTeacherID);
                    var docSnap = await newSecBookedRef.GetSnapshotAsync();
                    if (!docSnap.Exists) { await newSecBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                    await newSecBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayUnion(presentationDate));
                    await resultDocRef.UpdateAsync("secondaryTeacherID", secondaryTeacherID);
                    await resultDocRef.UpdateAsync("secondaryTeacherEmail", secondaryTeacherID);
                }
                // If presentationDate changed
                if (!string.IsNullOrEmpty(presentationDate) && presentationDate != oldPresentationDate)
                {
                    // Remove old date from both teachers
                    if (!string.IsNullOrEmpty(primaryTeacherID) && !string.IsNullOrEmpty(oldPresentationDate))
                    {
                        var primBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(primaryTeacherID);
                        var docSnap = await primBookedRef.GetSnapshotAsync();
                        if (!docSnap.Exists) { await primBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                        await primBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayRemove(oldPresentationDate));
                    }
                    if (!string.IsNullOrEmpty(oldSecondaryTeacherID) && !string.IsNullOrEmpty(oldPresentationDate))
                    {
                        var oldSecBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(oldSecondaryTeacherID);
                        var docSnap = await oldSecBookedRef.GetSnapshotAsync();
                        if (!docSnap.Exists) { await oldSecBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                        await oldSecBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayRemove(oldPresentationDate));
                    }
                    // Add new date to both teachers
                    if (!string.IsNullOrEmpty(primaryTeacherID))
                    {
                        var primBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(primaryTeacherID);
                        var docSnap = await primBookedRef.GetSnapshotAsync();
                        if (!docSnap.Exists) { await primBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                        await primBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayUnion(presentationDate));
                    }
                    if (!string.IsNullOrEmpty(secondaryTeacherID))
                    {
                        var newSecBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(secondaryTeacherID);
                        var docSnap = await newSecBookedRef.GetSnapshotAsync();
                        if (!docSnap.Exists) { await newSecBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                        await newSecBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayUnion(presentationDate));
                    }
                    await resultDocRef.UpdateAsync("presentationDate", presentationDate);
                }
                // Always set status to Scheduled if not set
                await resultDocRef.UpdateAsync("status", "Scheduled");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ChangePresentationStatus(string studentId, string projectId, string status)
        {
            try
            {
                var resultDocRef = _db.Collection("evaluation-project").Document("Evaluation").Collection("results").Document(studentId);
                await resultDocRef.UpdateAsync("status", status);
                // If status is not Scheduled, remove the presentation date from both teachers' bookedDates
                if (status != "Scheduled")
                {
                    var resultSnapshot = await resultDocRef.GetSnapshotAsync();
                    if (resultSnapshot.Exists)
                    {
                        var resultData = resultSnapshot.ToDictionary();
                        var primaryTeacherID = resultData.ContainsKey("primaryTeacherID") ? resultData["primaryTeacherID"].ToString() : "";
                        var secondaryTeacherID = resultData.ContainsKey("secondaryTeacherID") ? resultData["secondaryTeacherID"].ToString() : "";
                        var presentationDate = resultData.ContainsKey("presentationDate") ? resultData["presentationDate"].ToString() : "";
                        if (!string.IsNullOrEmpty(primaryTeacherID) && !string.IsNullOrEmpty(presentationDate))
                        {
                            var primBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(primaryTeacherID);
                            var docSnap = await primBookedRef.GetSnapshotAsync();
                            if (!docSnap.Exists) { await primBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                            await primBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayRemove(presentationDate));
                        }
                        if (!string.IsNullOrEmpty(secondaryTeacherID) && !string.IsNullOrEmpty(presentationDate))
                        {
                            var secBookedRef = _db.Collection("evaluation-project").Document("BookedDates").Collection("DatesThatBooked").Document(secondaryTeacherID);
                            var docSnap = await secBookedRef.GetSnapshotAsync();
                            if (!docSnap.Exists) { await secBookedRef.SetAsync(new Dictionary<string, object> { { "bookedDates", new List<string>() } }); }
                            await secBookedRef.UpdateAsync("bookedDates", FieldValue.ArrayRemove(presentationDate));
                        }
                    }
                }
                await LogAdminAction("ChangePresentationStatus", $"Changed status for student {studentId} in project {projectId} to {status}");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetProjectDetailsByStudentId(string studentId)
        {
            if (!IsAdmin())
                return Json(new { success = false, message = "Unauthorized" });
            try
            {
                var projectsSnapshot = await _db.Collection("evaluation-project").Document("ProjectList").Collection("Projects").GetSnapshotAsync();
                foreach (var projectDoc in projectsSnapshot.Documents)
                {
                    var pdata = projectDoc.ToDictionary();
                    if (pdata.ContainsKey("enrolled") && ((List<object>)pdata["enrolled"]).Select(x => x.ToString()).Contains(studentId))
                    {
                        return Json(new {
                            success = true,
                            projectId = projectDoc.Id,
                            title = pdata.ContainsKey("title") ? pdata["title"]?.ToString() : "",
                            professorEmail = pdata.ContainsKey("professorEmail") ? pdata["professorEmail"]?.ToString() : ""
                        });
                    }
                }
                return Json(new { success = false, message = "Student is not enrolled in any project." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
} 