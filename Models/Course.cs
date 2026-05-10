using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace GradeFlow.Models
{
    public class Course
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        public string InstructorId { get; set; } = string.Empty;

        public IdentityUser? Instructor { get; set; }

        [Required]
        [StringLength(30)]
        public string Term { get; set; } = string.Empty;

        public ICollection<Enrollment>? Enrollments { get; set; }
        public ICollection<Assignment>? Assignments { get; set; }
    }
}