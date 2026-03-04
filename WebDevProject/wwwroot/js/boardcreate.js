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

    toggleButton.addEventListener('keydown', function(event) {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            toggleMenu();
        }
    });
});