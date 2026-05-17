using Microsoft.AspNetCore.Identity;

namespace GradeFlow.Models
{
    public class Enrollment
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public IdentityUser? Student { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;  // Added timestamp
    }
}
