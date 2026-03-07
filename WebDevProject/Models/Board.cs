using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        public ICollection<Tag> Tags { get; set; } = [];

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

        public ICollection<BoardExternalParticipant> ExternalParticipants { get; set; } = [];

        public ICollection<BoardApplicant> Applicants { get; set; } = [];

        public ICollection<BoardDenied> DeniedUsers { get; set; } = [];

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

        public BoardJoinPolicy JoinPolicy { get; set; } = BoardJoinPolicy.Application;
    }

    public class BoardParticipant
    {
        public int BoardId { get; set; }

        public Board? Board { get; set; }

        public string UserId { get; set; } = string.Empty;

        public Users? User { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class BoardExternalParticipant
    {
        public int Id { get; set; }

        public int BoardId { get; set; }

        public Board? Board { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Note { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    public class BoardApplicant
    {
        public int BoardId { get; set; }

        public Board? Board { get; set; }

        public string UserId { get; set; } = string.Empty;

        public Users? User { get; set; }

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }

    public class BoardDenied
    {
        public int BoardId { get; set; }

        public Board? Board { get; set; }

        public string UserId { get; set; } = string.Empty;

        public Users? User { get; set; }

        public DateTime DeniedAt { get; set; } = DateTime.UtcNow;
    }

    public enum BoardStatus
    {
        Open,
        Full,
        Closed,
        Cancelled,
        Archived
    }

    public enum GroupManagement
    {
        CloseOnFull,
        AllowOverbooking,
        KeepOpenWhenFull
    }

    public enum BoardJoinPolicy
    {
        Application,
        FirstComeFirstServe
    }

    public class Tag
    {
        public int Id { get; set; }
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;
        public ICollection<Board> Boards { get; set; } = [];
    }
}
