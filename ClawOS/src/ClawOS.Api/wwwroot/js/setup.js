const setupForm = document.getElementById('setupForm');
const errorEl = document.getElementById('setupError');
const successEl = document.getElementById('setupSuccess');

// Check if setup is already complete
async function checkSetupStatus() {
    try {
        const response = await fetch('/api/v1/setup/status');
        const data = await response.json();

        if (data.hasUser) {
            // Already has user, redirect to main page (login modal will show if needed)
            window.location.href = '/clawos/index.html';
        }
    } catch (e) {
        console.error('Failed to check setup status:', e);
    }
}

setupForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    errorEl.textContent = '';
    successEl.textContent = '';

    const name = document.getElementById('name').value.trim();
    const email = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value;
    const confirmPassword = document.getElementById('confirmPassword').value;

    // Validate passwords match
    if (password !== confirmPassword) {
        errorEl.textContent = 'Passwords do not match';
        return;
    }

    setupForm.classList.add('loading');

    try {
        // Create the initial user account
        const response = await fetch('/api/v1/setup/init', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password, name })
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.detail || data.title || 'Setup failed');
        }

        // Auto-login after setup
        const loginResponse = await fetch('/api/v1/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        if (loginResponse.ok) {
            const authData = await loginResponse.json();
            saveAuth(authData);
            successEl.textContent = 'Account created! Redirecting...';
            setTimeout(() => {
                window.location.href = '/clawos/index.html';
            }, 1000);
        } else {
            // Login failed, but account was created - redirect to main page
            successEl.textContent = 'Account created! Please sign in...';
            setTimeout(() => {
                window.location.href = '/clawos/index.html';
            }, 1500);
        }

    } catch (err) {
        errorEl.textContent = err.message;
        setupForm.classList.remove('loading');
    }
});

// Check status on page load
checkSetupStatus();
