using System.ComponentModel.DataAnnotations;

namespace WebDevProject.Models
{
    public class OnboardingViewModel : IValidatableObject
    {
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
