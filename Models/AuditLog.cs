using System.ComponentModel.DataAnnotations;

namespace GradeFlow.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public string ExecutedByEmail { get; set; } = string.Empty;

        [Required]
        public string OperationAction { get; set; } = string.Empty;

        [Required]
        public string DetailsContext { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
