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

            const int pageSize = 15;
            var notifications = await _notificationsService.GetUserNotificationsAsync(userId, pageNumber, pageSize);
            var unreadCount = await _notificationsService.GetUnreadNotificationCountAsync(userId);

            ViewData["UnreadCount"] = unreadCount;
            ViewData["CurrentPage"] = pageNumber;
            ViewData["PageSize"] = pageSize;

            return View(notifications);
        }

        [HttpPost]
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
    }
}
