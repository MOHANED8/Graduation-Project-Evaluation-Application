using Microsoft.AspNetCore.Mvc;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using System.IO;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace SoftwareProject.Controllers
{
    public class LoginController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                // Try CATS (Student) Login
                var options = new ChromeOptions();
                options.AddArgument("--headless");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");

                using (var driver = new ChromeDriver(options))
                {
                    driver.Navigate().GoToUrl("https://cats.iku.edu.tr/access/login");
                    driver.FindElement(By.Id("eid")).SendKeys(username);
                    driver.FindElement(By.Id("pw")).SendKeys(password);
                    driver.FindElement(By.Id("submit")).Click();

                    await Task.Delay(3000);

                    bool loginSuccess = !driver.Url.Contains("access/login");

                    if (loginSuccess)
                    {
                        HttpContext.Session.SetString("Username", username);
                        HttpContext.Session.SetString("Role", "Student");
                        return RedirectToAction("Index", "Home");
                    }
                }

                // Try Admin Login from Firestore
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json");

                FirestoreDb db = FirestoreDb.Create("evaluation-project-31025", new FirestoreClientBuilder
                {
                    CredentialsPath = path
                }.Build());

                DocumentReference adminDocRef = db
                    .Collection("evaluation-project")
                    .Document("Admin");

                DocumentSnapshot adminSnapshot = await adminDocRef.GetSnapshotAsync();
                if (adminSnapshot.Exists)
                {
                    string adminUser = adminSnapshot.GetValue<string>("User Name");
                    string adminPass = adminSnapshot.GetValue<string>("Password");
                    if (username == adminUser && password == adminPass)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, username),
                            new Claim(ClaimTypes.Role, "Admin")
                        };
                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = true
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);

                        HttpContext.Session.SetString("Username", username);
                        HttpContext.Session.SetString("Role", "Admin");
                        return RedirectToAction("Dashboard", "Admin");
                    }
                }

                // Try Teacher Login from Firestore
                DocumentReference docRef = db
                    .Collection("evaluation-project")
                    .Document("Professor")
                    .Collection("Academician")
                    .Document(username);

                DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();
                if (!snapshot.Exists)
                {
                    TempData["Error"] = "Login failed for all user types.";
                    return RedirectToAction("Login");
                }

                string correctPassword = snapshot.GetValue<string>("pass");
                if (password != correctPassword)
                {
                    TempData["Error"] = "Incorrect teacher credentials.";
                    return RedirectToAction("Login");
                }

                HttpContext.Session.SetString("TeacherId", username);
                HttpContext.Session.SetString("Role", "Teacher");
                return RedirectToAction("Dashboard", "Teacher");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Login error: " + ex.Message;
                return RedirectToAction("Login");
            }
        }
    }
}
