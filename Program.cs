using FirebaseAdmin;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login";
        options.AccessDeniedPath = "/Login/Login";
    });

// Initialize Firebase Admin
FirebaseApp.Create(new AppOptions()
{
    Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile("evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json")
});

// Firestore setup
builder.Services.AddSingleton(provider =>
{
    var projectId = "evaluation-project-31025";
    var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile("evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json");
    return FirestoreDb.Create(projectId, new FirestoreClientBuilder { Credential = credential }.Build());
});

// Register FirebaseService for DI
builder.Services.AddScoped<SoftwareProject.Services.FirebaseService>();

// StorageClient setup
builder.Services.AddSingleton(provider =>
{
    var credential = Google.Apis.Auth.OAuth2.GoogleCredential
        .FromFile("evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json")
        .CreateScoped("https://www.googleapis.com/auth/devstorage.full_control");

    return StorageClient.Create(credential);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Login}/{id?}");

app.Run();
