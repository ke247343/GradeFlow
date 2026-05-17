using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
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

            double averageGrade = 0;
            if (await _context.Submissions.AnyAsync(s => s.Grade.HasValue))
            {
                averageGrade = await _context.Submissions
                    .Where(s => s.Grade.HasValue)
                    .AverageAsync(s => s.Grade ?? 0);
            }

            ViewBag.TotalUsers = totalUsers;
            ViewBag.TotalCourses = totalCourses;
            ViewBag.TotalAssignments = totalAssignments;
            ViewBag.TotalSubmissions = totalSubmissions;
            ViewBag.AverageGrade = averageGrade;

            // Load the recent audit logs into the panel feed natively
            var logs = await _context.AuditLogs.OrderByDescending(l => l.Timestamp).Take(10).ToListAsync();
            return View(logs);
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

            // LOG AUDIT ACTIVITY
            _context.AuditLogs.Add(new AuditLog
            {
                ExecutedByEmail = User.Identity?.Name ?? "System Admin",
                OperationAction = "ROLE_CHANGE",
                DetailsContext = $"Altered role constraints mapping for target account user: {user.Email} to {role} explicitly."
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Role '{role}' assigned to {user.Email}.";
            return RedirectToAction(nameof(Users));
        }

        // POST: Admin/DeleteUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // SECURITY DEFENSIVE CHECK: Prevent the currently logged-in Admin from purging themselves
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (user.Id == currentUserId)
            {
                TempData["Error"] = "Security clearance violation: You cannot purge your own active administrative session account.";
                return RedirectToAction(nameof(Users));
            }

            // Strip existing authorization rules maps to cleanly sever relational dependencies
            var userRoles = await _userManager.GetRolesAsync(user);
            if (userRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, userRoles);
            }

            // Execute the terminal removal query command from Core Identity frameworks
            var operationResult = await _userManager.DeleteAsync(user);
            if (operationResult.Succeeded)
            {
                // LOG AUDIT TRAIL DATA RECOVERY METRICS
                _context.AuditLogs.Add(new AuditLog
                {
                    ExecutedByEmail = User.Identity?.Name ?? "System Admin",
                    OperationAction = "PURGE_USER",
                    DetailsContext = $"Permanently erased user credentials account from platform infrastructure registers: {user.Email}."
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = $"User profile context for '{user.Email}' successfully scrubbed from identity storage systems.";
            }
            else
            {
                TempData["Error"] = "An unhandled validation anomaly occurred within Identity Core while destroying the user record tracking blocks.";
            }

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/TrashCan
        public async Task<IActionResult> TrashCan()
        {
            var deletedCourses = await _context.Courses
                .IgnoreQueryFilters()
                .Where(c => c.DeletedAt != null)
                .ToListAsync();

            var deletedAssignments = await _context.Assignments
                .IgnoreQueryFilters()
                .Where(a => a.DeletedAt != null)
                .Include(a => a.Course)
                .ToListAsync();

            ViewBag.DeletedCourses = deletedCourses;
            ViewBag.DeletedAssignments = deletedAssignments;

            return View();
        }

        // POST: Admin/RestoreCourse/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreCourse(int id)
        {
            var course = await _context.Courses.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return NotFound();

            course.DeletedAt = null;
            _context.Update(course);

            _context.AuditLogs.Add(new AuditLog
            {
                ExecutedByEmail = User.Identity?.Name ?? "System Admin",
                OperationAction = "RESTORE_COURSE",
                DetailsContext = $"Restored soft-deleted course module track: {course.Code} ({course.Title})."
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Course module '{course.Code}' restored successfully back to execution rosters!";
            return RedirectToAction(nameof(TrashCan));
        }

        // POST: Admin/PurgeCourse/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurgeCourse(int id)
        {
            var course = await _context.Courses.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
            if (course == null) return NotFound();

            _context.Courses.Remove(course);

            _context.AuditLogs.Add(new AuditLog
            {
                ExecutedByEmail = User.Identity?.Name ?? "System Admin",
                OperationAction = "PURGE_COURSE",
                DetailsContext = $"Permanently purged course module record track from physical architecture: {course.Code}."
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Course module '{course.Code}' has been permanently purged from database registers.";
            return RedirectToAction(nameof(TrashCan));
        }

        // POST: Admin/RestoreAssignment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreAssignment(int id)
        {
            var assignment = await _context.Assignments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null) return NotFound();

            assignment.DeletedAt = null;
            _context.Update(assignment);

            _context.AuditLogs.Add(new AuditLog
            {
                ExecutedByEmail = User.Identity?.Name ?? "System Admin",
                OperationAction = "RESTORE_ASSIGNMENT",
                DetailsContext = $"Restored soft-deleted task item context: {assignment.Title}."
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Assignment framework task element '{assignment.Title}' restored successfully!";
            return RedirectToAction(nameof(TrashCan));
        }

        // POST: Admin/PurgeAssignment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurgeAssignment(int id)
        {
            var assignment = await _context.Assignments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null) return NotFound();

            _context.Assignments.Remove(assignment);

            _context.AuditLogs.Add(new AuditLog
            {
                ExecutedByEmail = User.Identity?.Name ?? "System Admin",
                OperationAction = "PURGE_ASSIGNMENT",
                DetailsContext = $"Permanently wiped task element block structure out of data storage: {assignment.Title}."
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Task component structure '{assignment.Title}' purged completely.";
            return RedirectToAction(nameof(TrashCan));
        }

        // GET: Admin/Courses
        public async Task<IActionResult> Courses()
        {
            var courses = await _context.Courses
                .Select(c => new AdminCourseViewModel
                {
                    Id = c.Id,
                    Title = c.Title,
                    Code = c.Code,
                    InstructorEmail = c.Instructor != null ? (c.Instructor.Email ?? "No Email") : "Unknown",
                    Term = c.Term,
                    EnrollmentCount = c.Enrollments != null ? c.Enrollments.Count() : 0,
                    AssignmentCount = _context.Assignments.Count(a => a.CourseId == c.Id)
                })
                .OrderBy(c => c.Code)
                .ToListAsync();

            return View(courses);
        }
    }
}
