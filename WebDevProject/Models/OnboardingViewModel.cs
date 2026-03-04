using System.ComponentModel.DataAnnotations;

namespace WebDevProject.Models
{
    // The whole thing can be skipped if the user doesn't want to provide their date of birth, so no required attribute is needed.
    public class OnboardingViewModel
    {
        // Profile Image upload
        public IFormFile? ProfileImage { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public Users.Gender? UserGender { get; set; }

        public bool SkipOnboarding { get; set; }
    }
}
