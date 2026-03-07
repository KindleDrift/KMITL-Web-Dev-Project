using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebDevProject.Models
{
    public class BoardCreateViewModel
    {
        [Required]
        [StringLength(120, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000, MinimumLength = 10)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Board Image")]
        public IFormFile? BoardImage { get; set; }

        [Required]
        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [Required]
        public DateTime EventDate { get; set; }

        [Required]
        public DateTime Deadline { get; set; }

        [Required]
        [Range(1, 1000)]
        public int MaxParticipants { get; set; } = 2;

        public bool NotifyAuthorOnFull { get; set; }

        [Required]
        public string GroupManagementOption { get; set; } = string.Empty;

        [Required]
        public string JoinPolicyOption { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = [];

        public BoardStatus? CurrentStatus { get; set; }
    }
}
