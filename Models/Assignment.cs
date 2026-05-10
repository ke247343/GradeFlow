using System.ComponentModel.DataAnnotations;

namespace GradeFlow.Models
{
    public class Assignment
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public Course? Course { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime DueDate { get; set; }

        [Range(0, 1000)]
        public int MaxPoints { get; set; }

        public string AllowedFileTypes { get; set; } = ".pdf,.docx,.txt";

        public ICollection<Submission>? Submissions { get; set; }
    }
}