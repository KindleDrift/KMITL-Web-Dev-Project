using WebDevProject.Models;
using System.Web;

namespace WebDevProject.Services
{
    public class NotificationFormattingService
    {
        public string GetTimeAgoString(DateTime dateTime)
        {
            var timeSpan = DateTimeOffset.UtcNow - new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
            
            if (timeSpan.TotalSeconds < 60)
                return "just now";
            
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m ago";
            
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h ago";
            
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays}d ago";
            
            return dateTime.ToString("MMM d, yyyy");
        }

        public string GetNotificationBadgeClass(NotificationType type)
        {
            return type switch
            {
                NotificationType.NewRequest => "badge-info",
                NotificationType.IsAccepted => "badge-success",
                NotificationType.IsRejected => "badge-danger",
                NotificationType.AdminAction => "badge-warning",
                NotificationType.BoardFull => "badge-warning",
                _ => "badge-secondary"
            };
        }

        public string GetNotificationTypeDisplay(NotificationType type)
        {
            return type switch
            {
                NotificationType.NewRequest => "New Request",
                NotificationType.IsAccepted => "Accepted",
                NotificationType.IsRejected => "Rejected",
                NotificationType.AdminAction => "Admin Action",
                NotificationType.BoardFull => "Board Full",
                _ => "Notification"
            };
        }

        public string FormatNotificationDescription(
            Notification notification,
            Func<string, string> generateProfileUrl,
            Func<int, string> generateBoardUrl)
        {
            var relatedUserName = notification.RelatedUser?.DisplayName;
            var boardTitle = notification.Board?.Title;
            var boardId = notification.BoardId;

            var userLink = !string.IsNullOrEmpty(relatedUserName) 
                ? $"<a href=\"{generateProfileUrl(relatedUserName)}\" class=\"notification-link\">{HttpUtility.HtmlEncode(relatedUserName)}</a>"
                : "A new user";

            var boardLink = !string.IsNullOrEmpty(boardTitle) && boardId.HasValue
                ? $"<a href=\"{generateBoardUrl(boardId.Value)}\" class=\"notification-link\">{HttpUtility.HtmlEncode(boardTitle)}</a>"
                : "your board";

            // Generate description based on type
            return notification.Type switch
            {
                NotificationType.NewRequest when notification.Board != null => 
                    notification.Description?.Contains("joined") == true
                        ? $"{userLink} has joined {boardLink}."
                        : $"{userLink} has applied to join {boardLink}.",
                
                NotificationType.IsAccepted when notification.Board != null => 
                    $"Your application to join {boardLink} has been accepted.",
                
                NotificationType.IsRejected when notification.Board != null => 
                    $"Your application to join {boardLink} has been rejected.",
                
                NotificationType.BoardFull when notification.Board != null =>
                    $"Your board {boardLink} is now full!",
                
                NotificationType.AdminAction => 
                    !string.IsNullOrEmpty(notification.Description) 
                        ? HttpUtility.HtmlEncode(notification.Description) 
                        : "An admin action was performed on your account.",
                
                _ => !string.IsNullOrEmpty(notification.Description) 
                    ? HttpUtility.HtmlEncode(notification.Description) 
                    : $"{userLink} interacted with {boardLink}."
            };
        }
    }
}
