using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Admin,Instructor")]
    public class AssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssignmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int courseId)
        {
            var course = await _context.Courses.Include(c => c.Instructor).FirstOrDefaultAsync(c => c.Id == courseId);
            if (course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && course.InstructorId != userId) return Forbid();

            var assignments = await _context.Assignments.Where(a => a.CourseId == courseId).OrderBy(a => a.DueDate)
                .ToListAsync();
            ViewBag.Course = course;
            return View(assignments);
        }

        public async Task<IActionResult> Create(int courseId)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            var currentUtc = DateTime.UtcNow;
            return View(new Assignment
            {
                CourseId = courseId,
                // FIXED CS0117: Changed from Homework to CAT1 to match your new curriculum enum definition
                Category = AssignmentCategory.CAT1,
                ReleaseDate = currentUtc.Date,
                DueDate = currentUtc.AddDays(7).Date.AddHours(23).AddMinutes(59),
                CutOffDate = currentUtc.AddDays(9).Date.AddHours(23).AddMinutes(59),
                MaxPoints = 100
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CourseId,Title,Description,Category,ReleaseDate,DueDate,CutOffDate,MaxPoints,AllowedFileTypes")] Assignment assignment)
        {
            if (ModelState.IsValid)
            {
                assignment.ReleaseDate = DateTime.SpecifyKind(assignment.ReleaseDate, DateTimeKind.Utc);
                assignment.DueDate = DateTime.SpecifyKind(assignment.DueDate, DateTimeKind.Utc);
                assignment.CutOffDate = DateTime.SpecifyKind(assignment.CutOffDate, DateTimeKind.Utc);

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { courseId = assignment.CourseId });
            }
            return View(assignment);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var assignment = await _context.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == id);
            if (assignment == null) return NotFound();

            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CourseId,Title,Description,Category,ReleaseDate,DueDate,CutOffDate,MaxPoints,AllowedFileTypes")] Assignment assignment)
        {
            if (id != assignment.Id) return NotFound();

            if (ModelState.IsValid)
            {
                assignment.ReleaseDate = DateTime.SpecifyKind(assignment.ReleaseDate, DateTimeKind.Utc);
                assignment.DueDate = DateTime.SpecifyKind(assignment.DueDate, DateTimeKind.Utc);
                assignment.CutOffDate = DateTime.SpecifyKind(assignment.CutOffDate, DateTimeKind.Utc);

                _context.Update(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { courseId = assignment.CourseId });
            }
            return View(assignment);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            return View(await _context.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => id == a.Id));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.Assignments.FindAsync(id);
            if (assignment != null)
            {
                assignment.DeletedAt = DateTime.UtcNow;
                _context.Update(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { courseId = assignment.CourseId });
            }
            return NotFound();
        }
    }
}
