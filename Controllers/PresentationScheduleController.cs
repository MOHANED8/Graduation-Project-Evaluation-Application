using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using SoftwareProject.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

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
                    // Check if this project is already scheduled
                    var resultDoc = await _db.Collection("evaluation-project")
                        .Document("Evaluation")
                        .Collection("results")
                        .Document(enrolled.First())
                        .GetSnapshotAsync();

                    var isScheduled = false;
                    var scheduledDate = "";
                    var secondaryTeacherId = "";
                    var secondaryTeacherName = "";
                    var secondaryTeacherEmail = "";

                    if (resultDoc.Exists)
                    {
                        var resultData = resultDoc.ToDictionary();
                        string status = resultData.ContainsKey("status") ? resultData["status"].ToString() : "";
                        if (resultData.ContainsKey("presentationDate"))
                        {
                            isScheduled = true;
                            scheduledDate = resultData["presentationDate"].ToString();
                            
                            // Get secondary teacher info
                            if (resultData.ContainsKey("secondaryTeacherEmail"))
                            {
                                secondaryTeacherId = resultData["secondaryTeacherEmail"].ToString();
                                var secondTeacherDoc = await _db.Collection("evaluation-project")
                                    .Document("Professor")
                                    .Collection("Academician")
                                    .Document(secondaryTeacherId)
                                    .GetSnapshotAsync();
                                
                                if (secondTeacherDoc.Exists)
                                {
                                    var teacherData = secondTeacherDoc.ToDictionary();
                                    string firstName = teacherData.ContainsKey("First Name") ? teacherData["First Name"].ToString() : "";
                                    string lastName = teacherData.ContainsKey("Last Name") ? teacherData["Last Name"].ToString() : "";
                                    secondaryTeacherName = (firstName + " " + lastName).Trim();
                                    if (string.IsNullOrWhiteSpace(secondaryTeacherName)) secondaryTeacherName = "N/A";
                                    if (teacherData.ContainsKey("Mail")) secondaryTeacherEmail = teacherData["Mail"].ToString();
                                }
                            }
                        }
                        projectList.Add(new ProjectScheduleViewModel
                        {
                            ProjectId = doc.Id,
                            ProjectTitle = title,
                            Description = description,
                            EnrolledStudents = enrolled,
                            IsScheduled = isScheduled,
                            ScheduledDate = scheduledDate,
                            SecondaryTeacherId = secondaryTeacherId,
                            SecondaryTeacherName = secondaryTeacherName,
                            SecondaryTeacherEmail = secondaryTeacherEmail,
                            Status = status
                        });
                        continue;
                    }

                    projectList.Add(new ProjectScheduleViewModel
                    {
                        ProjectId = doc.Id,
                        ProjectTitle = title,
                        Description = description,
                        EnrolledStudents = enrolled,
                        IsScheduled = isScheduled,
                        ScheduledDate = scheduledDate,
                        SecondaryTeacherId = secondaryTeacherId,
                        SecondaryTeacherName = secondaryTeacherName,
                        SecondaryTeacherEmail = secondaryTeacherEmail,
                        Status = ""
                    });
                }
            }

            // Get all teachers except the current one
            var allTeachersSnapshot = await _db.Collection("evaluation-project")
                .Document("Professor")
                .Collection("Academician")
                .GetSnapshotAsync();
            var allTeachers = allTeachersSnapshot.Documents
                .Where(doc => doc.Id != professorEmail)
                .Select(doc => {
                    var d = doc.ToDictionary();
                    return new TeacherAvailability {
                        Id = doc.Id,
                        Email = d.ContainsKey("email") ? d["email"].ToString() : doc.Id,
                        Name = (d.ContainsKey("First Name") ? d["First Name"].ToString() : "") + " " + (d.ContainsKey("Last Name") ? d["Last Name"].ToString() : "")
                    };
                })
                .ToList();
            ViewBag.AllTeachers = allTeachers;

            return View(projectList);
        }

        public async Task<IActionResult> Schedule(string projectId, string secondTeacherId)
        {
            List<string?> availableTimesRaw = null;
            List<string> availableTimes = new List<string>();
            string secondTeacherName = "N/A";
            string secondTeacherMail = secondTeacherId;
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

            // Get the main teacher's numeric ID from the project data
            string mainTeacherId = projectData.ContainsKey("professorEmail") ? projectData["professorEmail"].ToString() : "";
            // Fetch main teacher info using numeric ID
            var mainTeacherDoc = await _db.Collection("evaluation-project")
                .Document("Professor")
                .Collection("Academician")
                .Document(mainTeacherId)
                .GetSnapshotAsync();
            string mainTeacherName = "N/A";
            string mainTeacherMail = "N/A";
            if (mainTeacherDoc.Exists)
            {
                var d = mainTeacherDoc.ToDictionary();
                string firstName = d.ContainsKey("First Name") ? d["First Name"].ToString() : "";
                string lastName = d.ContainsKey("Last Name") ? d["Last Name"].ToString() : "";
                mainTeacherName = (firstName + " " + lastName).Trim();
                if (string.IsNullOrWhiteSpace(mainTeacherName)) mainTeacherName = "N/A";
                if (d.ContainsKey("Mail")) mainTeacherMail = d["Mail"].ToString();
            }
            var mainTeacherAvailabilityDoc = await _db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(mainTeacherId)
                .GetSnapshotAsync();
            List<string> mainAvailableTimes = mainTeacherAvailabilityDoc.Exists && mainTeacherAvailabilityDoc.ContainsField("availableDates")
                ? mainTeacherAvailabilityDoc.GetValue<List<string>>("availableDates") ?? new List<string>()
                : new List<string>();
            ViewBag.MainTeacherId = mainTeacherId;
            ViewBag.MainTeacherName = mainTeacherName;
            ViewBag.MainTeacherEmail = mainTeacherMail;
            ViewBag.MainTeacherAvailableTimes = mainAvailableTimes;

            // Always fetch and set second teacher info for debug section
            List<string> secondAvailableTimes = new List<string>();
            if (!string.IsNullOrEmpty(secondTeacherId))
            {
                var secondTeacherDoc = await _db.Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .Document(secondTeacherId)
                    .GetSnapshotAsync();
                if (secondTeacherDoc.Exists)
                {
                    var d = secondTeacherDoc.ToDictionary();
                    string firstName = d.ContainsKey("First Name") ? d["First Name"].ToString() : "";
                    string lastName = d.ContainsKey("Last Name") ? d["Last Name"].ToString() : "";
                    secondTeacherName = (firstName + " " + lastName).Trim();
                    if (string.IsNullOrWhiteSpace(secondTeacherName)) secondTeacherName = "N/A";
                    if (d.ContainsKey("Mail")) secondTeacherMail = d["Mail"].ToString();
                }
                var secondTeacherAvailabilityDoc = await _db.Collection("evaluation-project")
                    .Document("BookedDates")
                    .Collection("DatesThatBooked")
                    .Document(secondTeacherId)
                    .GetSnapshotAsync();
                secondAvailableTimes = secondTeacherAvailabilityDoc.Exists && secondTeacherAvailabilityDoc.ContainsField("availableDates")
                    ? secondTeacherAvailabilityDoc.GetValue<List<string>>("availableDates") ?? new List<string>()
                    : new List<string>();
            }
            ViewBag.SecondTeacherId = secondTeacherId;
            ViewBag.SecondTeacherName = secondTeacherName;
            ViewBag.SecondTeacherEmail = secondTeacherMail;
            ViewBag.SecondTeacherAvailableTimes = secondAvailableTimes;

            // Fetch primary teacher availability
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
            if (teacherDates == null || teacherDates.Count == 0)
            {
                TempData["ScheduleMessage"] = "No available dates found.";
                return RedirectToAction("Index");
            }

            // Find a second available teacher
            var allTeachersSnapshot = await _db.Collection("evaluation-project")
                                              .Document("Professor")
                                              .Collection("Academician")
                                              .GetSnapshotAsync();

            string availableSecondTeacherId = secondTeacherId;
            if (string.IsNullOrEmpty(availableSecondTeacherId))
            {
                availableSecondTeacherId = await FindAvailableSecondTeacher(null, professorEmail, allTeachersSnapshot);
            }
            string availableSecondTeacherEmail = null;
            if (availableSecondTeacherId != null)
            {
                var secondTeacherDoc = await _db.Collection("evaluation-project")
                                                .Document("Professor")
                                                .Collection("Academician")
                                                .Document(availableSecondTeacherId)
                                                .GetSnapshotAsync();
                if (secondTeacherDoc.Exists && secondTeacherDoc.ContainsField("email"))
                    availableSecondTeacherEmail = secondTeacherDoc.GetValue<string>("email");
                else
                    availableSecondTeacherEmail = availableSecondTeacherId;
            }

            if (availableSecondTeacherId == null)
            {
                // Get all teachers and their available times
                var teachersWithAvailability = new List<TeacherAvailability>();
                foreach (var otherTeacherDoc in allTeachersSnapshot.Documents)
                {
                    var otherTeacherId = otherTeacherDoc.Id;
                    if (otherTeacherId == professorEmail) continue;
                    var otherTeacherData = otherTeacherDoc.ToDictionary();
                    var firstName = otherTeacherData.ContainsKey("First Name") ? otherTeacherData["First Name"].ToString() : "";
                    var lastName = otherTeacherData.ContainsKey("Last Name") ? otherTeacherData["Last Name"].ToString() : "";
                    var email = otherTeacherData.ContainsKey("email") ? otherTeacherData["email"].ToString() : otherTeacherId;
                    var otherTeacherAvailabilityDoc = await _db.Collection("evaluation-project")
                        .Document("BookedDates")
                        .Collection("DatesThatBooked")
                        .Document(otherTeacherId)
                        .GetSnapshotAsync();
                    availableTimesRaw = otherTeacherAvailabilityDoc.Exists && otherTeacherAvailabilityDoc.ContainsField("availableDates")
                        ? otherTeacherAvailabilityDoc.GetValue<List<string>>("availableDates")
                        : null;
                    availableTimes = (availableTimesRaw ?? new List<string?>()).Where(x => x != null).Select(x => x!).ToList();
                    teachersWithAvailability.Add(new TeacherAvailability
                    {
                        Email = email,
                        Name = $"{firstName} {lastName}",
                        NextAvailableDate = availableTimes.FirstOrDefault(),
                        StartTime = null,
                        EndTime = null,
                        AllAvailableTimes = availableTimes
                    });
                }
                ViewBag.TeachersWithAvailability = teachersWithAvailability;
                return View("NoSecondTeacher");
            }

            // Get all available slots for both teachers
            var secondTeacherDatesDoc = await _db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(availableSecondTeacherId)
                .GetSnapshotAsync();
            var secondTeacherDates = secondTeacherDatesDoc.Exists && secondTeacherDatesDoc.ContainsField("availableDates")
                ? secondTeacherDatesDoc.GetValue<List<string>>("availableDates")
                : new List<string>();

            // --- Helper methods for merging and adjusting intervals ---
            (DateTime Date, TimeSpan Start, TimeSpan End) ParseAvailability(string slot)
            {
                var parts = slot.Split('|');
                if (parts.Length == 3)
                {
                    var dateStr = parts[0].Trim();
                    var startStr = parts[1].Trim();
                    var endStr = parts[2].Trim();
                    DateTime date = DateTime.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    TimeSpan start = TimeSpan.ParseExact(startStr, "hh\\:mm", CultureInfo.InvariantCulture);
                    TimeSpan end = TimeSpan.ParseExact(endStr, "hh\\:mm", CultureInfo.InvariantCulture);
                    return (date, start, end);
                }
                throw new FormatException($"Unrecognized slot format: {slot}");
            }

            List<(TimeSpan Start, TimeSpan End)> MergeIntervals(List<(TimeSpan Start, TimeSpan End)> intervals)
            {
                if (intervals.Count == 0) return new List<(TimeSpan Start, TimeSpan End)>();
                var sorted = intervals.OrderBy(x => x.Start).ToList();
                var merged = new List<(TimeSpan Start, TimeSpan End)> { sorted[0] };
                for (int i = 1; i < sorted.Count; i++)
                {
                    var last = merged[merged.Count - 1];
                    var current = sorted[i];
                    if (current.Start <= last.End)
                        merged[merged.Count - 1] = (Start: last.Start, End: current.End > last.End ? current.End : last.End);
                    else
                        merged.Add(current);
                }
                return merged;
            }

            // --- Group and merge slots by date for both teachers ---
            Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End)>> GroupAndMerge(List<string> slots)
            {
                var grouped = new Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End)>>();
                foreach (var slot in slots)
                {
                    var (date, start, end) = ParseAvailability(slot);
                    if (!grouped.ContainsKey(date)) grouped[date] = new List<(TimeSpan Start, TimeSpan End)>();
                    grouped[date].Add((Start: start, End: end));
                }
                // Merge intervals for each date
                var merged = new Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End)>>();
                foreach (var kvp in grouped)
                {
                    merged[kvp.Key] = MergeIntervals(kvp.Value);
                }
                return merged;
            }

            var teacherMerged = GroupAndMerge(teacherDates);
            var secondTeacherMerged = GroupAndMerge(secondTeacherDates);

            // Add merged slots to ViewBag for display
            ViewBag.TeacherSlots = teacherMerged.SelectMany(kvp => kvp.Value.Select(interval => $"{kvp.Key:yyyy-MM-dd} {interval.Start:hh\\:mm}-{interval.End:hh\\:mm}")).ToList();
            ViewBag.SecondTeacherSlots = secondTeacherMerged.SelectMany(kvp => kvp.Value.Select(interval => $"{kvp.Key:yyyy-MM-dd} {interval.Start:hh\\:mm}-{interval.End:hh\\:mm}")).ToList();

            // --- Find 1-hour overlap after adjusting times ---
            (DateTime Date, TimeSpan Start, TimeSpan End)? firstValidSlot = null;
            // Defensive: Ensure teachers are not the same
            if (professorEmail == availableSecondTeacherId)
            {
                TempData["ScheduleMessage"] = "Cannot schedule with yourself as both teachers.";
                return RedirectToAction("Index");
            }
            // Fetch booked dates for both teachers
            var mainTeacherBookedDoc = await _db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(mainTeacherId)
                .GetSnapshotAsync();
            var secondTeacherBookedDoc = await _db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(availableSecondTeacherId)
                .GetSnapshotAsync();
            var mainTeacherBookedDates = mainTeacherBookedDoc.Exists && mainTeacherBookedDoc.ContainsField("bookedDates")
                ? mainTeacherBookedDoc.GetValue<List<string>>("bookedDates") ?? new List<string>()
                : new List<string>();
            var secondTeacherBookedDates = secondTeacherBookedDoc.Exists && secondTeacherBookedDoc.ContainsField("bookedDates")
                ? secondTeacherBookedDoc.GetValue<List<string>>("bookedDates") ?? new List<string>()
                : new List<string>();

            // Sort dates to ensure earliest is picked
            bool foundSlot = false;
            foreach (var date in teacherMerged.Keys.Intersect(secondTeacherMerged.Keys).OrderBy(d => d))
            {
                foreach (var t1 in teacherMerged[date].OrderBy(x => x.Start))
                {
                    foreach (var t2 in secondTeacherMerged[date].OrderBy(x => x.Start))
                    {
                        var overlapStartRaw = t1.Start > t2.Start ? t1.Start : t2.Start;
                        var overlapEndRaw = t1.End < t2.End ? t1.End : t2.End;
                        if ((overlapEndRaw - overlapStartRaw).TotalMinutes >= 60)
                        {
                            var adjStart = overlapStartRaw;
                            var adjEnd = overlapEndRaw;
                            if ((adjEnd - adjStart).TotalMinutes >= 60)
                            {
                                string candidateSlot = $"{date:yyyy-MM-dd}|{adjStart:hh\\:mm}|{adjStart.Add(TimeSpan.FromHours(1)):hh\\:mm}";
                                // Check if this slot is already booked for either teacher
                                if (!mainTeacherBookedDates.Contains(candidateSlot) && !secondTeacherBookedDates.Contains(candidateSlot))
                                {
                                    firstValidSlot = (date, adjStart, adjStart.Add(TimeSpan.FromHours(1)));
                                    foundSlot = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (foundSlot) break;
                }
                if (foundSlot) break;
            }
            if (firstValidSlot == null)
            {
                // No available slot found, show no match page
                var teachersWithAvailability = new List<TeacherAvailability>();
                foreach (var otherTeacherDoc in allTeachersSnapshot.Documents)
                {
                    var otherTeacherId = otherTeacherDoc.Id;
                    if (otherTeacherId == professorEmail) continue;
                    var otherTeacherData = otherTeacherDoc.ToDictionary();
                    var firstName = otherTeacherData.ContainsKey("First Name") ? otherTeacherData["First Name"].ToString() : "";
                    var lastName = otherTeacherData.ContainsKey("Last Name") ? otherTeacherData["Last Name"].ToString() : "";
                    var email = otherTeacherData.ContainsKey("email") ? otherTeacherData["email"].ToString() : otherTeacherId;
                    var otherTeacherAvailabilityDoc = await _db.Collection("evaluation-project")
                        .Document("BookedDates")
                        .Collection("DatesThatBooked")
                        .Document(otherTeacherId)
                        .GetSnapshotAsync();
                    availableTimesRaw = otherTeacherAvailabilityDoc.Exists && otherTeacherAvailabilityDoc.ContainsField("availableDates")
                        ? otherTeacherAvailabilityDoc.GetValue<List<string>>("availableDates")
                        : null;
                    availableTimes = (availableTimesRaw ?? new List<string?>()).Where(x => x != null).Select(x => x!).ToList();
                    teachersWithAvailability.Add(new TeacherAvailability
                    {
                        Email = email,
                        Name = $"{firstName} {lastName}",
                        NextAvailableDate = availableTimes.FirstOrDefault(),
                        StartTime = null,
                        EndTime = null,
                        AllAvailableTimes = availableTimes
                    });
                }
                ViewBag.TeachersWithAvailability = teachersWithAvailability;
                return View("NoSecondTeacher");
            }
            var selectedSlot = firstValidSlot.Value;
            string selectedDate = selectedSlot.Date.ToString("yyyy-MM-dd");
            string selectedStart = selectedSlot.Start.ToString(@"hh\:mm");
            string selectedEnd = selectedSlot.End.ToString(@"hh\:mm");
            string scheduledSlot = $"{selectedDate}|{selectedStart}|{selectedEnd}";

            // Save scheduled date with both teachers
            var resultRef = _db.Collection("evaluation-project")
                       .Document("Evaluation")
                       .Collection("results")
                       .Document(enrolledStudents.First());

            var resultData = new Dictionary<string, object>
            {
                { "projectId", projectId },
                { "primaryTeacherEmail", mainTeacherId },
                { "secondaryTeacherEmail", availableSecondTeacherEmail },
                { "presentationDate", scheduledSlot },
                { "text22", "" },
                { "totalGrateToPercent", 0 }
            };

            await resultRef.SetAsync(resultData, SetOptions.MergeAll);

            // Update booked dates for both teachers
            mainTeacherBookedDates.Add(scheduledSlot);
            secondTeacherBookedDates.Add(scheduledSlot);

            await _db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(mainTeacherId)
                .UpdateAsync("bookedDates", mainTeacherBookedDates);

            await _db.Collection("evaluation-project")
                .Document("BookedDates")
                .Collection("DatesThatBooked")
                .Document(availableSecondTeacherId)
                .UpdateAsync("bookedDates", secondTeacherBookedDates);

            // Pass data to view
            ViewBag.ProjectTitle = title;
            ViewBag.Description = description;
            ViewBag.ScheduledDate = $"{selectedDate} ({selectedStart} - {selectedEnd})";
            ViewBag.EnrolledStudents = enrolledStudents;
            ViewBag.PrimaryTeacher = mainTeacherMail;
            ViewBag.SecondaryTeacher = secondTeacherMail;

            // Fetch and pass detailed info for evaluators and students

            // --- Evaluators ---
            var primaryTeacherInfo = new Dictionary<string, string> {
                ["Id"] = mainTeacherId,
                ["Name"] = mainTeacherName,
                ["Email"] = mainTeacherMail
            };
            var secondaryTeacherInfo = new Dictionary<string, string> {
                ["Id"] = secondTeacherId ?? "",
                ["Name"] = secondTeacherName ?? "",
                ["Email"] = secondTeacherMail ?? ""
            };
            ViewBag.PrimaryTeacherInfo = primaryTeacherInfo;
            ViewBag.SecondaryTeacherInfo = secondaryTeacherInfo;

            // --- Students ---
            string resultProjectId = projectId;
            if (TempData.ContainsKey("LastScheduledProjectId"))
            {
                resultProjectId = TempData["LastScheduledProjectId"].ToString();
            }
            var resultProjectDoc = await _db.Collection("evaluation-project")
                .Document("ProjectList")
                .Collection("Projects")
                .Document(resultProjectId)
                .GetSnapshotAsync();
            var resultProjectData = resultProjectDoc.ToDictionary();
            enrolledStudents = resultProjectData.ContainsKey("enrolled")
                ? ((List<object>)resultProjectData["enrolled"]).Select(x => x.ToString()).ToList()
                : new List<string>();
            var studentsInfo = new List<Dictionary<string, string>>();
            var studentEmails = new List<string>();
            foreach (var studentId in enrolledStudents)
            {
                string email = $"{studentId}@stu.iku.edu.tr";
                studentsInfo.Add(new Dictionary<string, string> {
                    ["Id"] = studentId,
                    ["Name"] = studentId,
                    ["Email"] = email
                });
                studentEmails.Add(email);
            }
            ViewBag.EnrolledStudentsInfo = studentsInfo;

            // --- Mailto Link ---
            string mailTo = string.Join(",", studentEmails);
            string cc = secondaryTeacherInfo["Email"];
            string subject = $"Project Presentation Scheduled: {title}";
            string mailBody = $"Greetings everyone,%0D%0A%0D%0AYour project \"{title}\" will be scheduled to present on {selectedDate} ({selectedStart} - {selectedEnd}) with {secondaryTeacherInfo["Name"]}.%0D%0A%0D%0ABest regards,%0D%0A{primaryTeacherInfo["Name"]}";
            string mailtoLink = $"mailto:{mailTo}?cc={cc}&subject={Uri.EscapeDataString(subject)}&body={mailBody}";
            ViewBag.MailToLink = mailtoLink;

            // Debug: log slot counts
            Console.WriteLine($"teacherMerged.Count: {teacherMerged.Count}, secondTeacherMerged.Count: {secondTeacherMerged.Count}");

            TempData["LastScheduledProjectId"] = projectId;

            return View("ScheduleResult");
        }

        private async Task<string> FindAvailableSecondTeacher(string date, string primaryTeacherEmail, QuerySnapshot allTeachersSnapshot)
        {
            foreach (var otherTeacherDoc in allTeachersSnapshot.Documents)
            {
                var otherTeacherEmail = otherTeacherDoc.Id;
                if (otherTeacherEmail == primaryTeacherEmail) continue;

                var otherTeacherAvailabilityDoc = await _db.Collection("evaluation-project")
                                                      .Document("BookedDates")
                                                      .Collection("DatesThatBooked")
                                                      .Document(otherTeacherEmail)
                                                      .GetSnapshotAsync();

                if (otherTeacherAvailabilityDoc.Exists && otherTeacherAvailabilityDoc.ContainsField("availableDates"))
                {
                    var otherTeacherDates = otherTeacherAvailabilityDoc.GetValue<List<string>>("availableDates");
                    if (otherTeacherDates.Contains(date))
                    {
                        return otherTeacherEmail;
                    }
                }
            }
            return null;
        }

        private async Task<List<TeacherAvailability>> GetTeachersWithAvailability(string primaryTeacherEmail)
        {
            var teachersWithAvailability = new List<TeacherAvailability>();
            var allTeachersSnapshot = await _db.Collection("evaluation-project")
                                              .Document("Professor")
                                              .Collection("Academician")
                                              .GetSnapshotAsync();

            foreach (var otherTeacherDoc in allTeachersSnapshot.Documents)
            {
                var otherTeacherId = otherTeacherDoc.Id;
                if (otherTeacherId == primaryTeacherEmail) continue;

                var otherTeacherData = otherTeacherDoc.ToDictionary();
                var firstName = otherTeacherData.ContainsKey("First Name") ? otherTeacherData["First Name"].ToString() : "";
                var lastName = otherTeacherData.ContainsKey("Last Name") ? otherTeacherData["Last Name"].ToString() : "";
                var email = otherTeacherData.ContainsKey("email") ? otherTeacherData["email"].ToString() : otherTeacherId;

                var otherTeacherAvailabilityDoc = await _db.Collection("evaluation-project")
                                                      .Document("BookedDates")
                                                      .Collection("DatesThatBooked")
                                                      .Document(otherTeacherId)
                                                      .GetSnapshotAsync();

                if (otherTeacherAvailabilityDoc.Exists && otherTeacherAvailabilityDoc.ContainsField("availableDates"))
                {
                    var otherTeacherDates = otherTeacherAvailabilityDoc.GetValue<List<string>>("availableDates");
                    if (otherTeacherDates.Any())
                    {
                        teachersWithAvailability.Add(new TeacherAvailability
                        {
                            Email = email,
                            Name = $"{firstName} {lastName}",
                            NextAvailableDate = otherTeacherDates.First()
                        });
                    }
                }
            }

            return teachersWithAvailability;
        }
    }
}
