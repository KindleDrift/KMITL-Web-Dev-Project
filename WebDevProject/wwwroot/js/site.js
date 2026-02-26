// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// Navbar toggle (the hamburger menu for mobile)
document.addEventListener('DOMContentLoaded', function() {
    const toggleButton = document.getElementById('mobile-menu');
    const navbarMenu = document.querySelector('.navbar-menu');

    if (!toggleButton || !navbarMenu) {
        return;
    }

    const openIcon = toggleButton.getAttribute('data-open-icon') || 'menu_open';
    const closedIcon = toggleButton.getAttribute('data-closed-icon') || 'menu';

    const toggleMenu = function() {
        const isOpen = navbarMenu.classList.toggle('show');
        toggleButton.setAttribute('aria-expanded', String(isOpen));
        toggleButton.textContent = isOpen ? openIcon : closedIcon;
    };

    toggleButton.addEventListener('click', toggleMenu);

    toggleButton.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            toggleMenu();
        }
    });
});

// Password visibility toggle
document.addEventListener('DOMContentLoaded', function() {
    const togglePasswordBtns = document.querySelectorAll('.toggle-password');

    togglePasswordBtns.forEach(function(toggleBtn) {
        const targetId = toggleBtn.getAttribute('data-target');
        const inputField = targetId ? document.getElementById(targetId) : null;

        if (!inputField || (inputField.type !== 'password' && inputField.type !== 'text')) {
            return;
        }

        const showIcon = toggleBtn.getAttribute('data-show-icon') || 'visibility_off';
        const hideIcon = toggleBtn.getAttribute('data-hide-icon') || 'visibility';

        const togglePassword = function() {
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