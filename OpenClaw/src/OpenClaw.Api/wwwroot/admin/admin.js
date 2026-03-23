// Admin User Management Page

const API_BASE = '/api/v1/user-management';

// State
let allUsers = [];
let pendingUsers = [];
let selectedUser = null;
let userToDelete = null;

// DOM Elements
const pendingUserList = document.getElementById('pendingUserList');
const allUserList = document.getElementById('allUserList');
const pendingCount = document.getElementById('pendingCount');
const statusFilter = document.getElementById('statusFilter');
const tabs = document.querySelectorAll('.tab');
const tabContents = document.querySelectorAll('.tab-content');

// Modals
const userModal = document.getElementById('userModal');
const deleteModal = document.getElementById('deleteModal');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    // Check if user is SuperAdmin
    const user = getCurrentUser();
    if (!user || !user.roles || !user.roles.some(r => r.toLowerCase() === 'superadmin')) {
        alert('Access denied. SuperAdmin role required.');
        window.location.href = '/';
        return;
    }

    initTabs();
    initModals();
    initFilters();
    await loadUsers();
});

// Tab switching
function initTabs() {
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            const tabId = tab.dataset.tab;

            tabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');

            tabContents.forEach(content => {
                content.classList.toggle('active', content.id === `${tabId}Tab`);
            });
        });
    });
}

// Modal handlers
function initModals() {
    document.getElementById('closeModal').addEventListener('click', closeUserModal);
    document.getElementById('closeDeleteModal').addEventListener('click', closeDeleteModal);
    document.getElementById('cancelDelete').addEventListener('click', closeDeleteModal);
    document.getElementById('confirmDelete').addEventListener('click', confirmDeleteUser);

    // Close modals on overlay click
    userModal.addEventListener('click', e => {
        if (e.target === userModal) closeUserModal();
    });
    deleteModal.addEventListener('click', e => {
        if (e.target === deleteModal) closeDeleteModal();
    });
}

// Filter handlers
function initFilters() {
    statusFilter.addEventListener('change', () => {
        renderAllUsers();
    });
}

// Load all users
async function loadUsers() {
    try {
        // Load all users
        const response = await authFetch(`${API_BASE}`);
        if (!response.ok) throw new Error('Failed to load users');

        allUsers = await response.json();
        pendingUsers = allUsers.filter(u => u.status === 'Pending');

        // Update UI
        pendingCount.textContent = pendingUsers.length;
        pendingCount.style.display = pendingUsers.length > 0 ? '' : 'none';

        renderPendingUsers();
        renderAllUsers();
    } catch (error) {
        console.error('Error loading users:', error);
        pendingUserList.innerHTML = '<div class="empty-state"><p>Failed to load users</p></div>';
        allUserList.innerHTML = '<div class="empty-state"><p>Failed to load users</p></div>';
    }
}

// Render pending users
function renderPendingUsers() {
    if (pendingUsers.length === 0) {
        pendingUserList.innerHTML = `
            <div class="empty-state">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/>
                    <polyline points="22 4 12 14.01 9 11.01"/>
                </svg>
                <h3>No Pending Users</h3>
                <p>All user registrations have been processed</p>
            </div>
        `;
        return;
    }

    pendingUserList.innerHTML = pendingUsers.map(user => renderUserCard(user, true)).join('');
}

// Render all users with filter
function renderAllUsers() {
    const filterStatus = statusFilter.value;
    const filteredUsers = filterStatus
        ? allUsers.filter(u => u.status === filterStatus)
        : allUsers;

    if (filteredUsers.length === 0) {
        allUserList.innerHTML = `
            <div class="empty-state">
                <h3>No Users Found</h3>
                <p>${filterStatus ? `No users with status "${filterStatus}"` : 'No users in the system'}</p>
            </div>
        `;
        return;
    }

    allUserList.innerHTML = filteredUsers.map(user => renderUserCard(user, false)).join('');
}

// Render a single user card
function renderUserCard(user, isPendingView) {
    const initials = user.name
        .split(' ')
        .map(n => n[0])
        .join('')
        .toUpperCase()
        .slice(0, 2);

    const statusClass = user.status.toLowerCase();
    const roles = user.roles || [];
    const roleBadges = roles.map(role => {
        const roleClass = role.toLowerCase();
        return `<span class="role-badge ${roleClass}">${role}</span>`;
    }).join('');

    const registrationTime = user.createdAt
        ? new Date(user.createdAt).toLocaleDateString()
        : '';

    let actions = '';
    if (isPendingView) {
        actions = `
            <button class="btn btn-success btn-sm" onclick="approveUser('${user.id}')">
                Approve
            </button>
            <button class="btn btn-danger btn-sm" onclick="rejectUser('${user.id}')">
                Reject
            </button>
        `;
    } else {
        actions = `
            <button class="btn btn-outline btn-sm" onclick="showUserDetails('${user.id}')">
                Manage
            </button>
        `;
    }

    return `
        <div class="user-card" data-user-id="${user.id}">
            <div class="user-card-info">
                <div class="user-avatar">${initials}</div>
                <div class="user-details">
                    <h3>${escapeHtml(user.name)}</h3>
                    <p>${escapeHtml(user.email)}</p>
                    <div class="user-meta">
                        <span class="status-badge ${statusClass}">${user.status}</span>
                        ${roleBadges}
                    </div>
                </div>
            </div>
            <div class="user-card-actions">
                ${registrationTime ? `<span class="registration-time">${registrationTime}</span>` : ''}
                ${actions}
            </div>
        </div>
    `;
}

// Approve a pending user
async function approveUser(userId) {
    try {
        const response = await authFetch(`${API_BASE}/${userId}/approve`, {
            method: 'POST'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to approve user');
        }

        await loadUsers();
        showToast('User approved successfully', 'success');
    } catch (error) {
        console.error('Error approving user:', error);
        showToast(error.message, 'error');
    }
}

// Reject a pending user
async function rejectUser(userId) {
    try {
        const response = await authFetch(`${API_BASE}/${userId}/reject`, {
            method: 'POST'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to reject user');
        }

        await loadUsers();
        showToast('User rejected and removed', 'success');
    } catch (error) {
        console.error('Error rejecting user:', error);
        showToast(error.message, 'error');
    }
}

// Show user details modal
function showUserDetails(userId) {
    selectedUser = allUsers.find(u => u.id === userId);
    if (!selectedUser) return;

    const currentUser = getCurrentUser();
    const isSelf = currentUser && currentUser.id === userId;

    document.getElementById('modalTitle').textContent = selectedUser.name;

    const roles = ['User', 'Admin', 'SuperAdmin'];
    const userRoles = selectedUser.roles || [];

    document.getElementById('modalBody').innerHTML = `
        <div class="user-detail">
            <label>Email</label>
            <div class="value">${escapeHtml(selectedUser.email)}</div>
        </div>
        <div class="user-detail">
            <label>Status</label>
            <div class="value">
                <select id="userStatusSelect" ${isSelf ? 'disabled' : ''}>
                    <option value="Active" ${selectedUser.status === 'Active' ? 'selected' : ''}>Active</option>
                    <option value="Inactive" ${selectedUser.status === 'Inactive' ? 'selected' : ''}>Inactive</option>
                    <option value="Locked" ${selectedUser.status === 'Locked' ? 'selected' : ''}>Locked</option>
                </select>
            </div>
        </div>
        <div class="user-detail">
            <label>Roles</label>
            <div class="role-select">
                ${roles.map(role => `
                    <label class="role-checkbox ${userRoles.includes(role) ? 'checked' : ''}" ${isSelf && role === 'SuperAdmin' ? 'title="Cannot remove own SuperAdmin role"' : ''}>
                        <input type="checkbox" value="${role}"
                            ${userRoles.includes(role) ? 'checked' : ''}
                            ${isSelf && role === 'SuperAdmin' ? 'disabled' : ''}>
                        ${role}
                    </label>
                `).join('')}
            </div>
        </div>
        ${selectedUser.createdAt ? `
            <div class="user-detail">
                <label>Registered</label>
                <div class="value">${new Date(selectedUser.createdAt).toLocaleString()}</div>
            </div>
        ` : ''}
        ${selectedUser.lastLoginAt ? `
            <div class="user-detail">
                <label>Last Login</label>
                <div class="value">${new Date(selectedUser.lastLoginAt).toLocaleString()}</div>
            </div>
        ` : ''}
    `;

    // Setup role checkbox styling
    document.querySelectorAll('.role-checkbox input').forEach(checkbox => {
        checkbox.addEventListener('change', (e) => {
            e.target.parentElement.classList.toggle('checked', e.target.checked);
        });
    });

    document.getElementById('modalFooter').innerHTML = `
        ${!isSelf ? `<button class="btn btn-danger" onclick="showDeleteModal()">Delete</button>` : ''}
        <button class="btn btn-secondary" onclick="closeUserModal()">Cancel</button>
        <button class="btn btn-primary" onclick="saveUserChanges()">Save Changes</button>
    `;

    userModal.classList.add('active');
}

// Save user changes
async function saveUserChanges() {
    if (!selectedUser) return;

    const newStatus = document.getElementById('userStatusSelect').value;
    const newRoles = Array.from(document.querySelectorAll('.role-checkbox input:checked'))
        .map(cb => cb.value);

    try {
        // Update status if changed
        if (newStatus !== selectedUser.status) {
            const statusResponse = await authFetch(`${API_BASE}/${selectedUser.id}/status`, {
                method: 'PUT',
                body: JSON.stringify({ status: newStatus })
            });

            if (!statusResponse.ok) {
                const error = await statusResponse.json();
                throw new Error(error.title || 'Failed to update status');
            }
        }

        // Update roles if changed
        const rolesChanged = JSON.stringify(newRoles.sort()) !== JSON.stringify((selectedUser.roles || []).sort());
        if (rolesChanged) {
            const rolesResponse = await authFetch(`${API_BASE}/${selectedUser.id}/roles`, {
                method: 'PUT',
                body: JSON.stringify({ roles: newRoles })
            });

            if (!rolesResponse.ok) {
                const error = await rolesResponse.json();
                throw new Error(error.title || 'Failed to update roles');
            }
        }

        closeUserModal();
        await loadUsers();
        showToast('User updated successfully', 'success');
    } catch (error) {
        console.error('Error saving user changes:', error);
        showToast(error.message, 'error');
    }
}

// Show delete confirmation modal
function showDeleteModal() {
    if (!selectedUser) return;

    userToDelete = selectedUser;
    document.getElementById('deleteUserName').textContent = selectedUser.name;
    deleteModal.classList.add('active');
}

// Close user modal
function closeUserModal() {
    userModal.classList.remove('active');
    selectedUser = null;
}

// Close delete modal
function closeDeleteModal() {
    deleteModal.classList.remove('active');
    userToDelete = null;
}

// Confirm delete user
async function confirmDeleteUser() {
    if (!userToDelete) return;

    try {
        const response = await authFetch(`${API_BASE}/${userToDelete.id}`, {
            method: 'DELETE'
        });

        if (!response.ok && response.status !== 204) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to delete user');
        }

        closeDeleteModal();
        closeUserModal();
        await loadUsers();
        showToast('User deleted successfully', 'success');
    } catch (error) {
        console.error('Error deleting user:', error);
        showToast(error.message, 'error');
    }
}

// Helper: Escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Simple toast notification
function showToast(message, type = 'info') {
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.style.cssText = `
        position: fixed;
        bottom: 20px;
        right: 20px;
        padding: 1rem 1.5rem;
        border-radius: 8px;
        background: ${type === 'success' ? '#27ae60' : type === 'error' ? '#e74c3c' : '#3498db'};
        color: white;
        font-weight: 500;
        z-index: 9999;
        animation: slideIn 0.3s ease;
    `;
    toast.textContent = message;

    // Add animation keyframes if not exists
    if (!document.getElementById('toast-styles')) {
        const style = document.createElement('style');
        style.id = 'toast-styles';
        style.textContent = `
            @keyframes slideIn {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
        `;
        document.head.appendChild(style);
    }

    document.body.appendChild(toast);

    // Remove after 3 seconds
    setTimeout(() => {
        toast.style.animation = 'slideIn 0.3s ease reverse';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// Expose functions to global scope
window.approveUser = approveUser;
window.rejectUser = rejectUser;
window.showUserDetails = showUserDetails;
window.saveUserChanges = saveUserChanges;
window.showDeleteModal = showDeleteModal;
window.closeUserModal = closeUserModal;
window.closeDeleteModal = closeDeleteModal;
window.confirmDeleteUser = confirmDeleteUser;
