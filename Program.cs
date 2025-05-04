using Firebase.Database;
using Google.Api;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

// Firestore setup
builder.Services.AddSingleton(provider =>
{
    var projectId = "evaluation-project-31025";
    var credential = GoogleCredential.FromFile("evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json");
    return FirestoreDb.Create(projectId, new FirestoreClientBuilder { Credential = credential }.Build());
});

// StorageClient setup
builder.Services.AddSingleton(provider =>
{
    var credential = GoogleCredential
        .FromFile("evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json")
        .CreateScoped("https://www.googleapis.com/auth/devstorage.full_control");

    return StorageClient.Create(credential);
});

// Optional: for FirebaseOptions binding (if used)
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection("Firebase"));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();  // Only once
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Login}/{id?}");

app.Run();
