using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Admin,Instructor")]
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Courses
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.IsInRole("Admin"))
            {
                var courses = await _context.Courses.Include(c => c.Instructor).ToListAsync();
                return View(courses);
            }
            else
            {
                var courses = await _context.Courses
                    .Where(c => c.InstructorId == userId)
                    .Include(c => c.Instructor)
                    .ToListAsync();
                return View(courses);
            }
        }

        // GET: Courses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (course == null) return NotFound();
            return View(course);
        }

        // GET: Courses/Create
        public async Task<IActionResult> Create()
        {
            // Populate dropdown with all users
            var users = await _context.Users.ToListAsync();
            ViewBag.InstructorList = new SelectList(users, "Email", "Email");
            return View();
        }

        // POST: Courses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Code,Term")] Course course, string InstructorEmail)
        {
            ModelState.Remove("InstructorId");
            if (ModelState.IsValid)
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

                course.InstructorId = instructor.Id;
                _context.Add(course);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await PopulateInstructors();
            return View(course);
        }

        // GET: Courses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            // Get current instructor's email
            var instructor = await _context.Users.FindAsync(course.InstructorId);
            var users = await _context.Users.ToListAsync();
            ViewBag.InstructorList = new SelectList(users, "Email", "Email", instructor?.Email);
            return View(course);
        }

        // POST: Courses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Code,Term")] Course course, string InstructorEmail)
        {
            if (id != course.Id) return NotFound();

            ModelState.Remove("InstructorId");
            if (ModelState.IsValid)
            {
                try
                {
                    var instructor = await _context.Users.FirstOrDefaultAsync(u => u.Email == InstructorEmail);
                    if (instructor == null)
                    {
                        ModelState.AddModelError("", "Selected instructor does not exist.");
                        await PopulateInstructors();
                        return View(course);
                    }

                    course.InstructorId = instructor.Id;
                    _context.Update(course);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CourseExists(course.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateInstructors();
            return View(course);
        }

        // GET: Courses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var course = await _context.Courses
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (course == null) return NotFound();
            return View(course);
        }

        // POST: Courses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course != null) _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CourseExists(int id)
        {
            return _context.Courses.Any(e => e.Id == id);
        }

        private async Task PopulateInstructors()
        {
            var users = await _context.Users.ToListAsync();
            ViewBag.InstructorList = new SelectList(users, "Email", "Email");
        }
    }
}