using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;
using System.Text;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Admin,Instructor")]
    public class GradesController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Distribution criteria configured precisely to your syllabus specifications
        private static readonly Dictionary<AssignmentCategory, double> WeightMap = new()
        {
            { AssignmentCategory.CAT1,    0.20 },
            { AssignmentCategory.CAT2,    0.20 },
            { AssignmentCategory.Project, 0.20 },
            { AssignmentCategory.Exam,    0.40 }
        };

        public GradesController(ApplicationDbContext context) => _context = context;

        // GET: Grades/Index?assignmentId=5
        public async Task<IActionResult> Index(int assignmentId)
        {
            var assignment = await _context.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == assignmentId);
            if (assignment == null || assignment.Course == null) return NotFound();

            var submissions = await _context.Submissions.Include(s => s.Student).Where(s => s.AssignmentId == assignmentId).ToListAsync();
            var graded = submissions.Where(s => s.Grade.HasValue).ToList();

            ViewBag.ClassAverage = graded.Any() ? graded.Average(s => s.Grade!.Value) : 0.0;
            ViewBag.HighScore = graded.Any() ? graded.Max(s => s.Grade!.Value) : 0;
            ViewBag.LowScore = graded.Any() ? graded.Min(s => s.Grade!.Value) : 0;
            ViewBag.GradedCount = graded.Count;
            ViewBag.Assignment = assignment;

            return View(submissions);
        }

        // GET: Grades/Gradebook?courseId=5
        public async Task<IActionResult> Gradebook(int courseId)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
            if (course == null) return NotFound();

            var students = await _context.Enrollments.Where(e => e.CourseId == courseId).Include(e => e.Student).Select(e => e.Student).Where(s => s != null).ToListAsync();
            var assignments = await _context.Assignments.Where(a => a.CourseId == courseId).ToListAsync();
            var submissions = await _context.Submissions.Where(s => s.Assignment!.CourseId == courseId).ToListAsync();

            ViewBag.Course = course;
            ViewBag.Assignments = assignments;
            ViewBag.FinalGrades = ComputeWeightedMatrix(students!, assignments, submissions);

            return View(students);
        }

        // EXPORT PORTABILITY STREAMING COMPILER ENGINE
        public async Task<IActionResult> ExportGradebook(int courseId)
        {
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == courseId);
            if (course == null) return NotFound();

            var students = await _context.Enrollments.Where(e => e.CourseId == courseId).Include(e => e.Student).Select(e => e.Student).Where(s => s != null).ToListAsync();
            var assignments = await _context.Assignments.Where(a => a.CourseId == courseId).ToListAsync();
            var submissions = await _context.Submissions.Where(s => s.Assignment!.CourseId == courseId).ToListAsync();

            var metricsMatrix = ComputeWeightedMatrix(students!, assignments, submissions);
            var csvBuilder = new StringBuilder();

            csvBuilder.AppendLine("Student Email,CAT1 Avg %,CAT2 Avg %,Project Avg %,Exam Avg %,Final Weighted Standings %");

            foreach (var student in students)
            {
                if (metricsMatrix.TryGetValue(student!.Id, out var performance))
                {
                    csvBuilder.AppendLine($"{student.Email},{performance.Cat1Avg:0.0},{performance.Cat2Avg:0.0},{performance.ProjAvg:0.0},{performance.ExamAvg:0.0},{performance.FinalMark:0.0}");
                }
            }

            var downloadDataPayloadBytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            return File(downloadDataPayloadBytes, "text/csv", $"Gradebook_{course.Code}.csv");
        }

        // RESTORED: GET Action for displaying individual evaluation grading sheets
        // GET: Grades/Grade/5
        public async Task<IActionResult> Grade(int? id)
        {
            if (id == null) return NotFound();

            var submission = await _context.Submissions
                .Include(s => s.Assignment)
                .ThenInclude(a => a != null ? a.Course : null)
                .Include(s => s.Student)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return NotFound();
            if (submission.Assignment?.Course == null) return NotFound();

            // Guard rails check: only permit the actual assigned instructor or an administrative user
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
            var submission = await _context.Submissions.Include(s => s.Assignment).ThenInclude(a => a != null ? a.Course : null).FirstOrDefaultAsync(s => s.Id == id);
            if (submission == null || submission.Assignment?.Course == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && submission.Assignment.Course.InstructorId != userId)
                return Forbid();

            if (grade < 0 || grade > submission.Assignment.MaxPoints)
            {
                ModelState.AddModelError("", $"Grade must be between 0 and {submission.Assignment.MaxPoints}.");
                return View(submission);
            }

            submission.Grade = grade;
            submission.Feedback = feedback ?? string.Empty;
            _context.Update(submission);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Grade saved successfully.";
            return RedirectToAction(nameof(Index), new { assignmentId = submission.AssignmentId });
        }

        private static Dictionary<string, (double Cat1Avg, double Cat2Avg, double ProjAvg, double ExamAvg, double FinalMark)> ComputeWeightedMatrix(
            List<Microsoft.AspNetCore.Identity.IdentityUser> students, List<Assignment> assignments, List<Submission> submissions)
        {
            var trackingContainer = new Dictionary<string, (double, double, double, double, double)>();

            foreach (var student in students)
            {
                double cumulativeWeightedScore = 0;
                double accumulatedDistributionWeightCap = 0;

                var performanceMetrics = (Cat1Avg: 0.0, Cat2Avg: 0.0, ProjAvg: 0.0, ExamAvg: 0.0, FinalMark: 0.0);
                var categoricalGroupings = assignments.GroupBy(a => a.Category);

                foreach (var segmentGroup in categoricalGroupings)
                {
                    double earnedPointsInSegment = 0;
                    double totalMaxPointsInSegment = 0;

                    foreach (var task in segmentGroup)
                    {
                        var scoreMatch = submissions.FirstOrDefault(s => s.AssignmentId == task.Id && s.StudentId == student.Id && s.Grade.HasValue);
                        if (scoreMatch != null)
                        {
                            earnedPointsInSegment += scoreMatch.Grade!.Value;
                            totalMaxPointsInSegment += task.MaxPoints;
                        }
                    }

                    if (totalMaxPointsInSegment > 0)
                    {
                        double segmentRatioPercentage = (earnedPointsInSegment / totalMaxPointsInSegment) * 100;
                        double targetedDistributionWeight = WeightMap[segmentGroup.Key];

                        cumulativeWeightedScore += (segmentRatioPercentage * targetedDistributionWeight);
                        accumulatedDistributionWeightCap += targetedDistributionWeight;

                        if (segmentGroup.Key == AssignmentCategory.CAT1)    performanceMetrics.Cat1Avg = segmentRatioPercentage;
                        if (segmentGroup.Key == AssignmentCategory.CAT2)    performanceMetrics.Cat2Avg = segmentRatioPercentage;
                        if (segmentGroup.Key == AssignmentCategory.Project) performanceMetrics.ProjAvg = segmentRatioPercentage;
                        if (segmentGroup.Key == AssignmentCategory.Exam)    performanceMetrics.ExamAvg = segmentRatioPercentage;
                    }
                }

                if (accumulatedDistributionWeightCap > 0)
                {
                    performanceMetrics.FinalMark = cumulativeWeightedScore / accumulatedDistributionWeightCap;
                }

                trackingContainer[student.Id] = performanceMetrics;
            }

            return trackingContainer;
        }
    }
}
