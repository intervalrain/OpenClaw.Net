// Workspace File Explorer

const FILE_API = '/api/v1/workspace-file';

let currentPath = '/';
let currentEntries = [];
let pendingAction = null; // { type: 'rename'|'delete', name, isDirectory }

// Icons
const FOLDER_ICON = `<svg class="file-icon folder-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
    <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
</svg>`;

const FILE_ICON = `<svg class="file-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
    <polyline points="14 2 14 8 20 8"/>
</svg>`;

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadDirectory('/');
    loadQuota();
    setupEventListeners();
    setupDragAndDrop();
});

async function loadQuota() {
    try {
        const res = await authFetch(`${FILE_API}/usage`);
        if (!res.ok) return;
        const usage = await res.json();

        const bar = document.getElementById('quotaBar');
        const text = document.getElementById('quotaText');
        const fill = document.getElementById('quotaFill');

        bar.style.display = '';
        text.textContent = `${usage.usedMb} MB / ${usage.quotaMb} MB`;
        fill.style.width = `${Math.min(usage.usagePercent, 100)}%`;

        fill.classList.remove('warning', 'danger');
        if (usage.usagePercent >= 90) fill.classList.add('danger');
        else if (usage.usagePercent >= 70) fill.classList.add('warning');
    } catch { /* ignore */ }
}

async function loadDirectory(path) {
    currentPath = path || '/';
    const fileList = document.getElementById('fileList');
    fileList.innerHTML = '<div class="loading">Loading...</div>';

    try {
        const params = currentPath === '/' ? '' : `?path=${encodeURIComponent(currentPath)}`;
        const res = await authFetch(`${FILE_API}/list${params}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const data = await res.json();
        currentEntries = data.entries;
        renderBreadcrumb();
        renderFileList();
    } catch (e) {
        fileList.innerHTML = '<div class="empty-state"><p>Failed to load directory</p></div>';
    }
}

function renderBreadcrumb() {
    const nav = document.getElementById('breadcrumb');
    const parts = currentPath === '/' ? [''] : currentPath.split('/').filter(Boolean);

    let html = `<span class="breadcrumb-item" data-path="/">Home</span>`;
    let accumulated = '';

    for (const part of parts) {
        accumulated += '/' + part;
        html += `<span class="breadcrumb-separator">/</span>`;
        html += `<span class="breadcrumb-item" data-path="${accumulated}">${part}</span>`;
    }

    nav.innerHTML = html;

    nav.querySelectorAll('.breadcrumb-item').forEach(item => {
        item.addEventListener('click', () => loadDirectory(item.dataset.path));
    });
}

function renderFileList() {
    const fileList = document.getElementById('fileList');

    if (currentEntries.length === 0) {
        fileList.innerHTML = `
            <div class="empty-state">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
                </svg>
                <p>This folder is empty. Upload files or create a folder.</p>
            </div>`;
        return;
    }

    fileList.innerHTML = currentEntries.map(entry => {
        const icon = entry.isDirectory ? FOLDER_ICON : FILE_ICON;
        const nameClass = entry.isDirectory ? 'file-name dir-name' : 'file-name';
        const rowClass = entry.isDirectory ? 'file-row directory' : 'file-row';
        const size = entry.isDirectory ? '' : formatSize(entry.size);
        const modified = formatDate(entry.modifiedAt);

        return `<div class="${rowClass}" data-name="${entry.name}" data-is-dir="${entry.isDirectory}">
            ${icon}
            <span class="${nameClass}">${entry.name}</span>
            <span class="file-size">${size}</span>
            <span class="file-modified">${modified}</span>
            <div class="file-actions">
                ${!entry.isDirectory ? `<button class="file-action-btn download-btn" title="Download">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>
                        <polyline points="7 10 12 15 17 10"/>
                        <line x1="12" y1="15" x2="12" y2="3"/>
                    </svg>
                </button>` : ''}
                <button class="file-action-btn rename-btn" title="Rename">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                        <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                </button>
                <button class="file-action-btn danger delete-btn" title="Delete">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"/>
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                    </svg>
                </button>
            </div>
        </div>`;
    }).join('');

    // Directory click → navigate
    fileList.querySelectorAll('.file-row.directory').forEach(row => {
        row.addEventListener('click', (e) => {
            if (e.target.closest('.file-actions')) return;
            const name = row.dataset.name;
            const newPath = currentPath === '/' ? `/${name}` : `${currentPath}/${name}`;
            loadDirectory(newPath);
        });
    });

    // Download
    fileList.querySelectorAll('.download-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const name = btn.closest('.file-row').dataset.name;
            const filePath = currentPath === '/' ? name : `${currentPath}/${name}`;
            window.open(`${FILE_API}/download?path=${encodeURIComponent(filePath)}`, '_blank');
        });
    });

    // Rename
    fileList.querySelectorAll('.rename-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const row = btn.closest('.file-row');
            openRenameModal(row.dataset.name, row.dataset.isDir === 'true');
        });
    });

    // Delete
    fileList.querySelectorAll('.delete-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const row = btn.closest('.file-row');
            openDeleteModal(row.dataset.name, row.dataset.isDir === 'true');
        });
    });
}

function setupEventListeners() {
    // New Folder
    document.getElementById('newFolderBtn').addEventListener('click', async () => {
        const name = prompt('Folder name:');
        if (!name) return;
        const path = currentPath === '/' ? name : `${currentPath}/${name}`;
        try {
            const res = await authFetch(`${FILE_API}/mkdir`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ path })
            });
            if (res.ok) loadDirectory(currentPath);
            else alert('Failed to create folder');
        } catch { alert('Failed to create folder'); }
    });

    // Upload
    document.getElementById('fileInput').addEventListener('change', async (e) => {
        const files = e.target.files;
        if (!files.length) return;

        for (const file of files) {
            await uploadFile(file);
        }
        e.target.value = '';
        loadDirectory(currentPath);
        loadQuota();
    });

    // Rename modal
    document.getElementById('closeRenameModal').addEventListener('click', closeRenameModal);
    document.getElementById('cancelRename').addEventListener('click', closeRenameModal);
    document.getElementById('confirmRename').addEventListener('click', doRename);
    document.getElementById('renameInput').addEventListener('keydown', (e) => {
        if (e.key === 'Enter') doRename();
    });

    // Delete modal
    document.getElementById('closeDeleteModal').addEventListener('click', closeDeleteModal);
    document.getElementById('cancelDelete').addEventListener('click', closeDeleteModal);
    document.getElementById('confirmDelete').addEventListener('click', doDelete);
}

async function uploadFile(file) {
    const formData = new FormData();
    formData.append('file', file);
    const params = currentPath === '/' ? '' : `?path=${encodeURIComponent(currentPath)}`;

    try {
        const token = localStorage.getItem('token');
        const res = await fetch(`${FILE_API}/upload${params}`, {
            method: 'POST',
            headers: token ? { 'Authorization': `Bearer ${token}` } : {},
            body: formData
        });
        if (!res.ok) {
            const err = await res.text();
            alert(`Upload failed: ${err}`);
        }
    } catch (e) {
        alert(`Upload failed: ${e.message}`);
    }
}

// Drag and drop
function setupDragAndDrop() {
    const overlay = document.createElement('div');
    overlay.className = 'drop-overlay';
    overlay.textContent = 'Drop files to upload';
    document.body.appendChild(overlay);

    let dragCounter = 0;

    document.addEventListener('dragenter', (e) => {
        e.preventDefault();
        dragCounter++;
        overlay.classList.add('active');
    });

    document.addEventListener('dragleave', (e) => {
        e.preventDefault();
        dragCounter--;
        if (dragCounter === 0) overlay.classList.remove('active');
    });

    document.addEventListener('dragover', (e) => e.preventDefault());

    document.addEventListener('drop', async (e) => {
        e.preventDefault();
        dragCounter = 0;
        overlay.classList.remove('active');

        const files = e.dataTransfer.files;
        for (const file of files) {
            await uploadFile(file);
        }
        loadDirectory(currentPath);
        loadQuota();
    });
}

// Rename
function openRenameModal(name, isDirectory) {
    pendingAction = { type: 'rename', name, isDirectory };
    document.getElementById('renameInput').value = name;
    document.getElementById('renameModalTitle').textContent = isDirectory ? 'Rename Folder' : 'Rename File';
    document.getElementById('renameModal').classList.add('active');
    document.getElementById('renameInput').select();
}

function closeRenameModal() {
    document.getElementById('renameModal').classList.remove('active');
    pendingAction = null;
}

async function doRename() {
    if (!pendingAction) return;
    const newName = document.getElementById('renameInput').value.trim();
    if (!newName || newName === pendingAction.name) {
        closeRenameModal();
        return;
    }

    const oldPath = currentPath === '/' ? pendingAction.name : `${currentPath}/${pendingAction.name}`;
    const newPath = currentPath === '/' ? newName : `${currentPath}/${newName}`;

    try {
        const res = await authFetch(`${FILE_API}/rename`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ oldPath, newPath })
        });
        if (res.ok) {
            closeRenameModal();
            loadDirectory(currentPath);
        } else {
            const err = await res.text();
            alert(`Rename failed: ${err}`);
        }
    } catch { alert('Rename failed'); }
}

// Delete
function openDeleteModal(name, isDirectory) {
    pendingAction = { type: 'delete', name, isDirectory };
    document.getElementById('deleteFileName').textContent = name;
    document.getElementById('deleteModal').classList.add('active');
}

function closeDeleteModal() {
    document.getElementById('deleteModal').classList.remove('active');
    pendingAction = null;
}

async function doDelete() {
    if (!pendingAction) return;
    const path = currentPath === '/' ? pendingAction.name : `${currentPath}/${pendingAction.name}`;

    try {
        const res = await authFetch(`${FILE_API}?path=${encodeURIComponent(path)}`, {
            method: 'DELETE'
        });
        if (res.ok) {
            closeDeleteModal();
            loadDirectory(currentPath);
        } else {
            const err = await res.text();
            alert(`Delete failed: ${err}`);
        }
    } catch { alert('Delete failed'); }
}

// Helpers
function formatSize(bytes) {
    if (bytes == null) return '';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1073741824) return `${(bytes / 1048576).toFixed(1)} MB`;
    return `${(bytes / 1073741824).toFixed(1)} GB`;
}

function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    const M = String(d.getMonth() + 1).padStart(2, '0');
    const D = String(d.getDate()).padStart(2, '0');
    const h = String(d.getHours()).padStart(2, '0');
    const m = String(d.getMinutes()).padStart(2, '0');
    return `${d.getFullYear()}-${M}-${D} ${h}:${m}`;
}
