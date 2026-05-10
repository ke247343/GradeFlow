using Microsoft.AspNetCore.Identity;

namespace GradeFlow.Models
{
    public class Submission
    {
        public int Id { get; set; }
        public int AssignmentId { get; set; }
        public Assignment? Assignment { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public IdentityUser? Student { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public bool IsLate { get; set; }
        public int? Grade { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }
}