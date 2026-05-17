using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize]
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public CoursesController(ApplicationDbContext context,
                                 UserManager<IdentityUser> userManager,
                                 RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Admin"))
            {
                return View(await _context.Courses.Include(c => c.Instructor).ToListAsync());
            }
            else if (User.IsInRole("Instructor"))
            {
                return View(await _context.Courses.Where(c => c.InstructorId == userId).Include(c => c.Instructor).ToListAsync());
            }
            else if (User.IsInRole("Student"))
            {
                return View(await _context.Courses.Include(c => c.Instructor).Where(c => c.Enrollments!.Any(e => e.StudentId == userId)).ToListAsync());
            }
            return View(new List<Course>());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var course = await _context.Courses.Include(c => c.Instructor).FirstOrDefaultAsync(m => m.Id == id);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Instructor") && !_context.Enrollments.Any(e => e.CourseId == id && e.StudentId == userId))
            {
                return Forbid();
            }

            return View(course);
        }

        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("Instructor"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                ViewBag.InstructorEmail = currentUser?.Email;
                ViewBag.HideDropdown = true;
            }
            else if (User.IsInRole("Admin"))
            {
                var users = await _context.Users.ToListAsync();
                ViewBag.InstructorList = new SelectList(users, "Email", "Email");
                ViewBag.HideDropdown = false;
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Create([Bind("Title,Code,Term")] Course course, string? InstructorEmail)
        {
            ModelState.Remove("InstructorId");
            if (ModelState.IsValid)
            {
                string instructorId;
                if (User.IsInRole("Instructor"))
                {
                    instructorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                }
                else
                {
                    if (string.IsNullOrEmpty(InstructorEmail))
                    {
                        ModelState.AddModelError("", "Please select an instructor.");
                        await PopulateInstructors();
                        return View(course);
                    }
                    var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Email == InstructorEmail);
                    if (instructor == null)
                    {
                        ModelState.AddModelError("", "Selected instructor does not exist.");
                        await PopulateInstructors();
                        return View(course);
                    }
                    instructorId = instructor.Id;
                }
                course.InstructorId = instructorId;
                _context.Add(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await PopulateInstructors();
            return View(course);
        }

        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && course.InstructorId != userId) return Forbid();

            var instructor = await _context.Users.FindAsync(course.InstructorId);
            var users = await _context.Users.ToListAsync();
            ViewBag.InstructorList = new SelectList(users, "Email", "Email", instructor?.Email);
            return View(course);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Code,Term")] Course course, string? InstructorEmail)
        {
            if (id != course.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingCourse = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (existingCourse == null) return NotFound();

            if (!User.IsInRole("Admin") && existingCourse.InstructorId != userId) return Forbid();

            ModelState.Remove("InstructorId");
            if (ModelState.IsValid)
            {
                string instructorId = User.IsInRole("Admin") ? existingCourse.InstructorId : userId ?? string.Empty;

                if (User.IsInRole("Admin") && !string.IsNullOrEmpty(InstructorEmail))
                {
                    var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Email == InstructorEmail);
                    if (instructor != null) instructorId = instructor.Id;
                }

                course.InstructorId = instructorId;
                _context.Update(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await PopulateInstructors();
            return View(course);
        }

        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var course = await _context.Courses.Include(c => c.Instructor).FirstOrDefaultAsync(m => m.Id == id);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && course.InstructorId != userId) return Forbid();

            return View(course);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course != null)
            {
                // SOFT DELETE INTERCEPTION EXECUTION
                course.DeletedAt = DateTime.UtcNow;
                _context.Update(course);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Enroll(int? id)
        {
            if (id == null) return NotFound();
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && course.InstructorId != userId) return Forbid();

            var studentIdsInRole = await _userManager.GetUsersInRoleAsync("Student");
            var enrolledStudentIds = await _context.Enrollments.Where(e => e.CourseId == id).Select(e => e.StudentId).ToListAsync();
            var availableStudents = studentIdsInRole.Where(s => !enrolledStudentIds.Contains(s.Id)).Select(s => new { s.Id, s.Email }).ToList();

            if (!availableStudents.Any()) ViewBag.NoStudents = true;
            else ViewBag.Students = new SelectList(availableStudents, "Id", "Email");

            ViewBag.Course = course;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Instructor")]
        public async Task<IActionResult> Enroll(int id, string studentId)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && course.InstructorId != userId) return Forbid();

            if (string.IsNullOrEmpty(studentId)) return RedirectToAction(nameof(Enroll), new { id });

            var enrollment = new Enrollment { CourseId = id, StudentId = studentId };
            _context.Enrollments.Add(enrollment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateInstructors()
        {
            var users = await _context.Users.ToListAsync();
            ViewBag.InstructorList = new SelectList(users, "Email", "Email");
        }
    }
}
