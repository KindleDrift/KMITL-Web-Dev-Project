using WebDevProject.Data;
using WebDevProject.Models;
using Microsoft.EntityFrameworkCore;

namespace WebDevProject.Services
{
    public class NotificationsService
    {
        private readonly ApplicationDbContext _context;

        public NotificationsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateNotificationAsync(string userId, string title, string? description, NotificationType type, int? boardId = null, string? relatedUserId = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Description = description,
                CreatedDate = DateTimeOffset.UtcNow.UtcDateTime,
                Type = type,
                IsRead = false,
                BoardId = boardId,
                RelatedUserId = relatedUserId
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task CreateNotificationsForMultipleUsersAsync(List<string> userIds, string title, string? description, NotificationType type, int? boardId = null, string? relatedUserId = null)
        {
            var notifications = userIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = title,
                Description = description,
                CreatedDate = DateTimeOffset.UtcNow.UtcDateTime,
                Type = type,
                IsRead = false,
                BoardId = boardId,
                RelatedUserId = relatedUserId
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();
            
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }
            
            if (unreadNotifications.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int pageNumber = 1, int pageSize = 10)
        {
            var skip = (pageNumber - 1) * pageSize;
            return await _context.Notifications
                .Include(n => n.Board)
                .Include(n => n.RelatedUser)
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();
        }

        public async Task<Notification?> GetNotificationAsync(int notificationId)
        {
            return await _context.Notifications.FindAsync(notificationId);
        }

        public async Task DeleteNotificationAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteUserNotificationsAsync(string userId, NotificationType? type = null)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId);
            if (type.HasValue)
            {
                query = query.Where(n => n.Type == type.Value);
            }

            var notificationsToDelete = await query.ToListAsync();
            _context.Notifications.RemoveRange(notificationsToDelete);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllUserNotificationsAsync(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .ToListAsync();
            
            if (notifications.Any())
            {
                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
            }
        }
    }
}
