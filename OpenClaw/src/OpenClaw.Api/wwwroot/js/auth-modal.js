/**
 * Shared Auth Modal Component
 * Provides login, registration, and email verification functionality.
 * Requires: auth.js to be loaded first
 */

class AuthModal {
    constructor() {
        this.modal = null;
        this.currentTab = 'login';
        this.onAuthSuccess = null;
        this.verifyEmail = null;
        this.resendCooldown = 0;
        this.init();
    }

    init() {
        this.createModal();
        this.bindEvents();
    }

    createModal() {
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

                    <div class="auth-tabs" id="authTabs">
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
                            <div class="auth-forgot"><button type="button" class="auth-forgot-btn" id="forgotPasswordBtn">Forgot password?</button></div>
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

                    <!-- Verification Panel -->
                    <div class="auth-panel" id="verifyPanel">
                        <div class="verify-icon">&#9993;</div>
                        <p class="verify-subtitle">Enter the 6-digit code sent to</p>
                        <p class="verify-email" id="verifyEmailDisplay"></p>

                        <form class="auth-form" id="authVerifyForm">
                            <div class="verify-code-group">
                                <input type="text" id="verifyCode1" maxlength="1" inputmode="numeric" pattern="[0-9]" autocomplete="one-time-code">
                                <input type="text" id="verifyCode2" maxlength="1" inputmode="numeric" pattern="[0-9]">
                                <input type="text" id="verifyCode3" maxlength="1" inputmode="numeric" pattern="[0-9]">
                                <input type="text" id="verifyCode4" maxlength="1" inputmode="numeric" pattern="[0-9]">
                                <input type="text" id="verifyCode5" maxlength="1" inputmode="numeric" pattern="[0-9]">
                                <input type="text" id="verifyCode6" maxlength="1" inputmode="numeric" pattern="[0-9]">
                            </div>
                            <div class="auth-message" id="verifyMessage"></div>
                            <button type="submit" class="auth-submit">Verify</button>
                        </form>

                        <div class="verify-footer">
                            <span>Didn't receive the code?</span>
                            <button class="verify-resend" id="resendCodeBtn">Resend Code</button>
                        </div>
                        <button class="verify-back" id="verifyBackBtn">&larr; Change email</button>
                    </div>

                    <!-- Forgot Password Panel -->
                    <div class="auth-panel" id="forgotPanel">
                        <p class="verify-subtitle">Enter your email to receive a password reset link.</p>
                        <form class="auth-form" id="authForgotForm">
                            <div class="auth-form-group">
                                <label for="forgotEmail">Email</label>
                                <input type="email" id="forgotEmail" required placeholder="your@email.com" autocomplete="email">
                            </div>
                            <div class="auth-message" id="forgotMessage"></div>
                            <button type="submit" class="auth-submit">Send Reset Link</button>
                        </form>
                        <button class="verify-back" id="forgotBackBtn">&larr; Back to sign in</button>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHTML);
        this.modal = document.getElementById('authModal');
    }

    bindEvents() {
        document.getElementById('authModalClose').addEventListener('click', () => this.hide());

        this.modal.addEventListener('click', (e) => {
            if (e.target === this.modal) this.hide();
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.modal.classList.contains('show')) this.hide();
        });

        document.querySelectorAll('.auth-tab').forEach(tab => {
            tab.addEventListener('click', () => this.switchTab(tab.dataset.tab));
        });

        document.getElementById('authLoginForm').addEventListener('submit', (e) => this.handleLogin(e));
        document.getElementById('authRegisterForm').addEventListener('submit', (e) => this.handleRegister(e));
        document.getElementById('authVerifyForm').addEventListener('submit', (e) => this.handleVerify(e));
        document.getElementById('resendCodeBtn').addEventListener('click', () => this.handleResend());
        document.getElementById('verifyBackBtn').addEventListener('click', () => this.showRegisterPanel());
        document.getElementById('forgotPasswordBtn').addEventListener('click', () => this.showForgotPanel());
        document.getElementById('forgotBackBtn').addEventListener('click', () => this.switchTab('login'));
        document.getElementById('authForgotForm').addEventListener('submit', (e) => this.handleForgotPassword(e));

        // Wire up 6-digit code inputs
        this.initCodeInputs();
    }

    initCodeInputs() {
        const inputs = document.querySelectorAll('.verify-code-group input');
        inputs.forEach((input, i) => {
            input.addEventListener('input', (e) => {
                const val = e.target.value.replace(/\D/g, '');
                e.target.value = val;
                if (val && i < inputs.length - 1) {
                    inputs[i + 1].focus();
                }
                // Auto-submit when all 6 digits entered
                if (i === inputs.length - 1 && val) {
                    const code = this.getVerifyCode();
                    if (code.length === 6) {
                        document.getElementById('authVerifyForm').requestSubmit();
                    }
                }
            });
            input.addEventListener('keydown', (e) => {
                if (e.key === 'Backspace' && !e.target.value && i > 0) {
                    inputs[i - 1].focus();
                }
            });
            // Handle paste
            input.addEventListener('paste', (e) => {
                e.preventDefault();
                const paste = (e.clipboardData.getData('text') || '').replace(/\D/g, '').slice(0, 6);
                paste.split('').forEach((char, j) => {
                    if (inputs[i + j]) inputs[i + j].value = char;
                });
                const focusIdx = Math.min(i + paste.length, inputs.length - 1);
                inputs[focusIdx].focus();
                if (paste.length === 6) {
                    document.getElementById('authVerifyForm').requestSubmit();
                }
            });
        });
    }

    getVerifyCode() {
        return Array.from(document.querySelectorAll('.verify-code-group input'))
            .map(i => i.value).join('');
    }

    clearVerifyCode() {
        document.querySelectorAll('.verify-code-group input').forEach(i => { i.value = ''; });
        document.getElementById('verifyCode1').focus();
    }

    switchTab(tab) {
        this.currentTab = tab;

        document.querySelectorAll('.auth-tab').forEach(t => {
            t.classList.toggle('active', t.dataset.tab === tab);
        });

        document.getElementById('loginPanel').classList.toggle('active', tab === 'login');
        document.getElementById('registerPanel').classList.toggle('active', tab === 'register');
        document.getElementById('verifyPanel').classList.remove('active');
        document.getElementById('forgotPanel').classList.remove('active');
        document.getElementById('authTabs').style.display = '';

        document.getElementById('authModalTitle').textContent = tab === 'login' ? 'Sign In' : 'Create Account';

        document.getElementById('loginMessage').textContent = '';
        document.getElementById('registerMessage').textContent = '';
        document.getElementById('forgotMessage').textContent = '';
    }

    showVerifyPanel(email) {
        this.verifyEmail = email;
        document.getElementById('loginPanel').classList.remove('active');
        document.getElementById('registerPanel').classList.remove('active');
        document.getElementById('verifyPanel').classList.add('active');
        document.getElementById('authTabs').style.display = 'none';
        document.getElementById('authModalTitle').textContent = 'Verify Email';
        document.getElementById('verifyEmailDisplay').textContent = email;
        document.getElementById('verifyMessage').textContent = '';
        this.clearVerifyCode();
    }

    showRegisterPanel() {
        document.getElementById('verifyPanel').classList.remove('active');
        document.getElementById('registerPanel').classList.add('active');
        document.getElementById('authTabs').style.display = '';
        document.getElementById('authModalTitle').textContent = 'Create Account';
    }

    showForgotPanel() {
        document.getElementById('loginPanel').classList.remove('active');
        document.getElementById('registerPanel').classList.remove('active');
        document.getElementById('verifyPanel').classList.remove('active');
        document.getElementById('forgotPanel').classList.add('active');
        document.getElementById('authTabs').style.display = 'none';
        document.getElementById('authModalTitle').textContent = 'Forgot Password';
        document.getElementById('forgotMessage').textContent = '';
        document.getElementById('forgotEmail').focus();
    }

    show(tab = 'login', callback = null) {
        this.onAuthSuccess = callback;
        this.switchTab(tab);
        this.modal.classList.add('show');

        setTimeout(() => {
            const input = tab === 'login'
                ? document.getElementById('loginEmail')
                : document.getElementById('registerName');
            input?.focus();
        }, 100);
    }

    hide() {
        this.modal.classList.remove('show');
        document.getElementById('authLoginForm').reset();
        document.getElementById('authRegisterForm').reset();
        document.getElementById('loginMessage').textContent = '';
        document.getElementById('registerMessage').textContent = '';
        document.getElementById('verifyMessage').textContent = '';
        document.getElementById('authLoginForm').classList.remove('loading');
        document.getElementById('authRegisterForm').classList.remove('loading');
        document.getElementById('authVerifyForm').classList.remove('loading');
        this.clearVerifyCode();
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

            saveAuth(data);

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
                let errorMsg = data.detail || data.title || 'Registration failed';
                if (data.errors) {
                    const messages = Object.values(data.errors).flat();
                    if (messages.length > 0) errorMsg = messages.join('. ');
                }
                throw new Error(errorMsg);
            }

            form.classList.remove('loading');

            // Show verification code panel
            this.showVerifyPanel(email);

        } catch (err) {
            messageEl.textContent = err.message;
            messageEl.className = 'auth-message error';
            form.classList.remove('loading');
        }
    }

    async handleVerify(e) {
        e.preventDefault();
        const form = e.target;
        const messageEl = document.getElementById('verifyMessage');
        const code = this.getVerifyCode();

        if (code.length !== 6) {
            messageEl.textContent = 'Please enter the 6-digit code';
            messageEl.className = 'auth-message error';
            return;
        }

        messageEl.textContent = '';
        messageEl.className = 'auth-message';
        form.classList.add('loading');

        try {
            const response = await fetch('/api/v1/auth/register/verify', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email: this.verifyEmail, code })
            });

            const data = await response.json();

            if (!response.ok) {
                let errorMsg = data.detail || data.title || 'Verification failed';
                if (data.errors) {
                    const messages = Object.values(data.errors).flat();
                    if (messages.length > 0) errorMsg = messages.join('. ');
                }
                throw new Error(errorMsg);
            }

            form.classList.remove('loading');

            // Show success message
            messageEl.textContent = data.message || 'Registration complete! Please wait for admin approval.';
            messageEl.className = 'auth-message success';

            // Switch to login tab after delay
            setTimeout(() => {
                this.switchTab('login');
                const loginMsg = document.getElementById('loginMessage');
                loginMsg.textContent = 'Account created. Please wait for admin approval before signing in.';
                loginMsg.className = 'auth-message success';
            }, 2500);

        } catch (err) {
            messageEl.textContent = err.message;
            messageEl.className = 'auth-message error';
            form.classList.remove('loading');
            this.clearVerifyCode();
        }
    }

    async handleResend() {
        const btn = document.getElementById('resendCodeBtn');
        const messageEl = document.getElementById('verifyMessage');

        if (this.resendCooldown > 0) return;

        btn.disabled = true;

        try {
            const response = await fetch('/api/v1/auth/register/resend', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email: this.verifyEmail })
            });

            const data = await response.json();

            if (!response.ok) {
                throw new Error(data.detail || data.title || 'Failed to resend code');
            }

            messageEl.textContent = 'New code sent!';
            messageEl.className = 'auth-message success';
            this.clearVerifyCode();

            // Start cooldown (60s)
            this.resendCooldown = 60;
            const interval = setInterval(() => {
                this.resendCooldown--;
                btn.textContent = `Resend Code (${this.resendCooldown}s)`;
                if (this.resendCooldown <= 0) {
                    clearInterval(interval);
                    btn.textContent = 'Resend Code';
                    btn.disabled = false;
                }
            }, 1000);

        } catch (err) {
            messageEl.textContent = err.message;
            messageEl.className = 'auth-message error';
            btn.disabled = false;
        }
    }

    async handleForgotPassword(e) {
        e.preventDefault();
        const form = e.target;
        const messageEl = document.getElementById('forgotMessage');
        const email = document.getElementById('forgotEmail').value.trim();

        messageEl.textContent = '';
        messageEl.className = 'auth-message';
        form.classList.add('loading');

        try {
            const response = await fetch('/api/v1/auth/forgot-password', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email })
            });

            const data = await response.json();

            if (!response.ok) {
                throw new Error(data.detail || data.title || 'Request failed');
            }

            form.classList.remove('loading');
            messageEl.textContent = data.message || 'If an account exists, a reset link has been sent.';
            messageEl.className = 'auth-message success';

        } catch (err) {
            messageEl.textContent = err.message;
            messageEl.className = 'auth-message error';
            form.classList.remove('loading');
        }
    }
}

// Global instance
let authModal = null;

function initAuthModal() {
    if (!authModal) {
        authModal = new AuthModal();
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAuthModal);
} else {
    initAuthModal();
}

// Public API
function showLoginModal(callback) {
    if (!authModal) initAuthModal();
    if (authModal) authModal.show('login', callback);
}

function showRegisterModal(callback) {
    if (!authModal) initAuthModal();
    if (authModal) authModal.show('register', callback);
}

function hideAuthModal() {
    if (authModal) authModal.hide();
}
