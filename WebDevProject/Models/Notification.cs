namespace WebDevProject.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        public required DateTime CreatedDate { get; set; }
        public NotificationType Type { get; set; }

        public string UserId { get; set; } = string.Empty;
        public Users? User { get; set; }

        public bool IsRead { get; set; } = false;

        public int? BoardId { get; set; }
        public Board? Board { get; set; }

        public string? RelatedUserId { get; set; }
        public Users? RelatedUser { get; set; }
    }

    public enum NotificationType
    {
        NewRequest, // For new application or user joins board
        IsAccepted, // user is accepted to a board after applying
        IsRejected, // user is rejected from a board after applying or removed from a board and when cancelled.
        AdminAction, // admin edits something that affects the user
        BoardFull // board the author created becomes full
    }
}
