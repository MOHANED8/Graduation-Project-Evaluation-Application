using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using System.IO;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "evaluation-project-31025-firebase-adminsdk-fbsvc-3e3d6a6377.json");
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", path);

        services.AddSingleton(provider =>
        {
            return FirestoreDb.Create("evaluation-project-31025");
        });

        services.AddControllersWithViews();
        services.AddSession(); // Required if you use sessions like professorEmail
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();
        app.UseSession();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        });
    }
}
