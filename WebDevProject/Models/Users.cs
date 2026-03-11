using Microsoft.AspNetCore.Identity;

namespace WebDevProject.Models
{
    public class Users : IdentityUser
    {
        public required string DisplayName { get; set; }
        public required string NormalizedDisplayName { get; set; }

        public bool HasCompletedOnboarding { get; set; } = false;

        public DateTime? DateOfBirth { get; set; }

        public enum Gender
        {
            Male,
            Female,
            Other
        }

        public Gender? UserGender { get; set; }

        public string? ProfilePictureUrl { get; set; }

        public string? Bio { get; set; }

        public required DateTime CreatedAt { get; set; }

        public ICollection<Board> AuthoredBoards { get; set; } = [];

        public ICollection<BoardParticipant> BoardParticipations { get; set; } = [];

        public ICollection<BoardApplicant> BoardApplications { get; set; } = [];

        public ICollection<BoardDenied> BoardDenials { get; set; } = [];
    }
}
