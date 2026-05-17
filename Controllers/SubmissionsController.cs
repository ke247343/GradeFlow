using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradeFlow.Data;
using GradeFlow.Models;
using System.Security.Claims;

namespace GradeFlow.Controllers
{
    [Authorize(Roles = "Student")]
    public class SubmissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubmissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Submissions/Create?assignmentId=5
        public async Task<IActionResult> Create(int assignmentId)
        {
            var assignment = await _context.Assignments
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound();

            // RECOMMENDATION 2 SECURITY: Halt requests targeting unreleased elements
            var requestTime = DateTime.UtcNow;
            if (requestTime < assignment.ReleaseDate)
            {
                return Forbid();
            }

            if (requestTime > assignment.CutOffDate)
            {
                TempData["Error"] = "Submission matrix window permanently locked out.";
                return RedirectToAction("Details", "Courses", new { id = assignment.CourseId });
            }

            ViewBag.Assignment = assignment;
            return View();
        }

        // POST: Submissions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int assignmentId, IFormFile file)
        {
            var assignment = await _context.Assignments.FindAsync(assignmentId);
            if (assignment == null) return NotFound();

            var executionTime = DateTime.UtcNow;

            // RECOMMENDATION 2 ARCHITECTURE: Absolute Cutoff Window Enforcement
            if (executionTime > assignment.CutOffDate)
            {
                ModelState.AddModelError("", "The absolute hard cutoff portal window for this task has expired.");
                ViewBag.Assignment = assignment;
                return View();
            }
            if (executionTime < assignment.ReleaseDate)
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please upload a valid document deliverable payload.");
                ViewBag.Assignment = assignment;
                return View();
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            var allowedExtensions = assignment.AllowedFileTypes
                .Split(',')
                .Select(ext => ext.Trim().ToLower())
                .ToList();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("", $"Extension violation format type rejection. Permitted types are: {assignment.AllowedFileTypes}");
                ViewBag.Assignment = assignment;
                return View();
            }

            // ADDED DEFENSIVE HARDENING: Verify underlying binary signatures before writing payload to disk
            if (!VerifyFileSignatures(file, extension))
            {
                ModelState.AddModelError("", "Content structural validation failed. The internal file signature headers mismatch the spoofed file extension.");
                ViewBag.Assignment = assignment;
                return View();
            }

            var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var targetDirectory = Path.Combine(webRootPath, "uploads", "submissions");

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + extension;
            var completePhysicalFilePath = Path.Combine(targetDirectory, uniqueFileName);

            using (var streamingBuffer = new FileStream(completePhysicalFilePath, FileMode.Create))
            {
                await file.CopyToAsync(streamingBuffer);
            }

            var studentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool submissionLateFlag = executionTime > assignment.DueDate.AddMinutes(5);

            var submissionRecord = new Submission
            {
                AssignmentId = assignmentId,
                StudentId = studentId ?? string.Empty,
                FilePath = "/uploads/submissions/" + uniqueFileName,
                SubmittedAt = executionTime,
                IsLate = submissionLateFlag,
                Feedback = string.Empty
            };

            _context.Submissions.Add(submissionRecord);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Deliverable document payload committed successfully to index records ledger!";
            return RedirectToAction("Index", "Courses");
        }

        // ADDED DEFENSIVE HARDENING ENGINE: Binary hex byte sequence matching validation routine
        private static bool VerifyFileSignatures(IFormFile file, string extension)
        {
            // Register standard enterprise hex header masks mapping to their expected formats
            var fileHexSignatures = new Dictionary<string, byte[][]>
            {
                { ".pdf",  new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } },                               // %PDF
                { ".docx", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } },                               // PK ZIP (Office Open XML Open Container)
                { ".zip",  new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } } },                               // Standard PK ZIP archive archive
                { ".png",  new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },       // Portable Network Graphics Image
                { ".jpg",  new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },                                     // Joint Photographic Experts Group image
                { ".jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } } }                                      // Joint Photographic Experts Group image
            };

            // If an instructor whitelists plain text file streams (.txt, .cs, .csv), bypass magic numbers sequence matching
            if (!fileHexSignatures.ContainsKey(extension))
            {
                return true;
            }

            // Read the initial stream block allocation array without breaking data downstream
            using var operationalStream = file.OpenReadStream();
            using var binaryParser = new BinaryReader(operationalStream);

            var targetSignaturesList = fileHexSignatures[extension];
            var longestSignatureHeader = targetSignaturesList.Max(sig => sig.Length);
            var extractedHeaderBytes = binaryParser.ReadBytes(longestSignatureHeader);

            // CRITICAL: Reset memory stream index pointers back to 0 so downstream file system save allocations function completely
            if (operationalStream.CanSeek)
            {
                operationalStream.Seek(0, SeekOrigin.Begin);
            }

            // Return true if any registered array pattern satisfies sequence matching thresholds
            return targetSignaturesList.Any(sig => extractedHeaderBytes.Take(sig.Length).SequenceEqual(sig));
        }
    }
}
