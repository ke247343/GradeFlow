using Microsoft.AspNetCore.Identity;
using GradeFlow.Models;

namespace GradeFlow.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            // 1. Ensure Identity Roles are safely seeded
            string[] systemRoles = { "Admin", "Instructor", "Student" };
            foreach (var roleName in systemRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // 2. Seed Default Admin Account
            var adminEmail = "admin@gradeflow.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                var result = await userManager.CreateAsync(adminUser, "GradeFlow2026!");
                if (result.Succeeded) await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 3. Seed Default Instructor Account
            var instructorEmail = "instructor@gradeflow.com";
            if (await userManager.FindByEmailAsync(instructorEmail) == null)
            {
                var instructorUser = new IdentityUser { UserName = instructorEmail, Email = instructorEmail, EmailConfirmed = true };
                var result = await userManager.CreateAsync(instructorUser, "GradeFlow2026!");
                if (result.Succeeded) await userManager.AddToRoleAsync(instructorUser, "Instructor");
            }

            // 4. Seed Default Student Account
            var studentEmail = "student@gradeflow.com";
            if (await userManager.FindByEmailAsync(studentEmail) == null)
            {
                var studentUser = new IdentityUser { UserName = studentEmail, Email = studentEmail, EmailConfirmed = true };
                var result = await userManager.CreateAsync(studentUser, "GradeFlow2026!");
                if (result.Succeeded) await userManager.AddToRoleAsync(studentUser, "Student");
            }
        }
    }
}
