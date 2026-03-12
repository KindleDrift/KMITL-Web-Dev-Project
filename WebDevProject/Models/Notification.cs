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
        NewRequest, 
        IsAccepted, 
        IsRejected, 
        AdminAction, 
        BoardFull
    }
}
