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

        // Profile picture URL
        public string? ProfilePictureUrl { get; set; }

        public string? Bio { get; set; }

        public required DateTime CreatedAt { get; set; }
    }
}
