using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;

namespace Controllers
{
    public class StudentController : Controller
    {
        private readonly FirestoreDb _db;

        public StudentController(FirestoreDb db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> ViewFeedback()
        {
            var studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId)) return RedirectToAction("Login", "Login");

            var feedbackSnapshot = await _db.Collection("evaluation-project")
                .Document("Feedback")
                .Collection("ProjectFeedback")
                .WhereEqualTo("studentId", studentId)
                .OrderByDescending("timestamp")
                .GetSnapshotAsync();

            var notificationsSnapshot = await _db.Collection("evaluation-project")
                .Document("Notifications")
                .Collection("StudentNotifications")
                .WhereEqualTo("studentId", studentId)
                .WhereEqualTo("isRead", false)
                .OrderByDescending("timestamp")
                .GetSnapshotAsync();

            var feedback = new List<Dictionary<string, object>>();
            foreach (var doc in feedbackSnapshot.Documents)
            {
                feedback.Add(doc.ToDictionary());
            }

            var notifications = new List<Dictionary<string, object>>();
            foreach (var doc in notificationsSnapshot.Documents)
            {
                notifications.Add(doc.ToDictionary());
            }

            ViewBag.Feedback = feedback;
            ViewBag.Notifications = notifications;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationAsRead(string notificationId)
        {
            var studentId = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(studentId)) return RedirectToAction("Login", "Login");

            await _db.Collection("evaluation-project")
                .Document("Notifications")
                .Collection("StudentNotifications")
                .Document(notificationId)
                .UpdateAsync("isRead", true);

            return RedirectToAction("ViewFeedback");
        }
    }
} 