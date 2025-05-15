using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using SoftwareProject.Models;

namespace SoftwareProject.Services
{
    public class FirebaseService
    {
        private readonly FirestoreDb _db;
        private const string PROJECTS_COLLECTION = "projects";
        private const string STUDENTS_COLLECTION = "students";
        private const string TEACHERS_COLLECTION = "teachers";
        private const string MEETINGS_COLLECTION = "meetings";
        private const string ACTIVITY_LOGS_COLLECTION = "activity_logs";

        public FirebaseService(FirestoreDb db)
        {
            _db = db;
        }

        // Project Management Methods
        public async Task<List<ProjectModel>> GetAllProjects(bool onlyApproved = false)
        {
            var projects = new List<ProjectModel>();
            var snapshot = await _db.Collection("evaluation-project")
                .Document("ProjectList")
                .Collection("Projects")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                var status = data.GetValueOrDefault("Status", "Pending").ToString();
                if (onlyApproved && status != "Approved") continue;
                projects.Add(new ProjectModel
                {
                    Id = doc.Id,
                    Title = data.GetValueOrDefault("title", "").ToString(),
                    Description = data.GetValueOrDefault("description", "").ToString(),
                    ProfessorEmail = data.GetValueOrDefault("professorEmail", "").ToString(),
                    TotalSlots = Convert.ToInt32(data.GetValueOrDefault("totalSlots", 0)),
                    Enrolled = data.ContainsKey("enrolled") ? ((List<object>)data["enrolled"]).Select(x => x.ToString()).ToList() : new List<string>(),
                    Requested = data.ContainsKey("requested") ? ((List<object>)data["requested"]).Select(x => x.ToString()).ToList() : new List<string>(),
                    Status = status,
                    IsArchived = data.ContainsKey("IsArchived") && Convert.ToBoolean(data["IsArchived"]),
                    Progress = Convert.ToInt32(data.GetValueOrDefault("Progress", 0))
                });
            }
            return projects;
        }

        public async Task<ProjectModel> GetProject(string id)
        {
            var doc = await _db.Collection(PROJECTS_COLLECTION).Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<ProjectModel>() : null;
        }

        public async Task AddProject(ProjectModel project)
        {
            var docRef = _db.Collection(PROJECTS_COLLECTION).Document(project.Id);
            await docRef.SetAsync(project);
        }

        public async Task UpdateProject(ProjectModel project)
        {
            var docRef = _db.Collection(PROJECTS_COLLECTION).Document(project.Id);
            await docRef.SetAsync(project, SetOptions.MergeAll);
        }

        public async Task DeleteProject(string id)
        {
            await _db.Collection(PROJECTS_COLLECTION).Document(id).DeleteAsync();
        }

        // Student Management Methods
        public async Task<List<StudentModel>> GetAllStudents()
        {
            var students = new List<StudentModel>();
            var snapshot = await _db.Collection("evaluation-project")
                .Document("Student")
                .Collection("Students")
                .GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                if (data == null) continue;

                // Safe parsing for IsActive
                bool isActive = false;
                if (data.TryGetValue("IsActive", out var isActiveObj) && isActiveObj != null)
                {
                    bool.TryParse(isActiveObj.ToString(), out isActive);
                }

                students.Add(new StudentModel
                {
                    Id = doc.Id ?? string.Empty,
                    FirstName = data.ContainsKey("FirstName") && data["FirstName"] != null ? data["FirstName"].ToString() : "",
                    LastName = data.ContainsKey("LastName") && data["LastName"] != null ? data["LastName"].ToString() : "",
                    Email = data.ContainsKey("Email") && data["Email"] != null ? data["Email"].ToString() : "",
                    Department = data.ContainsKey("Department") && data["Department"] != null ? data["Department"].ToString() : "",
                    Phone = data.ContainsKey("Phone") && data["Phone"] != null ? data["Phone"].ToString() : "",
                    Role = data.ContainsKey("Role") && data["Role"] != null ? data["Role"].ToString() : "Student",
                    IsActive = isActive,
                    Password = data.ContainsKey("Password") && data["Password"] != null ? data["Password"].ToString() : ""
                });
            }
            return students ?? new List<StudentModel>();
        }

        public async Task<bool> AddStudent(StudentModel student)
        {
            try
            {
                var studentData = new Dictionary<string, object>
                {
                    { "FirstName", student.FirstName },
                    { "LastName", student.LastName },
                    { "Email", student.Email },
                    { "Department", student.Department },
                    { "Phone", student.Phone },
                    { "Role", student.Role },
                    { "Password", student.Password },
                    { "IsActive", student.IsActive }
                };

                await _db.Collection("evaluation-project")
                    .Document("Student")
                    .Collection("Students")
                    .Document(student.Id)
                    .SetAsync(studentData);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // Teacher Management Methods
        public async Task<List<TeacherModel>> GetAllTeachers()
        {
            try
            {
                var snapshot = await _db.Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .GetSnapshotAsync();

                var teachers = new List<TeacherModel>();
                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();
                    teachers.Add(new TeacherModel
                    {
                        Id = doc.Id,
                        FirstName = data.GetValueOrDefault("FirstName", "").ToString(),
                        LastName = data.GetValueOrDefault("LastName", "").ToString(),
                        Email = data.GetValueOrDefault("Email", "").ToString(),
                        Role = "Teacher",
                        IsActive = Convert.ToBoolean(data.GetValueOrDefault("IsActive", true)),
                        Password = data.GetValueOrDefault("Password", "").ToString()
                    });
                }
                return teachers;
            }
            catch (Exception ex)
            {
                return new List<TeacherModel>();
            }
        }

        // Meeting Management Methods
        public async Task<List<MeetingModel>> GetAllMeetings()
        {
            var snapshot = await _db.Collection(MEETINGS_COLLECTION).GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<MeetingModel>()).ToList();
        }

        public async Task<MeetingModel> GetMeeting(string id)
        {
            var doc = await _db.Collection(MEETINGS_COLLECTION).Document(id).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<MeetingModel>() : null;
        }

        public async Task AddMeeting(MeetingModel meeting)
        {
            var docRef = _db.Collection(MEETINGS_COLLECTION).Document(meeting.Id);
            await docRef.SetAsync(meeting);
        }

        public async Task UpdateMeeting(MeetingModel meeting)
        {
            var docRef = _db.Collection(MEETINGS_COLLECTION).Document(meeting.Id);
            await docRef.SetAsync(meeting, SetOptions.MergeAll);
        }

        public async Task DeleteMeeting(string id)
        {
            await _db.Collection(MEETINGS_COLLECTION).Document(id).DeleteAsync();
        }

        // Activity Logging
        public async Task AddActivityLog(ActivityLog log)
        {
            var docRef = _db.Collection(ACTIVITY_LOGS_COLLECTION).Document(log.Id);
            await docRef.SetAsync(log);
        }

        public async Task<List<ActivityLog>> GetActivityLogs()
        {
            var snapshot = await _db.Collection(ACTIVITY_LOGS_COLLECTION)
                .OrderByDescending("Timestamp")
                .GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<ActivityLog>()).ToList();
        }
    }
} 