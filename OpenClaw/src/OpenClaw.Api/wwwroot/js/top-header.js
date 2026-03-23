/**
 * Shared Top Header Navigation Component
 * Automatically inserts navigation header at the top of the page
 *
 * Usage: Include this script and call initTopHeader() or let it auto-init
 */

function createTopHeader(activePage = '') {
    // Detect active page from URL if not provided
    if (!activePage) {
        const path = window.location.pathname;
        if (path.includes('/openclaw')) activePage = 'chat';
        else if (path.includes('/workflows')) activePage = 'workflows';
        else if (path.includes('/pipelines')) activePage = 'pipelines';
        else if (path.includes('/wiki')) activePage = 'wiki';
        else if (path.includes('/wedally')) activePage = 'wedally';
    }

    const headerHTML = `
        <header class="top-header">
            <nav class="top-nav">
                <a href="/" class="nav-link home-link" title="Home">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
                        <polyline points="9 22 9 12 15 12 15 22"/>
                    </svg>
                </a>
                <a href="/openclaw/index.html" class="nav-link ${activePage === 'chat' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z"/>
                    </svg>
                    <span>Chat</span>
                </a>
                <a href="/workflows/index.html" class="nav-link ${activePage === 'workflows' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="6" cy="6" r="3"/>
                        <circle cx="6" cy="18" r="3"/>
                        <line x1="20" y1="4" x2="8.12" y2="15.88"/>
                        <line x1="14.47" y1="14.48" x2="20" y2="20"/>
                        <line x1="8.12" y1="8.12" x2="12" y2="12"/>
                    </svg>
                    <span>Workflows</span>
                </a>
                <a href="/pipelines/index.html" class="nav-link ${activePage === 'pipelines' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="6" y1="3" x2="6" y2="15"/>
                        <circle cx="18" cy="6" r="3"/>
                        <circle cx="6" cy="18" r="3"/>
                        <path d="M18 9a9 9 0 0 1-9 9"/>
                    </svg>
                    <span>Pipelines</span>
                </a>
                <a href="/wiki/index.html" class="nav-link ${activePage === 'wiki' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
                        <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
                    </svg>
                    <span>Wiki</span>
                </a>
                <a href="/wedally/index.html" class="nav-link admin-only ${activePage === 'wedally' ? 'active' : ''}" style="display: none;">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/>
                    </svg>
                    <span>Wedally</span>
                </a>
                <a href="/swagger" class="nav-link admin-only" target="_blank" style="display: none;">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                        <polyline points="14 2 14 8 20 8"/>
                        <line x1="16" y1="13" x2="8" y2="13"/>
                        <line x1="16" y1="17" x2="8" y2="17"/>
                    </svg>
                    <span>API</span>
                </a>
                <div class="nav-spacer"></div>
                <button class="theme-toggle" id="themeToggle" title="Toggle theme">
                    <svg class="icon-sun" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="5"/>
                        <line x1="12" y1="1" x2="12" y2="3"/>
                        <line x1="12" y1="21" x2="12" y2="23"/>
                        <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/>
                        <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
                        <line x1="1" y1="12" x2="3" y2="12"/>
                        <line x1="21" y1="12" x2="23" y2="12"/>
                        <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/>
                        <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
                    </svg>
                    <svg class="icon-moon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
                    </svg>
                </button>
                <div class="nav-user-area" id="navUserArea">
                    <!-- Will be populated by updateNavUserArea -->
                </div>
            </nav>
        </header>
    `;

    return headerHTML;
}

function updateNavUserArea() {
    const userArea = document.getElementById('navUserArea');
    if (!userArea) return;

    // Check if auth functions are available
    if (typeof getCurrentUser === 'function' && typeof isAuthenticated === 'function') {
        const user = getCurrentUser();
        if (isAuthenticated() && user) {
            userArea.innerHTML = `
                <span class="user-name">${user.name || user.email}</span>
                <span class="logout-link" onclick="handleNavLogout()">Logout</span>
            `;
        } else {
            userArea.innerHTML = `
                <span class="logout-link" onclick="handleNavLogin()">Login</span>
            `;
        }
    }

    // Show admin-only items if user has admin role
    showAdminNavItems();
}

function showAdminNavItems() {
    if (typeof getCurrentUser !== 'function') return;

    const user = getCurrentUser();
    if (!user || !user.roles) return;

    const isAdmin = user.roles.some(role =>
        role.toLowerCase() === 'admin' || role.toLowerCase() === 'superadmin'
    );

    if (isAdmin) {
        document.querySelectorAll('.top-nav .admin-only').forEach(el => {
            el.style.display = '';
        });
    }
}

function handleNavLogout() {
    if (typeof clearAuth === 'function') {
        clearAuth();
    }
    if (typeof showLoginModal === 'function') {
        showLoginModal(() => window.location.reload());
    } else {
        window.location.reload();
    }
}

function handleNavLogin() {
    if (typeof showLoginModal === 'function') {
        showLoginModal(() => window.location.reload());
    }
}

// Theme management
function getStoredTheme() {
    return localStorage.getItem('theme') || 'light';
}

function setTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('theme', theme);
}

function toggleTheme() {
    const currentTheme = getStoredTheme();
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    setTheme(newTheme);
}

function initTheme() {
    const theme = getStoredTheme();
    setTheme(theme);

    const toggle = document.getElementById('themeToggle');
    if (toggle) {
        toggle.addEventListener('click', toggleTheme);
    }
}

function initTopHeader(activePage = '') {
    // Check if header already exists
    if (document.querySelector('.top-header')) {
        updateNavUserArea();
        initTheme();
        return;
    }

    // Insert header at the start of body
    const headerHTML = createTopHeader(activePage);
    document.body.insertAdjacentHTML('afterbegin', headerHTML);

    // Update user area
    updateNavUserArea();

    // Initialize theme toggle
    initTheme();
}

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => initTopHeader());
} else {
    initTopHeader();
}
