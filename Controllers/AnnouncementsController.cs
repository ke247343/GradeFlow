using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Admin,Instructor")]
    public class AnnouncementsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnnouncementsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // RECOMMENDATION 3 POST BACKEND: Create Announcement Action
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int courseId, string title, string content)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(content))
            {
                TempData["Error"] = "Broadcast fields cannot be posted blank.";
                return RedirectToAction("Details", "Courses", new { id = courseId });
            }

            var broadcast = new Announcement
            {
                CourseId = courseId,
                Title = title,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Announcements.Add(broadcast);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Class broadcast distributed successfully to feed matrix!";
            return RedirectToAction("Details", "Courses", new { id = courseId });
        }
    }
}
