namespace WebDevProject.Models
{
    public class UserProfileViewModel
    {
        public required string UserId { get; set; }
        public required string DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public Users.Gender? UserGender { get; set; }

        public int BoardsCreatedCount { get; set; }
        public int BoardParticipationsCount { get; set; }

        // if user is viewing their own profile
        public bool IsOwnProfile { get; set; }
        
        // only shown when viewing own profile
        public string? Email { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}
