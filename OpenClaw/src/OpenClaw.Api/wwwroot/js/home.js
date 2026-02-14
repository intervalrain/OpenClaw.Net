// DOM Elements
const loginBtn = document.getElementById('loginBtn');
const loginModal = document.getElementById('loginModal');
const closeModal = document.getElementById('closeModal');
const loginForm = document.getElementById('loginForm');
const loginError = document.getElementById('loginError');
const userArea = document.getElementById('userArea');
const userInfo = document.getElementById('userInfo');

// Storage key
const TOKEN_KEY = 'weda_auth_token';
const USER_KEY = 'weda_user';

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    checkAuth();
});

// Check authentication status
function checkAuth() {
    const token = localStorage.getItem(TOKEN_KEY);
    const user = localStorage.getItem(USER_KEY);

    if (token && user) {
        try {
            const userData = JSON.parse(user);
            showLoggedInState(userData);
        } catch (e) {
            logout();
        }
    } else {
        showLoggedOutState();
    }
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

    // Show user info section
    userInfo.style.display = 'block';
    document.getElementById('userName').textContent = user.name;
    document.getElementById('userEmail').textContent = user.email;
    document.getElementById('userRoles').textContent = roles;
}

// Show logged out state
function showLoggedOutState() {
    userArea.innerHTML = `<button class="btn btn-primary" id="loginBtn">Login</button>`;
    document.getElementById('loginBtn').addEventListener('click', showLoginModal);
    userInfo.style.display = 'none';
}

// Show login modal
function showLoginModal() {
    loginModal.classList.add('show');
    loginError.textContent = '';
    loginForm.reset();
}

// Hide login modal
function hideLoginModal() {
    loginModal.classList.remove('show');
}

// Event listeners
loginBtn.addEventListener('click', showLoginModal);
closeModal.addEventListener('click', hideLoginModal);

loginModal.addEventListener('click', (e) => {
    if (e.target === loginModal) {
        hideLoginModal();
    }
});

// Handle login form submission
loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();

    const email = document.getElementById('email').value;
    const password = document.getElementById('password').value;

    try {
        const response = await fetch('/api/v1/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ email, password })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Login failed');
        }

        const data = await response.json();

        // Store token and user info
        localStorage.setItem(TOKEN_KEY, data.token);
        localStorage.setItem(USER_KEY, JSON.stringify({
            id: data.id,
            name: data.name,
            email: data.email,
            roles: data.roles,
            permissions: data.permissions
        }));

        hideLoginModal();
        showLoggedInState({
            id: data.id,
            name: data.name,
            email: data.email,
            roles: data.roles,
            permissions: data.permissions
        });

    } catch (error) {
        loginError.textContent = error.message;
    }
});

// Logout
function logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    showLoggedOutState();
}

// Expose logout to global scope for onclick handler
window.logout = logout;
