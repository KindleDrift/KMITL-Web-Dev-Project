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

        public bool IsRead { get; set; } = false;
    }

    public enum NotificationType
    {
        NewRequest, // For when a new application is submitted or user joins a no application board
        IsAccepted, // For when a user is accepted to a board after applying
        IsRejected, // For when a user is rejected from a board after applying or removed from a board.
                    // Including when board gets cancelled.
        AdminAction, // For when an admin edits something that affects the user.
    }
}
