// ── Agents Page ──

let allTools = [];
let allSkills = [];
let dagData = null;
let dagRendered = false;

// ── Init ──

document.addEventListener('DOMContentLoaded', async () => {
    initTopHeader('agents');

    // Tab switching
    document.querySelectorAll('.tab').forEach(tab => {
        tab.addEventListener('click', () => {
            document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
            tab.classList.add('active');
            document.getElementById(`${tab.dataset.tab}-panel`).classList.add('active');

            if (tab.dataset.tab === 'dag' && !dagRendered) {
                renderDag();
            }
        });
    });

    await loadData();
});

// ── Data Loading ──

async function loadData() {
    const headers = { 'Authorization': `Bearer ${getToken()}` };

    try {
        const [toolsRes, skillsRes] = await Promise.all([
            fetch('/api/v1/agents/tools', { headers }),
            fetch('/api/v1/agents/skills', { headers })
        ]);

        if (toolsRes.ok) allTools = await toolsRes.json();
        if (skillsRes.ok) allSkills = await skillsRes.json();

        renderToolList();
        renderSkillList();
    } catch (err) {
        console.error('Failed to load agents data:', err);
    }
}

// ── Sidebar Rendering ──

function renderToolList() {
    const list = document.getElementById('tool-list');
    list.innerHTML = allTools.map(t => `
        <div class="agent-item" data-type="tool" data-name="${t.name}">
            <span>${t.name}</span>
            ${t.isStreaming ? '<span class="badge badge-streaming">stream</span>' : ''}
            <span class="badge badge-${t.permissionLevel.toLowerCase()}">${t.permissionLevel}</span>
        </div>
    `).join('');

    list.querySelectorAll('.agent-item').forEach(el => {
        el.addEventListener('click', () => selectAgent('tool', el.dataset.name));
    });
}

function renderSkillList() {
    const list = document.getElementById('skill-list');
    list.innerHTML = allSkills.map(s => `
        <div class="agent-item" data-type="skill" data-name="${s.name}">
            <span>@${s.name}</span>
            <span class="badge" style="background: var(--accent); color: #fff;">${s.tools.length} tools</span>
        </div>
    `).join('');

    list.querySelectorAll('.agent-item').forEach(el => {
        el.addEventListener('click', () => selectAgent('skill', el.dataset.name));
    });
}

// ── Detail View ──

function selectAgent(type, name) {
    // Update active state
    document.querySelectorAll('.agent-item').forEach(el => el.classList.remove('active'));
    document.querySelector(`.agent-item[data-type="${type}"][data-name="${name}"]`)?.classList.add('active');

    const panel = document.getElementById('detail-panel');

    if (type === 'tool') {
        const tool = allTools.find(t => t.name === name);
        if (!tool) return;
        panel.innerHTML = renderToolDetail(tool);
    } else {
        const skill = allSkills.find(s => s.name === name);
        if (!skill) return;
        panel.innerHTML = renderSkillDetail(skill);
    }
}

function renderToolDetail(tool) {
    let paramsHtml = '<p style="color: var(--text-muted); font-size: 0.85rem;">No parameters</p>';

    if (tool.parameters?.properties) {
        const required = new Set(tool.parameters.required || []);
        const rows = Object.entries(tool.parameters.properties).map(([name, prop]) => `
            <tr>
                <td class="param-name">${name} ${required.has(name) ? '<span class="param-required">*</span>' : ''}</td>
                <td>${prop.type || '-'}</td>
                <td>${prop.description || '-'}</td>
            </tr>
        `).join('');

        paramsHtml = `
            <table class="param-table">
                <thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>
        `;
    }

    return `
        <div class="detail-header">
            <h2>${tool.name} <span class="badge badge-${tool.permissionLevel.toLowerCase()}">${tool.permissionLevel}</span>
            ${tool.isStreaming ? '<span class="badge badge-streaming">streaming</span>' : ''}</h2>
            <p>${tool.description}</p>
        </div>
        <div class="detail-section">
            <h4>Properties</h4>
            <div class="detail-meta">
                <span class="meta-chip">Permission: ${tool.permissionLevel}</span>
                <span class="meta-chip">Streaming: ${tool.isStreaming ? 'Yes' : 'No'}</span>
            </div>
        </div>
        <div class="detail-section">
            <h4>Parameters</h4>
            ${paramsHtml}
        </div>
    `;
}

function renderSkillDetail(skill) {
    const toolTags = skill.tools.map(t => `<span class="tool-tag">${t}</span>`).join('');

    return `
        <div class="detail-header">
            <h2>@${skill.name}</h2>
            <p>${skill.description}</p>
        </div>
        <div class="detail-section">
            <h4>Required Tools (${skill.tools.length})</h4>
            <div>${toolTags || '<span style="color: var(--text-muted);">None</span>'}</div>
        </div>
    `;
}

// ── DAG Rendering ──

async function renderDag() {
    try {
        const headers = { 'Authorization': `Bearer ${getToken()}` };
        const res = await fetch('/api/v1/agents/dag', { headers });
        if (!res.ok) return;
        dagData = await res.json();

        // Build Mermaid flowchart from DAG data
        let mermaidCode = 'graph LR\n';

        // Style classes
        mermaidCode += '  classDef tool fill:#1a3a2a,stroke:#3fb950,color:#e6edf3\n';
        mermaidCode += '  classDef skill fill:#1a2a3a,stroke:#58a6ff,color:#e6edf3\n';
        mermaidCode += '  classDef pipeline fill:#2a1a3a,stroke:#bc8cff,color:#e6edf3\n';

        // Nodes
        for (const node of dagData.nodes) {
            const shape = node.type === 'skill' ? `([${node.label}])`
                        : node.type === 'pipeline' ? `{{${node.label}}}`
                        : `[${node.label}]`;
            mermaidCode += `  ${sanitizeId(node.id)}${shape}\n`;
            mermaidCode += `  class ${sanitizeId(node.id)} ${node.type}\n`;
        }

        // Edges
        for (const edge of dagData.edges) {
            const label = edge.label ? `|${edge.label}|` : '';
            mermaidCode += `  ${sanitizeId(edge.from)} -->${label} ${sanitizeId(edge.to)}\n`;
        }

        const container = document.getElementById('dag-chart');
        container.textContent = mermaidCode;

        // Re-initialize mermaid
        mermaid.initialize({
            startOnLoad: false,
            theme: document.documentElement.getAttribute('data-theme') === 'light' ? 'default' : 'dark',
            flowchart: { curve: 'basis', padding: 15 },
            securityLevel: 'loose'
        });

        const { svg } = await mermaid.render('dag-svg', mermaidCode);
        container.innerHTML = svg;
        dagRendered = true;

    } catch (err) {
        console.error('Failed to render DAG:', err);
        document.getElementById('dag-chart').innerHTML =
            '<p style="color: var(--text-muted); text-align: center;">Failed to load DAG</p>';
    }
}

function sanitizeId(id) {
    return id.replace(/[^a-zA-Z0-9_]/g, '_');
}

// Re-render DAG on theme change
window.addEventListener('themechange', () => {
    if (dagRendered) {
        dagRendered = false;
        if (document.querySelector('.tab[data-tab="dag"]')?.classList.contains('active')) {
            renderDag();
        }
    }
});
