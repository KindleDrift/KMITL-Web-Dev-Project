using System.ComponentModel.DataAnnotations;

namespace WebDevProject.Models
{
    // The whole thing can be skipped if the user doesn't want to provide their date of birth, so no required attribute is needed.
    public class OnboardingViewModel : IValidatableObject
    {
        // Profile Image upload
        public IFormFile? ProfileImage { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public Users.Gender? UserGender { get; set; }

        [StringLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
        public string? Bio { get; set; }

        public bool SkipOnboarding { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue && DateOfBirth.Value.Date > DateTime.Today)
            {
                yield return new ValidationResult("Birthdate cannot be later than today.", new[] { nameof(DateOfBirth) });
            }
        }
    }
}
