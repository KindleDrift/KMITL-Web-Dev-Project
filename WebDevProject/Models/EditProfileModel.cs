using System.ComponentModel.DataAnnotations;

namespace WebDevProject.Models
{
    public class EditProfileViewModel
    {
        // Profile Image upload
        public IFormFile? ProfileImage { get; set; }

        // Current profile picture URL (for display)
        public string? CurrentProfilePictureUrl { get; set; }

        public required string UserName { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public Users.Gender? UserGender { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public required string Email { get; set; }

    }
}