// Home page - uses shared auth.js and auth-modal.js

// DOM Elements
const userArea = document.getElementById('userArea');
const userInfo = document.getElementById('userInfo');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    // Try to refresh token if we have one but might be expired
    if (getRefreshToken() && !isAuthenticated()) {
        await refreshAccessToken();
    }

    checkAuth();
    initAdminCards();
});

// Check authentication status
function checkAuth() {
    const user = getCurrentUser();

    if (user) {
        showLoggedInState(user);
    } else {
        showLoggedOutState();
    }
}

// Show/hide role-restricted cards based on user role
function initAdminCards() {
    const user = getCurrentUser();

    if (!user || !user.roles) {
        // Hide admin and superadmin cards for non-authenticated users
        document.querySelectorAll('.admin-only, .superadmin-only').forEach(el => {
            el.style.display = 'none';
        });
        return;
    }

    const isSuperAdmin = user.roles.some(role =>
        role.toLowerCase() === 'superadmin'
    );

    const isAdmin = isSuperAdmin || user.roles.some(role =>
        role.toLowerCase() === 'admin'
    );

    // Show/hide admin-only cards (visible to Admin and SuperAdmin)
    document.querySelectorAll('.admin-only').forEach(el => {
        el.style.display = isAdmin ? '' : 'none';
    });

    // Show/hide superadmin-only cards (visible only to SuperAdmin)
    document.querySelectorAll('.superadmin-only').forEach(el => {
        el.style.display = isSuperAdmin ? '' : 'none';
    });
}

// Show logged in state
function showLoggedInState(user) {
    const initials = user.name
        .split(' ')
        .map(n => n[0])
        .join('')
        .toUpperCase()
        .slice(0, 2);

    const roles = user.roles ? user.roles.join(', ') : 'User';

    userArea.innerHTML = `
        <div class="user-info-header">
            <div class="user-avatar">${initials}</div>
            <div>
                <div class="user-name">${user.name}</div>
                <div class="user-role">${roles}</div>
            </div>
        </div>
        <button class="btn btn-secondary" onclick="logout()">Logout</button>
    `;

    // Show user info section if it exists
    if (userInfo) {
        userInfo.style.display = 'block';
        const userName = document.getElementById('userName');
        const userEmail = document.getElementById('userEmail');
        const userRoles = document.getElementById('userRoles');
        if (userName) userName.textContent = user.name;
        if (userEmail) userEmail.textContent = user.email;
        if (userRoles) userRoles.textContent = roles;
    }

    // Update admin cards visibility
    initAdminCards();
}

// Show logged out state
function showLoggedOutState() {
    userArea.innerHTML = `
        <button class="btn btn-secondary" id="registerBtn">Register</button>
        <button class="btn btn-primary" id="loginBtn">Login</button>
    `;

    document.getElementById('loginBtn').addEventListener('click', () => {
        showLoginModal(handleAuthSuccess);
    });

    document.getElementById('registerBtn').addEventListener('click', () => {
        showRegisterModal(handleAuthSuccess);
    });

    if (userInfo) {
        userInfo.style.display = 'none';
    }

    // Hide admin cards
    initAdminCards();
}

// Handle successful authentication
function handleAuthSuccess(data) {
    showLoggedInState({
        id: data.id,
        name: data.name,
        email: data.email,
        roles: data.roles,
        permissions: data.permissions
    });
    initAdminCards();
}

// Logout
function logout() {
    clearAuth();
    showLoggedOutState();
}

// Expose logout to global scope for onclick handler
window.logout = logout;
