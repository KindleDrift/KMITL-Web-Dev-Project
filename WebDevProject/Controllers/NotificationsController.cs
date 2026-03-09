using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebDevProject.Services;

namespace WebDevProject.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly NotificationsService _notificationsService;

        public NotificationsController(NotificationsService notificationsService)
        {
            _notificationsService = notificationsService;
        }

        public async Task<IActionResult> Index(int pageNumber = 1)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var notifications = await _notificationsService.GetUserNotificationsAsync(userId, 1, 1000);
            var unreadCount = await _notificationsService.GetUnreadNotificationCountAsync(userId);

            ViewData["UnreadCount"] = unreadCount;

            return View(notifications);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var notification = await _notificationsService.GetNotificationAsync(notificationId);
            if (notification == null || notification.UserId != userId)
            {
                return NotFound();
            }

            await _notificationsService.MarkAsReadAsync(notificationId);
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            await _notificationsService.MarkAllAsReadAsync(userId);
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Delete(int notificationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var notification = await _notificationsService.GetNotificationAsync(notificationId);
            if (notification == null || notification.UserId != userId)
            {
                return NotFound();
            }

            await _notificationsService.DeleteNotificationAsync(notificationId);
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            await _notificationsService.DeleteAllUserNotificationsAsync(userId);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var unreadCount = await _notificationsService.GetUnreadNotificationCountAsync(userId);
            return Json(new { count = unreadCount });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentNotifications(int limit = 5)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var notifications = await _notificationsService.GetUserNotificationsAsync(userId, 1, limit);
            var unreadCount = await _notificationsService.GetUnreadNotificationCountAsync(userId);

            return Json(new 
            { 
                notifications = notifications.Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Description,
                    n.IsRead,
                    n.Type,
                    n.CreatedDate,
                    n.BoardId,
                    BoardTitle = n.Board?.Title,
                    RelatedUserDisplayName = n.RelatedUser?.DisplayName
                }).ToList(),
                unreadCount = unreadCount
            });
        }
    }
}
