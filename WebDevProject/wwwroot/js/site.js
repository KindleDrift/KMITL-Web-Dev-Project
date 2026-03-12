document.addEventListener('DOMContentLoaded', function () {
    const guestMenuToggleButton = document.getElementById('guest-menu-toggle');
    const guestNavbarMenu = document.getElementById('guest-navbar-menu');

    if (!guestMenuToggleButton || !guestNavbarMenu) {
        return;
    }

    const openIcon = guestMenuToggleButton.getAttribute('data-open-icon') || 'menu_open';
    const closedIcon = guestMenuToggleButton.getAttribute('data-closed-icon') || 'menu';

    const toggleGuestMenu = function () {
        const isOpen = guestNavbarMenu.classList.toggle('show');
        guestMenuToggleButton.setAttribute('aria-expanded', String(isOpen));
        guestMenuToggleButton.textContent = isOpen ? openIcon : closedIcon;
    };

    guestMenuToggleButton.addEventListener('click', toggleGuestMenu);

    guestMenuToggleButton.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            toggleGuestMenu();
        }
    });
});

// side panel modal thing
document.addEventListener('DOMContentLoaded', function () {
    const profileToggleButton = document.getElementById('profile-panel-toggle');
    const profileSidePanel = document.getElementById('profile-side-panel');
    const profileSideOverlay = document.getElementById('profile-side-overlay');
    const closeProfilePanelButton = document.getElementById('profile-side-panel-close');

    if (!profileToggleButton || !profileSidePanel || !profileSideOverlay) {
        return;
    }

    const openProfilePanel = function () {
        profileSidePanel.classList.add('open');
        profileSideOverlay.classList.add('show');
        profileToggleButton.setAttribute('aria-expanded', 'true');
        profileSidePanel.setAttribute('aria-hidden', 'false');
        profileSideOverlay.setAttribute('aria-hidden', 'false');
        document.body.classList.add('profile-panel-open');
    };

    const closeProfilePanel = function () {
        profileSidePanel.classList.remove('open');
        profileSideOverlay.classList.remove('show');
        profileToggleButton.setAttribute('aria-expanded', 'false');
        profileSidePanel.setAttribute('aria-hidden', 'true');
        profileSideOverlay.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('profile-panel-open');
    };

    const toggleProfilePanel = function () {
        if (profileSidePanel.classList.contains('open')) {
            closeProfilePanel();
            return;
        }

        openProfilePanel();
    };

    profileToggleButton.addEventListener('click', toggleProfilePanel);

    profileToggleButton.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            toggleProfilePanel();
        }
    });

    if (closeProfilePanelButton) {
        closeProfilePanelButton.addEventListener('click', closeProfilePanel);
    }

    profileSideOverlay.addEventListener('click', closeProfilePanel);

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape' && profileSidePanel.classList.contains('open')) {
            closeProfilePanel();
        }
    });

    const panelLinks = profileSidePanel.querySelectorAll('a');
    panelLinks.forEach(function (link) {
        link.addEventListener('click', closeProfilePanel);
    });
});

// password visibility toggle
document.addEventListener('DOMContentLoaded', function () {
    const togglePasswordBtns = document.querySelectorAll('.toggle-password');

    togglePasswordBtns.forEach(function (toggleBtn) {
        const targetId = toggleBtn.getAttribute('data-target');
        const inputField = targetId ? document.getElementById(targetId) : null;

        if (!inputField || (inputField.type !== 'password' && inputField.type !== 'text')) {
            return;
        }

        const showIcon = toggleBtn.getAttribute('data-show-icon') || 'visibility_off';
        const hideIcon = toggleBtn.getAttribute('data-hide-icon') || 'visibility';

        const togglePassword = function () {
            const isPassword = inputField.type === 'password';
            inputField.type = isPassword ? 'text' : 'password';
            toggleBtn.textContent = isPassword ? hideIcon : showIcon;
            toggleBtn.setAttribute('aria-pressed', String(!isPassword));
        };

        toggleBtn.addEventListener('click', togglePassword);

        toggleBtn.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                togglePassword();
            }
        });
    });
});

// utc datetime localization
document.addEventListener('DOMContentLoaded', function () {
    const dateTimeElements = document.querySelectorAll('[data-utc-datetime]');
    if (!dateTimeElements.length) {
        return;
    }

    const formatters = {
        datetime: new Intl.DateTimeFormat('en-GB', {
            year: 'numeric',
            month: 'short',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        }),
        date: new Intl.DateTimeFormat('en-GB', {
            year: 'numeric',
            month: 'short',
            day: '2-digit'
        }),
        time: new Intl.DateTimeFormat('en-GB', {
            hour: '2-digit',
            minute: '2-digit'
        })
    };

    dateTimeElements.forEach(function (element) {
        const rawUtc = element.getAttribute('data-utc-datetime');
        if (!rawUtc) {
            return;
        }

        const utcDate = new Date(rawUtc.endsWith('Z') ? rawUtc : `${rawUtc}Z`);
        if (Number.isNaN(utcDate.getTime())) {
            return;
        }

        const formatKey = element.getAttribute('data-utc-format') || 'datetime';
        const formatter = formatters[formatKey] || formatters.datetime;
        element.textContent = formatter.format(utcDate);
    });
});