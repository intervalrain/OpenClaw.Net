// Shared authentication utility
// Storage keys
const TOKEN_KEY = 'weda_auth_token';
const REFRESH_TOKEN_KEY = 'weda_refresh_token';
const USER_KEY = 'weda_user';

// Get authentication headers for API calls
function getAuthHeaders() {
    const token = localStorage.getItem(TOKEN_KEY);
    const headers = {
        'Content-Type': 'application/json'
    };
    if (token) {
        headers['Authorization'] = 'Bearer ' + token;
    }
    return headers;
}

// Get current user
function getCurrentUser() {
    const user = localStorage.getItem(USER_KEY);
    if (user) {
        try {
            return JSON.parse(user);
        } catch (e) {
            return null;
        }
    }
    return null;
}

// Get token
function getToken() {
    return localStorage.getItem(TOKEN_KEY);
}

function getRefreshToken() {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
}

// Check if authenticated
function isAuthenticated() {
    return !!getToken();
}

// save auth data
function saveAuth(data) {
    localStorage.setItem(TOKEN_KEY, data.token);
    localStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify({
        id: data.id,
        name: data.name,
        email: data.email,
        roles: data.roles,
        permissions: data.permissions
    }));
}

// Clear auth (logout)
function clearAuth() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
}

// Refresh the access token
async function refreshAccessToken() {
    const refreshToken = getRefreshToken();
    if (!refreshToken) {
        return false;
    }

    try {
        const response = await fetch('/api/v1/auth/refresh', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken })
        });

        if (!response.ok) {
            return false;
        }

        const data = await response.json();
        saveAuth(data);
        return true;
    } catch (e) {
        return false;
    }
}

// Authenticated fetch wrapper
async function authFetch(url, options = {}) {
    const headers = getAuthHeaders();
    if (options.headers) {
        Object.assign(headers, options.headers);
    }
    options.headers = headers;

    let response = await fetch(url, options);

    // Handle 401 Unauthorized - try to refresh token
    if (response.status === 401) {
        const refreshed = await refreshAccessToken();
        if (refreshed) {
            // Retry with new token
            options.headers = getAuthHeaders();
            response = await fetch(url, options);
        } else {
            // Refresh failed, show login modal if available
            clearAuth();
            if (typeof showLoginModal === 'function') {
                showLoginModal(() => window.location.reload());
            } else {
                // Fallback to main page (which will show login modal)
                window.location.href = '/openclaw/index.html';
            }
        }
    }

    return response;
}
