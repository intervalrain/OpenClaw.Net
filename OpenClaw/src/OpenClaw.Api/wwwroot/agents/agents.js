// ── Agents Page ──

let agents = [];
let availableTools = [];
let selectedAgentId = null;
let dagRendered = false;

const API = '/api/v1/agents';
const headers = () => ({
    'Authorization': `Bearer ${getToken()}`,
    'Content-Type': 'application/json'
});

// ── Init ──

document.addEventListener('DOMContentLoaded', async () => {
    initTopHeader('agents');

    document.querySelectorAll('.tab').forEach(tab => {
        tab.addEventListener('click', () => {
            document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
            tab.classList.add('active');
            document.getElementById(`${tab.dataset.tab}-panel`).classList.add('active');
            if (tab.dataset.tab === 'dag') renderDag();
        });
    });

    document.getElementById('create-agent-btn').addEventListener('click', createNewAgent);
    document.getElementById('save-agent-btn').addEventListener('click', saveAgent);
    document.getElementById('delete-agent-btn').addEventListener('click', deleteAgent);

    await loadData();
});

// ── Data ──

async function loadData() {
    try {
        const [agentsRes, toolsRes] = await Promise.all([
            fetch(API, { headers: headers() }),
            fetch(`${API}/tools`, { headers: headers() })
        ]);
        if (agentsRes.ok) agents = await agentsRes.json();
        if (toolsRes.ok) availableTools = await toolsRes.json();
        renderAgentList();
    } catch (err) {
        console.error('Failed to load data:', err);
    }
}

// ── Sidebar ──

function renderAgentList() {
    const list = document.getElementById('agent-list');
    if (agents.length === 0) {
        list.innerHTML = '<div class="empty-hint">No agents yet</div>';
        return;
    }
    list.innerHTML = agents.map(a => `
        <div class="agent-item ${a.id === selectedAgentId ? 'active' : ''}" data-id="${a.id}">
            <div class="agent-item-name">${a.name}</div>
            <div class="agent-item-desc">${a.description || ''}</div>
        </div>
    `).join('');

    list.querySelectorAll('.agent-item').forEach(el => {
        el.addEventListener('click', () => selectAgent(el.dataset.id));
    });
}

// ── Editor ──

function selectAgent(id) {
    selectedAgentId = id;
    const agent = agents.find(a => a.id === id);
    if (!agent) return;

    document.getElementById('empty-state').style.display = 'none';
    document.getElementById('agent-editor').style.display = 'block';

    document.getElementById('agent-name').value = agent.name;
    document.getElementById('agent-description').value = agent.description;
    document.getElementById('agent-system-prompt').value = agent.systemPrompt;
    document.getElementById('agent-max-iterations').value = agent.maxIterations;

    renderToolSelector(agent.tools);
    renderSubAgentSelector(agent.subAgentIds);
    renderAgentList();
}

function createNewAgent() {
    selectedAgentId = null;
    document.getElementById('empty-state').style.display = 'none';
    document.getElementById('agent-editor').style.display = 'block';
    document.getElementById('agent-name').value = '';
    document.getElementById('agent-description').value = '';
    document.getElementById('agent-system-prompt').value = '';
    document.getElementById('agent-max-iterations').value = '10';
    renderToolSelector([]);
    renderSubAgentSelector([]);
    renderAgentList();
}

function renderToolSelector(selectedTools) {
    const set = new Set(selectedTools || []);
    const el = document.getElementById('tool-selector');
    el.innerHTML = availableTools.map(t => `
        <label class="chip-label">
            <input type="checkbox" value="${t.name}" ${set.has(t.name) ? 'checked' : ''}>
            <span class="chip">${t.name}</span>
        </label>
    `).join('');
}

function renderSubAgentSelector(selectedIds) {
    const set = new Set((selectedIds || []).map(String));
    const el = document.getElementById('subagent-selector');
    const otherAgents = agents.filter(a => a.id !== selectedAgentId);
    if (otherAgents.length === 0) {
        el.innerHTML = '<span class="empty-hint">No other agents available</span>';
        return;
    }
    el.innerHTML = otherAgents.map(a => `
        <label class="chip-label">
            <input type="checkbox" value="${a.id}" ${set.has(a.id) ? 'checked' : ''}>
            <span class="chip">${a.name}</span>
        </label>
    `).join('');
}

function getEditorValues() {
    const tools = [...document.querySelectorAll('#tool-selector input:checked')].map(c => c.value);
    const subAgentIds = [...document.querySelectorAll('#subagent-selector input:checked')].map(c => c.value);
    return {
        name: document.getElementById('agent-name').value.trim(),
        description: document.getElementById('agent-description').value.trim(),
        systemPrompt: document.getElementById('agent-system-prompt').value,
        tools,
        subAgentIds,
        maxIterations: parseInt(document.getElementById('agent-max-iterations').value) || 10
    };
}

async function saveAgent() {
    const data = getEditorValues();
    if (!data.name) { alert('Name is required'); return; }

    try {
        let res;
        if (selectedAgentId) {
            res = await fetch(`${API}/${selectedAgentId}`, {
                method: 'PUT', headers: headers(), body: JSON.stringify(data)
            });
        } else {
            res = await fetch(API, {
                method: 'POST', headers: headers(), body: JSON.stringify(data)
            });
        }

        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            alert(err.title || err.detail || 'Failed to save');
            return;
        }

        const saved = await res.json();
        selectedAgentId = saved.id;
        await loadData();
        selectAgent(saved.id);
        dagRendered = false;
    } catch (err) {
        alert('Failed to save: ' + err.message);
    }
}

async function deleteAgent() {
    if (!selectedAgentId) return;
    if (!confirm('Delete this agent?')) return;

    try {
        const res = await fetch(`${API}/${selectedAgentId}`, {
            method: 'DELETE', headers: headers()
        });
        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            alert(err.title || err.detail || 'Failed to delete');
            return;
        }
        selectedAgentId = null;
        document.getElementById('agent-editor').style.display = 'none';
        document.getElementById('empty-state').style.display = 'flex';
        await loadData();
        dagRendered = false;
    } catch (err) {
        alert('Failed to delete: ' + err.message);
    }
}

// ── DAG ──

async function renderDag() {
    try {
        const res = await fetch(`${API}/dag`, { headers: headers() });
        if (!res.ok) throw new Error(`API ${res.status}`);
        const dag = await res.json();

        if (dag.nodes.length === 0) {
            document.getElementById('dag-chart').innerHTML =
                '<p style="color: var(--text-muted); text-align: center; margin-top: 40px;">No agents yet. Create agents to see the DAG.</p>';
            return;
        }

        let code = 'graph TD\n';
        for (const node of dag.nodes) {
            const id = sanitizeId(node.id);
            const label = escapeLabel(node.label);
            code += node.type === 'root'
                ? `  ${id}(["${label}"])\n`
                : `  ${id}["${label}"]\n`;
        }
        for (const edge of dag.edges) {
            code += `  ${sanitizeId(edge.from)} -->|${escapeLabel(edge.label || '')}| ${sanitizeId(edge.to)}\n`;
        }
        code += '  classDef root fill:#2a1a3a,stroke:#bc8cff,color:#e6edf3\n';
        code += '  classDef agent fill:#1a2a3a,stroke:#58a6ff,color:#e6edf3\n';
        for (const node of dag.nodes) {
            code += `  class ${sanitizeId(node.id)} ${node.type}\n`;
        }

        const existing = document.getElementById('dag-svg');
        if (existing) existing.remove();

        mermaid.initialize({
            startOnLoad: false,
            theme: document.documentElement.getAttribute('data-theme') === 'light' ? 'default' : 'dark',
            flowchart: { curve: 'basis', padding: 15 },
            securityLevel: 'loose'
        });

        const { svg } = await mermaid.render('dag-svg', code);
        document.getElementById('dag-chart').innerHTML = svg;
        dagRendered = true;
    } catch (err) {
        console.error('DAG render failed:', err);
        document.getElementById('dag-chart').innerHTML =
            `<p style="color: var(--text-muted); text-align: center;">Failed to load DAG: ${err.message}</p>`;
    }
}

function sanitizeId(id) { return 'n' + id.replace(/[^a-zA-Z0-9]/g, ''); }
function escapeLabel(t) { return t.replace(/"/g, "'").replace(/[<>{}]/g, ''); }

window.addEventListener('themechange', () => { dagRendered = false; });
