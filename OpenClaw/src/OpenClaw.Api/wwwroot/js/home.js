// Home page - uses shared auth.js and auth-modal.js
// Note: User login/logout UI is handled by top-header.js

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    // Try to refresh token if we have one but might be expired
    if (getRefreshToken() && !isAuthenticated()) {
        await refreshAccessToken();
    }

    initAdminCards();
});

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
