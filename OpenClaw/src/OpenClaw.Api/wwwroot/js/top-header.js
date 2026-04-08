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
        else if (path.includes('/workspace')) activePage = 'workspace';
        else if (path.includes('/cronjobs')) activePage = 'cronjobs';
        else if (path.includes('/agents')) activePage = 'agents';
        else if (path.includes('/village')) activePage = 'village';
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
                <a href="/cronjobs/index.html" class="nav-link ${activePage === 'cronjobs' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"/>
                        <polyline points="12 6 12 12 16 14"/>
                    </svg>
                    <span>Cron Jobs</span>
                </a>
                <a href="/agents/index.html" class="nav-link ${activePage === 'agents' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M12 2a4 4 0 0 1 4 4v1a4 4 0 0 1-8 0V6a4 4 0 0 1 4-4z"/>
                        <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/>
                        <circle cx="12" cy="7" r="4"/>
                    </svg>
                    <span>Agents</span>
                </a>
                <a href="/village/index.html" class="nav-link ${activePage === 'village' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="2" y="7" width="20" height="14" rx="2"/>
                        <path d="M16 7V5a4 4 0 0 0-8 0v2"/>
                        <circle cx="12" cy="14" r="2"/>
                    </svg>
                    <span>Village</span>
                </a>
                <a href="/wiki/index.html" class="nav-link ${activePage === 'wiki' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/>
                        <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
                    </svg>
                    <span>Wiki</span>
                </a>
                <a href="/workspace/index.html" class="nav-link ${activePage === 'workspace' ? 'active' : ''}">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
                    </svg>
                    <span>Files</span>
                </a>
                <a href="/wedally/index.html" class="nav-link superadmin-only ${activePage === 'wedally' ? 'active' : ''}" target="_blank" style="display: none;">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/>
                    </svg>
                    <span>Wedally</span>
                </a>
                <a href="/admin/index.html" class="nav-link admin-only" target="_blank" style="display: none;">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="3"/>
                        <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-2 2 2 2 0 01-2-2v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83 0 2 2 0 010-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 01-2-2 2 2 0 012-2h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 010-2.83 2 2 0 012.83 0l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 012-2 2 2 0 012 2v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 0 2 2 0 010 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 012 2 2 2 0 01-2 2h-.09a1.65 1.65 0 00-1.51 1z"/>
                    </svg>
                    <span>Settings</span>
                </a>
                <a href="/swagger" class="nav-link superadmin-only" target="_blank" style="display: none;">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                        <polyline points="14 2 14 8 20 8"/>
                        <line x1="16" y1="13" x2="8" y2="13"/>
                        <line x1="16" y1="17" x2="8" y2="17"/>
                    </svg>
                    <span>API</span>
                </a>
                <div class="nav-spacer"></div>
                <div class="nav-update-badge" id="navUpdateBadge" style="display: none;" title="Update available">
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/>
                        <polyline points="7 10 12 15 17 10"/>
                        <line x1="12" y1="15" x2="12" y2="3"/>
                    </svg>
                    <span class="update-dot"></span>
                </div>
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
                <div class="nav-user-menu">
                    <button class="nav-user-btn" id="navUserBtn">
                        <span class="user-name">${user.name || user.email}</span>
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M6 9l6 6 6-6"/>
                        </svg>
                    </button>
                    <div class="nav-user-dropdown" id="navUserDropdown">
                        <button class="nav-dropdown-item" id="navTokenUsageBtn">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M12 2v20M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6"/>
                            </svg>
                            <span>Token Usage</span>
                        </button>
                        <div class="nav-dropdown-divider"></div>
                        <button class="nav-dropdown-item nav-dropdown-logout" id="navLogoutBtn">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4"/>
                                <polyline points="16 17 21 12 16 7"/>
                                <line x1="21" y1="12" x2="9" y2="12"/>
                            </svg>
                            <span>Logout</span>
                        </button>
                    </div>
                </div>
            `;
            document.getElementById('navUserBtn').addEventListener('click', toggleNavUserDropdown);
            document.getElementById('navLogoutBtn').addEventListener('click', handleNavLogout);
            document.getElementById('navTokenUsageBtn').addEventListener('click', () => {
                closeNavUserDropdown();
                if (typeof openTokenUsageModal === 'function') openTokenUsageModal();
            });
            // Close dropdown on outside click
            document.addEventListener('click', (e) => {
                const menu = document.querySelector('.nav-user-menu');
                if (menu && !menu.contains(e.target)) closeNavUserDropdown();
            });
        } else {
            userArea.innerHTML = `
                <span class="logout-link" id="navLoginBtn">Login</span>
            `;
            document.getElementById('navLoginBtn').addEventListener('click', handleNavLogin);
        }
    }

    // Show admin-only items if user has admin role
    showAdminNavItems();
}

function toggleNavUserDropdown() {
    const dropdown = document.getElementById('navUserDropdown');
    if (dropdown) dropdown.classList.toggle('open');
}

function closeNavUserDropdown() {
    const dropdown = document.getElementById('navUserDropdown');
    if (dropdown) dropdown.classList.remove('open');
}

function showAdminNavItems() {
    if (typeof getCurrentUser !== 'function') return;

    const user = getCurrentUser();
    if (!user || !user.roles) return;

    const isSuperAdmin = user.roles.some(role => role.toLowerCase() === 'superadmin');
    const isAdmin = isSuperAdmin || user.roles.some(role => role.toLowerCase() === 'admin');

    if (isAdmin) {
        document.querySelectorAll('.top-nav .admin-only').forEach(el => {
            el.style.display = '';
        });
    }
    if (isSuperAdmin) {
        document.querySelectorAll('.superadmin-only').forEach(el => {
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
// NOTE: Default is dark (no data-theme attribute = dark theme in CSS)
function getStoredTheme() {
    return localStorage.getItem('theme') || 'dark';
}

function setTheme(theme) {
    if (theme === 'dark') {
        // Dark is default, remove attribute for cleaner DOM
        document.documentElement.removeAttribute('data-theme');
    } else {
        document.documentElement.setAttribute('data-theme', theme);
    }
    localStorage.setItem('theme', theme);

    // Update theme toggle icons via JS (more reliable than CSS)
    updateThemeIcons(theme);

    // Dispatch custom event for pages that need to update additional UI
    window.dispatchEvent(new CustomEvent('themechange', { detail: { theme } }));
}

function updateThemeIcons(theme) {
    const sunIcon = document.querySelector('.top-nav .theme-toggle .icon-sun');
    const moonIcon = document.querySelector('.top-nav .theme-toggle .icon-moon');

    if (sunIcon && moonIcon) {
        if (theme === 'dark') {
            sunIcon.style.display = 'block';
            moonIcon.style.display = 'none';
        } else {
            sunIcon.style.display = 'none';
            moonIcon.style.display = 'block';
        }
    }
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

// Check for updates and show badge / overlay
async function checkForUpdateBadge() {
    try {
        const token = typeof getToken === 'function' ? getToken() : null;
        if (!token) return;
        const res = await fetch('/api/v1/updates/status', {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!res.ok) return;
        const data = await res.json();

        // Show progress overlay if update is in progress
        if (data.updateStatus === 'pulling' || data.updateStatus === 'restarting') {
            showUpdateOverlay(data.updateStatus, data.statusMessage);
            pollUpdateProgress();
            return;
        }

        // Show "completed" briefly then dismiss
        if (data.updateStatus === 'completed') {
            showUpdateOverlay('completed', data.statusMessage);
            setTimeout(() => removeUpdateOverlay(), 5000);
        }

        // Show badge if update available
        const badge = document.getElementById('navUpdateBadge');
        if (badge && data.updateAvailable) {
            badge.style.display = 'flex';
            badge.title = `Update available: ${data.latestVersion}`;
            badge.style.cursor = 'pointer';
            badge.addEventListener('click', () => {
                if (data.latestVersion) {
                    window.open(`https://github.com/intervalrain/OpenClaw.Net/releases/tag/${data.latestVersion}`, '_blank');
                }
            });
        }
    } catch {}
}

function showUpdateOverlay(status, message) {
    let overlay = document.getElementById('update-overlay');
    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = 'update-overlay';
        overlay.className = 'update-overlay';
        document.body.appendChild(overlay);
    }
    const icon = status === 'completed'
        ? '<svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#3fb950" stroke-width="2"><path d="M22 11.08V12a10 10 0 11-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>'
        : '<svg class="spin" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#58a6ff" stroke-width="2"><path d="M21 12a9 9 0 11-6.219-8.56"/></svg>';
    overlay.innerHTML = `
        <div class="update-overlay-content">
            ${icon}
            <h3>${status === 'completed' ? 'Update Complete' : 'Updating...'}</h3>
            <p>${message || ''}</p>
            ${status === 'restarting' ? '<p class="update-hint">Server is restarting. This page will reload automatically.</p>' : ''}
        </div>
    `;
}

function removeUpdateOverlay() {
    document.getElementById('update-overlay')?.remove();
}

function pollUpdateProgress() {
    const token = typeof getToken === 'function' ? getToken() : null;
    const interval = setInterval(async () => {
        try {
            const res = await fetch('/api/v1/updates/status', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (!res.ok) throw new Error('offline');
            const data = await res.json();

            if (data.updateStatus === 'completed') {
                clearInterval(interval);
                showUpdateOverlay('completed', data.statusMessage);
                setTimeout(() => window.location.reload(), 3000);
            } else if (data.updateStatus === 'failed') {
                clearInterval(interval);
                showUpdateOverlay('failed', data.statusMessage);
            } else {
                showUpdateOverlay(data.updateStatus, data.statusMessage);
            }
        } catch {
            // Server is down (restarting) — keep polling
            showUpdateOverlay('restarting', 'Server is restarting...');
        }
    }, 3000);
}

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => { initTopHeader(); checkForUpdateBadge(); });
} else {
    initTopHeader();
    checkForUpdateBadge();
}
