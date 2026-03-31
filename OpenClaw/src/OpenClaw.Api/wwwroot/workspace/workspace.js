// Workspace File Explorer — Enhanced

const FILE_API = '/api/v1/workspace-file';

let currentPath = '/';
let currentEntries = [];
let selectedFiles = new Set(); // multi-select
let pendingAction = null;
let viewingScope = 'my'; // 'my' or 'admin'
let clipboard = null; // { action: 'copy'|'cut', paths: [...] }

// Icons
const FOLDER_ICON = `<svg class="file-icon folder-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>`;
const FILE_ICON = `<svg class="file-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>`;
const IMAGE_ICON = `<svg class="file-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>`;

const IMAGE_EXTS = new Set(['png', 'jpg', 'jpeg', 'gif', 'bmp', 'svg', 'webp', 'ico']);
const TEXT_EXTS = new Set(['txt', 'md', 'json', 'xml', 'yaml', 'yml', 'csv', 'log', 'cs', 'js', 'ts', 'css', 'html', 'py', 'sh', 'dockerfile', 'toml', 'ini', 'cfg', 'env']);

function getFileExt(name) { return (name.split('.').pop() || '').toLowerCase(); }
function isImage(name) { return IMAGE_EXTS.has(getFileExt(name)); }
function isText(name) { return TEXT_EXTS.has(getFileExt(name)); }
function getFileIcon(entry) {
    if (entry.isDirectory) return FOLDER_ICON;
    if (isImage(entry.name)) return IMAGE_ICON;
    return FILE_ICON;
}

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    loadDirectory('/');
    loadQuota();
    setupEventListeners();
    setupDragAndDrop();
    setupKeyboard();
});

// ===== Quota =====
async function loadQuota() {
    try {
        const res = await authFetch(`${FILE_API}/usage`);
        if (!res.ok) return;
        const usage = await res.json();
        const bar = document.getElementById('quotaBar');
        const text = document.getElementById('quotaText');
        const fill = document.getElementById('quotaFill');

        if (usage.unlimited) {
            bar.style.display = '';
            text.textContent = `${usage.usedMb} MB (Unlimited)`;
            fill.style.width = '0%';
            fill.classList.remove('warning', 'danger');
            return;
        }

        bar.style.display = '';
        text.textContent = `${usage.usedMb} MB / ${usage.quotaMb} MB`;
        fill.style.width = `${Math.min(usage.usagePercent, 100)}%`;
        fill.classList.remove('warning', 'danger');
        if (usage.usagePercent >= 90) fill.classList.add('danger');
        else if (usage.usagePercent >= 70) fill.classList.add('warning');
    } catch { /* ignore */ }
}

// ===== Directory Loading =====
async function loadDirectory(path) {
    currentPath = path || '/';
    selectedFiles.clear();
    updateSelectionUI();
    const fileList = document.getElementById('fileList');
    fileList.innerHTML = '<div class="loading">Loading...</div>';

    try {
        const params = new URLSearchParams();
        if (currentPath !== '/') params.set('path', currentPath.replace(/^\//, ''));
        if (viewingScope !== 'my') params.set('scope', viewingScope);
        const res = await authFetch(`${FILE_API}/list?${params}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = await res.json();
        currentEntries = data.entries;
        renderBreadcrumb();
        renderFileList();
    } catch (e) {
        fileList.innerHTML = '<div class="empty-state"><p>Failed to load directory</p></div>';
    }
}

// ===== Breadcrumb =====
function renderBreadcrumb() {
    const nav = document.getElementById('breadcrumb');
    const prefix = viewingScope === 'admin' ? 'All Users' : '~';
    const parts = currentPath === '/' ? [] : currentPath.split('/').filter(Boolean);

    let html = `<span class="breadcrumb-item" data-path="/">${prefix}</span>`;
    let accumulated = '';
    for (const part of parts) {
        accumulated += '/' + part;
        html += `<span class="breadcrumb-separator">/</span><span class="breadcrumb-item" data-path="${accumulated}">${part}</span>`;
    }
    nav.innerHTML = html;
    nav.querySelectorAll('.breadcrumb-item').forEach(item => {
        item.addEventListener('click', () => loadDirectory(item.dataset.path));
    });
}

// ===== File List Rendering =====
function renderFileList() {
    const fileList = document.getElementById('fileList');

    if (currentEntries.length === 0) {
        fileList.innerHTML = `<div class="empty-state">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>
            <p>This folder is empty. Upload files or create a folder.</p>
        </div>`;
        return;
    }

    fileList.innerHTML = currentEntries.map(entry => {
        const icon = getFileIcon(entry);
        const nameClass = entry.isDirectory ? 'file-name dir-name' : 'file-name';
        const rowClass = entry.isDirectory ? 'file-row directory' : 'file-row';
        const size = entry.isDirectory ? '' : formatSize(entry.size);
        const modified = formatDate(entry.modifiedAt);

        return `<div class="${rowClass}" data-name="${entry.name}" data-is-dir="${entry.isDirectory}">
            <input type="checkbox" class="file-checkbox" data-name="${entry.name}">
            ${icon}
            <span class="${nameClass}">${entry.name}</span>
            <span class="file-size">${size}</span>
            <span class="file-modified">${modified}</span>
            <div class="file-actions">
                ${!entry.isDirectory ? `<button class="file-action-btn preview-btn" title="Preview"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg></button>` : ''}
                ${!entry.isDirectory ? `<button class="file-action-btn download-btn" title="Download"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg></button>` : ''}
                <button class="file-action-btn rename-btn" title="Rename"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg></button>
                <button class="file-action-btn danger delete-btn" title="Delete"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg></button>
            </div>
        </div>`;
    }).join('');

    // Event delegation for all file row actions
    fileList.addEventListener('click', handleFileListClick);
    fileList.addEventListener('contextmenu', handleContextMenu);
}

function handleFileListClick(e) {
    const row = e.target.closest('.file-row');
    if (!row) return;
    const name = row.dataset.name;
    const isDir = row.dataset.isDir === 'true';

    // Checkbox
    if (e.target.closest('.file-checkbox')) {
        toggleSelection(name);
        return;
    }

    // Action buttons
    if (e.target.closest('.preview-btn')) { e.stopPropagation(); openPreview(name); return; }
    if (e.target.closest('.download-btn')) { e.stopPropagation(); downloadFile(name); return; }
    if (e.target.closest('.rename-btn')) { e.stopPropagation(); openRenameModal(name, isDir); return; }
    if (e.target.closest('.delete-btn')) { e.stopPropagation(); openDeleteModal(name, isDir); return; }

    // Click on row
    if (e.target.closest('.file-actions')) return;

    if (isDir) {
        const newPath = currentPath === '/' ? `/${name}` : `${currentPath}/${name}`;
        loadDirectory(newPath);
    } else if (e.ctrlKey || e.metaKey) {
        toggleSelection(name);
    } else {
        // Single click on file → preview
        openPreview(name);
    }
}

// ===== Multi-Select =====
function toggleSelection(name) {
    if (selectedFiles.has(name)) selectedFiles.delete(name);
    else selectedFiles.add(name);
    updateSelectionUI();
}

function updateSelectionUI() {
    // Update checkboxes
    document.querySelectorAll('.file-checkbox').forEach(cb => {
        cb.checked = selectedFiles.has(cb.dataset.name);
    });
    // Update row highlight
    document.querySelectorAll('.file-row').forEach(row => {
        row.classList.toggle('selected', selectedFiles.has(row.dataset.name));
    });
    // Selection toolbar
    const toolbar = document.getElementById('selectionToolbar');
    if (toolbar) {
        toolbar.style.display = selectedFiles.size > 0 ? '' : 'none';
        const count = document.getElementById('selectionCount');
        if (count) count.textContent = `${selectedFiles.size} selected`;
    }
}

async function deleteSelected() {
    if (selectedFiles.size === 0) return;
    if (!confirm(`Delete ${selectedFiles.size} item(s)?`)) return;

    for (const name of selectedFiles) {
        const path = currentPath === '/' ? name : `${currentPath}/${name}`;
        await authFetch(`${FILE_API}?path=${encodeURIComponent(path)}`, { method: 'DELETE' });
    }
    selectedFiles.clear();
    loadDirectory(currentPath);
    loadQuota();
}

// ===== Context Menu =====
function handleContextMenu(e) {
    e.preventDefault();
    const row = e.target.closest('.file-row');
    const name = row?.dataset.name;
    const isDir = row?.dataset.isDir === 'true';

    // Remove existing menu
    document.getElementById('ctxMenu')?.remove();

    const menu = document.createElement('div');
    menu.id = 'ctxMenu';
    menu.className = 'context-menu';
    menu.style.left = `${e.clientX}px`;
    menu.style.top = `${e.clientY}px`;

    let items = [];
    if (row) {
        if (!isDir) items.push({ label: 'Preview', action: () => openPreview(name) });
        if (!isDir) items.push({ label: 'Download', action: () => downloadFile(name) });
        items.push({ label: 'Rename', action: () => openRenameModal(name, isDir) });
        items.push({ label: 'Copy', action: () => { clipboard = { action: 'copy', paths: [resolvePath(name)] }; } });
        items.push({ label: 'Cut', action: () => { clipboard = { action: 'cut', paths: [resolvePath(name)] }; } });
        items.push({ divider: true });
        items.push({ label: 'Delete', danger: true, action: () => openDeleteModal(name, isDir) });
    } else {
        items.push({ label: 'New Folder', action: () => document.getElementById('newFolderBtn').click() });
        items.push({ label: 'Upload', action: () => document.getElementById('fileInput').click() });
        if (clipboard) items.push({ label: 'Paste', action: doPaste });
    }

    menu.innerHTML = items.map(item => {
        if (item.divider) return '<div class="ctx-divider"></div>';
        const cls = item.danger ? 'ctx-item danger' : 'ctx-item';
        return `<div class="${cls}">${item.label}</div>`;
    }).join('');

    // Bind actions
    let idx = 0;
    menu.querySelectorAll('.ctx-item').forEach(el => {
        while (items[idx]?.divider) idx++;
        const action = items[idx]?.action;
        if (action) el.addEventListener('click', () => { action(); menu.remove(); });
        idx++;
    });

    document.body.appendChild(menu);

    // Close on click elsewhere
    const close = () => { menu.remove(); document.removeEventListener('click', close); };
    setTimeout(() => document.addEventListener('click', close), 0);
}

async function doPaste() {
    if (!clipboard) return;
    for (const srcPath of clipboard.paths) {
        const name = srcPath.split('/').pop();
        const destPath = currentPath === '/' ? name : `${currentPath}/${name}`;
        await authFetch(`${FILE_API}/rename`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ oldPath: srcPath, newPath: destPath })
        });
    }
    if (clipboard.action === 'cut') clipboard = null;
    loadDirectory(currentPath);
}

// ===== File Preview =====
async function openPreview(name) {
    const filePath = resolvePath(name);
    const ext = getFileExt(name);
    const panel = document.getElementById('previewPanel');
    const title = document.getElementById('previewTitle');
    const body = document.getElementById('previewBody');

    title.textContent = name;
    panel.classList.add('active');

    if (isImage(name)) {
        body.innerHTML = '<div class="loading">Loading...</div>';
        try {
            const imgPath = filePath.replace(/^\//, '');
            const imgRes = await authFetch(`${FILE_API}/download?path=${encodeURIComponent(imgPath)}`);
            const blob = await imgRes.blob();
            const url = URL.createObjectURL(blob);
            body.innerHTML = `<img src="${url}" alt="${name}" class="preview-image">`;
        } catch {
            body.innerHTML = '<p>Failed to load image.</p>';
        }
    } else if (isText(name)) {
        body.innerHTML = '<div class="loading">Loading...</div>';
        try {
            const downloadPath = filePath.replace(/^\//, '');
            const res = await authFetch(`${FILE_API}/download?path=${encodeURIComponent(downloadPath)}`);
            const text = await res.text();
            const isReadonly = viewingScope === 'admin';
            body.innerHTML = `
                <div class="editor-toolbar">
                    <span class="editor-filename">${name}</span>
                    ${!isReadonly ? '<button class="btn btn-primary btn-sm" id="editorSaveBtn">Save</button>' : '<span class="editor-readonly">Read-only</span>'}
                </div>
                <textarea class="editor-textarea" id="editorTextarea" ${isReadonly ? 'readonly' : ''} spellcheck="false">${escapeHtml(text)}</textarea>`;
            document.getElementById('editorSaveBtn')?.addEventListener('click', async () => {
                const content = document.getElementById('editorTextarea').value;
                const token = localStorage.getItem('weda_auth_token');
                const blob = new Blob([content], { type: 'text/plain' });
                const formData = new FormData();
                formData.append('file', blob, name);
                const dirPath = currentPath === '/' ? '' : currentPath.replace(/^\//, '');
                const uploadParams = dirPath ? `?path=${encodeURIComponent(dirPath)}` : '';
                const saveRes = await fetch(`${FILE_API}/upload${uploadParams}`, {
                    method: 'POST',
                    headers: token ? { 'Authorization': `Bearer ${token}` } : {},
                    body: formData
                });
                if (saveRes.ok) {
                    appendTerminalLine(`Saved: ${name}`, 'system');
                } else {
                    alert('Save failed: ' + await saveRes.text());
                }
            });
        } catch {
            body.innerHTML = '<p>Failed to load file.</p>';
        }
    } else {
        body.innerHTML = `<div class="preview-unsupported">
            <p>Preview not available for .${ext} files</p>
            <button class="btn btn-primary btn-sm" id="previewDownloadBtn">Download</button>
        </div>`;
        document.getElementById('previewDownloadBtn')?.addEventListener('click', () => downloadFile(name));
    }
}

function closePreview() {
    document.getElementById('previewPanel').classList.remove('active');
}

// ===== Search =====
function filterFiles(query) {
    const q = query.toLowerCase();
    document.querySelectorAll('.file-row').forEach(row => {
        const name = row.dataset.name.toLowerCase();
        row.style.display = name.includes(q) ? '' : 'none';
    });
}

// ===== Admin Browse Toggle =====
function toggleAdminView() {
    viewingScope = viewingScope === 'admin' ? 'my' : 'admin';
    const btn = document.getElementById('adminBrowseBtn');
    if (btn) {
        btn.classList.toggle('active', viewingScope === 'admin');
        btn.textContent = viewingScope === 'admin' ? 'My Files' : 'All Users';
    }
    loadDirectory('/');
}

// ===== Event Listeners =====
function setupEventListeners() {
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

    document.getElementById('fileInput').addEventListener('change', async (e) => {
        const files = e.target.files;
        if (!files.length) return;
        for (const file of files) await uploadFile(file);
        e.target.value = '';
        loadDirectory(currentPath);
        loadQuota();
    });

    // Modals
    document.getElementById('closeRenameModal').addEventListener('click', closeRenameModal);
    document.getElementById('cancelRename').addEventListener('click', closeRenameModal);
    document.getElementById('confirmRename').addEventListener('click', doRename);
    document.getElementById('renameInput').addEventListener('keydown', (e) => { if (e.key === 'Enter') doRename(); });
    document.getElementById('closeDeleteModal').addEventListener('click', closeDeleteModal);
    document.getElementById('cancelDelete').addEventListener('click', closeDeleteModal);
    document.getElementById('confirmDelete').addEventListener('click', doDelete);

    // Preview panel close
    document.getElementById('closePreview')?.addEventListener('click', closePreview);

    // Admin browse toggle (SuperAdmin only)
    document.getElementById('adminBrowseBtn')?.addEventListener('click', toggleAdminView);

    // Search
    document.getElementById('searchInput')?.addEventListener('input', (e) => filterFiles(e.target.value));

    // Selection toolbar
    document.getElementById('deleteSelectedBtn')?.addEventListener('click', deleteSelected);
    document.getElementById('selectAllBtn')?.addEventListener('click', () => {
        if (selectedFiles.size === currentEntries.length) {
            selectedFiles.clear();
        } else {
            currentEntries.forEach(e => selectedFiles.add(e.name));
        }
        updateSelectionUI();
    });
}

// Keyboard shortcuts
function setupKeyboard() {
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closePreview();
            closeRenameModal();
            closeDeleteModal();
            document.getElementById('ctxMenu')?.remove();
        }
        if (e.key === 'Delete' && selectedFiles.size > 0) deleteSelected();
        if ((e.ctrlKey || e.metaKey) && e.key === 'a') {
            e.preventDefault();
            currentEntries.forEach(entry => selectedFiles.add(entry.name));
            updateSelectionUI();
        }
    });
}

// ===== Upload =====
async function uploadFile(file) {
    const formData = new FormData();
    formData.append('file', file);
    const normalizedPath = currentPath === '/' ? '' : currentPath.replace(/^\//, '');
    const params = normalizedPath ? `?path=${encodeURIComponent(normalizedPath)}` : '';
    try {
        // Don't use authFetch — it adds Content-Type: application/json which breaks FormData.
        // Manually set only Authorization header; let browser set multipart boundary.
        const token = localStorage.getItem('weda_auth_token');
        const res = await fetch(`${FILE_API}/upload${params}`, {
            method: 'POST',
            headers: token ? { 'Authorization': `Bearer ${token}` } : {},
            body: formData
        });
        if (!res.ok) {
            const err = await res.text();
            alert(`Upload failed: ${err}`);
        }
    } catch (e) { alert(`Upload failed: ${e.message}`); }
}

// Drag and drop
function setupDragAndDrop() {
    const overlay = document.createElement('div');
    overlay.className = 'drop-overlay';
    overlay.textContent = 'Drop files to upload';
    document.body.appendChild(overlay);
    let dragCounter = 0;
    document.addEventListener('dragenter', (e) => { e.preventDefault(); dragCounter++; overlay.classList.add('active'); });
    document.addEventListener('dragleave', (e) => { e.preventDefault(); dragCounter--; if (dragCounter === 0) overlay.classList.remove('active'); });
    document.addEventListener('dragover', (e) => e.preventDefault());
    document.addEventListener('drop', async (e) => {
        e.preventDefault(); dragCounter = 0; overlay.classList.remove('active');
        for (const file of e.dataTransfer.files) await uploadFile(file);
        loadDirectory(currentPath);
        loadQuota();
    });
}

// ===== Rename / Delete Modals =====
function openRenameModal(name, isDirectory) {
    pendingAction = { type: 'rename', name, isDirectory };
    document.getElementById('renameInput').value = name;
    document.getElementById('renameModalTitle').textContent = isDirectory ? 'Rename Folder' : 'Rename File';
    document.getElementById('renameModal').classList.add('active');
    setTimeout(() => document.getElementById('renameInput').select(), 50);
}
function closeRenameModal() { document.getElementById('renameModal').classList.remove('active'); pendingAction = null; }

async function doRename() {
    if (!pendingAction) return;
    const newName = document.getElementById('renameInput').value.trim();
    if (!newName || newName === pendingAction.name) { closeRenameModal(); return; }
    const oldPath = resolvePath(pendingAction.name);
    const newPath = currentPath === '/' ? newName : `${currentPath}/${newName}`;
    try {
        const res = await authFetch(`${FILE_API}/rename`, {
            method: 'PUT', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ oldPath, newPath })
        });
        if (res.ok) { closeRenameModal(); loadDirectory(currentPath); }
        else alert(`Rename failed: ${await res.text()}`);
    } catch { alert('Rename failed'); }
}

function openDeleteModal(name, isDirectory) {
    pendingAction = { type: 'delete', name, isDirectory };
    document.getElementById('deleteFileName').textContent = name;
    document.getElementById('deleteModal').classList.add('active');
}
function closeDeleteModal() { document.getElementById('deleteModal').classList.remove('active'); pendingAction = null; }

async function doDelete() {
    if (!pendingAction) return;
    const path = resolvePath(pendingAction.name);
    try {
        const res = await authFetch(`${FILE_API}?path=${encodeURIComponent(path)}`, { method: 'DELETE' });
        if (res.ok) { closeDeleteModal(); loadDirectory(currentPath); loadQuota(); }
        else alert(`Delete failed: ${await res.text()}`);
    } catch { alert('Delete failed'); }
}

// ===== Helpers =====
function resolvePath(name) { return currentPath === '/' ? name : `${currentPath}/${name}`; }
async function downloadFile(name) {
    const path = resolvePath(name).replace(/^\//, '');
    try {
        const res = await authFetch(`${FILE_API}/download?path=${encodeURIComponent(path)}`);
        if (!res.ok) { alert('Download failed'); return; }
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.click();
        URL.revokeObjectURL(url);
    } catch (e) {
        alert(`Download failed: ${e.message}`);
    }
}
function escapeHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
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

// ===== Terminal =====
const TERMINAL_API = '/api/v1/workspace-terminal';
let terminalHistory = [];
let historyIndex = -1;
let tabCompletionCache = null; // { prefix, matches, index }

const TERMINAL_COMMANDS = [
    'ls', 'dir', 'pwd', 'echo', 'cat', 'head', 'tail', 'grep', 'find', 'wc',
    'date', 'whoami', 'hostname', 'mkdir', 'touch', 'cp', 'mv', 'rm',
    'git', 'dotnet', 'npm', 'node', 'python', 'python3',
    'cd', 'clear', 'help'
];

function handleTabCompletion(input) {
    const value = input.value;
    const cursorPos = input.selectionStart;
    const beforeCursor = value.substring(0, cursorPos);

    // Split into tokens; complete the last one
    const tokens = beforeCursor.split(/\s+/);
    const partial = tokens[tokens.length - 1];
    const isFirstToken = tokens.length <= 1 || (tokens.length === 2 && tokens[0] === '');

    // Check if we're cycling through previous matches
    if (tabCompletionCache && tabCompletionCache.prefix === partial && tabCompletionCache.matches.length > 1) {
        tabCompletionCache.index = (tabCompletionCache.index + 1) % tabCompletionCache.matches.length;
        applyCompletion(input, tokens, tabCompletionCache.matches[tabCompletionCache.index], value, cursorPos);
        return;
    }

    let matches = [];

    if (isFirstToken) {
        // Complete command names
        matches = TERMINAL_COMMANDS.filter(c => c.startsWith(partial.toLowerCase()));
    } else {
        // Complete file/directory names from current entries
        matches = currentEntries
            .map(e => e.name)
            .filter(n => n.toLowerCase().startsWith(partial.toLowerCase()));
    }

    if (matches.length === 0) return;

    if (matches.length === 1) {
        // Single match — complete it
        const completed = matches[0];
        const entry = currentEntries.find(e => e.name === completed);
        const suffix = entry?.isDirectory ? '/' : ' ';
        applyCompletion(input, tokens, completed + suffix, value, cursorPos);
        tabCompletionCache = null;
    } else {
        // Multiple matches — complete common prefix, show options
        const commonPrefix = getCommonPrefix(matches);
        if (commonPrefix.length > partial.length) {
            applyCompletion(input, tokens, commonPrefix, value, cursorPos);
        } else {
            // Show all matches
            appendTerminalLine(matches.join('  '), 'system');
        }
        tabCompletionCache = { prefix: partial, matches, index: 0 };
    }
}

function applyCompletion(input, tokens, replacement, originalValue, cursorPos) {
    tokens[tokens.length - 1] = replacement;
    const newValue = tokens.join(' ');
    const afterCursor = originalValue.substring(cursorPos);
    input.value = newValue + afterCursor;
    input.selectionStart = input.selectionEnd = newValue.length;
}

function getCommonPrefix(strings) {
    if (strings.length === 0) return '';
    let prefix = strings[0];
    for (let i = 1; i < strings.length; i++) {
        while (!strings[i].startsWith(prefix)) {
            prefix = prefix.slice(0, -1);
        }
    }
    return prefix;
}

function initTerminal() {
    const input = document.getElementById('terminalInput');
    const toggleBtn = document.getElementById('terminalToggleBtn');
    const clearBtn = document.getElementById('terminalClearBtn');
    const header = document.getElementById('terminalHeader');

    if (!input) return;

    // Capture Tab globally when terminal input is focused
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Tab' && document.activeElement === input) {
            e.preventDefault();
            e.stopPropagation();
            handleTabCompletion(input);
        }
    }, true); // capture phase to intercept before browser handles it

    input.addEventListener('keydown', (e) => {
        // Reset tab completion cache on any non-Tab key
        tabCompletionCache = null;
        if (e.key === 'Enter') {
            e.preventDefault();
            const cmd = input.value.trim();
            if (!cmd) return;
            input.value = '';
            terminalHistory.push(cmd);
            historyIndex = terminalHistory.length;
            executeTerminalCommand(cmd);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (historyIndex > 0) {
                historyIndex--;
                input.value = terminalHistory[historyIndex];
            }
        } else if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (historyIndex < terminalHistory.length - 1) {
                historyIndex++;
                input.value = terminalHistory[historyIndex];
            } else {
                historyIndex = terminalHistory.length;
                input.value = '';
            }
        }
    });

    toggleBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        document.getElementById('terminalPanel').classList.toggle('collapsed');
    });

    header.addEventListener('dblclick', () => {
        document.getElementById('terminalPanel').classList.toggle('collapsed');
    });

    clearBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        document.getElementById('terminalOutput').innerHTML = '';
    });

    updateTerminalPrompt();
}

function updateTerminalPrompt() {
    const prompt = document.getElementById('terminalPrompt');
    if (prompt) {
        const dir = currentPath === '/' ? '~' : `~${currentPath}`;
        prompt.textContent = `${dir}$`;
    }
}

async function executeTerminalCommand(cmd) {
    const output = document.getElementById('terminalOutput');

    // Show command
    appendTerminalLine(`$ ${cmd}`, 'command');

    // Built-in commands
    if (cmd === 'help') {
        appendTerminalLine('Available commands: ls, cat, head, tail, grep, find, mkdir, touch, cp, mv, rm, pwd, echo, git, dotnet, npm, node, python', 'system');
        appendTerminalLine('Built-in: cd <dir>, clear, help', 'system');
        return;
    }

    if (cmd === 'clear') {
        output.innerHTML = '';
        return;
    }

    if (cmd === 'pwd') {
        appendTerminalLine(currentPath === '/' ? '~' : `~${currentPath}`, '');
        return;
    }

    if (cmd.startsWith('cd ')) {
        const target = cmd.slice(3).trim();
        if (target === '..') {
            const parts = currentPath.split('/').filter(Boolean);
            parts.pop();
            currentPath = parts.length ? '/' + parts.join('/') : '/';
        } else if (target === '/' || target === '~') {
            currentPath = '/';
        } else {
            currentPath = currentPath === '/' ? `/${target}` : `${currentPath}/${target}`;
        }
        updateTerminalPrompt();
        loadDirectory(currentPath);
        return;
    }

    // Execute via API
    try {
        const res = await authFetch(`${TERMINAL_API}/exec`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ command: cmd, cwd: currentPath === '/' ? null : currentPath })
        });

        if (!res.ok) {
            const err = await res.json().catch(() => ({ detail: 'Request failed' }));
            appendTerminalLine(err.detail || err.title || 'Error', 'error');
            return;
        }

        const data = await res.json();
        if (data.output) {
            appendTerminalLine(data.output, data.exitCode !== 0 ? 'error' : '');
        }

        // Refresh file list if command may have changed files
        if (/^(mkdir|touch|cp|mv|rm|git)/.test(cmd)) {
            loadDirectory(currentPath);
            loadQuota();
        }
    } catch (e) {
        appendTerminalLine(`Error: ${e.message}`, 'error');
    }
}

function appendTerminalLine(text, cls = '') {
    const output = document.getElementById('terminalOutput');
    const line = document.createElement('div');
    line.className = `terminal-line ${cls}`;
    line.textContent = text;
    output.appendChild(line);
    output.scrollTop = output.scrollHeight;
}

// Init terminal after DOM ready (called from main init)
document.addEventListener('DOMContentLoaded', () => initTerminal());
