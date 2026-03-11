using System.ComponentModel.DataAnnotations;

namespace WebDevProject.Models
{
    public class EditProfileViewModel : IValidatableObject
    {
        // Profile Image upload
        public IFormFile? ProfileImage { get; set; }

        // Current profile picture URL (For Display)
        public string? CurrentProfilePictureUrl { get; set; }

        public required string UserName { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public Users.Gender? UserGender { get; set; }

        public string? Bio { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue && DateOfBirth.Value.Date > DateTime.Today)
            {
                yield return new ValidationResult("Birthdate cannot be later than today.", new[] { nameof(DateOfBirth) });
            }
        }
    }
}