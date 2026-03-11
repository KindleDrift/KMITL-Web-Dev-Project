async function markAsRead(notificationId) {
    try {
        const response = await fetch(`/Notifications/MarkAsRead?notificationId=${notificationId}`, {
            method: 'POST'
        });

        if (response.ok) {
            const element = document.getElementById(`notification-${notificationId}`);
            element.classList.remove('unread');
            
            const badgeNew = element.querySelector('.badge-new');
            if (badgeNew) badgeNew.remove();
            
            const markReadBtn = element.querySelector('.btn-mark-read');
            if (markReadBtn) markReadBtn.remove();
            
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
        const response = await fetch('/Notifications/MarkAllAsRead', {
            method: 'POST'
        });

        if (response.ok) {
            location.reload();
        } else {
            console.error('Failed to mark all as read:', response.status);
            alert('Failed to mark all notifications as read. Please try again.');
        }
    } catch (error) {
        console.error('Error marking all as read:', error);
        alert('An error occurred. Please try again.');
    }
}

async function deleteNotification(notificationId) {
    if (!confirm('Are you sure you want to delete this notification?')) {
        return;
    }

    try {
        const response = await fetch(`/Notifications/Delete?notificationId=${notificationId}`, {
            method: 'POST'
        });

        if (response.ok) {
            const element = document.getElementById(`notification-${notificationId}`);
            element.remove();
            updateUnreadCount();
            
            const notificationsList = document.querySelector('.notifications-list');
            if (notificationsList && notificationsList.children.length === 0) {
                location.reload();
            }
        } else {
            console.error('Failed to delete notification:', response.status);
        }
    } catch (error) {
        console.error('Error deleting notification:', error);
    }
}

async function deleteAllNotifications() {
    if (!confirm('Are you sure you want to delete all notifications? This action cannot be undone.')) {
        return;
    }

    try {
        const response = await fetch('/Notifications/DeleteAll', {
            method: 'POST'
        });

        if (response.ok) {
            location.reload();
        } else {
            console.error('Failed to delete all notifications:', response.status);
            alert('Failed to delete all notifications. Please try again.');
        }
    } catch (error) {
        console.error('Error deleting all notifications:', error);
        alert('An error occurred. Please try again.');
    }
}

async function updateUnreadCount() {
    try {
        const response = await fetch('/Notifications/GetUnreadCount');
        const data = await response.json();
        
        const unreadCountElement = document.getElementById('unread-count');
        if (unreadCountElement) {
            unreadCountElement.textContent = data.count;
        }

        const markAllBtn = document.querySelector('.btn-mark-all');
        if (markAllBtn && data.count === 0) {
            markAllBtn.style.display = 'none';
        }

        updateNotificationDots(data.count);
    } catch (error) {
        console.error('Error updating unread count:', error);
    }
}

function updateNotificationDots(count) {
    const profileDot = document.querySelector('.profile-notification-dot');
    const optionDot = document.querySelector('.option-notification-dot');
    
    if (count > 0) {
        if (profileDot) profileDot.style.display = 'block';
        if (optionDot) optionDot.style.display = 'block';
    } else {
        if (profileDot) profileDot.style.display = 'none';
        if (optionDot) optionDot.style.display = 'none';
    }
}

document.addEventListener('DOMContentLoaded', function() {
    updateUnreadCount();
    
    setInterval(updateUnreadCount, 30000);
});
