namespace GradeFlow.Models
{
    public class UserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CurrentRole { get; set; } = string.Empty;
    }

    public class AdminCourseViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string InstructorEmail { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public int EnrollmentCount { get; set; }
        public int AssignmentCount { get; set; }
    }
}
