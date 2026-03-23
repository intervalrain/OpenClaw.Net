// Workflow List Page

// State
let workflows = [];
let currentWorkflow = null;

// DOM Elements
const workflowList = document.getElementById('workflowList');
const drawer = document.getElementById('workflowDrawer');
const drawerOverlay = document.getElementById('drawerOverlay');
const createModal = document.getElementById('createModal');
const deleteModal = document.getElementById('deleteModal');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    // Check authentication - show login modal if not authenticated
    if (!isAuthenticated()) {
        showLoginModal(() => window.location.reload());
        return;
    }

    // Setup create button (now in page-header)
    const createBtn = document.getElementById('createWorkflowBtn');
    if (createBtn) {
        createBtn.addEventListener('click', openCreateModal);
    }

    await loadWorkflows();
    setupEventListeners();
});

// Load Workflows
async function loadWorkflows() {
    try {
        const response = await authFetch('/api/v1/workflow');
        if (!response.ok) {
            throw new Error('Failed to load workflows');
        }
        workflows = await response.json();
        renderWorkflows();
    } catch (error) {
        console.error('Error loading workflows:', error);
        workflowList.innerHTML = `<div class="empty-state">Failed to load workflows. Please try again.</div>`;
    }
}

// Render Workflows
function renderWorkflows() {
    if (workflows.length === 0) {
        workflowList.innerHTML = `
            <div class="empty-state">
                <div class="empty-state-icon">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                        <path d="M14 2v6h6"/>
                        <path d="M12 18v-6"/>
                        <path d="M9 15h6"/>
                    </svg>
                </div>
                <h3>No workflows yet</h3>
                <p>Create your first workflow to get started</p>
            </div>
        `;
        return;
    }

    workflowList.innerHTML = workflows.map(workflow => {
        const scheduleText = formatSchedule(workflow.schedule);
        const lastExec = workflow.lastExecution;

        return `
            <div class="workflow-card" data-id="${workflow.id}" onclick="openDrawer('${workflow.id}')">
                <div class="workflow-card-header">
                    <h3>${escapeHtml(workflow.name)}</h3>
                    <div class="workflow-status">
                        <span class="status-dot ${workflow.isActive ? 'active' : 'inactive'}"></span>
                        <span>${workflow.isActive ? 'Active' : 'Inactive'}</span>
                    </div>
                </div>
                <p class="workflow-card-description">${escapeHtml(workflow.description || 'No description')}</p>
                <div class="workflow-card-meta">
                    <div class="workflow-schedule">
                        ${scheduleText ? `
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <circle cx="12" cy="12" r="10"/>
                                <path d="M12 6v6l4 2"/>
                            </svg>
                            <span>${scheduleText}</span>
                        ` : '<span>Manual</span>'}
                    </div>
                    <span class="workflow-nodes">${workflow.nodeCount} nodes</span>
                </div>
                <div class="workflow-card-footer">
                    <div class="last-execution">
                        ${lastExec ? `
                            <span class="execution-status-badge ${lastExec.status.toLowerCase()}">${lastExec.status}</span>
                            <span>${formatRelativeTime(lastExec.startedAt)}</span>
                        ` : '<span class="text-muted">Never executed</span>'}
                    </div>
                    <button class="btn btn-run" onclick="event.stopPropagation(); runWorkflow('${workflow.id}')">
                        Run
                    </button>
                </div>
            </div>
        `;
    }).join('');
}

// Format schedule for display
function formatSchedule(schedule) {
    if (!schedule || !schedule.isEnabled) return null;

    const time = schedule.timeOfDay || '09:00';
    const freq = schedule.frequency;

    switch (freq) {
        case 'Daily':
            return `Daily at ${time}`;
        case 'Weekly':
            const days = schedule.daysOfWeek?.map(d => d.slice(0, 3)).join(', ') || 'Mon-Fri';
            return `${days} at ${time}`;
        case 'Monthly':
            return `Monthly on day ${schedule.dayOfMonth || 1} at ${time}`;
        default:
            return `${freq} at ${time}`;
    }
}

// Format relative time
function formatRelativeTime(dateStr) {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
}

// Open Drawer
async function openDrawer(workflowId) {
    currentWorkflow = workflows.find(w => w.id === workflowId);
    if (!currentWorkflow) return;

    // Update drawer header
    document.getElementById('drawerTitle').textContent = currentWorkflow.name;

    // Update schedule info
    renderScheduleInfo(currentWorkflow.schedule);

    // Load and render executions
    await loadExecutionHistory(workflowId);

    // Render mini graph (placeholder for now)
    renderMiniGraph(currentWorkflow);

    // Show drawer
    drawer.classList.add('open');
    drawerOverlay.classList.add('show');
}

function closeDrawer() {
    drawer.classList.remove('open');
    drawerOverlay.classList.remove('show');
    currentWorkflow = null;
}

function renderScheduleInfo(schedule) {
    const container = document.getElementById('scheduleInfo');

    if (!schedule || !schedule.isEnabled) {
        container.innerHTML = '<span class="no-schedule">No schedule configured</span>';
        return;
    }

    const time = schedule.timeOfDay || '09:00';
    const tz = schedule.timezone || 'UTC';

    let frequencyText = '';
    switch (schedule.frequency) {
        case 'Daily':
            frequencyText = 'Every day';
            break;
        case 'Weekly':
            const days = schedule.daysOfWeek?.map(d => d.slice(0, 3)).join(', ') || 'Mon-Fri';
            frequencyText = `Every week on ${days}`;
            break;
        case 'Monthly':
            frequencyText = `Every month on day ${schedule.dayOfMonth || 1}`;
            break;
    }

    container.innerHTML = `
        <div class="schedule-detail">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                <line x1="16" y1="2" x2="16" y2="6"/>
                <line x1="8" y1="2" x2="8" y2="6"/>
                <line x1="3" y1="10" x2="21" y2="10"/>
            </svg>
            <span>${frequencyText}</span>
        </div>
        <div class="schedule-detail">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10"/>
                <path d="M12 6v6l4 2"/>
            </svg>
            <span>at ${time} (${tz})</span>
        </div>
    `;
}

async function loadExecutionHistory(workflowId) {
    const container = document.getElementById('executionHistory');

    try {
        const response = await authFetch(`/api/v1/workflow/executions?workflowId=${workflowId}&limit=5`);
        if (!response.ok) throw new Error('Failed to load executions');

        const executions = await response.json();

        if (executions.length === 0) {
            container.innerHTML = '<div class="empty-state">No executions yet</div>';
            return;
        }

        container.innerHTML = executions.map(exec => `
            <div class="execution-item" onclick="viewExecution('${exec.id}')">
                <div class="execution-info">
                    <span class="execution-trigger">${exec.trigger}</span>
                    <span class="execution-time">${formatRelativeTime(exec.startedAt)}</span>
                </div>
                <div>
                    <span class="execution-status-badge ${exec.status.toLowerCase()}">${exec.status}</span>
                    ${exec.duration ? `<span class="execution-duration">${formatDuration(exec.duration)}</span>` : ''}
                </div>
            </div>
        `).join('');
    } catch (error) {
        console.error('Error loading executions:', error);
        container.innerHTML = '<div class="empty-state">Failed to load executions</div>';
    }
}

function formatDuration(ms) {
    if (ms < 1000) return `${ms}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
    return `${Math.floor(ms / 60000)}m ${Math.floor((ms % 60000) / 1000)}s`;
}

function renderMiniGraph(workflow) {
    const container = document.getElementById('miniGraph');
    // For now, show a placeholder. Cytoscape will be added in editor.
    container.innerHTML = `
        <div class="mini-graph-placeholder">
            <svg width="200" height="100" viewBox="0 0 200 100">
                <circle cx="20" cy="50" r="10" fill="#3498db"/>
                <line x1="30" y1="50" x2="60" y2="30" stroke="#ccc" stroke-width="2"/>
                <line x1="30" y1="50" x2="60" y2="70" stroke="#ccc" stroke-width="2"/>
                <circle cx="70" cy="30" r="10" fill="#27ae60"/>
                <circle cx="70" cy="70" r="10" fill="#27ae60"/>
                <line x1="80" y1="30" x2="110" y2="50" stroke="#ccc" stroke-width="2"/>
                <line x1="80" y1="70" x2="110" y2="50" stroke="#ccc" stroke-width="2"/>
                <circle cx="120" cy="50" r="10" fill="#f1c40f"/>
                <line x1="130" y1="50" x2="160" y2="50" stroke="#ccc" stroke-width="2"/>
                <circle cx="170" cy="50" r="10" fill="#e74c3c"/>
            </svg>
            <div style="font-size: 0.8rem; color: var(--text-muted); margin-top: 8px;">${workflow.nodeCount} nodes</div>
        </div>
    `;
}

// Run Workflow
async function runWorkflow(workflowId) {
    try {
        const response = await authFetch(`/api/v1/workflow/${workflowId}/execute`, {
            method: 'POST',
            body: JSON.stringify({})
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to execute workflow');
        }

        const data = await response.json();
        alert(`Workflow started! Execution ID: ${data.executionId}`);

        // Refresh the list
        await loadWorkflows();

        // If drawer is open, refresh execution history
        if (currentWorkflow && currentWorkflow.id === workflowId) {
            await loadExecutionHistory(workflowId);
        }
    } catch (error) {
        console.error('Error executing workflow:', error);
        alert('Failed to execute workflow: ' + error.message);
    }
}

// View Execution (navigate to execution viewer)
function viewExecution(executionId) {
    window.location.href = `editor.html?execution=${executionId}`;
}

// Expand to Editor
function expandToEditor() {
    if (currentWorkflow) {
        window.location.href = `editor.html?id=${currentWorkflow.id}`;
    }
}

// Create Workflow
function openCreateModal() {
    document.getElementById('workflowName').value = '';
    document.getElementById('workflowDescription').value = '';
    document.getElementById('createError').textContent = '';
    createModal.classList.add('show');
}

function closeCreateModal() {
    createModal.classList.remove('show');
}

async function createWorkflow() {
    const name = document.getElementById('workflowName').value.trim();
    const description = document.getElementById('workflowDescription').value.trim();
    const errorEl = document.getElementById('createError');

    if (!name) {
        errorEl.textContent = 'Name is required';
        return;
    }

    try {
        // Create with a minimal workflow definition
        const defaultDefinition = {
            nodes: [
                { id: 'start', type: 'start', position: { x: 100, y: 200 } },
                { id: 'end', type: 'end', position: { x: 500, y: 200 } }
            ],
            edges: [
                { id: 'e1', source: 'start', target: 'end' }
            ],
            variables: {}
        };

        const response = await authFetch('/api/v1/workflow', {
            method: 'POST',
            body: JSON.stringify({
                name,
                description,
                definition: defaultDefinition,
                schedule: null
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to create workflow');
        }

        const workflow = await response.json();
        closeCreateModal();

        // Navigate to editor
        window.location.href = `editor.html?id=${workflow.id}`;
    } catch (error) {
        console.error('Error creating workflow:', error);
        errorEl.textContent = error.message;
    }
}

// Delete Workflow
function openDeleteModal() {
    if (!currentWorkflow) return;
    document.getElementById('deleteWorkflowName').textContent = currentWorkflow.name;
    deleteModal.classList.add('show');
}

function closeDeleteModal() {
    deleteModal.classList.remove('show');
}

async function deleteWorkflow() {
    if (!currentWorkflow) return;

    try {
        const response = await authFetch(`/api/v1/workflow/${currentWorkflow.id}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to delete workflow');
        }

        closeDeleteModal();
        closeDrawer();
        await loadWorkflows();
    } catch (error) {
        console.error('Error deleting workflow:', error);
        alert('Failed to delete workflow: ' + error.message);
    }
}

// Clone Workflow
async function cloneWorkflow() {
    if (!currentWorkflow) return;

    try {
        const response = await authFetch(`/api/v1/workflow/${currentWorkflow.id}/clone`, {
            method: 'POST',
            body: JSON.stringify({
                name: `${currentWorkflow.name} (Copy)`
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.title || 'Failed to clone workflow');
        }

        const workflow = await response.json();
        closeDrawer();

        // Navigate to editor with the new workflow
        window.location.href = `editor.html?id=${workflow.id}`;
    } catch (error) {
        console.error('Error cloning workflow:', error);
        alert('Failed to clone workflow: ' + error.message);
    }
}

// Event Listeners
function setupEventListeners() {
    // Drawer
    document.getElementById('closeDrawerBtn').addEventListener('click', closeDrawer);
    document.getElementById('expandDrawerBtn').addEventListener('click', expandToEditor);
    drawerOverlay.addEventListener('click', closeDrawer);

    // Drawer actions
    document.getElementById('runWorkflowBtn').addEventListener('click', () => {
        if (currentWorkflow) runWorkflow(currentWorkflow.id);
    });
    document.getElementById('cloneWorkflowBtn').addEventListener('click', cloneWorkflow);
    document.getElementById('deleteWorkflowBtn').addEventListener('click', openDeleteModal);

    // Create modal
    document.getElementById('closeCreateModal').addEventListener('click', closeCreateModal);
    document.getElementById('cancelCreate').addEventListener('click', closeCreateModal);
    document.getElementById('confirmCreate').addEventListener('click', createWorkflow);

    // Delete modal
    document.getElementById('closeDeleteModal').addEventListener('click', closeDeleteModal);
    document.getElementById('cancelDelete').addEventListener('click', closeDeleteModal);
    document.getElementById('confirmDelete').addEventListener('click', deleteWorkflow);

    // Modal backdrop clicks
    createModal.addEventListener('click', (e) => {
        if (e.target === createModal) closeCreateModal();
    });
    deleteModal.addEventListener('click', (e) => {
        if (e.target === deleteModal) closeDeleteModal();
    });

    // Escape key
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeDrawer();
            closeCreateModal();
            closeDeleteModal();
        }
    });
}

// Utilities
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
