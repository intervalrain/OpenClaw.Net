// Admin Page - User Management & Model Provider Management

const API_BASE = '/api/v1/user-management';
const PROVIDER_API = '/api/v1/model-provider';
const APP_CONFIG_API = '/api/v1/app-config';
const AUDIT_API = '/api/v1/audit-log';

// State
let allUsers = [];
let pendingUsers = [];
let selectedUser = null;
let userToDelete = null;
let globalProviders = [];
let editingProvider = null;
let appConfigItems = [];

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
    initProviderUI();
    initAppConfigUI();
    initEmailSettingsUI();
    initEventDelegation();
    await Promise.all([loadUsers(), loadGlobalProviders(), loadAppConfigs()]);
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

// Event delegation for dynamically rendered buttons
function initEventDelegation() {
    // User list actions (approve, reject, manage)
    document.querySelector('.main-content').addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;
        const action = btn.dataset.action;
        const id = btn.dataset.id;
        if (action === 'approve') approveUser(id);
        else if (action === 'reject') rejectUser(id);
        else if (action === 'manage') showUserDetails(id);
        else if (action === 'ban-prompt') banUserPrompt(id);
        else if (action === 'unban') unbanUser(id);
    });

    // Provider list actions
    document.getElementById('providerList').addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;
        const action = btn.dataset.action;
        const id = btn.dataset.id;
        if (action === 'toggle-active') toggleProviderActive(id, btn.dataset.active === 'true');
        else if (action === 'edit-provider') openEditProviderModal(id);
        else if (action === 'delete-provider') deleteProvider(id);
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
        updateDashboardSummary();
    } catch (error) {
        console.error('Error loading users:', error);
        pendingUserList.innerHTML = '<div class="empty-state"><p>Failed to load users</p></div>';
        allUserList.innerHTML = '<div class="empty-state"><p>Failed to load users</p></div>';
    }
}

function updateDashboardSummary() {
    const total = allUsers.length;
    const active = allUsers.filter(u => u.status === 'Active').length;
    const pending = pendingUsers.length;
    const banned = allUsers.filter(u => u.status === 'Banned').length;
    const providers = globalProviders.length;

    const setVal = (id, val) => {
        const el = document.querySelector(`#${id} .summary-value`);
        if (el) el.textContent = val;
    };
    setVal('summaryTotal', total);
    setVal('summaryActive', active);
    setVal('summaryPending', pending);
    setVal('summaryBanned', banned);
    setVal('summaryProviders', providers);

    // Hide highlight if no pending
    const pendingCard = document.getElementById('summaryPending');
    if (pendingCard) pendingCard.classList.toggle('highlight', pending > 0);
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

// Role priority for sorting: SuperAdmin > Admin > User
function getRolePriority(user) {
    const roles = user.roles || [];
    if (roles.includes('SuperAdmin')) return 0;
    if (roles.includes('Admin')) return 1;
    return 2;
}

// Render all users with filter, sorted by role hierarchy
function renderAllUsers() {
    const filterStatus = statusFilter.value;
    let filteredUsers = filterStatus
        ? allUsers.filter(u => u.status === filterStatus)
        : allUsers;

    // Sort: SuperAdmin first, then Admin, then User
    filteredUsers = [...filteredUsers].sort((a, b) => getRolePriority(a) - getRolePriority(b));

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

    const isAdminOrAbove = roles.some(r => r === 'SuperAdmin' || r === 'Admin');
    const isBanned = user.status === 'Banned';

    let actions = '';
    if (isPendingView) {
        actions = `
            <button class="btn btn-success btn-sm" data-action="approve" data-id="${user.id}">
                Approve
            </button>
            <button class="btn btn-danger btn-sm" data-action="reject" data-id="${user.id}">
                Reject
            </button>
        `;
    } else if (isBanned) {
        actions = `
            <button class="btn btn-success btn-sm" data-action="unban" data-id="${user.id}">Unban</button>
            <button class="btn btn-outline btn-sm" data-action="manage" data-id="${user.id}">Details</button>
        `;
    } else if (!isAdminOrAbove) {
        actions = `
            <button class="btn btn-outline btn-sm" data-action="manage" data-id="${user.id}">Manage</button>
            <button class="btn btn-danger btn-sm" data-action="ban-prompt" data-id="${user.id}">Ban</button>
        `;
    } else {
        actions = `
            <button class="btn btn-outline btn-sm" data-action="manage" data-id="${user.id}">Details</button>
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
                    ${isBanned && user.banReason ? `<div class="ban-reason">Reason: ${escapeHtml(user.banReason)}</div>` : ''}
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

// Ban user with reason
async function banUserPrompt(userId) {
    const reason = prompt('Enter ban reason:');
    if (!reason || !reason.trim()) return;

    try {
        const response = await authFetch(`${API_BASE}/${userId}/ban`, {
            method: 'POST',
            body: JSON.stringify({ reason: reason.trim() })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.detail || error.title || 'Failed to ban user');
        }

        await loadUsers();
        showToast('User banned', 'success');
    } catch (error) {
        console.error('Error banning user:', error);
        showToast(error.message, 'error');
    }
}

// Unban user
async function unbanUser(userId) {
    if (!confirm('Unban this user? They will be restored to Active status.')) return;

    try {
        const response = await authFetch(`${API_BASE}/${userId}/unban`, {
            method: 'POST'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.detail || error.title || 'Failed to unban user');
        }

        await loadUsers();
        showToast('User unbanned', 'success');
    } catch (error) {
        console.error('Error unbanning user:', error);
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

    const userRoles = selectedUser.roles || [];
    const isSuperAdminTarget = userRoles.includes('SuperAdmin');
    const isAdminTarget = userRoles.includes('Admin');

    document.getElementById('modalBody').innerHTML = `
        <div class="user-detail">
            <label>Email</label>
            <div class="value">${escapeHtml(selectedUser.email)}</div>
        </div>
        <div class="user-detail">
            <label>Status</label>
            <div class="value">
                <select id="userStatusSelect" ${isSelf || isSuperAdminTarget ? 'disabled' : ''}>
                    <option value="Active" ${selectedUser.status === 'Active' ? 'selected' : ''}>Active</option>
                    <option value="Inactive" ${selectedUser.status === 'Inactive' ? 'selected' : ''}>Inactive</option>
                    <option value="Locked" ${selectedUser.status === 'Locked' ? 'selected' : ''}>Locked</option>
                </select>
            </div>
        </div>
        <div class="user-detail">
            <label>Roles</label>
            ${isSuperAdminTarget
                ? '<div class="text-muted">SuperAdmin (assigned at system setup)</div>'
                : `<div class="role-select">
                    <label class="role-checkbox ${isAdminTarget ? 'checked' : ''}">
                        <input type="checkbox" value="Admin" ${isAdminTarget ? 'checked' : ''}>
                        Admin
                    </label>
                </div>`}
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
        ${!isSuperAdminTarget ? `
            <div class="user-detail">
                <label>Workspace Quota (MB)</label>
                <div class="value">
                    <input type="number" id="userQuotaInput" min="0" max="10240" placeholder="Default (100)"
                        value="${selectedUser.workspaceQuotaMb || ''}" style="width: 140px; padding: 0.4rem 0.6rem; border: 1px solid var(--border-color); border-radius: 6px; background: var(--card-bg); color: var(--text-color);">
                    <span class="text-muted" style="margin-left: 0.5rem; font-size: 0.8rem;">Empty = system default</span>
                </div>
            </div>
        ` : ''}
    `;

    // Setup role checkbox styling
    document.querySelectorAll('.role-checkbox input').forEach(checkbox => {
        checkbox.addEventListener('change', (e) => {
            e.target.parentElement.classList.toggle('checked', e.target.checked);
        });
    });

    const canModify = !isSelf && !isSuperAdminTarget;
    const footer = document.getElementById('modalFooter');
    footer.innerHTML = `
        ${canModify ? `<button class="btn btn-danger" id="modalDeleteBtn">Delete</button>` : ''}
        <button class="btn btn-secondary" id="modalCancelBtn">Cancel</button>
        ${canModify ? `<button class="btn btn-primary" id="modalSaveBtn">Save Changes</button>` : ''}
    `;
    if (canModify) {
        document.getElementById('modalDeleteBtn').addEventListener('click', showDeleteModal);
        document.getElementById('modalSaveBtn').addEventListener('click', saveUserChanges);
    }
    document.getElementById('modalCancelBtn').addEventListener('click', closeUserModal);

    userModal.classList.add('active');
}

// Save user changes
async function saveUserChanges() {
    if (!selectedUser) return;

    const newStatus = document.getElementById('userStatusSelect').value;
    // Build roles: always "User" as base, optionally "Admin" if checked
    const isAdminChecked = document.querySelector('.role-checkbox input[value="Admin"]')?.checked ?? false;
    const newRoles = isAdminChecked ? ['User', 'Admin'] : ['User'];

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

        // Update workspace quota if changed
        const quotaInput = document.getElementById('userQuotaInput');
        if (quotaInput) {
            const newQuota = quotaInput.value ? parseInt(quotaInput.value) : null;
            const oldQuota = selectedUser.workspaceQuotaMb || null;
            if (newQuota !== oldQuota) {
                const quotaResponse = await authFetch(`${API_BASE}/${selectedUser.id}/quota`, {
                    method: 'PUT',
                    body: JSON.stringify({ quotaMb: newQuota })
                });
                if (!quotaResponse.ok) {
                    const error = await quotaResponse.json();
                    throw new Error(error.title || 'Failed to update quota');
                }
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

// ============================================================
// Model Provider Management
// ============================================================

const providerModal = document.getElementById('providerModal');

function initProviderUI() {
    document.getElementById('addProviderBtn').addEventListener('click', openAddProviderModal);
    document.getElementById('closeProviderModal').addEventListener('click', closeProviderModal);
    document.getElementById('cancelProviderModal').addEventListener('click', closeProviderModal);
    document.getElementById('saveProviderBtn').addEventListener('click', saveProvider);
    providerModal.addEventListener('click', e => { if (e.target === providerModal) closeProviderModal(); });

    // Toggle API key / URL fields based on provider type
    document.getElementById('adminProviderType').addEventListener('change', updateProviderTypeFields);
}

function updateProviderTypeFields() {
    const type = document.getElementById('adminProviderType').value;
    const urlGroup = document.getElementById('adminUrlGroup');
    const apiKeyGroup = document.getElementById('adminApiKeyGroup');

    if (type === 'openai' || type === 'anthropic') {
        urlGroup.style.display = 'none';
        apiKeyGroup.style.display = '';
    } else if (type === 'ollama') {
        urlGroup.style.display = '';
        apiKeyGroup.style.display = 'none';
    } else {
        urlGroup.style.display = '';
        apiKeyGroup.style.display = '';
    }
}

async function loadGlobalProviders() {
    try {
        const res = await authFetch(PROVIDER_API);
        if (res.ok) {
            globalProviders = await res.json();
        }
    } catch (e) {
        console.error('Failed to load providers:', e);
        globalProviders = [];
    }
    renderProviderList();
    updateDashboardSummary();
}

function renderProviderList() {
    const listEl = document.getElementById('providerList');

    if (globalProviders.length === 0) {
        listEl.innerHTML = `
            <div class="empty-state">
                <h3>No Global Providers</h3>
                <p>Add a model provider to make it available to all users</p>
            </div>`;
        return;
    }

    listEl.innerHTML = globalProviders.map(p => {
        const statusClass = p.isActive ? 'active' : 'inactive';
        const statusLabel = p.isActive ? 'Active' : 'Inactive';
        return `
        <div class="user-card" data-provider-id="${p.id}">
            <div class="user-card-info">
                <div class="user-avatar provider-avatar">${p.type.charAt(0).toUpperCase()}</div>
                <div class="user-details">
                    <h3>${escapeHtml(p.name)}</h3>
                    <p>${escapeHtml(p.type)} &mdash; ${escapeHtml(p.modelName)}</p>
                    ${p.description ? `<p class="text-muted">${escapeHtml(p.description)}</p>` : ''}
                    <div class="user-meta">
                        <span class="status-badge ${statusClass}">${statusLabel}</span>
                        ${p.allowUserOverride ? '<span class="role-badge">User Override</span>' : ''}
                    </div>
                </div>
            </div>
            <div class="user-card-actions">
                <button class="btn btn-outline btn-sm" data-action="toggle-active" data-id="${p.id}" data-active="${p.isActive}">
                    ${p.isActive ? 'Deactivate' : 'Activate'}
                </button>
                <button class="btn btn-outline btn-sm" data-action="edit-provider" data-id="${p.id}">Edit</button>
                <button class="btn btn-danger btn-sm" data-action="delete-provider" data-id="${p.id}">Delete</button>
            </div>
        </div>`;
    }).join('');
}

function openAddProviderModal() {
    editingProvider = null;
    document.getElementById('providerModalTitle').textContent = 'Add Global Model Provider';
    document.getElementById('editProviderId').value = '';
    document.getElementById('adminProviderType').value = 'ollama';
    document.getElementById('adminProviderName').value = '';
    document.getElementById('adminProviderUrl').value = '';
    document.getElementById('adminProviderApiKey').value = '';
    document.getElementById('adminProviderModel').value = '';
    document.getElementById('adminProviderDesc').value = '';
    document.getElementById('adminAllowOverride').checked = true;
    updateProviderTypeFields();
    providerModal.classList.add('active');
}

function openEditProviderModal(id) {
    const p = globalProviders.find(x => x.id === id);
    if (!p) return;
    editingProvider = p;
    document.getElementById('providerModalTitle').textContent = 'Edit Model Provider';
    document.getElementById('editProviderId').value = p.id;
    document.getElementById('adminProviderType').value = p.type.toLowerCase();
    document.getElementById('adminProviderName').value = p.name;
    document.getElementById('adminProviderUrl').value = p.url || '';
    document.getElementById('adminProviderApiKey').value = '';
    document.getElementById('adminProviderModel').value = p.modelName;
    document.getElementById('adminProviderDesc').value = p.description || '';
    document.getElementById('adminAllowOverride').checked = p.allowUserOverride;
    updateProviderTypeFields();
    providerModal.classList.add('active');
}

function closeProviderModal() {
    providerModal.classList.remove('active');
    editingProvider = null;
}

async function saveProvider() {
    const id = document.getElementById('editProviderId').value;
    const isEdit = !!id;

    const type = document.getElementById('adminProviderType').value;
    let url = document.getElementById('adminProviderUrl').value.trim();
    if (type === 'openai') url = 'https://api.openai.com';
    else if (type === 'anthropic') url = 'https://api.anthropic.com';

    const body = {
        type,
        name: document.getElementById('adminProviderName').value.trim(),
        url,
        modelName: document.getElementById('adminProviderModel').value.trim(),
        apiKey: document.getElementById('adminProviderApiKey').value.trim() || null,
        description: document.getElementById('adminProviderDesc').value.trim() || null,
        allowUserOverride: document.getElementById('adminAllowOverride').checked,
    };

    if (!body.name || !body.modelName) {
        showToast('Name and Model are required', 'error');
        return;
    }

    try {
        let res;
        if (isEdit) {
            res = await authFetch(`${PROVIDER_API}/${id}`, { method: 'PUT', body: JSON.stringify(body) });
        } else {
            body.isActive = true;
            res = await authFetch(PROVIDER_API, { method: 'POST', body: JSON.stringify(body) });
        }
        if (!res.ok) throw new Error('Failed to save');
        closeProviderModal();
        await loadGlobalProviders();
        showToast(isEdit ? 'Provider updated' : 'Provider created', 'success');
    } catch (e) {
        showToast(e.message, 'error');
    }
}

async function toggleProviderActive(id, currentlyActive) {
    try {
        const action = currentlyActive ? 'deactivate' : 'activate';
        const res = await authFetch(`${PROVIDER_API}/${id}/${action}`, { method: 'POST' });
        if (!res.ok) throw new Error(`Failed to ${action}`);
        await loadGlobalProviders();
        showToast(`Provider ${action}d`, 'success');
    } catch (e) {
        showToast(e.message, 'error');
    }
}

async function deleteProvider(id) {
    if (!confirm('Delete this global model provider? Users referencing it will lose access.')) return;
    try {
        const res = await authFetch(`${PROVIDER_API}/${id}`, { method: 'DELETE' });
        if (!res.ok && res.status !== 204) throw new Error('Failed to delete');
        await loadGlobalProviders();
        showToast('Provider deleted', 'success');
    } catch (e) {
        showToast(e.message, 'error');
    }
}

// ============================================================
// App Config Management
// ============================================================

function initAppConfigUI() {
    document.getElementById('admin-add-config-btn').addEventListener('click', addAppConfig);

    // Event delegation for config list
    document.getElementById('appConfigList').addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;
        const key = btn.dataset.key;
        if (btn.dataset.action === 'edit-config') {
            const newValue = prompt(`Set new value for "${key}":`);
            if (newValue !== null && newValue !== '') {
                saveAppConfig(key, newValue, true);
            }
        } else if (btn.dataset.action === 'delete-config') {
            if (confirm(`Delete config "${key}"?`)) deleteAppConfig(key);
        }
    });
}

async function loadAppConfigs() {
    try {
        const res = await authFetch(APP_CONFIG_API);
        if (res.ok) appConfigItems = await res.json();
    } catch (e) {
        console.error('Failed to load app configs:', e);
        appConfigItems = [];
    }
    renderAppConfigList();
}

function renderAppConfigList() {
    const listEl = document.getElementById('appConfigList');
    if (appConfigItems.length === 0) {
        listEl.innerHTML = '<div class="empty-state"><h3>No Configuration</h3><p>Add global configuration keys above</p></div>';
        return;
    }
    listEl.innerHTML = appConfigItems.map(c => `
        <div class="user-card" data-key="${escapeHtml(c.key)}">
            <div class="user-card-info">
                <div class="user-avatar config-avatar">${c.isSecret ? '🔒' : '⚙'}</div>
                <div class="user-details">
                    <h3>${escapeHtml(c.key)}</h3>
                    <p>${c.isSecret ? 'Encrypted' : (c.value || '(empty)')}</p>
                </div>
            </div>
            <div class="user-card-actions">
                <button class="btn btn-outline btn-sm" data-action="edit-config" data-key="${escapeHtml(c.key)}">Edit</button>
                <button class="btn btn-danger btn-sm" data-action="delete-config" data-key="${escapeHtml(c.key)}">Delete</button>
            </div>
        </div>
    `).join('');
}

async function addAppConfig() {
    const keyInput = document.getElementById('admin-config-key');
    const valueInput = document.getElementById('admin-config-value');
    const key = keyInput.value.trim().toUpperCase();
    const value = valueInput.value;
    if (!key || !value) { showToast('Key and Value are required', 'error'); return; }
    await saveAppConfig(key, value, true);
    keyInput.value = '';
    valueInput.value = '';
}

async function saveAppConfig(key, value, isSecret) {
    try {
        const res = await authFetch(`${APP_CONFIG_API}/${encodeURIComponent(key)}`, {
            method: 'PUT',
            body: JSON.stringify({ value, isSecret })
        });
        if (!res.ok) throw new Error('Failed to save');
        await loadAppConfigs();
        showToast('Config saved', 'success');
    } catch (e) {
        showToast(e.message, 'error');
    }
}

async function deleteAppConfig(key) {
    try {
        const res = await authFetch(`${APP_CONFIG_API}/${encodeURIComponent(key)}`, { method: 'DELETE' });
        if (!res.ok && res.status !== 204) throw new Error('Failed to delete');
        await loadAppConfigs();
        showToast('Config deleted', 'success');
    } catch (e) {
        showToast(e.message, 'error');
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

// ===== Audit Log Tab =====
let auditLogs = [];
let auditOffset = 0;
const auditLimit = 50;

async function loadAuditLogs() {
    const action = document.getElementById('auditActionFilter')?.value || '';
    const fromDate = document.getElementById('auditDateFrom')?.value || '';
    const toDate = document.getElementById('auditDateTo')?.value || '';

    const params = new URLSearchParams({ limit: auditLimit, offset: auditOffset });
    if (action) params.set('action', action);

    // Default: last 1 hour if no date filter set
    if (fromDate) {
        params.set('from', new Date(fromDate).toISOString());
    } else {
        params.set('from', new Date(Date.now() - 60 * 60 * 1000).toISOString());
    }
    if (toDate) {
        params.set('to', new Date(toDate).toISOString());
    }

    try {
        const res = await authFetch(`${AUDIT_API}?${params}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        auditLogs = await res.json();
        renderAuditLogs();
    } catch (e) {
        document.getElementById('auditLogBody').innerHTML =
            `<tr><td colspan="7" style="text-align:center; color: var(--text-secondary);">Failed to load audit logs</td></tr>`;
    }
}

/** Format timestamp as absolute time: YYYY-MM-DD HH:mm:ss */
function formatAuditTime(timestamp) {
    const d = new Date(timestamp);
    const Y = d.getFullYear();
    const M = String(d.getMonth() + 1).padStart(2, '0');
    const D = String(d.getDate()).padStart(2, '0');
    const h = String(d.getHours()).padStart(2, '0');
    const m = String(d.getMinutes()).padStart(2, '0');
    const s = String(d.getSeconds()).padStart(2, '0');
    return `${Y}-${M}-${D} ${h}:${m}:${s}`;
}

function renderAuditLogs() {
    const body = document.getElementById('auditLogBody');
    const countEl = document.getElementById('auditResultCount');
    const pageInfo = document.getElementById('auditPageInfo');

    if (!auditLogs.length) {
        body.innerHTML = '<tr><td colspan="7" style="text-align:center; color: var(--text-secondary);">No audit logs found</td></tr>';
        countEl.textContent = '';
        pageInfo.textContent = '';
        document.getElementById('auditPrevBtn').disabled = true;
        document.getElementById('auditNextBtn').disabled = true;
        return;
    }

    body.innerHTML = auditLogs.map(log => {
        const relTime = formatAuditTime(log.timestamp);
        const fullTime = new Date(log.timestamp).toLocaleString();
        const statusClass = log.statusCode >= 400 ? 'audit-status-error' : 'audit-status-ok';
        return `<tr>
            <td class="audit-time" title="${fullTime}">${relTime}</td>
            <td><span class="audit-action-badge">${log.action}</span></td>
            <td>${log.userEmail || log.userId || 'anonymous'}</td>
            <td><span class="audit-method audit-method-${log.httpMethod.toLowerCase()}">${log.httpMethod}</span></td>
            <td class="audit-path" title="${log.path}">${log.path.length > 40 ? log.path.substring(0, 40) + '...' : log.path}</td>
            <td><span class="${statusClass}">${log.statusCode}</span></td>
            <td class="audit-ip">${log.ipAddress || '-'}</td>
        </tr>`;
    }).join('');

    const page = Math.floor(auditOffset / auditLimit) + 1;
    pageInfo.textContent = `Page ${page}`;
    countEl.textContent = auditLogs.length === auditLimit ? `${auditLimit}+ results` : `${auditLogs.length} results`;
    document.getElementById('auditPrevBtn').disabled = auditOffset === 0;
    document.getElementById('auditNextBtn').disabled = auditLogs.length < auditLimit;
}

// Audit log event listeners
document.getElementById('auditSearchBtn')?.addEventListener('click', () => {
    auditOffset = 0;
    loadAuditLogs();
});

document.getElementById('auditClearBtn')?.addEventListener('click', () => {
    document.getElementById('auditActionFilter').value = '';
    document.getElementById('auditDateFrom').value = '';
    document.getElementById('auditDateTo').value = '';
    auditOffset = 0;
    loadAuditLogs();
});

document.getElementById('auditPrevBtn')?.addEventListener('click', () => {
    auditOffset = Math.max(0, auditOffset - auditLimit);
    loadAuditLogs();
});

document.getElementById('auditNextBtn')?.addEventListener('click', () => {
    auditOffset += auditLimit;
    loadAuditLogs();
});

// Load audit logs when tab is activated
document.querySelectorAll('.tab').forEach(tab => {
    tab.addEventListener('click', () => {
        if (tab.dataset.tab === 'auditlog' && auditLogs.length === 0) {
            loadAuditLogs();
        }
        if (tab.dataset.tab === 'email') {
            loadEmailSettings();
        }
    });
});

// ── Email Settings ──

const EMAIL_CONFIG_KEYS = {
    enabled: 'Email:Enabled',
    fromEmail: 'Email:FromEmail',
    fromName: 'Email:FromName',
    smtpServer: 'Email:SmtpServer',
    smtpPort: 'Email:SmtpPort',
    smtpUsername: 'Email:SmtpUsername',
    smtpPassword: 'Email:SmtpPassword',
    smtpUseSsl: 'Email:SmtpUseSsl',
};

function initEmailSettingsUI() {
    document.getElementById('saveEmailSettings').addEventListener('click', saveEmailSettings);
    document.getElementById('testEmailBtn').addEventListener('click', sendTestEmail);
}

async function loadEmailSettings() {
    try {
        const res = await authFetch(APP_CONFIG_API);
        if (!res.ok) return;
        const configs = await res.json();
        const configMap = {};
        configs.forEach(c => { configMap[c.key] = c.value; });

        document.getElementById('emailEnabled').checked = configMap[EMAIL_CONFIG_KEYS.enabled] === 'true';
        document.getElementById('emailFromEmail').value = configMap[EMAIL_CONFIG_KEYS.fromEmail] || '';
        document.getElementById('emailFromName').value = configMap[EMAIL_CONFIG_KEYS.fromName] || '';
        document.getElementById('emailSmtpServer').value = configMap[EMAIL_CONFIG_KEYS.smtpServer] || '';
        document.getElementById('emailSmtpPort').value = configMap[EMAIL_CONFIG_KEYS.smtpPort] || '587';
        document.getElementById('emailSmtpUsername').value = configMap[EMAIL_CONFIG_KEYS.smtpUsername] || '';
        // Don't load password into field for security — show placeholder
        document.getElementById('emailSmtpPassword').value = '';
        document.getElementById('emailSmtpPassword').placeholder = configMap[EMAIL_CONFIG_KEYS.smtpPassword] ? '••••••••  (saved)' : 'App password or SMTP password';
        document.getElementById('emailSmtpUseSsl').checked = configMap[EMAIL_CONFIG_KEYS.smtpUseSsl] !== 'false';
    } catch (e) {
        console.error('Failed to load email settings', e);
    }
}

async function saveEmailSettings() {
    const btn = document.getElementById('saveEmailSettings');
    btn.disabled = true;
    btn.textContent = 'Saving...';

    try {
        const settings = [
            { key: EMAIL_CONFIG_KEYS.enabled, value: document.getElementById('emailEnabled').checked ? 'true' : 'false' },
            { key: EMAIL_CONFIG_KEYS.fromEmail, value: document.getElementById('emailFromEmail').value },
            { key: EMAIL_CONFIG_KEYS.fromName, value: document.getElementById('emailFromName').value || 'OpenClaw' },
            { key: EMAIL_CONFIG_KEYS.smtpServer, value: document.getElementById('emailSmtpServer').value },
            { key: EMAIL_CONFIG_KEYS.smtpPort, value: document.getElementById('emailSmtpPort').value || '587' },
            { key: EMAIL_CONFIG_KEYS.smtpUsername, value: document.getElementById('emailSmtpUsername').value },
            { key: EMAIL_CONFIG_KEYS.smtpUseSsl, value: document.getElementById('emailSmtpUseSsl').checked ? 'true' : 'false' },
        ];

        // Only update password if user entered a new one
        const pwd = document.getElementById('emailSmtpPassword').value;
        if (pwd) {
            settings.push({ key: EMAIL_CONFIG_KEYS.smtpPassword, value: pwd, isSecret: true });
        }

        for (const s of settings) {
            await authFetch(`${APP_CONFIG_API}/${encodeURIComponent(s.key)}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ value: s.value, isSecret: s.isSecret || false })
            });
        }

        btn.textContent = 'Saved!';
        setTimeout(() => { btn.textContent = 'Save Settings'; btn.disabled = false; }, 2000);
    } catch (e) {
        btn.textContent = 'Save Settings';
        btn.disabled = false;
        alert('Failed to save email settings: ' + e.message);
    }
}

async function sendTestEmail() {
    const btn = document.getElementById('testEmailBtn');
    const result = document.getElementById('emailTestResult');
    btn.disabled = true;
    btn.textContent = 'Sending...';
    result.textContent = '';

    try {
        const user = getCurrentUser();
        const res = await authFetch('/api/v1/email/test', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ to: user.email })
        });

        if (res.ok) {
            result.className = 'email-test-result success';
            result.textContent = `Test email sent to ${user.email}`;
        } else {
            const err = await res.json().catch(() => ({}));
            result.className = 'email-test-result error';
            result.textContent = err.message || err.title || 'Failed to send test email';
        }
    } catch (e) {
        result.className = 'email-test-result error';
        result.textContent = 'Error: ' + e.message;
    } finally {
        btn.textContent = 'Send Test Email';
        btn.disabled = false;
    }
}
