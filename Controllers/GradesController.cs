using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Admin,Instructor")]
    public class GradesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GradesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Grades/Index?assignmentId=5
        public async Task<IActionResult> Index(int assignmentId)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (assignment == null) return NotFound();
            if (assignment.Course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && assignment.Course.InstructorId != userId)
                return Forbid();

            var submissions = await _context.Submissions
                .Include(s => s.Student)
                .Where(s => s.AssignmentId == assignmentId)
                .OrderBy(s => s.Student!.Email)
                .ToListAsync();

            ViewBag.Assignment = assignment;
            return View(submissions);
        }

        // GET: Grades/Grade/5
        public async Task<IActionResult> Grade(int? id)
        {
            if (id == null) return NotFound();
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a!.Course)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null) return NotFound();
            if (submission.Assignment?.Course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && submission.Assignment.Course.InstructorId != userId)
                return Forbid();

            return View(submission);
        }

        // POST: Grades/Grade/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grade(int id, int grade, string feedback)
        {
            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a!.Course)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null) return NotFound();
            if (submission.Assignment?.Course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && submission.Assignment.Course.InstructorId != userId)
                return Forbid();

            if (grade < 0 || grade > submission.Assignment.MaxPoints)
            {
                ModelState.AddModelError("", $"Grade must be between 0 and {submission.Assignment.MaxPoints}.");
                return View(submission);
            }

            submission.Grade = grade;
            submission.Feedback = feedback;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Grade saved successfully.";
            return RedirectToAction(nameof(Index), new { assignmentId = submission.AssignmentId });
        }
    }
}