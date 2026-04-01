/**
 * Shared Auth Modal Component
 * Provides login and registration functionality across all pages.
 * Requires: auth.js to be loaded first
 */

class AuthModal {
    constructor() {
        this.modal = null;
        this.currentTab = 'login';
        this.onAuthSuccess = null;
        this.init();
    }

    init() {
        // Create modal HTML
        this.createModal();
        this.bindEvents();
    }

    createModal() {
        // Check if modal already exists
        if (document.getElementById('authModal')) {
            this.modal = document.getElementById('authModal');
            return;
        }

        const modalHTML = `
            <div class="auth-modal" id="authModal">
                <div class="auth-modal-content">
                    <div class="auth-modal-header">
                        <h2 id="authModalTitle">Sign In</h2>
                        <button class="auth-modal-close" id="authModalClose">&times;</button>
                    </div>

                    <div class="auth-tabs">
                        <button class="auth-tab active" data-tab="login">Sign In</button>
                        <button class="auth-tab" data-tab="register">Register</button>
                    </div>

                    <!-- Login Panel -->
                    <div class="auth-panel active" id="loginPanel">
                        <form class="auth-form" id="authLoginForm">
                            <div class="auth-form-group">
                                <label for="loginEmail">Email</label>
                                <input type="email" id="loginEmail" name="email" required
                                       placeholder="your@email.com" autocomplete="email">
                            </div>
                            <div class="auth-form-group">
                                <label for="loginPassword">Password</label>
                                <input type="password" id="loginPassword" name="password" required
                                       placeholder="Enter your password" autocomplete="current-password">
                            </div>
                            <div class="auth-message" id="loginMessage"></div>
                            <button type="submit" class="auth-submit">Sign In</button>
                        </form>
                    </div>

                    <!-- Register Panel -->
                    <div class="auth-panel" id="registerPanel">
                        <form class="auth-form" id="authRegisterForm">
                            <div class="auth-form-group">
                                <label for="registerName">Name</label>
                                <input type="text" id="registerName" name="name" required
                                       placeholder="Your display name" autocomplete="name">
                            </div>
                            <div class="auth-form-group">
                                <label for="registerEmail">Email</label>
                                <input type="email" id="registerEmail" name="email" required
                                       placeholder="your@email.com" autocomplete="email">
                            </div>
                            <div class="auth-form-group">
                                <label for="registerPassword">Password</label>
                                <input type="password" id="registerPassword" name="password" required
                                       placeholder="At least 8 characters" minlength="8" autocomplete="new-password">
                                <div class="password-hint">Minimum 8 characters</div>
                            </div>
                            <div class="auth-form-group">
                                <label for="registerConfirmPassword">Confirm Password</label>
                                <input type="password" id="registerConfirmPassword" name="confirmPassword" required
                                       placeholder="Re-enter your password" autocomplete="new-password">
                            </div>
                            <div class="auth-message" id="registerMessage"></div>
                            <button type="submit" class="auth-submit">Create Account</button>
                        </form>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);
        this.modal = document.getElementById('authModal');
    }

    bindEvents() {
        // Close button
        document.getElementById('authModalClose').addEventListener('click', () => this.hide());

        // Click outside to close
        this.modal.addEventListener('click', (e) => {
            if (e.target === this.modal) {
                this.hide();
            }
        });

        // ESC key to close
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.modal.classList.contains('show')) {
                this.hide();
            }
        });

        // Tab switching
        document.querySelectorAll('.auth-tab').forEach(tab => {
            tab.addEventListener('click', () => this.switchTab(tab.dataset.tab));
        });

        // Login form
        document.getElementById('authLoginForm').addEventListener('submit', (e) => this.handleLogin(e));

        // Register form
        document.getElementById('authRegisterForm').addEventListener('submit', (e) => this.handleRegister(e));
    }

    switchTab(tab) {
        this.currentTab = tab;

        // Update tab buttons
        document.querySelectorAll('.auth-tab').forEach(t => {
            t.classList.toggle('active', t.dataset.tab === tab);
        });

        // Update panels
        document.getElementById('loginPanel').classList.toggle('active', tab === 'login');
        document.getElementById('registerPanel').classList.toggle('active', tab === 'register');

        // Update title
        document.getElementById('authModalTitle').textContent = tab === 'login' ? 'Sign In' : 'Create Account';

        // Clear messages
        document.getElementById('loginMessage').textContent = '';
        document.getElementById('registerMessage').textContent = '';
    }

    show(tab = 'login', callback = null) {
        this.onAuthSuccess = callback;
        this.switchTab(tab);
        this.modal.classList.add('show');

        // Focus first input
        setTimeout(() => {
            const input = tab === 'login'
                ? document.getElementById('loginEmail')
                : document.getElementById('registerName');
            input?.focus();
        }, 100);
    }

    hide() {
        this.modal.classList.remove('show');
        // Reset forms
        document.getElementById('authLoginForm').reset();
        document.getElementById('authRegisterForm').reset();
        document.getElementById('loginMessage').textContent = '';
        document.getElementById('registerMessage').textContent = '';
        document.getElementById('authLoginForm').classList.remove('loading');
        document.getElementById('authRegisterForm').classList.remove('loading');
    }

    async handleLogin(e) {
        e.preventDefault();
        const form = e.target;
        const messageEl = document.getElementById('loginMessage');

        const email = document.getElementById('loginEmail').value.trim();
        const password = document.getElementById('loginPassword').value;

        messageEl.textContent = '';
        messageEl.className = 'auth-message';
        form.classList.add('loading');

        try {
            const response = await fetch('/api/v1/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password })
            });

            const data = await response.json();

            if (!response.ok) {
                throw new Error(data.detail || data.title || 'Login failed');
            }

            // Save auth data
            saveAuth(data);

            // Success callback or reload
            if (this.onAuthSuccess) {
                this.hide();
                this.onAuthSuccess(data);
            } else {
                window.location.reload();
            }

        } catch (err) {
            messageEl.textContent = err.message;
            messageEl.className = 'auth-message error';
            form.classList.remove('loading');
        }
    }

    async handleRegister(e) {
        e.preventDefault();
        const form = e.target;
        const messageEl = document.getElementById('registerMessage');

        const name = document.getElementById('registerName').value.trim();
        const email = document.getElementById('registerEmail').value.trim();
        const password = document.getElementById('registerPassword').value;
        const confirmPassword = document.getElementById('registerConfirmPassword').value;

        messageEl.textContent = '';
        messageEl.className = 'auth-message';

        // Validate passwords match
        if (password !== confirmPassword) {
            messageEl.textContent = 'Passwords do not match';
            messageEl.className = 'auth-message error';
            return;
        }

        form.classList.add('loading');

        try {
            const response = await fetch('/api/v1/auth/register', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password, name })
            });

            const data = await response.json();

            if (!response.ok) {
                // Parse ASP.NET validation errors
                let errorMsg = data.detail || data.title || 'Registration failed';
                if (data.errors) {
                    const messages = Object.values(data.errors).flat();
                    if (messages.length > 0) errorMsg = messages.join('. ');
                }
                throw new Error(errorMsg);
            }

            form.classList.remove('loading');

            // Check if registration is pending approval (no token returned)
            if (data.status === 'Pending' || !data.token) {
                // Show success message for pending approval
                messageEl.textContent = data.message || 'Registration submitted. Please wait for admin approval.';
                messageEl.className = 'auth-message success';

                // Reset form
                form.reset();

                // Switch to login tab after a delay
                setTimeout(() => {
                    this.switchTab('login');
                }, 3000);
            } else {
                // Legacy: auto-login if token is returned (for backwards compatibility)
                saveAuth(data);

                // Success callback or reload
                if (this.onAuthSuccess) {
                    this.hide();
                    this.onAuthSuccess(data);
                } else {
                    window.location.reload();
                }
            }

        } catch (err) {
            messageEl.textContent = err.message;
            messageEl.className = 'auth-message error';
            form.classList.remove('loading');
        }
    }
}

// Global instance
let authModal = null;

// Initialize immediately if DOM is ready, otherwise wait
function initAuthModal() {
    if (!authModal) {
        authModal = new AuthModal();
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAuthModal);
} else {
    // DOM is already ready
    initAuthModal();
}

// Public API
function showLoginModal(callback) {
    // Ensure modal is initialized
    if (!authModal) {
        initAuthModal();
    }
    if (authModal) {
        authModal.show('login', callback);
    }
}

function showRegisterModal(callback) {
    // Ensure modal is initialized
    if (!authModal) {
        initAuthModal();
    }
    if (authModal) {
        authModal.show('register', callback);
    }
}

function hideAuthModal() {
    if (authModal) {
        authModal.hide();
    }
}
