// Shared authentication utility
// Storage keys
const TOKEN_KEY = 'weda_auth_token';
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

// Check if authenticated
function isAuthenticated() {
    return !!getToken();
}

// Clear auth (logout)
function clearAuth() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
}

// Authenticated fetch wrapper
async function authFetch(url, options = {}) {
    const headers = getAuthHeaders();
    if (options.headers) {
        Object.assign(headers, options.headers);
    }
    options.headers = headers;

    const response = await fetch(url, options);

    // Handle 401 Unauthorized - redirect to login or clear auth
    if (response.status === 401) {
        clearAuth();
        // Optionally redirect to home page for login
        // window.location.href = '/';
    }

    return response;
}
