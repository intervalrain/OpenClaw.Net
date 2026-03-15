// Pipeline Management UI

// State
let pipelines = [];
let executions = [];
let currentPipeline = null;
let pollingIntervals = {};

// DOM Elements
const pipelineList = document.getElementById('pipelineList');
const executionList = document.getElementById('executionList');
const executeModal = document.getElementById('executeModal');
const approvalModal = document.getElementById('approvalModal');
const userArea = document.getElementById('userArea');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    setupAuth();
    await loadPipelines();
    setupEventListeners();
    loadExecutionsFromStorage();
});

// Auth Setup
function setupAuth() {
    const user = getCurrentUser();
    if (user) {
        userArea.innerHTML = `
            <div class="user-info-header">
                <div class="user-avatar">${getInitials(user.name)}</div>
                <div>
                    <div class="user-name">${user.name}</div>
                    <div class="user-role">${user.roles?.[0] || 'User'}</div>
                </div>
            </div>
            <button class="btn btn-secondary" id="logoutBtn">Logout</button>
        `;
        document.getElementById('logoutBtn').addEventListener('click', () => {
            clearAuth();
            window.location.reload();
        });
    }
}

function getInitials(name) {
    if (!name) return '?';
    return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
}

// Load Pipelines
async function loadPipelines() {
    try {
        const response = await authFetch('/api/v1/pipeline');
        if (!response.ok) {
            throw new Error('Failed to load pipelines');
        }
        const data = await response.json();
        pipelines = data.pipelines || [];
        renderPipelines();
    } catch (error) {
        console.error('Error loading pipelines:', error);
        pipelineList.innerHTML = `<div class="empty-state">Failed to load pipelines. Please try again.</div>`;
    }
}

// Render Pipelines
function renderPipelines() {
    if (pipelines.length === 0) {
        pipelineList.innerHTML = `<div class="empty-state">No pipelines available</div>`;
        return;
    }

    pipelineList.innerHTML = pipelines.map(pipeline => `
        <div class="pipeline-card" data-name="${pipeline.name}">
            <div class="pipeline-card-header">
                <h3>${pipeline.name}</h3>
            </div>
            <p class="pipeline-card-description">${pipeline.description || 'No description'}</p>
            <div class="pipeline-card-footer">
                <span class="pipeline-params-badge">
                    ${pipeline.parameters ? 'Has parameters' : 'No parameters'}
                </span>
                <button class="btn btn-execute" onclick="openExecuteModal('${pipeline.name}')">
                    Execute
                </button>
            </div>
        </div>
    `).join('');
}

// Open Execute Modal
function openExecuteModal(pipelineName) {
    currentPipeline = pipelines.find(p => p.name === pipelineName);
    if (!currentPipeline) return;

    document.getElementById('modalTitle').textContent = `Execute: ${currentPipeline.name}`;
    document.getElementById('pipelineDescription').textContent = currentPipeline.description || '';

    renderParametersForm(currentPipeline.parameters);
    executeModal.classList.add('show');
}

// Render Parameters Form
function renderParametersForm(schema) {
    const form = document.getElementById('parametersForm');

    if (!schema || !schema.properties) {
        form.innerHTML = '<p class="empty-state">No parameters required</p>';
        return;
    }

    const properties = schema.properties;
    const required = schema.required || [];

    form.innerHTML = Object.entries(properties).map(([name, prop]) => {
        const isRequired = required.includes(name);
        const inputType = getInputType(prop.type);
        const defaultValue = prop.default !== undefined ? prop.default : '';

        return `
            <div class="param-group">
                <label for="param-${name}">
                    ${name}
                    ${isRequired ? '<span class="param-required">*</span>' : ''}
                </label>
                ${renderInput(name, prop, inputType, defaultValue)}
                ${prop.description ? `<span class="param-description">${prop.description}</span>` : ''}
            </div>
        `;
    }).join('');
}

function getInputType(schemaType) {
    switch (schemaType) {
        case 'integer':
        case 'number':
            return 'number';
        case 'boolean':
            return 'checkbox';
        case 'array':
            return 'textarea';
        default:
            return 'text';
    }
}

function renderInput(name, prop, inputType, defaultValue) {
    if (prop.enum) {
        return `
            <select id="param-${name}" name="${name}">
                ${prop.enum.map(v => `<option value="${v}" ${v === defaultValue ? 'selected' : ''}>${v}</option>`).join('')}
            </select>
        `;
    }

    if (inputType === 'checkbox') {
        return `<input type="checkbox" id="param-${name}" name="${name}" ${defaultValue ? 'checked' : ''}>`;
    }

    if (inputType === 'textarea' || prop.type === 'object') {
        const val = typeof defaultValue === 'object' ? JSON.stringify(defaultValue, null, 2) : defaultValue;
        return `<textarea id="param-${name}" name="${name}" placeholder="${prop.description || ''}">${val}</textarea>`;
    }

    return `<input type="${inputType}" id="param-${name}" name="${name}" value="${defaultValue}" placeholder="${prop.description || ''}">`;
}

// Execute Pipeline
async function executePipeline() {
    if (!currentPipeline) return;

    const form = document.getElementById('parametersForm');
    const args = {};

    if (currentPipeline.parameters?.properties) {
        const properties = currentPipeline.parameters.properties;

        for (const [name, prop] of Object.entries(properties)) {
            const input = form.querySelector(`[name="${name}"]`);
            if (!input) continue;

            let value;
            if (input.type === 'checkbox') {
                value = input.checked;
            } else if (input.type === 'number') {
                value = input.value ? Number(input.value) : undefined;
            } else if (prop.type === 'array' || prop.type === 'object') {
                try {
                    value = input.value ? JSON.parse(input.value) : undefined;
                } catch (e) {
                    alert(`Invalid JSON for ${name}`);
                    return;
                }
            } else {
                value = input.value || undefined;
            }

            if (value !== undefined) {
                args[name] = value;
            }
        }
    }

    closeModal();

    try {
        const response = await authFetch(`/api/v1/pipeline/${currentPipeline.name}/execute`, {
            method: 'POST',
            body: JSON.stringify({ args: Object.keys(args).length > 0 ? args : null })
        });

        if (!response.ok) {
            throw new Error('Failed to execute pipeline');
        }

        const data = await response.json();
        const executionId = data.executionId;

        // Add to executions list
        const execution = {
            id: executionId,
            pipelineName: currentPipeline.name,
            status: 'Running',
            startedAt: new Date().toISOString()
        };
        executions.unshift(execution);
        saveExecutionsToStorage();
        renderExecutions();

        // Start polling for status
        startPolling(executionId);

    } catch (error) {
        console.error('Error executing pipeline:', error);
        alert('Failed to execute pipeline: ' + error.message);
    }
}

// Polling for execution status
function startPolling(executionId) {
    if (pollingIntervals[executionId]) {
        clearInterval(pollingIntervals[executionId]);
    }

    const poll = async () => {
        try {
            const response = await authFetch(`/api/v1/pipeline/executions/${executionId}`);
            if (!response.ok) return;

            const data = await response.json();
            updateExecution(executionId, data);

            // Check if waiting for approval
            if (data.status === 'WaitingForApproval' && data.pendingApproval) {
                showApprovalModal(executionId, data.pendingApproval);
            }

            // Stop polling if completed or failed
            if (['Completed', 'Failed', 'Rejected'].includes(data.status)) {
                stopPolling(executionId);
            }
        } catch (error) {
            console.error('Polling error:', error);
        }
    };

    poll(); // Initial poll
    pollingIntervals[executionId] = setInterval(poll, 2000);
}

function stopPolling(executionId) {
    if (pollingIntervals[executionId]) {
        clearInterval(pollingIntervals[executionId]);
        delete pollingIntervals[executionId];
    }
}

function updateExecution(executionId, data) {
    const index = executions.findIndex(e => e.id === executionId);
    if (index === -1) return;

    executions[index] = {
        ...executions[index],
        status: data.status,
        summary: data.summary,
        steps: data.steps,
        pendingApproval: data.pendingApproval
    };
    saveExecutionsToStorage();
    renderExecutions();
}

// Approval Modal
let currentApprovalExecutionId = null;

function showApprovalModal(executionId, approvalInfo) {
    currentApprovalExecutionId = executionId;
    document.getElementById('approvalMessage').textContent = approvalInfo.description || approvalInfo.message || 'Approval required to continue';

    const details = document.getElementById('approvalDetails');

    // Check if we have structured proposed changes
    if (approvalInfo.proposedChanges && approvalInfo.proposedChanges.length > 0) {
        details.innerHTML = renderProposedChanges(approvalInfo.proposedChanges);
        details.style.display = 'block';
    } else if (approvalInfo.details) {
        const detailsContent = typeof approvalInfo.details === 'string'
            ? approvalInfo.details
            : JSON.stringify(approvalInfo.details, null, 2);
        details.innerHTML = `<pre>${escapeHtml(detailsContent)}</pre>`;
        details.style.display = 'block';
    } else {
        details.style.display = 'none';
    }

    approvalModal.classList.add('show');
}

function renderProposedChanges(changes) {
    return changes.map(change => `
        <div class="proposed-change">
            <div class="change-header">
                <span class="work-item-type ${change.workItemType?.toLowerCase() || 'task'}">${escapeHtml(change.workItemType || 'Task')}</span>
                <a href="${escapeHtml(change.workItemUrl || '#')}" target="_blank" class="work-item-link">
                    #${change.workItemId}
                </a>
                <span class="work-item-title">${escapeHtml(change.title || 'Unknown')}</span>
            </div>
            <div class="state-change">
                <span class="state current">${escapeHtml(change.currentState)}</span>
                <span class="arrow">→</span>
                <span class="state proposed">${escapeHtml(change.proposedState)}</span>
            </div>
            <div class="change-reason">${escapeHtml(change.reason)}</div>
            ${change.relatedCommits && change.relatedCommits.length > 0 ? `
                <div class="related-commits">
                    <div class="commits-header">Related Commits:</div>
                    <ul class="commits-list">
                        ${change.relatedCommits.map(commit => `<li><code>${escapeHtml(commit)}</code></li>`).join('')}
                    </ul>
                </div>
            ` : ''}
        </div>
    `).join('');
}

async function submitApproval(approved) {
    if (!currentApprovalExecutionId) return;

    const action = approved ? 'approve' : 'reject';

    try {
        const response = await authFetch(`/api/v1/pipeline/executions/${currentApprovalExecutionId}/${action}`, {
            method: 'POST'
        });

        if (!response.ok) {
            throw new Error(`Failed to ${action} execution`);
        }

        closeApprovalModal();

        // Update status immediately
        const execution = executions.find(e => e.id === currentApprovalExecutionId);
        if (execution) {
            execution.status = approved ? 'Running' : 'Rejected';
            execution.pendingApproval = null;
            saveExecutionsToStorage();
            renderExecutions();
        }

    } catch (error) {
        console.error('Approval error:', error);
        alert('Failed to submit approval: ' + error.message);
    }
}

// Render Executions
function renderExecutions() {
    if (executions.length === 0) {
        executionList.innerHTML = '<p class="empty-state">No executions yet</p>';
        return;
    }

    executionList.innerHTML = executions.map(exec => `
        <div class="execution-item" data-id="${exec.id}">
            <div class="execution-info">
                <span class="execution-name">${exec.pipelineName}</span>
                <span class="execution-id">${exec.id}</span>
                ${exec.summary ? `<span class="execution-summary">${exec.summary}</span>` : ''}
            </div>
            <div class="execution-status">
                <span class="status-badge status-${exec.status.toLowerCase()}">${exec.status}</span>
                ${exec.status === 'WaitingForApproval' ? `
                    <div class="execution-actions">
                        <button class="btn btn-success btn-sm" onclick="resumeApproval('${exec.id}')">Review</button>
                    </div>
                ` : ''}
            </div>
            ${exec.steps ? renderSteps(exec.steps) : ''}
        </div>
    `).join('');
}

function renderSteps(steps) {
    if (!steps || steps.length === 0) return '';

    return `
        <div class="execution-steps">
            ${steps.map(step => `
                <div class="step-item">
                    <div class="step-icon ${step.success ? 'step-success' : 'step-error'}">
                        ${step.success ? '✓' : '✗'}
                    </div>
                    <div class="step-content">
                        <span class="step-name">${step.name}</span>
                        ${step.message ? `<span class="step-message">${step.message}</span>` : ''}
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}

function resumeApproval(executionId) {
    const execution = executions.find(e => e.id === executionId);
    if (execution?.pendingApproval) {
        showApprovalModal(executionId, execution.pendingApproval);
    } else {
        // Re-fetch to get approval info
        startPolling(executionId);
    }
}

// Storage
function saveExecutionsToStorage() {
    localStorage.setItem('pipeline_executions', JSON.stringify(executions));
}

function loadExecutionsFromStorage() {
    try {
        const stored = localStorage.getItem('pipeline_executions');
        if (stored) {
            executions = JSON.parse(stored);
            renderExecutions();

            // Resume polling for running executions
            executions
                .filter(e => ['Running', 'WaitingForApproval'].includes(e.status))
                .forEach(e => startPolling(e.id));
        }
    } catch (e) {
        executions = [];
    }
}

function clearExecutions() {
    // Stop all polling
    Object.keys(pollingIntervals).forEach(stopPolling);
    executions = [];
    saveExecutionsToStorage();
    renderExecutions();
}

// Event Listeners
function setupEventListeners() {
    // Execute modal
    document.getElementById('closeModal').addEventListener('click', closeModal);
    document.getElementById('cancelExecute').addEventListener('click', closeModal);
    document.getElementById('confirmExecute').addEventListener('click', executePipeline);

    // Approval modal
    document.getElementById('closeApprovalModal').addEventListener('click', closeApprovalModal);
    document.getElementById('approveBtn').addEventListener('click', () => submitApproval(true));
    document.getElementById('rejectBtn').addEventListener('click', () => submitApproval(false));

    // Clear executions
    document.getElementById('clearExecutions').addEventListener('click', clearExecutions);

    // Close modals on backdrop click
    executeModal.addEventListener('click', (e) => {
        if (e.target === executeModal) closeModal();
    });
    approvalModal.addEventListener('click', (e) => {
        if (e.target === approvalModal) closeApprovalModal();
    });

    // Escape key
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeModal();
            closeApprovalModal();
        }
    });
}

// Modal helpers
function closeModal() {
    executeModal.classList.remove('show');
    currentPipeline = null;
}

function closeApprovalModal() {
    approvalModal.classList.remove('show');
    currentApprovalExecutionId = null;
}

// Utilities
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
