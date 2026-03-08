// Notification Management JavaScript
// This file handles all notification interactions: mark as read, delete, and count updates

async function markAsRead(notificationId) {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const response = await fetch(`/Notifications/MarkAsRead?notificationId=${notificationId}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            }
        });

        if (response.ok) {
            const element = document.getElementById(`notification-${notificationId}`);
            element.classList.remove('border-primary');
            const badge = element.querySelector('.badge.bg-primary');
            if (badge) badge.remove();
            updateUnreadCount();
        } else {
            console.error('Failed to mark as read:', response.status);
        }
    } catch (error) {
        console.error('Error marking notification as read:', error);
    }
}

async function markAllAsRead() {
    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const response = await fetch('/Notifications/MarkAllAsRead', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            }
        });

        if (response.ok) {
            location.reload();
        } else {
            console.error('Failed to mark all as read:', response.status);
        }
    } catch (error) {
        console.error('Error marking all as read:', error);
    }
}

async function deleteNotification(notificationId) {
    if (!confirm('Are you sure you want to delete this notification?')) {
        return;
    }

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const response = await fetch(`/Notifications/Delete?notificationId=${notificationId}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            }
        });

        if (response.ok) {
            const element = document.getElementById(`notification-${notificationId}`);
            element.remove();
            updateUnreadCount();
        } else {
            console.error('Failed to delete notification:', response.status);
        }
    } catch (error) {
        console.error('Error deleting notification:', error);
    }
}

async function updateUnreadCount() {
    try {
        const response = await fetch('/Notifications/GetUnreadCount');
        const data = await response.json();
        document.getElementById('unread-count').textContent = data.count;
    } catch (error) {
        console.error('Error updating unread count:', error);
    }
}
