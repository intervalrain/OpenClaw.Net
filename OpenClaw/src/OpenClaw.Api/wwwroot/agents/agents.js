// Agents Page — browse agents, run DAGs, manage definitions

const AGENTS_API = '/api/v1/agents';
const EXECUTION_API = '/api/v1/agent-execution';

// State
let allAgents = [];
let currentRunAgent = null;

// DOM Elements
const agentList = document.getElementById('agentList');
const dagJsonInput = document.getElementById('dagJsonInput');
const dagResults = document.getElementById('dagResults');
const definitionList = document.getElementById('definitionList');
const tabs = document.querySelectorAll('.tab');
const tabContents = document.querySelectorAll('.tab-content');

// Modal
const agentRunModal = document.getElementById('agentRunModal');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    initTabs();
    initModals();
    initAdminFeatures();

    document.getElementById('runDagBtn').addEventListener('click', runDag);

    await loadAgents();
});

// --------------- Tabs ---------------

function initTabs() {
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            const tabId = tab.dataset.tab;

            tabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');

            tabContents.forEach(content => {
                content.classList.toggle('active', content.id === `${tabId}Tab`);
            });

            // Lazy-load tab data
            if (tabId === 'definitions' && definitionList.querySelector('.loading')) {
                loadDefinitions();
            }
        });
    });
}

// --------------- Modals ---------------

function initModals() {
    document.getElementById('closeRunModal').addEventListener('click', closeRunModal);
    document.getElementById('cancelRunModal').addEventListener('click', closeRunModal);
    document.getElementById('executeAgentBtn').addEventListener('click', () => {
        if (currentRunAgent) runAgent(currentRunAgent);
    });

    agentRunModal.addEventListener('click', e => {
        if (e.target === agentRunModal) closeRunModal();
    });
}

function closeRunModal() {
    agentRunModal.classList.remove('active');
    currentRunAgent = null;
}

// --------------- Admin Features ---------------

function initAdminFeatures() {
    const user = typeof getCurrentUser === 'function' ? getCurrentUser() : null;
    if (!user || !user.roles) return;

    const isAdmin = user.roles.some(r =>
        r.toLowerCase() === 'superadmin' || r.toLowerCase() === 'admin'
    );

    if (isAdmin) {
        const toolbar = document.getElementById('definitionsToolbar');
        toolbar.style.display = '';
        document.getElementById('reloadDefinitionsBtn').addEventListener('click', reloadDefinitions);
    }
}

// --------------- Agents ---------------

async function loadAgents() {
    try {
        const res = await authFetch(AGENTS_API);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        allAgents = await res.json();
        renderAgents();
    } catch (err) {
        agentList.innerHTML = `<div class="empty-state"><h3>Failed to load agents</h3><p>${escapeHtml(err.message)}</p></div>`;
    }
}

function renderAgents() {
    if (!allAgents || allAgents.length === 0) {
        agentList.innerHTML = '<div class="empty-state"><h3>No agents found</h3><p>No agents are registered yet.</p></div>';
        return;
    }

    agentList.innerHTML = allAgents.map(agent => {
        const typeClass = (agent.type || 'deterministic').toLowerCase();
        const typeLabel = agent.type || 'deterministic';
        const version = agent.version ? `v${agent.version}` : '';
        return `
            <div class="agent-card" onclick="openRunModal('${escapeHtml(agent.name)}', '${escapeAttr(agent.description || '')}')">
                <div class="agent-card-header">
                    <h3 class="agent-card-name">${escapeHtml(agent.name)}</h3>
                    <span class="type-badge ${typeClass}">${escapeHtml(typeLabel)}</span>
                </div>
                <p class="agent-card-desc">${escapeHtml(agent.description || 'No description')}</p>
                <div class="agent-card-meta">
                    ${version ? `<span class="agent-card-version">${escapeHtml(version)}</span>` : ''}
                </div>
            </div>
        `;
    }).join('');
}

// --------------- Run Agent ---------------

function openRunModal(name, description) {
    currentRunAgent = name;
    document.getElementById('runModalTitle').textContent = `Run: ${name}`;
    document.getElementById('runModalDescription').textContent = description || '';
    document.getElementById('agentInput').value = '{}';
    document.getElementById('agentRunResult').style.display = 'none';
    document.getElementById('executeAgentBtn').disabled = false;
    document.getElementById('executeAgentBtn').textContent = 'Run';
    agentRunModal.classList.add('active');
}

async function runAgent(name) {
    const inputEl = document.getElementById('agentInput');
    const resultEl = document.getElementById('agentRunResult');
    const runBtn = document.getElementById('executeAgentBtn');

    let body;
    try {
        body = JSON.parse(inputEl.value || '{}');
    } catch (e) {
        alert('Invalid JSON input: ' + e.message);
        return;
    }

    runBtn.disabled = true;
    runBtn.textContent = 'Running...';
    resultEl.style.display = 'none';

    try {
        const res = await authFetch(`${EXECUTION_API}/run/${encodeURIComponent(name)}`, {
            method: 'POST',
            body: JSON.stringify(body)
        });

        const data = await res.json();
        renderAgentResult(data, res.ok);
    } catch (err) {
        renderAgentResult({ status: 'failed', output: err.message, timeline: [] }, false);
    } finally {
        runBtn.disabled = false;
        runBtn.textContent = 'Run';
    }
}

function renderAgentResult(data, ok) {
    const resultEl = document.getElementById('agentRunResult');
    resultEl.style.display = '';

    // Status
    const statusEl = document.getElementById('resultStatus');
    const status = data.status || (ok ? 'completed' : 'failed');
    statusEl.textContent = status;
    statusEl.className = 'node-status ' + status.toLowerCase();

    // Tokens
    const tokensEl = document.getElementById('resultTokens');
    tokensEl.textContent = data.totalTokens != null ? data.totalTokens : (data.tokens != null ? data.tokens : '--');

    // Output
    const outputEl = document.getElementById('resultOutput');
    const outputData = data.output != null ? data.output : data.result;
    if (typeof outputData === 'object') {
        outputEl.textContent = JSON.stringify(outputData, null, 2);
    } else {
        outputEl.textContent = outputData != null ? String(outputData) : '--';
    }

    // Timeline
    const timelineEl = document.getElementById('resultTimeline');
    const timeline = data.timeline || data.events || [];
    if (timeline.length > 0) {
        timelineEl.innerHTML = timeline.map(ev => `
            <div class="timeline-item">
                <span class="timeline-time">${escapeHtml(formatTime(ev.timestamp || ev.time))}</span>
                <span class="timeline-event">${escapeHtml(ev.event || ev.message || ev.description || '')}</span>
            </div>
        `).join('');
    } else {
        timelineEl.innerHTML = '<div class="text-muted" style="font-size:0.82rem;">No timeline events.</div>';
    }
}

// --------------- DAG Runner ---------------

async function runDag() {
    const runBtn = document.getElementById('runDagBtn');
    let graph;

    try {
        graph = JSON.parse(dagJsonInput.value);
    } catch (e) {
        alert('Invalid JSON: ' + e.message);
        return;
    }

    runBtn.disabled = true;
    runBtn.textContent = 'Running...';
    dagResults.innerHTML = '<div class="loading">Executing DAG...</div>';

    try {
        const res = await authFetch(`${EXECUTION_API}/run-dag`, {
            method: 'POST',
            body: JSON.stringify(graph)
        });

        const data = await res.json();
        renderDagResults(data, res.ok);
    } catch (err) {
        dagResults.innerHTML = `<div class="empty-state"><h3>Execution failed</h3><p>${escapeHtml(err.message)}</p></div>`;
    } finally {
        runBtn.disabled = false;
        runBtn.textContent = 'Run DAG';
    }
}

function renderDagResults(data, ok) {
    const nodes = data.nodeResults || data.nodes || [];

    if (nodes.length === 0 && !ok) {
        dagResults.innerHTML = `<div class="empty-state"><h3>DAG execution failed</h3><p>${escapeHtml(data.error || data.message || 'Unknown error')}</p></div>`;
        return;
    }

    let html = '';

    // Overall status
    if (data.status) {
        html += `<div style="margin-bottom:0.75rem;"><span class="node-status ${(data.status).toLowerCase()}">${escapeHtml(data.status)}</span></div>`;
    }

    // Node cards
    html += nodes.map(node => {
        const status = node.status || 'pending';
        const output = node.output != null ? (typeof node.output === 'object' ? JSON.stringify(node.output, null, 2) : String(node.output)) : '';
        return `
            <div class="dag-node-card">
                <div class="dag-node-header">
                    <span class="dag-node-id">${escapeHtml(node.id || node.nodeId || '?')}</span>
                    <span class="node-status ${status.toLowerCase()}">${escapeHtml(status)}</span>
                </div>
                <div class="dag-node-agent">${escapeHtml(node.agentName || '')}</div>
                ${output ? `<div class="dag-node-output">${escapeHtml(output)}</div>` : ''}
            </div>
        `;
    }).join('');

    // Timeline
    const timeline = data.timeline || data.events || [];
    if (timeline.length > 0) {
        html += '<div class="timeline-section" style="margin-top:1rem;"><label>Timeline</label><div class="timeline">';
        html += timeline.map(ev => `
            <div class="timeline-item">
                <span class="timeline-time">${escapeHtml(formatTime(ev.timestamp || ev.time))}</span>
                <span class="timeline-event">${escapeHtml(ev.event || ev.message || ev.description || '')}</span>
            </div>
        `).join('');
        html += '</div></div>';
    }

    dagResults.innerHTML = html || '<div class="empty-state"><p>No results returned.</p></div>';
}

// --------------- Definitions ---------------

async function loadDefinitions() {
    try {
        const res = await authFetch(`${AGENTS_API}/definitions`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const defs = await res.json();
        renderDefinitions(defs);
    } catch (err) {
        definitionList.innerHTML = `<div class="empty-state"><h3>Failed to load definitions</h3><p>${escapeHtml(err.message)}</p></div>`;
    }
}

function renderDefinitions(defs) {
    const items = Array.isArray(defs) ? defs : [];
    if (items.length === 0) {
        definitionList.innerHTML = '<div class="empty-state"><h3>No definitions</h3><p>No AGENT.md definitions found.</p></div>';
        return;
    }

    definitionList.innerHTML = items.map(def => `
        <div class="definition-card">
            <div>
                <div class="definition-name">${escapeHtml(def.name || def.fileName || '?')}</div>
                ${def.path ? `<div class="definition-path">${escapeHtml(def.path)}</div>` : ''}
            </div>
            <div class="definition-meta">
                ${def.type ? `<span class="type-badge ${(def.type || '').toLowerCase()}">${escapeHtml(def.type)}</span>` : ''}
            </div>
        </div>
    `).join('');
}

async function reloadDefinitions() {
    const btn = document.getElementById('reloadDefinitionsBtn');
    btn.disabled = true;
    btn.textContent = 'Reloading...';

    try {
        const res = await authFetch(`${AGENTS_API}/definitions/reload`, { method: 'POST' });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        await loadDefinitions();
    } catch (err) {
        alert('Failed to reload definitions: ' + err.message);
    } finally {
        btn.disabled = false;
        btn.textContent = 'Reload Definitions';
    }
}

// --------------- Utilities ---------------

function escapeHtml(str) {
    if (!str) return '';
    const d = document.createElement('div');
    d.textContent = String(str);
    return d.innerHTML;
}

function escapeAttr(str) {
    return String(str || '').replace(/'/g, "\\'").replace(/"/g, '&quot;');
}

function formatTime(ts) {
    if (!ts) return '--';
    try {
        const d = new Date(ts);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    } catch {
        return String(ts);
    }
}
