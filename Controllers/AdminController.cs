using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<IdentityUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var totalCourses = await _context.Courses.CountAsync();
            var totalAssignments = await _context.Assignments.CountAsync();
            var totalSubmissions = await _context.Submissions.CountAsync();

            // Calculate average grade only if there are graded submissions
            double averageGrade = 0;
            if (await _context.Submissions.AnyAsync(s => s.Grade.HasValue))
            {
                averageGrade = await _context.Submissions
                    .Where(s => s.Grade.HasValue)
                    .AverageAsync(s => s.Grade ?? 0);
            }

            var stats = new
            {
                TotalUsers = totalUsers,
                TotalCourses = totalCourses,
                TotalAssignments = totalAssignments,
                TotalSubmissions = totalSubmissions,
                AverageGrade = averageGrade
            };
            return View(stats);
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRoles = new List<UserRoleViewModel>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles.Add(new UserRoleViewModel
                {
                    UserId = user.Id,
                    Email = user.Email ?? "No Email",
                    CurrentRole = roles.FirstOrDefault() ?? "None"
                });
            }
            return View(userRoles);
        }

        // POST: Admin/AssignRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["Success"] = $"Role '{role}' assigned to {user.Email}.";
            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = string.Join(", ", roles);
            return View(user);
        }

        // POST: Admin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Prevent admin from deleting themselves
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "You cannot delete your own admin account.";
                return RedirectToAction(nameof(Users));
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = $"User {user.Email} has been deleted.";
            }
            else
            {
                TempData["Error"] = "Failed to delete user. They may have existing submissions or enrollments.";
            }
            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/Courses
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.Courses
                .Include(c => c.Instructor)
                .Include(c => c.Enrollments)
                .Select(c => new AdminCourseViewModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    Code = c.Code,
                    InstructorEmail = c.Instructor != null ? (c.Instructor.Email ?? "No Email") : "Unknown",
                    Term = c.Term,
                    EnrollmentCount = c.Enrollments != null ? c.Enrollments.Count : 0,
                    AssignmentCount = _context.Assignments.Count(a => a.CourseId == c.Id)
                })
                .OrderBy(c => c.Code)
                .ToListAsync();

            return View(courses);
        }
    }

    // ViewModel for user list
    public class UserRoleViewModel
    {
        public required string UserId { get; set; }
        public required string Email { get; set; }
        public required string CurrentRole { get; set; }
    }

    // ViewModel for course overview
    public class AdminCourseViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string InstructorEmail { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
        public int AssignmentCount { get; set; }
    }
}
