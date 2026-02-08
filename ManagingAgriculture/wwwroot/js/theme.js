//  theme.js - Dark/Light Theme Switcher with Cookie Persistence
// This script runs on every page load and manages the theme preference

(function () {
    'use strict';

    // Get theme from cookie (works even when not logged in)
    function getThemeFromCookie() {
        const name = 'theme=';
        const decodedCookie = decodeURIComponent(document.cookie);
        const cookieArray = decodedCookie.split(';');

        for (let i = 0; i < cookieArray.length; i++) {
            let cookie = cookieArray[i].trim();
            if (cookie.indexOf(name) === 0) {
                return cookie.substring(name.length);
            }
        }
        return 'light'; // default
    }

    // Set theme cookie (persists for 365 days)
    function setThemeCookie(theme) {
        const d = new Date();
        d.setTime(d.getTime() + (365 * 24 * 60 * 60 * 1000)); // 1 year
        const expires = 'expires=' + d.toUTCString();
        document.cookie = 'theme=' + theme + ';' + expires + ';path=/';
    }

    // Apply theme to document
    function applyTheme(theme) {
        if (theme === 'dark') {
            document.documentElement.setAttribute('data-theme', 'dark');
        } else {
            document.documentElement.removeAttribute('data-theme');
        }

        // Update toggle button if it exists
        const themeToggle = document.getElementById('theme-toggle');
        if (themeToggle) {
            const icon = themeToggle.querySelector('i');
            if (icon) {
                if (theme === 'dark') {
                    icon.className = 'fas fa-sun';
                    themeToggle.title = 'Switch to Light Mode';
                } else {
                    icon.className = 'fas fa-moon';
                    themeToggle.title = 'Switch to Dark Mode';
                }
            }
        }
    }

    // Toggle theme
    function toggleTheme() {
        const currentTheme = getThemeFromCookie();
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

        setThemeCookie(newTheme);
        applyTheme(newTheme);

        // Optionally sync to server if user is logged in
        syncThemeToServer(newTheme);
    }

    // Sync theme preference to server (for logged-in users)
    function syncThemeToServer(theme) {
        // Only sync if user is authenticated (check if there's an antiforgery token)
        const tokenElement = document.querySelector('input[name=\"__RequestVerificationToken\"]');
        if (!tokenElement) return; // User not logged in

        fetch('/Account/SaveThemePreference', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': tokenElement.value
            },
            body: JSON.stringify({ theme: theme })
        }).catch(err => {
            console.log('Could not sync theme to server:', err);
        });
    }

    // Initialize theme on page load
    function initTheme() {
        const theme = getThemeFromCookie();
        applyTheme(theme);
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initTheme);
    } else {
        initTheme();
    }

    // Expose toggle function globally
    window.toggleTheme = toggleTheme;
})();
