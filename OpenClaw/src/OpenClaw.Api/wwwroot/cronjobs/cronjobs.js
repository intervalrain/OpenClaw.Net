/**
 * Cron Jobs Editor
 * Manages cron jobs, tool instances, and executions.
 */

// ===== State =====
let jobs = [];
let toolInstances = [];
let tools = [];
let skills = [];
let currentJobId = null;
let editingToolInstanceId = null;

// Tool arg values (persisted across tag switches)
let _toolArgValues = {};

// Autocomplete state
let acActiveDropdown = null;
let acItems = [];
let acIndex = -1;
let acTriggerChar = '';
let acStartPos = 0;

// ===== Initialization =====
document.addEventListener('DOMContentLoaded', async () => {
    document.getElementById('newJobBtn').addEventListener('click', newJob);
    document.getElementById('newToolInstanceBtn').addEventListener('click', () => newToolInstance());
    document.getElementById('saveJobBtn').addEventListener('click', saveJob);
    document.getElementById('deleteJobBtn').addEventListener('click', deleteJob);
    document.getElementById('runJobBtn').addEventListener('click', runJob);
    document.getElementById('cancelTiBtn').addEventListener('click', cancelToolInstance);
    document.getElementById('saveTiBtn').addEventListener('click', saveToolInstance);

    // Schedule frequency change
    document.getElementById('schedFrequency').addEventListener('change', updateScheduleUI);

    // Tool select change
    document.getElementById('tiToolSelect').addEventListener('change', (e) => {
        renderToolArgs(e.target.value);
    });

    // Autocomplete on textareas
    setupAutocomplete('jobContext', 'contextAutocomplete', ['@']);
    setupAutocomplete('jobContent', 'contentAutocomplete', ['@', '#']);

    // Event delegation for dynamic elements
    document.getElementById('jobsList').addEventListener('click', (e) => {
        const item = e.target.closest('[data-action="select-job"]');
        if (item) selectJob(item.dataset.id);
    });
    document.getElementById('toolInstancesList').addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;
        if (btn.dataset.action === 'edit-ti') editToolInstance(btn.dataset.id);
        else if (btn.dataset.action === 'delete-ti') deleteToolInstance(btn.dataset.id);
    });
    document.getElementById('executionsList').addEventListener('click', (e) => {
        const item = e.target.closest('[data-action="toggle-exec"]');
        if (item) toggleExecution(item);
    });

    // Load data
    await Promise.all([
        loadJobs(),
        loadToolInstances(),
        loadTools(),
        loadSkills()
    ]);
});

// ===== Utility =====
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function showToast(message, type = 'info') {
    const container = document.getElementById('toast-container');
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.textContent = message;
    container.appendChild(toast);
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(100%)';
        setTimeout(() => toast.remove(), 200);
    }, 3000);
}

function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleString();
}

function formatDuration(ms) {
    if (!ms && ms !== 0) return '';
    if (ms < 1000) return `${ms}ms`;
    const s = Math.floor(ms / 1000);
    if (s < 60) return `${s}s`;
    const m = Math.floor(s / 60);
    return `${m}m ${s % 60}s`;
}

// ===== Jobs =====
async function loadJobs() {
    try {
        const res = await authFetch('/api/v1/cron-job');
        if (!res.ok) throw new Error('Failed to load jobs');
        const raw = await res.json();
        // Transform API response: parse scheduleJson/contextJson into schedule/context
        jobs = raw.map(job => ({
            ...job,
            schedule: parseJsonField(job.scheduleJson),
            context: parseContextJson(job.contextJson)
        }));
        renderJobsList();
    } catch (err) {
        console.error('loadJobs error:', err);
        jobs = [];
        renderJobsList();
    }
}

function parseJsonField(jsonStr) {
    if (!jsonStr) return null;
    try { return JSON.parse(jsonStr); } catch { return null; }
}

function parseContextJson(jsonStr) {
    if (!jsonStr) return '';
    try {
        const arr = JSON.parse(jsonStr);
        if (Array.isArray(arr)) return arr.map(s => `@${s}`).join(' ');
        return jsonStr;
    } catch { return jsonStr; }
}

function renderJobsList() {
    const list = document.getElementById('jobsList');
    if (jobs.length === 0) {
        list.innerHTML = '<div class="empty-state" style="padding: 24px; font-size: 0.85rem;">No cron jobs yet.</div>';
        return;
    }
    list.innerHTML = jobs.map(job => {
        const isActive = job.id === currentJobId;
        const scheduleLabel = getScheduleLabel(job.schedule);
        const isEnabled = job.wakeMode !== 'Manual';
        return `
            <div class="job-item ${isActive ? 'active' : ''}" data-action="select-job" data-id="${escapeHtml(job.id)}">
                <div class="job-status-dot ${isEnabled ? 'active' : 'disabled'}"></div>
                <div class="job-item-info">
                    <div class="job-item-name">${escapeHtml(job.name || 'Untitled')}</div>
                    <div class="job-item-schedule">${escapeHtml(scheduleLabel)}</div>
                </div>
            </div>
        `;
    }).join('');
}

function getScheduleLabel(schedule) {
    if (!schedule) return 'No schedule';
    const freq = schedule.frequency || 'Daily';
    const time = schedule.timeOfDay || '09:00';
    const tz = schedule.timezone || 'UTC';
    return `${freq} at ${time} (${tz})`;
}

async function selectJob(id) {
    currentJobId = id;
    renderJobsList();

    const job = jobs.find(j => j.id === id);
    if (!job) return;

    // Show editor, hide empty state
    document.getElementById('emptyState').classList.add('hidden');
    document.getElementById('jobEditor').classList.remove('hidden');

    // Populate fields
    document.getElementById('jobName').value = job.name || '';
    document.getElementById('jobWakeMode').value = job.wakeMode || 'Both';

    // Schedule
    const sched = job.schedule || {};
    document.getElementById('schedFrequency').value = sched.frequency || 'Daily';
    document.getElementById('schedTime').value = sched.timeOfDay || '09:00';
    document.getElementById('schedTimezone').value = sched.timezone || 'UTC';
    if (sched.frequency === 'Weekly' && sched.daysOfWeek) {
        document.querySelectorAll('#weeklyDays input[type="checkbox"]').forEach(cb => {
            cb.checked = sched.daysOfWeek.includes(parseInt(cb.value));
        });
    }
    if (sched.frequency === 'Monthly' && sched.dayOfMonth) {
        document.getElementById('schedDayOfMonth').value = sched.dayOfMonth;
    }
    updateScheduleUI();

    // Context and content
    document.getElementById('jobContext').value = job.context || '';
    document.getElementById('jobContent').value = job.content || '';

    // Hide tool instance editor
    cancelToolInstance();

    // Load executions
    await loadExecutions(id);
}

function newJob() {
    currentJobId = null;
    renderJobsList();

    document.getElementById('emptyState').classList.add('hidden');
    document.getElementById('jobEditor').classList.remove('hidden');

    // Clear form
    document.getElementById('jobName').value = '';
    document.getElementById('jobWakeMode').value = 'Both';
    document.getElementById('schedFrequency').value = 'Daily';
    document.getElementById('schedTime').value = '09:00';
    document.getElementById('schedTimezone').value = 'UTC';
    document.getElementById('jobContext').value = '';
    document.getElementById('jobContent').value = '';
    updateScheduleUI();

    cancelToolInstance();
    document.getElementById('executionsList').innerHTML = '';

    // Focus name
    setTimeout(() => document.getElementById('jobName').focus(), 50);
}

async function saveJob() {
    const name = document.getElementById('jobName').value.trim();
    if (!name) {
        showToast('Job name is required', 'error');
        return;
    }

    const schedule = buildScheduleObject();
    const contextText = document.getElementById('jobContext').value;
    // Convert "@skill1 @skill2" to JSON array ["skill1", "skill2"]
    const skillNames = contextText.split(/\s+/)
        .map(s => s.replace(/^@/, ''))
        .filter(s => s.length > 0);
    const payload = {
        name,
        wakeMode: document.getElementById('jobWakeMode').value,
        scheduleJson: JSON.stringify(schedule),
        contextJson: skillNames.length > 0 ? JSON.stringify(skillNames) : null,
        content: document.getElementById('jobContent').value
    };

    try {
        let res;
        if (currentJobId) {
            res = await authFetch(`/api/v1/cron-job/${currentJobId}`, {
                method: 'PUT',
                body: JSON.stringify(payload)
            });
        } else {
            res = await authFetch('/api/v1/cron-job', {
                method: 'POST',
                body: JSON.stringify(payload)
            });
        }

        if (!res.ok) {
            const err = await res.text();
            throw new Error(err || 'Save failed');
        }

        const saved = await res.json();
        showToast('Job saved', 'success');

        await loadJobs();
        if (!currentJobId) {
            currentJobId = saved.id;
        }
        renderJobsList();
    } catch (err) {
        console.error('saveJob error:', err);
        showToast(`Save failed: ${err.message}`, 'error');
    }
}

async function deleteJob() {
    if (!currentJobId) return;
    if (!confirm('Delete this cron job?')) return;

    try {
        const res = await authFetch(`/api/v1/cron-job/${currentJobId}`, {
            method: 'DELETE'
        });
        if (!res.ok) throw new Error('Delete failed');

        showToast('Job deleted', 'success');
        currentJobId = null;

        document.getElementById('emptyState').classList.remove('hidden');
        document.getElementById('jobEditor').classList.add('hidden');

        await loadJobs();
    } catch (err) {
        console.error('deleteJob error:', err);
        showToast(`Delete failed: ${err.message}`, 'error');
    }
}

async function runJob() {
    if (!currentJobId) return;

    try {
        const res = await authFetch(`/api/v1/cron-job/${currentJobId}/execute`, {
            method: 'POST'
        });
        if (!res.ok) throw new Error('Execution failed');

        showToast('Job execution started', 'success');
        // Reload executions after a brief delay
        setTimeout(() => loadExecutions(currentJobId), 1000);
    } catch (err) {
        console.error('runJob error:', err);
        showToast(`Run failed: ${err.message}`, 'error');
    }
}

function buildScheduleObject() {
    const freq = document.getElementById('schedFrequency').value;
    const obj = {
        frequency: freq,
        timeOfDay: document.getElementById('schedTime').value,
        timezone: document.getElementById('schedTimezone').value,
        isEnabled: true
    };

    if (freq === 'Weekly') {
        const days = [];
        document.querySelectorAll('#weeklyDays input[type="checkbox"]:checked').forEach(cb => {
            days.push(parseInt(cb.value));
        });
        obj.daysOfWeek = days;
    }

    if (freq === 'Monthly') {
        obj.dayOfMonth = parseInt(document.getElementById('schedDayOfMonth').value) || 1;
    }

    return obj;
}

function updateScheduleUI() {
    const freq = document.getElementById('schedFrequency').value;
    const weeklyDays = document.getElementById('weeklyDays');
    const monthlyDay = document.getElementById('monthlyDay');

    weeklyDays.classList.toggle('hidden', freq !== 'Weekly');
    monthlyDay.classList.toggle('hidden', freq !== 'Monthly');
}

// ===== Tool Instances =====
async function loadToolInstances() {
    try {
        const res = await authFetch('/api/v1/tool-instance');
        if (!res.ok) throw new Error('Failed to load tool instances');
        toolInstances = await res.json();
        renderToolInstancesList();
    } catch (err) {
        console.error('loadToolInstances error:', err);
        toolInstances = [];
        renderToolInstancesList();
    }
}

function renderToolInstancesList() {
    const list = document.getElementById('toolInstancesList');
    if (toolInstances.length === 0) {
        list.innerHTML = '';
        return;
    }
    list.innerHTML = toolInstances.map(ti => `
        <div class="ti-item" data-id="${escapeHtml(ti.id)}">
            <span class="ti-item-name">${escapeHtml(ti.name)}</span>
            <span class="ti-item-tool">${escapeHtml(ti.toolName || ti.tool || '')}</span>
            <span class="ti-item-desc">${escapeHtml(ti.description || '')}</span>
            <div class="ti-item-actions">
                <button class="btn-icon" data-action="edit-ti" data-id="${escapeHtml(ti.id)}" title="Edit">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
                        <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                </button>
                <button class="btn-icon" data-action="delete-ti" data-id="${escapeHtml(ti.id)}" title="Delete">
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"/>
                        <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/>
                    </svg>
                </button>
            </div>
        </div>
    `).join('');
}

function newToolInstance() {
    editingToolInstanceId = null;
    document.getElementById('tiId').value = '';
    document.getElementById('tiName').value = '';
    document.getElementById('tiToolSelect').value = '';
    document.getElementById('tiDescription').value = '';
    document.getElementById('tiArgs').innerHTML = '';
    document.getElementById('toolInstanceEditor').classList.remove('hidden');
    setTimeout(() => document.getElementById('tiName').focus(), 50);
}

function editToolInstance(id) {
    const ti = toolInstances.find(t => t.id === id);
    if (!ti) return;

    editingToolInstanceId = id;
    document.getElementById('tiId').value = id;
    document.getElementById('tiName').value = ti.name || '';
    document.getElementById('tiToolSelect').value = ti.toolName || ti.tool || '';
    document.getElementById('tiDescription').value = ti.description || '';

    renderToolArgs(ti.toolName || ti.tool || '', ti.args || ti.arguments || {});
    document.getElementById('toolInstanceEditor').classList.remove('hidden');
}

async function saveToolInstance() {
    const name = document.getElementById('tiName').value.trim();
    const toolName = document.getElementById('tiToolSelect').value;

    if (!name) {
        showToast('Tool instance name is required', 'error');
        return;
    }
    if (!toolName) {
        showToast('Please select a tool', 'error');
        return;
    }

    // Gather args from tag editor
    const args = { ..._toolArgValues }; // saved values from tag editor interactions
    // Also capture currently visible editor input
    const activeInput = document.querySelector('#argEditorPanel [data-arg-key]');
    if (activeInput) {
        const key = activeInput.dataset.argKey;
        if (key && activeInput.value) args[key] = activeInput.value;
    }
    // Remove empty values
    Object.keys(args).forEach(k => { if (!args[k]) delete args[k]; });

    const payload = {
        name,
        toolName,
        argsJson: JSON.stringify(args),
        description: document.getElementById('tiDescription').value.trim()
    };

    try {
        let res;
        if (editingToolInstanceId) {
            res = await authFetch(`/api/v1/tool-instance/${editingToolInstanceId}`, {
                method: 'PUT',
                body: JSON.stringify(payload)
            });
        } else {
            res = await authFetch('/api/v1/tool-instance', {
                method: 'POST',
                body: JSON.stringify(payload)
            });
        }

        if (!res.ok) {
            const err = await res.text();
            throw new Error(err || 'Save failed');
        }

        showToast('Tool instance saved', 'success');
        cancelToolInstance();
        await loadToolInstances();
    } catch (err) {
        console.error('saveToolInstance error:', err);
        showToast(`Save failed: ${err.message}`, 'error');
    }
}

function cancelToolInstance() {
    editingToolInstanceId = null;
    document.getElementById('toolInstanceEditor').classList.add('hidden');
}

async function deleteToolInstance(id) {
    if (!confirm('Delete this tool instance?')) return;

    try {
        const res = await authFetch(`/api/v1/tool-instance/${id}`, {
            method: 'DELETE'
        });
        if (!res.ok) throw new Error('Delete failed');

        showToast('Tool instance deleted', 'success');
        if (editingToolInstanceId === id) {
            cancelToolInstance();
        }
        await loadToolInstances();
    } catch (err) {
        console.error('deleteToolInstance error:', err);
        showToast(`Delete failed: ${err.message}`, 'error');
    }
}

// ===== Tools / Skills Loading =====
async function loadTools() {
    try {
        const res = await authFetch('/api/v1/cron-job/tools');
        if (!res.ok) throw new Error('Failed to load tools');
        tools = await res.json();
        populateToolSelect();
    } catch (err) {
        console.error('loadTools error:', err);
        tools = [];
    }
}

function populateToolSelect() {
    const select = document.getElementById('tiToolSelect');
    // Keep the placeholder option
    const placeholder = select.querySelector('option[value=""]');
    select.innerHTML = '';
    if (placeholder) select.appendChild(placeholder);
    else {
        const opt = document.createElement('option');
        opt.value = '';
        opt.textContent = 'Select tool...';
        select.appendChild(opt);
    }

    tools.forEach(tool => {
        const opt = document.createElement('option');
        opt.value = tool.name || tool;
        opt.textContent = tool.name || tool;
        select.appendChild(opt);
    });
}

async function loadSkills() {
    try {
        const res = await authFetch('/api/v1/cron-job/skills');
        if (!res.ok) throw new Error('Failed to load skills');
        skills = await res.json();
    } catch (err) {
        console.error('loadSkills error:', err);
        skills = [];
    }
}

function renderToolArgs(toolName, existingArgs) {
    const container = document.getElementById('tiArgs');
    container.innerHTML = '';

    if (!toolName) return;

    const tool = tools.find(t => (t.name || t) === toolName);
    if (!tool || !tool.parameters) {
        container.innerHTML = '<div class="arg-hint">No parameters for this tool.</div>';
        return;
    }

    const schema = tool.parameters;
    const properties = schema.properties || {};
    const requiredList = schema.required || [];
    // Initialize arg values from existing args
    _toolArgValues = {};
    const parsed = existingArgs || {};
    Object.entries(parsed).forEach(([k, v]) => { if (v) _toolArgValues[k] = v; });

    if (Object.keys(properties).length === 0) {
        container.innerHTML = '<div class="arg-hint">This tool has no parameters.</div>';
        return;
    }

    // Sort: required first, then optional
    const entries = Object.entries(properties).sort(([aKey], [bKey]) => {
        const aReq = requiredList.includes(aKey) ? 0 : 1;
        const bReq = requiredList.includes(bKey) ? 0 : 1;
        return aReq - bReq;
    });

    // Tag cloud
    const tagsDiv = document.createElement('div');
    tagsDiv.className = 'arg-tags';

    // Editor panel (shown when a tag is clicked)
    const editorDiv = document.createElement('div');
    editorDiv.className = 'arg-editor-panel';
    editorDiv.id = 'argEditorPanel';

    entries.forEach(([key, prop]) => {
        const isRequired = requiredList.includes(key);
        const val = parsed[key] || '';
        const hasValue = val !== '';

        // Determine tag status class
        let statusClass = 'arg-tag-optional'; // blue
        if (isRequired && !hasValue) statusClass = 'arg-tag-required'; // red
        else if (isRequired && hasValue) statusClass = 'arg-tag-ready'; // green
        else if (hasValue) statusClass = 'arg-tag-filled'; // teal

        const tag = document.createElement('span');
        tag.className = `arg-tag ${statusClass}`;
        tag.dataset.argKey = key;
        tag.textContent = key;

        // Badge for count/type
        const badge = document.createElement('span');
        badge.className = 'arg-tag-badge';
        badge.textContent = prop.type || 'str';
        tag.appendChild(badge);

        tag.onclick = () => openArgEditor(key, prop, isRequired, editorDiv, tagsDiv);
        tagsDiv.appendChild(tag);
    });

    container.appendChild(tagsDiv);
    container.appendChild(editorDiv);

    // Auto-fill from AppConfig > UserConfig > UserPreference
    autoFillToolArgs(entries, tagsDiv);
}

function openArgEditor(key, prop, isRequired, editorDiv, tagsDiv) {
    // Save current editor value before switching
    const prevInput = editorDiv.querySelector('[data-arg-key] input, [data-arg-key] textarea, [data-arg-key] select');
    if (prevInput) {
        const prevKey = prevInput.closest('[data-arg-key]')?.dataset.argKey || prevInput.dataset.argKey;
        if (prevKey && prevInput.value) _toolArgValues[prevKey] = prevInput.value;
    }

    // Highlight active tag
    tagsDiv.querySelectorAll('.arg-tag').forEach(t => t.classList.remove('arg-tag-active'));
    const activeTag = tagsDiv.querySelector(`[data-arg-key="${key}"]`);
    if (activeTag) activeTag.classList.add('arg-tag-active');

    // Restore saved value
    const currentVal = _toolArgValues[key] || '';

    editorDiv.innerHTML = `
        <div class="arg-editor-row" data-arg-key="${escapeHtml(key)}">
            <div class="arg-editor-header">
                <span class="arg-editor-name">${escapeHtml(key)}</span>
                ${isRequired ? '<span class="arg-editor-required">required</span>' : '<span class="arg-editor-optional">optional</span>'}
                <span class="arg-editor-type">${escapeHtml(prop.type || 'string')}</span>
            </div>
            ${prop.description ? `<div class="arg-editor-desc">${escapeHtml(prop.description)}</div>` : ''}
            <div class="arg-editor-input">
                ${buildArgInput(key, prop, currentVal)}
            </div>
        </div>
    `;

    // Focus the input
    const input = editorDiv.querySelector('input, textarea, select');
    if (input) {
        input.focus();
        // Update tag status on input change
        input.addEventListener('input', () => updateTagStatus(key, input.value, isRequired, tagsDiv));
        input.addEventListener('change', () => updateTagStatus(key, input.value, isRequired, tagsDiv));
    }
}

function buildArgInput(key, prop, val) {
    if (prop.type === 'boolean') {
        return `<select data-arg-key="${escapeHtml(key)}">
            <option value="" ${!val ? 'selected' : ''}>-- not set --</option>
            <option value="true" ${val === 'true' ? 'selected' : ''}>true</option>
            <option value="false" ${val === 'false' ? 'selected' : ''}>false</option>
        </select>`;
    }
    if (prop.type === 'integer' || prop.type === 'number') {
        return `<input type="number" data-arg-key="${escapeHtml(key)}" value="${escapeHtml(val)}" placeholder="Enter ${escapeHtml(key)}...">`;
    }
    if (prop.description && prop.description.length > 100) {
        return `<textarea data-arg-key="${escapeHtml(key)}" rows="4" placeholder="Enter ${escapeHtml(key)}...">${escapeHtml(val)}</textarea>`;
    }
    return `<input type="text" data-arg-key="${escapeHtml(key)}" value="${escapeHtml(val)}" placeholder="Enter ${escapeHtml(key)}...">`;
}

async function autoFillToolArgs(entries, tagsDiv) {
    // Collect defaultKeys that don't already have values
    const keysToLookup = [];
    const keyToParam = {}; // defaultKey -> paramName
    entries.forEach(([key, prop]) => {
        if (_toolArgValues[key]) return; // already has value
        const defaultKey = prop.defaultKey || key; // use defaultKey if available, else try param name itself
        keysToLookup.push(defaultKey);
        keyToParam[defaultKey] = key;
    });

    if (keysToLookup.length === 0) return;

    try {
        const res = await authFetch('/api/v1/tool-instance/args/suggest', {
            method: 'POST',
            body: JSON.stringify(keysToLookup)
        });
        if (!res.ok) return;

        const suggestions = await res.json();
        for (const [lookupKey, value] of Object.entries(suggestions)) {
            const paramName = keyToParam[lookupKey];
            if (paramName && value && !_toolArgValues[paramName]) {
                _toolArgValues[paramName] = value;
                // Update tag status
                const isRequired = entries.find(([k]) => k === paramName);
                updateTagStatus(paramName, value, !!isRequired, tagsDiv);
            }
        }
    } catch (err) {
        console.error('Auto-fill tool args error:', err);
    }
}

function updateTagStatus(key, value, isRequired, tagsDiv) {
    const tag = tagsDiv.querySelector(`[data-arg-key="${key}"]`);
    if (!tag) return;
    tag.classList.remove('arg-tag-required', 'arg-tag-ready', 'arg-tag-optional', 'arg-tag-filled');
    const hasValue = value && value.trim() !== '';
    if (isRequired && !hasValue) tag.classList.add('arg-tag-required');
    else if (isRequired && hasValue) tag.classList.add('arg-tag-ready');
    else if (hasValue) tag.classList.add('arg-tag-filled');
    else tag.classList.add('arg-tag-optional');
}

// ===== Executions =====
async function loadExecutions(jobId) {
    const list = document.getElementById('executionsList');

    if (!jobId) {
        list.innerHTML = '';
        return;
    }

    try {
        const res = await authFetch(`/api/v1/cron-job/executions?cronJobId=${jobId}`);
        if (!res.ok) throw new Error('Failed to load executions');
        const executions = await res.json();
        renderExecutions(executions);
    } catch (err) {
        console.error('loadExecutions error:', err);
        list.innerHTML = '<div class="exec-empty">Failed to load executions.</div>';
    }
}

function renderExecutions(executions) {
    const list = document.getElementById('executionsList');

    if (!executions || executions.length === 0) {
        list.innerHTML = '<div class="exec-empty">No executions yet.</div>';
        return;
    }

    list.innerHTML = executions.map(exec => {
        const status = (exec.status || 'unknown').toLowerCase();
        const badgeClass = status === 'succeeded' || status === 'completed' ? 'success'
            : status === 'failed' ? 'failed'
            : status === 'running' ? 'running'
            : 'pending';
        const duration = exec.durationMs ? formatDuration(exec.durationMs) : '';

        return `
            <div class="exec-item" data-action="toggle-exec">
                <div class="exec-item-header">
                    <span class="exec-status-badge ${badgeClass}">${escapeHtml(exec.status || 'Unknown')}</span>
                    <span class="exec-time">${escapeHtml(formatDate(exec.startedAt || exec.createdAt))}</span>
                    ${duration ? `<span class="exec-duration">${escapeHtml(duration)}</span>` : ''}
                    <svg class="exec-expand-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="6 9 12 15 18 9"/>
                    </svg>
                </div>
                <div class="exec-item-body">
                    <div class="exec-output">${escapeHtml(exec.output || exec.result || 'No output')}</div>
                </div>
            </div>
        `;
    }).join('');
}

function toggleExecution(el) {
    el.classList.toggle('expanded');
}

// ===== Autocomplete =====
function setupAutocomplete(textareaId, dropdownId, triggers) {
    const textarea = document.getElementById(textareaId);
    const dropdown = document.getElementById(dropdownId);

    textarea.addEventListener('input', (e) => {
        handleAutocompleteInput(textarea, dropdown, triggers);
    });

    textarea.addEventListener('keydown', (e) => {
        if (!dropdown.classList.contains('hidden')) {
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                acIndex = Math.min(acIndex + 1, acItems.length - 1);
                updateAcHighlight(dropdown);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                acIndex = Math.max(acIndex - 1, 0);
                updateAcHighlight(dropdown);
            } else if (e.key === 'Enter' || e.key === 'Tab') {
                if (acIndex >= 0 && acIndex < acItems.length) {
                    e.preventDefault();
                    insertAcItem(textarea, dropdown, acItems[acIndex]);
                }
            } else if (e.key === 'Escape') {
                hideAutocomplete(dropdown);
            }
        }
    });

    textarea.addEventListener('blur', () => {
        // Delay to allow click on dropdown item
        setTimeout(() => hideAutocomplete(dropdown), 150);
    });
}

function handleAutocompleteInput(textarea, dropdown, triggers) {
    const value = textarea.value;
    const cursorPos = textarea.selectionStart;

    // Find the trigger character before cursor
    let triggerPos = -1;
    let trigger = '';

    for (let i = cursorPos - 1; i >= 0; i--) {
        const ch = value[i];
        if (ch === ' ' || ch === '\n' || ch === '\t') break;
        if (triggers.includes(ch)) {
            triggerPos = i;
            trigger = ch;
            break;
        }
    }

    if (triggerPos === -1) {
        hideAutocomplete(dropdown);
        return;
    }

    const query = value.substring(triggerPos + 1, cursorPos).toLowerCase();
    acTriggerChar = trigger;
    acStartPos = triggerPos;

    let items = [];
    if (trigger === '@') {
        items = skills.filter(s => {
            const name = (s.name || s).toLowerCase();
            return name.includes(query);
        }).map(s => ({
            name: s.name || s,
            description: s.description || ''
        }));
    } else if (trigger === '#') {
        items = toolInstances.filter(ti => {
            const name = (ti.name || '').toLowerCase();
            return name.includes(query);
        }).map(ti => ({
            name: ti.name,
            description: ti.toolName || ti.tool || ''
        }));
    }

    if (items.length === 0) {
        hideAutocomplete(dropdown);
        return;
    }

    acItems = items;
    acIndex = 0;
    acActiveDropdown = dropdown;

    dropdown.innerHTML = items.map((item, i) => `
        <div class="ac-item ${i === 0 ? 'active' : ''}" data-index="${i}"
             onmousedown="insertAcItemByIndex(event, '${textarea.id}', '${dropdown.id}', ${i})">
            <span class="ac-name">${acTriggerChar}${escapeHtml(item.name)}</span>
            ${item.description ? `<span class="ac-desc">${escapeHtml(item.description)}</span>` : ''}
        </div>
    `).join('');

    dropdown.classList.remove('hidden');
}

function updateAcHighlight(dropdown) {
    dropdown.querySelectorAll('.ac-item').forEach((el, i) => {
        el.classList.toggle('active', i === acIndex);
    });

    // Scroll into view
    const activeEl = dropdown.querySelector('.ac-item.active');
    if (activeEl) {
        activeEl.scrollIntoView({ block: 'nearest' });
    }
}

function insertAcItem(textarea, dropdown, item) {
    const value = textarea.value;
    const cursorPos = textarea.selectionStart;

    const before = value.substring(0, acStartPos);
    const after = value.substring(cursorPos);
    const inserted = acTriggerChar + item.name + ' ';

    textarea.value = before + inserted + after;
    const newPos = before.length + inserted.length;
    textarea.setSelectionRange(newPos, newPos);
    textarea.focus();

    hideAutocomplete(dropdown);
}

function insertAcItemByIndex(event, textareaId, dropdownId, index) {
    event.preventDefault();
    const textarea = document.getElementById(textareaId);
    const dropdown = document.getElementById(dropdownId);
    if (acItems[index]) {
        insertAcItem(textarea, dropdown, acItems[index]);
    }
}

function hideAutocomplete(dropdown) {
    if (dropdown) {
        dropdown.classList.add('hidden');
        dropdown.innerHTML = '';
    }
    acActiveDropdown = null;
    acItems = [];
    acIndex = -1;
}
