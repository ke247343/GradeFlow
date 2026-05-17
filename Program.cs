using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;

var builder = WebApplication.CreateBuilder(args);

// Add PostgreSQL DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity with roles
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()          // 👈 THIS ENABLES ROLE MANAGER
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add MVC and Razor Pages
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Seed roles and admin user
using (var scope = app.Services.CreateScope())
{
    await RoleInitializer.InitializeAsync(scope.ServiceProvider);
}
// AUTOMATED DATA SEEDING CONTEXT TRIGGER
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Run database execution seeds asynchronously on startup sequence
        await GradeFlow.Data.DbInitializer.SeedAsync(userManager, roleManager, context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An anomaly occurred during the database seeding cycle process initialization.");
    }
}

app.Run();
