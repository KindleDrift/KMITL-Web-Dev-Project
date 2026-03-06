using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace WebDevProject.Models
{
    public class Board
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2048)]
        public string? ImageUrl { get; set; }

        [StringLength(60)]
        public string? Category { get; set; }

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [ValidateNever]
        public string AuthorId { get; set; } = string.Empty;

        public Users? Author { get; set; }

        [Range(1, 1000)]
        public int MaxParticipants { get; set; } = 1;

        public ICollection<BoardParticipant> Participants { get; set; } = [];

        [Required]
        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [Required]
        public DateTime EventDate { get; set; }
        [Required]
        public DateTime Deadline { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool NotifyAuthorOnFull { get; set; }

        public BoardStatus CurrentStatus { get; set; } = BoardStatus.Open;

        public GroupManagement GroupManagementOption { get; set; } = GroupManagement.CloseOnFull;
    }

    public class BoardParticipant
    {
        public int BoardId { get; set; }

        public Board? Board { get; set; }

        public string UserId { get; set; } = string.Empty;

        public Users? User { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public enum BoardStatus
    {
        Open,
        Full,
        Closed,
        Archived
    }

    public enum GroupManagement
    {
        CloseOnFull,
        IncreaseMaxParticipantsOnFull,
        ManualIncreaseMaxParticipants
    }
}
