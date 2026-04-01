// Display ban reason from URL params or localStorage
const params = new URLSearchParams(window.location.search);
const reason = params.get('reason');
if (reason) {
    document.getElementById('reason-text').textContent = reason;
}

document.getElementById('logout-btn').addEventListener('click', () => {
    if (typeof clearAuth === 'function') clearAuth();
    window.location.href = '/';
});
