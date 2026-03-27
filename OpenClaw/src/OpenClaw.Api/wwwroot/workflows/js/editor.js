// Workflow Editor with Cytoscape.js

// State
let workflowId = null;
let workflow = null;
let cy = null;
let selectedNode = null;
let isDirty = false;
let skills = [];
let currentExecution = null;
let executionPollingInterval = null;

// Undo/Redo history
const undoStack = [];
const redoStack = [];
const MAX_HISTORY = 50;

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    initCytoscape();
    await loadSkills();
    await loadWorkflow();
    setupEventListeners();
    setupDragAndDrop();
    setupOnboarding();
});

// Initialize Cytoscape
function initCytoscape() {
    cy = cytoscape({
        container: document.getElementById('cy'),
        style: [
            // Node styles
            {
                selector: 'node',
                style: {
                    'label': 'data(label)',
                    'text-valign': 'bottom',
                    'text-margin-y': 8,
                    'font-size': 12,
                    'width': 50,
                    'height': 50,
                    'border-width': 2,
                    'border-color': '#ccc'
                }
            },
            {
                selector: 'node[type="start"]',
                style: {
                    'background-color': '#27ae60',
                    'shape': 'ellipse',
                    'width': 40,
                    'height': 40,
                    'label': 'Start'
                }
            },
            {
                selector: 'node[type="end"]',
                style: {
                    'background-color': '#e74c3c',
                    'shape': 'ellipse',
                    'width': 40,
                    'height': 40,
                    'label': 'End'
                }
            },
            {
                selector: 'node[type="skill"]',
                style: {
                    'background-color': '#3498db',
                    'shape': 'roundrectangle',
                    'width': 60,
                    'height': 40
                }
            },
            {
                selector: 'node[type="approval"]',
                style: {
                    'background-color': '#f39c12',
                    'shape': 'diamond',
                    'width': 50,
                    'height': 50
                }
            },
            {
                selector: 'node[type="wait"]',
                style: {
                    'background-color': '#9b59b6',
                    'shape': 'ellipse',
                    'width': 45,
                    'height': 45
                }
            },
            // Execution status styles (from canvas polling)
            {
                selector: 'node.cy-node-running',
                style: {
                    'border-color': '#3498db',
                    'border-width': 4,
                    'shadow-blur': 10,
                    'shadow-color': '#3498db',
                    'shadow-opacity': 0.5,
                    'shadow-offset-x': 0,
                    'shadow-offset-y': 0
                }
            },
            {
                selector: 'node.cy-node-completed',
                style: {
                    'border-color': '#27ae60',
                    'border-width': 3
                }
            },
            {
                selector: 'node.cy-node-failed',
                style: {
                    'border-color': '#e74c3c',
                    'border-width': 3
                }
            },
            {
                selector: 'node.cy-node-waiting',
                style: {
                    'border-color': '#f39c12',
                    'border-width': 4,
                    'border-style': 'dashed'
                }
            },
            // Legacy styles (for modal execution view)
            {
                selector: 'node.running',
                style: {
                    'border-color': '#f1c40f',
                    'border-width': 4
                }
            },
            {
                selector: 'node.completed',
                style: {
                    'border-color': '#27ae60',
                    'border-width': 3
                }
            },
            {
                selector: 'node.failed',
                style: {
                    'border-color': '#e74c3c',
                    'border-width': 3
                }
            },
            {
                selector: 'node.waiting',
                style: {
                    'border-color': '#3498db',
                    'border-width': 4,
                    'border-style': 'dashed'
                }
            },
            {
                selector: 'node:selected',
                style: {
                    'border-color': '#9b59b6',
                    'border-width': 3
                }
            },
            // Edge styles
            {
                selector: 'edge',
                style: {
                    'width': 2,
                    'line-color': '#95a5a6',
                    'target-arrow-color': '#95a5a6',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier'
                }
            },
            {
                selector: 'edge:selected',
                style: {
                    'line-color': '#9b59b6',
                    'target-arrow-color': '#9b59b6'
                }
            },
            // Edge creation mode - highlight source node
            {
                selector: 'node.edge-source',
                style: {
                    'border-color': '#e74c3c',
                    'border-width': 4,
                    'border-style': 'dashed'
                }
            }
        ],
        layout: { name: 'preset' },
        minZoom: 0.3,
        maxZoom: 3,
        wheelSensitivity: 0.3
    });

    // Event handlers
    cy.on('tap', 'node', function(evt) {
        selectNode(evt.target);
    });

    // Allow selecting edges too
    cy.on('tap', 'edge', function(evt) {
        cy.elements().unselect();
        evt.target.select();
        selectedNode = null;
        renderProperties(null);
    });

    cy.on('tap', function(evt) {
        if (evt.target === cy) {
            deselectNode();
        }
    });

    cy.on('drag', 'node', function() {
        markDirty();
    });

    // Edge creation by right-click or Shift+click on node
    cy.on('cxttap', 'node', function(evt) {
        const sourceNode = evt.target;
        startEdgeCreation(sourceNode);
    });

    // Also support Shift+click for edge creation (more discoverable)
    cy.on('tap', 'node', function(evt) {
        if (evt.originalEvent.shiftKey) {
            const sourceNode = evt.target;
            startEdgeCreation(sourceNode);
            evt.stopPropagation();
        }
    });
}

// Load skills from API
async function loadSkills() {
    try {
        // Load both Skills (markdown-defined) and Tools (C# function calling)
        const [skillsRes, toolsRes] = await Promise.all([
            authFetch('/api/v1/workflow/skills'),
            authFetch('/api/v1/workflow/tools')
        ]);

        const mdSkills = skillsRes.ok ? await skillsRes.json() : [];
        const tools = toolsRes.ok ? await toolsRes.json() : [];

        // Skills come first, then Tools as fallback
        skills = [
            ...mdSkills.map(s => ({ ...s, _type: 'skill' })),
            ...tools.map(t => ({ ...t, _type: 'tool' }))
        ];
        renderSkillList();
    } catch (error) {
        console.error('Error loading skills:', error);
        document.getElementById('skillList').innerHTML = '<div class="empty-state">Failed to load skills</div>';
    }
}

function renderSkillList() {
    const container = document.getElementById('skillList');
    if (skills.length === 0) {
        container.innerHTML = '<div class="empty-state">No skills available</div>';
        return;
    }

    const mdSkills = skills.filter(s => s._type === 'skill');
    const tools = skills.filter(s => s._type === 'tool');

    let html = '';
    if (mdSkills.length > 0) {
        html += mdSkills.map(s => `
            <div class="skill-item skill-md" draggable="true" data-type="skill" data-skill="${escapeHtml(s.name)}" title="${escapeHtml(s.description || '')}">
                <span class="skill-badge">Skill</span> ${escapeHtml(s.name)}
            </div>
        `).join('');
    }
    if (tools.length > 0) {
        if (mdSkills.length > 0) html += '<div class="skill-list-divider">Tools</div>';
        html += tools.map(t => `
            <div class="skill-item skill-tool" draggable="true" data-type="skill" data-skill="${escapeHtml(t.name)}" title="${escapeHtml(t.description || '')}">
                <span class="tool-badge">Tool</span> ${escapeHtml(t.name)}
            </div>
        `).join('');
    }

    container.innerHTML = html;

    // Add drag handlers
    container.querySelectorAll('.skill-item').forEach(item => {
        item.addEventListener('dragstart', handleDragStart);
    });
}

function truncateText(text, maxLength) {
    if (!text || text.length <= maxLength) return text;
    return text.slice(0, maxLength) + '...';
}

// Load workflow
async function loadWorkflow() {
    const params = new URLSearchParams(window.location.search);
    workflowId = params.get('id');
    const executionId = params.get('execution');

    if (executionId) {
        // Viewing execution
        await loadExecution(executionId);
        return;
    }

    if (!workflowId) {
        // New workflow - show default
        document.getElementById('workflowName').value = 'New Workflow';
        return;
    }

    try {
        const response = await authFetch(`/api/v1/workflow/${workflowId}`);
        if (!response.ok) throw new Error('Failed to load workflow');

        workflow = await response.json();
        document.getElementById('workflowName').value = workflow.name;

        // Load graph
        loadGraph(workflow.definition);

        // Hide hint if nodes exist
        if (workflow.definition.nodes.length > 2) {
            document.getElementById('canvasHint').classList.add('hidden');
        }
    } catch (error) {
        console.error('Error loading workflow:', error);
        alert('Failed to load workflow: ' + error.message);
    }
}

function loadGraph(definition) {
    cy.elements().remove();

    // Add nodes
    definition.nodes.forEach(node => {
        cy.add({
            group: 'nodes',
            data: {
                id: node.id,
                type: node.type,
                label: node.label || node.type,
                skillName: node.skillName,
                args: node.args,
                approvalName: node.approvalName,
                description: node.description,
                scheduledBehavior: node.scheduledBehavior,
                timeoutSeconds: node.timeoutSeconds,
                // Wait node properties
                waitType: node.waitType,
                durationSeconds: node.durationSeconds,
                waitUntil: node.waitUntil
            },
            position: node.position
        });
    });

    // Add edges
    definition.edges.forEach(edge => {
        cy.add({
            group: 'edges',
            data: {
                id: edge.id,
                source: edge.source,
                target: edge.target,
                condition: edge.condition
            }
        });
    });

    cy.fit(50);

    // Save initial state for undo
    undoStack.length = 0;
    redoStack.length = 0;
    saveState();
}

// Export graph to WorkflowGraph format
function exportGraph() {
    const nodes = cy.nodes().map(node => {
        const data = node.data();
        const pos = node.position();

        // IMPORTANT: 'type' MUST be the FIRST property for .NET polymorphic JSON deserialization
        const base = {
            type: data.type,
            id: data.id,
            position: { x: Math.round(pos.x), y: Math.round(pos.y) },
            label: data.label
        };

        if (data.type === 'skill') {
            // Clean internal fields from args before saving
            const cleanArgs = {};
            if (data.args) {
                for (const [key, val] of Object.entries(data.args)) {
                    const clean = {};
                    if (val.filledValue !== undefined && val.filledValue !== null) clean.filledValue = val.filledValue;
                    if (val.configKey) clean.configKey = val.configKey;
                    if (val.inputMapping) clean.inputMapping = val.inputMapping;
                    if (val.userPreferenceKey) clean.userPreferenceKey = val.userPreferenceKey;
                    cleanArgs[key] = clean;
                }
            }
            return {
                type: data.type,
                id: data.id,
                position: { x: Math.round(pos.x), y: Math.round(pos.y) },
                label: data.label,
                skillName: data.skillName || '',
                args: cleanArgs,
                timeoutSeconds: data.timeoutSeconds || 300
            };
        }

        if (data.type === 'approval') {
            return {
                type: data.type,
                id: data.id,
                position: { x: Math.round(pos.x), y: Math.round(pos.y) },
                label: data.label,
                approvalName: data.approvalName || data.label || 'Approval',
                description: data.description || '',
                scheduledBehavior: data.scheduledBehavior || 'WaitForApproval'
            };
        }

        if (data.type === 'wait') {
            return {
                type: data.type,
                id: data.id,
                position: { x: Math.round(pos.x), y: Math.round(pos.y) },
                label: data.label,
                waitType: data.waitType || 'duration',
                durationSeconds: data.durationSeconds || 60,
                waitUntil: data.waitUntil || null
            };
        }

        return base;
    });

    const edges = cy.edges().map(edge => ({
        id: edge.data('id'),
        source: edge.data('source'),
        target: edge.data('target'),
        condition: edge.data('condition')
    }));

    return {
        nodes,
        edges
    };
}

// Select node
function selectNode(node) {
    selectedNode = node;
    cy.nodes().unselect();
    node.select();
    renderProperties(node);
}

function deselectNode() {
    selectedNode = null;
    cy.elements().unselect();
    renderProperties(null);
}

// Render properties panel
function renderProperties(node) {
    const panel = document.getElementById('propertiesPanel');

    if (!node) {
        panel.innerHTML = `
            <div class="panel-empty">
                <p>Select a node to edit its properties</p>
            </div>
        `;
        return;
    }

    const data = node.data();
    const type = data.type;

    let content = `
        <div class="properties-content">
            <div class="properties-header">
                <h3>${type.charAt(0).toUpperCase() + type.slice(1)} Node</h3>
                ${type !== 'start' && type !== 'end' ? `
                    <button class="btn-delete-node" onclick="deleteSelectedNode()">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>
                        </svg>
                    </button>
                ` : ''}
            </div>

            <div class="property-group">
                <label>Label</label>
                <input type="text" id="propLabel" value="${escapeHtml(data.label || '')}" onchange="updateNodeProperty('label', this.value)">
            </div>
    `;

    if (type === 'skill') {
        content += renderSkillProperties(data);
    } else if (type === 'approval') {
        content += renderApprovalProperties(data);
    } else if (type === 'wait') {
        content += renderWaitProperties(data);
    }

    content += '</div>';
    panel.innerHTML = content;
}

function renderSkillProperties(data) {
    const skillOptions = skills.map(s =>
        `<option value="${escapeHtml(s.name)}" ${s.name === data.skillName ? 'selected' : ''}>${escapeHtml(s.name)} (${s._type})</option>`
    ).join('');

    const skill = skills.find(s => s.name === data.skillName);

    let html = `
        <div class="property-group">
            <label>Skill / Tool</label>
            <select id="propSkillName" onchange="updateNodeProperty('skillName', this.value); updateArgsEditor()">
                <option value="">Select...</option>
                ${skillOptions}
            </select>
        </div>
    `;

    if (!skill) return html;

    // Show description
    if (skill.description) {
        html += `
            <div class="skill-description markdown-content">
                ${renderMarkdown(skill.description)}
            </div>
        `;
    }

    html += `
        <div class="property-group">
            <label>Timeout (seconds)</label>
            <input type="number" id="propTimeout" value="${data.timeoutSeconds || 300}" min="1" max="3600" onchange="updateNodeProperty('timeoutSeconds', parseInt(this.value))">
        </div>
    `;

    if (skill._type === 'skill') {
        // Markdown Skill — show tools list and instructions preview
        if (skill.tools && skill.tools.length > 0) {
            html += `
                <div class="property-group">
                    <label>Uses Tools</label>
                    <div class="skill-tools-list">${skill.tools.map(t => `<span class="tool-tag">${escapeHtml(t)}</span>`).join(' ')}</div>
                </div>
            `;
        }
        if (skill.instructions) {
            html += `
                <div class="property-group">
                    <label>Instructions <small class="text-muted">(click to expand)</small></label>
                    <div class="skill-instructions collapsed markdown-content" onclick="this.classList.toggle('collapsed')">
                        ${renderMarkdown(skill.instructions)}
                    </div>
                </div>
            `;
        }
        // Skill nodes can have a free-form input context
        html += `
            <div class="property-group">
                <label>Additional Context (optional)</label>
                <textarea id="propSkillContext" rows="3" placeholder="Extra instructions or context for this skill execution..."
                    oninput="updateNodeProperty('skillContext', this.value)">${escapeHtml(data.skillContext || '')}</textarea>
            </div>
        `;
    } else {
        // Direct Tool — show parameter args editor
        const hasArgs = skill.parameters && skill.parameters.properties && Object.keys(skill.parameters.properties).length > 0;
        if (hasArgs) {
            html += `
                <div class="args-section">
                    <h4>Arguments</h4>
                    ${renderArgsEditor(skill.parameters.properties, skill.parameters.required || [], data.args || {})}
                </div>
            `;
        } else {
            html += `
                <div class="args-section">
                    <h4>Arguments</h4>
                    <p class="text-muted">This tool has no parameters</p>
                </div>
            `;
        }
    }

    return html;
}

function renderArgsEditor(properties, requiredList, currentArgs) {
    if (!properties || Object.keys(properties).length === 0) {
        return '<p class="text-muted">This skill has no parameters</p>';
    }

    // Ensure all skill params exist in node args (so they get saved even if empty)
    if (selectedNode) {
        const args = selectedNode.data('args') || {};
        let changed = false;
        for (const name of Object.keys(properties)) {
            if (!(name in args)) {
                args[name] = {};
                changed = true;
            }
        }
        if (changed) {
            selectedNode.data('args', args);
            Object.assign(currentArgs, args);
        }
    }

    // Trigger auto-fill from ConfigStore/UserPreference for empty args
    autoFillArgs(properties, currentArgs);

    return Object.entries(properties).map(([name, param]) => {
        const argSource = currentArgs[name] || {};
        const activeSource = getActiveArgSource(argSource);
        const isRequired = Array.isArray(requiredList) && requiredList.includes(name);
        const autoFilled = argSource._autoFilled;
        const currentValue = getArgValue(argSource, activeSource);
        const hasValue = currentValue !== '';

        // Color class: required+empty=red, required+filled=green, optional=blue, ai=purple
        let colorClass = 'arg-optional';
        if (activeSource === 'ai') colorClass = 'arg-ai';
        else if (isRequired && !hasValue) colorClass = 'arg-required-empty';
        else if (isRequired && hasValue) colorClass = 'arg-required-filled';

        return `
            <div class="arg-item ${colorClass}" data-arg="${escapeHtml(name)}">
                <div class="arg-header" onclick="toggleArgExpand(this)">
                    <span class="arg-name">
                        ${escapeHtml(name)} ${isRequired ? '<span class="required-mark">*</span>' : ''}
                        ${activeSource === 'ai' ? '<span class="ai-badge">AI</span>' : ''}
                        <span class="arg-expand-icon">&#9654;</span>
                    </span>
                    <span class="arg-type">${escapeHtml(param.type || 'string')}</span>
                </div>
                ${param.description ? `<div class="arg-description markdown-content">${renderMarkdown(param.description)}</div>` : ''}
                <div class="arg-body">
                    <div class="arg-source-tabs">
                        <button class="arg-source-tab ${activeSource === 'filled' ? 'active' : ''}"
                                onclick="event.stopPropagation(); setArgSource('${name}', 'filled')"
                                title="直接填入固定值">
                            Value
                        </button>
                        <button class="arg-source-tab ${activeSource === 'input' ? 'active' : ''}"
                                onclick="event.stopPropagation(); setArgSource('${name}', 'input')"
                                title="從上游節點取值">
                            Input
                        </button>
                        <button class="arg-source-tab ${activeSource === 'ai' ? 'active' : ''}"
                                onclick="event.stopPropagation(); setArgSource('${name}', 'ai')"
                                title="由 AI 根據上游結果自動填入">
                            AI Fill
                        </button>
                    </div>
                    ${activeSource === 'ai' ? '<div class="arg-source-help"><span class="ai-fill-hint">AI will fill this based on upstream node outputs at runtime</span></div>' : ''}
                    ${autoFilled ? `<div class="arg-source-help"><span class="auto-filled-hint">auto-filled from ${escapeHtml(autoFilled)}</span></div>` : ''}
                    ${activeSource !== 'ai' ? `
                    <input type="text" class="arg-value-input"
                           id="arg-${name}"
                           value="${escapeHtml(currentValue)}"
                           placeholder="${getArgPlaceholder(activeSource, param)}"
                           oninput="updateArgValue('${name}', this.value)">
                    ${renderArgExamples(activeSource, param, name)}
                    ` : '<input type="hidden" id="arg-' + name + '" value="">'}
                </div>
            </div>
        `;
    }).join('');
}

// Auto-fill empty args from ConfigStore → UserPreference
// Updates node data and DOM inputs in-place — does NOT re-render the panel
async function autoFillArgs(properties, currentArgs) {
    if (!selectedNode || autoFillArgs._running) return;

    // Build a map of defaultKey → paramName for params that have a defaultKey and no value yet
    const keyToParam = {};
    for (const [name, param] of Object.entries(properties)) {
        if (!param.defaultKey) continue;
        const arg = currentArgs[name];
        if (arg && (arg.filledValue || arg.inputMapping || arg._aiFill)) continue;
        keyToParam[param.defaultKey] = name;
    }

    const keysToLookup = Object.keys(keyToParam);
    if (keysToLookup.length === 0) return;

    autoFillArgs._running = true;
    try {
        const response = await authFetch('/api/v1/workflow/args/suggest', {
            method: 'POST',
            body: JSON.stringify(keysToLookup)
        });
        if (!response.ok) return;

        const suggestions = await response.json();
        if (!suggestions || Object.keys(suggestions).length === 0) return;

        const args = selectedNode.data('args') || {};

        for (const [lookupKey, value] of Object.entries(suggestions)) {
            const paramName = keyToParam[lookupKey];
            if (!paramName || value === null || value === undefined) continue;

            // Only fill if user hasn't typed something in the meantime
            const currentVal = args[paramName];
            if (currentVal && currentVal.filledValue) continue;

            args[paramName] = {
                filledValue: value,
                _autoFilled: lookupKey
            };

            // Update DOM input in-place (no re-render)
            const inputEl = document.getElementById(`arg-${paramName}`);
            if (inputEl && !inputEl.value) {
                inputEl.value = value;
            }
        }

        selectedNode.data('args', args);
    } catch (error) {
        console.error('Auto-fill args error:', error);
    } finally {
        autoFillArgs._running = false;
    }
}

function toggleArgExpand(headerEl) {
    const argItem = headerEl.closest('.arg-item');
    argItem.classList.toggle('arg-expanded');
}

function renderArgExamples(source, param, argName) {
    let examples = [];

    switch (source) {
        case 'filled':
            if (param.type === 'boolean') {
                examples = ['true', 'false'];
            } else if (param.type === 'number' || param.type === 'integer') {
                examples = ['100', '0', '-1'];
            } else if (param.enum && param.enum.length > 0) {
                examples = param.enum.slice(0, 3);
            } else if (param.default) {
                examples = [param.default];
            }
            break;
        case 'input':
            // Get upstream nodes dynamically
            examples = getUpstreamNodeExamples();
            if (examples.length === 0) {
                examples = ['upstream_node.output'];
            }
            break;
    }

    if (examples.length === 0) return '';

    return `
        <div class="arg-examples">
            <span class="examples-label">e.g.</span>
            ${examples.map(ex => `<code class="example-value" onclick="setArgExample('${argName}', '${escapeHtml(ex)}')">${escapeHtml(ex)}</code>`).join('')}
        </div>
    `;
}

function getUpstreamNodeExamples() {
    if (!selectedNode || !cy) return [];

    const examples = [];

    // Get all predecessors (upstream nodes)
    const predecessors = selectedNode.predecessors('node');

    predecessors.forEach(node => {
        const nodeId = node.id();
        const nodeType = node.data('type');

        // Skip start node
        if (nodeType === 'start') return;

        // Add example based on node type
        if (nodeType === 'skill') {
            examples.push(`${nodeId}.output`);
        } else if (nodeType === 'approval') {
            examples.push(`${nodeId}.approved`);
        } else {
            examples.push(`${nodeId}.output`);
        }
    });

    return examples.slice(0, 4); // Limit to 4 examples
}

function getActiveArgSource(argSource) {
    if (argSource._aiFill) return 'ai';
    if (argSource.filledValue !== undefined && argSource.filledValue !== null) return 'filled';
    if (argSource.inputMapping) return 'input';
    return 'filled';
}

function getArgValue(argSource, activeSource) {
    switch (activeSource) {
        case 'filled': return argSource.filledValue || '';
        case 'input': return argSource.inputMapping || '';
        default: return '';
    }
}

function getArgPlaceholder(activeSource, param) {
    switch (activeSource) {
        case 'filled': return param.default || 'Enter value...';
        case 'input': return 'Node output (e.g., nodeId.output)';
        default: return '';
    }
}

function setArgSource(argName, source) {
    if (!selectedNode) return;

    const args = selectedNode.data('args') || {};
    const oldArg = args[argName] || {};
    const oldSource = getActiveArgSource(oldArg);

    // Don't switch if already on this source
    if (oldSource === source) return;

    // When switching source, start fresh (don't carry value across)
    const newArg = {};
    switch (source) {
        case 'filled':
            newArg.filledValue = oldArg._prevFilled || '';
            break;
        case 'input':
            newArg.inputMapping = oldArg._prevInput || '';
            break;
        case 'ai':
            newArg._aiFill = true;
            break;
    }
    // Preserve previous values for switching back
    if (oldSource === 'filled') newArg._prevFilled = oldArg.filledValue || '';
    if (oldSource === 'input') newArg._prevInput = oldArg.inputMapping || '';

    args[argName] = newArg;
    selectedNode.data('args', args);
    markDirty();

    // Re-render but skip auto-fill (user is manually switching)
    autoFillArgs._rendering = true;
    renderProperties(selectedNode);
    autoFillArgs._rendering = false;
}

function updateArgValue(argName, value) {
    if (!selectedNode) return;

    const args = selectedNode.data('args') || {};
    const currentArg = args[argName] || {};
    const activeSource = getActiveArgSource(currentArg);

    switch (activeSource) {
        case 'filled':
            currentArg.filledValue = value;
            // Clear auto-filled hint when user manually edits
            delete currentArg._autoFilled;
            break;
        case 'input':
            currentArg.inputMapping = value;
            break;
    }

    args[argName] = currentArg;
    selectedNode.data('args', args);
    markDirty();
}

function setArgExample(argName, value) {
    const input = document.getElementById(`arg-${argName}`);
    if (input) {
        input.value = value;
        updateArgValue(argName, value);
    }
}

function updateArgsEditor() {
    if (!selectedNode) return;
    const skillName = document.getElementById('propSkillName').value;
    selectedNode.data('skillName', skillName);
    renderProperties(selectedNode);
}

function renderApprovalProperties(data) {
    const behavior = data.scheduledBehavior || 'WaitForApproval';

    return `
        <div class="property-group">
            <label>Approval Name</label>
            <input type="text" id="propApprovalName" value="${escapeHtml(data.approvalName || '')}" onchange="updateNodeProperty('approvalName', this.value)">
        </div>

        <div class="property-group">
            <label>Description</label>
            <textarea id="propDescription" onchange="updateNodeProperty('description', this.value)">${escapeHtml(data.description || '')}</textarea>
        </div>

        <div class="approval-behavior">
            <label>Scheduled Execution Behavior</label>
            <div class="behavior-option">
                <input type="radio" name="behavior" value="WaitForApproval" ${behavior === 'WaitForApproval' ? 'checked' : ''} onchange="updateNodeProperty('scheduledBehavior', this.value)">
                <label>Wait for Approval</label>
            </div>
            <div class="behavior-hint">Pause and notify until manually approved</div>

            <div class="behavior-option">
                <input type="radio" name="behavior" value="AutoApprove" ${behavior === 'AutoApprove' ? 'checked' : ''} onchange="updateNodeProperty('scheduledBehavior', this.value)">
                <label>Auto Approve</label>
            </div>
            <div class="behavior-hint">Automatically approve when run on schedule</div>

            <div class="behavior-option">
                <input type="radio" name="behavior" value="AutoReject" ${behavior === 'AutoReject' ? 'checked' : ''} onchange="updateNodeProperty('scheduledBehavior', this.value)">
                <label>Auto Reject</label>
            </div>
            <div class="behavior-hint">Automatically reject and stop when run on schedule</div>
        </div>
    `;
}

function renderWaitProperties(data) {
    const waitType = data.waitType || 'duration';
    const durationSeconds = data.durationSeconds || 60;

    return `
        <div class="property-group">
            <label>Wait Type</label>
            <select id="propWaitType" onchange="updateNodeProperty('waitType', this.value); renderProperties(selectedNode);">
                <option value="duration" ${waitType === 'duration' ? 'selected' : ''}>Duration</option>
                <option value="until" ${waitType === 'until' ? 'selected' : ''}>Until Time</option>
            </select>
        </div>

        ${waitType === 'duration' ? `
            <div class="property-group">
                <label>Duration (seconds)</label>
                <input type="number" id="propDurationSeconds" value="${durationSeconds}" min="1" max="86400"
                       onchange="updateNodeProperty('durationSeconds', parseInt(this.value))">
                <div class="text-muted" style="font-size: 0.8rem; margin-top: 4px;">
                    ${formatDuration(durationSeconds)}
                </div>
            </div>
            <div class="property-group">
                <label>Quick Set</label>
                <div style="display: flex; gap: 8px; flex-wrap: wrap;">
                    <button class="btn btn-secondary" style="padding: 4px 8px; font-size: 0.8rem;" onclick="setWaitDuration(30)">30s</button>
                    <button class="btn btn-secondary" style="padding: 4px 8px; font-size: 0.8rem;" onclick="setWaitDuration(60)">1m</button>
                    <button class="btn btn-secondary" style="padding: 4px 8px; font-size: 0.8rem;" onclick="setWaitDuration(300)">5m</button>
                    <button class="btn btn-secondary" style="padding: 4px 8px; font-size: 0.8rem;" onclick="setWaitDuration(900)">15m</button>
                    <button class="btn btn-secondary" style="padding: 4px 8px; font-size: 0.8rem;" onclick="setWaitDuration(3600)">1h</button>
                </div>
            </div>
        ` : `
            <div class="property-group">
                <label>Wait Until</label>
                <input type="datetime-local" id="propWaitUntil" value="${data.waitUntil || ''}"
                       onchange="updateNodeProperty('waitUntil', this.value)">
                <div class="text-muted" style="font-size: 0.8rem; margin-top: 4px;">
                    Execution will pause until this time
                </div>
            </div>
        `}

        <div class="property-group" style="margin-top: 16px; padding-top: 16px; border-top: 1px solid var(--border-color);">
            <div class="text-muted" style="font-size: 0.85rem;">
                <strong>Wait Node</strong><br>
                Pauses workflow execution for a specified duration or until a specific time.
                Useful for rate limiting, scheduling delays, or waiting for external processes.
            </div>
        </div>
    `;
}

function formatDuration(seconds) {
    if (seconds < 60) return `${seconds} seconds`;
    if (seconds < 3600) {
        const mins = Math.floor(seconds / 60);
        const secs = seconds % 60;
        return secs > 0 ? `${mins} minute${mins > 1 ? 's' : ''} ${secs} second${secs > 1 ? 's' : ''}` : `${mins} minute${mins > 1 ? 's' : ''}`;
    }
    const hours = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    return mins > 0 ? `${hours} hour${hours > 1 ? 's' : ''} ${mins} minute${mins > 1 ? 's' : ''}` : `${hours} hour${hours > 1 ? 's' : ''}`;
}

function setWaitDuration(seconds) {
    if (!selectedNode) return;
    selectedNode.data('durationSeconds', seconds);
    markDirty();
    renderProperties(selectedNode);
}

function updateNodeProperty(prop, value) {
    if (!selectedNode) return;
    selectedNode.data(prop, value);
    markDirty();
}

function deleteSelectedNode() {
    if (!selectedNode) return;

    const type = selectedNode.data('type');
    if (type === 'start' || type === 'end') {
        alert('Cannot delete start or end nodes');
        return;
    }

    selectedNode.remove();
    deselectNode();
    markDirty();
}

function deleteSelectedElements() {
    const selected = cy.$(':selected');
    if (selected.length === 0) return;

    // Check if trying to delete start/end nodes
    const protectedNodes = selected.filter('node[type="start"], node[type="end"]');
    if (protectedNodes.length > 0) {
        alert('Cannot delete start or end nodes');
        // Remove only non-protected elements
        selected.not(protectedNodes).remove();
    } else {
        selected.remove();
    }

    deselectNode();
    markDirty();
}

// Drag and drop
let dragData = null;

function setupDragAndDrop() {
    const canvas = document.getElementById('cy');

    document.querySelectorAll('.palette-node').forEach(item => {
        item.addEventListener('dragstart', handleDragStart);
    });

    canvas.addEventListener('dragover', handleDragOver);
    canvas.addEventListener('drop', handleDrop);
}

function handleDragStart(e) {
    dragData = {
        type: e.target.dataset.type,
        skillName: e.target.dataset.skill
    };
    e.dataTransfer.effectAllowed = 'copy';
}

function handleDragOver(e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
}

function handleDrop(e) {
    e.preventDefault();

    if (!dragData) return;

    const rect = cy.container().getBoundingClientRect();
    const position = cy.renderer().projectIntoViewport(
        e.clientX - rect.left,
        e.clientY - rect.top
    );

    addNode(dragData.type, { x: position[0], y: position[1] }, dragData.skillName);
    dragData = null;

    document.getElementById('canvasHint').classList.add('hidden');
}

function addNode(type, position, skillName = null) {
    const id = `node_${Date.now()}`;

    const data = {
        id,
        type,
        label: type === 'skill' && skillName ? skillName : type.charAt(0).toUpperCase() + type.slice(1)
    };

    if (type === 'skill') {
        data.skillName = skillName || '';
        data.args = {};
        data.timeoutSeconds = 300;
    }

    if (type === 'approval') {
        data.approvalName = 'Approval';
        data.description = '';
        data.scheduledBehavior = 'WaitForApproval';
    }

    if (type === 'wait') {
        data.waitType = 'duration';
        data.durationSeconds = 60;
        data.waitUntil = null;
    }

    cy.add({
        group: 'nodes',
        data,
        position
    });

    markDirty();
}

// Edge creation
let edgeCreationSource = null;

function startEdgeCreation(sourceNode) {
    edgeCreationSource = sourceNode;
    // Highlight the source node
    sourceNode.addClass('edge-source');
    showConnectionHint(`Click another node to connect from "${sourceNode.data('label')}"`);

    // Setup listeners
    cy.once('tap', 'node', completeEdgeCreation);

    // Cancel on ESC or click on canvas
    const cancelHandler = (evt) => {
        if (evt.target === cy) {
            cancelEdgeCreation();
        }
    };
    cy.once('tap', cancelHandler);

    document.addEventListener('keydown', function escHandler(e) {
        if (e.key === 'Escape') {
            cancelEdgeCreation();
            document.removeEventListener('keydown', escHandler);
        }
    });
}

function cancelEdgeCreation() {
    if (edgeCreationSource) {
        edgeCreationSource.removeClass('edge-source');
        edgeCreationSource = null;
    }
    hideConnectionHint();
}

function completeEdgeCreation(evt) {
    if (!edgeCreationSource) return;

    const targetNode = evt.target;

    // Clean up source highlight
    edgeCreationSource.removeClass('edge-source');

    if (targetNode.id() === edgeCreationSource.id()) {
        edgeCreationSource = null;
        hideConnectionHint();
        return;
    }

    // Check if edge already exists
    const existingEdge = cy.edges().filter(edge =>
        edge.source().id() === edgeCreationSource.id() &&
        edge.target().id() === targetNode.id()
    );

    if (existingEdge.length === 0) {
        cy.add({
            group: 'edges',
            data: {
                id: `edge_${Date.now()}`,
                source: edgeCreationSource.id(),
                target: targetNode.id()
            }
        });
        markDirty();
    }

    edgeCreationSource = null;
    hideConnectionHint();
}

function showConnectionHint(message) {
    const hint = document.getElementById('canvasHint');
    hint.textContent = message;
    hint.classList.remove('hidden');
    hint.classList.add('connection-mode');
}

function hideConnectionHint() {
    const hint = document.getElementById('canvasHint');
    hint.classList.remove('connection-mode');
    // Only hide if there are nodes (otherwise show default hint)
    if (cy.nodes().length > 2) {
        hint.classList.add('hidden');
    } else {
        hint.textContent = 'Drag nodes from the left panel to add them to your workflow';
    }
}

// Auto layout
function autoLayout() {
    cy.layout({
        name: 'dagre',
        rankDir: 'LR',
        nodeSep: 80,
        rankSep: 120,
        padding: 50
    }).run();
    markDirty();
}

// Save workflow
async function saveWorkflow() {
    const name = document.getElementById('workflowName').value.trim();
    if (!name) {
        alert('Please enter a workflow name');
        return;
    }

    const definition = exportGraph();

    try {
        let response;
        const body = {
            name,
            description: workflow?.description || '',
            definition,
            schedule: workflow?.schedule || null,
            isActive: workflow?.isActive ?? true
        };

        if (workflowId) {
            response = await authFetch(`/api/v1/workflow/${workflowId}`, {
                method: 'PUT',
                body: JSON.stringify(body)
            });
        } else {
            response = await authFetch('/api/v1/workflow', {
                method: 'POST',
                body: JSON.stringify(body)
            });
        }

        if (!response.ok) {
            const contentType = response.headers.get('content-type');
            let errorMessage = `HTTP ${response.status}`;
            if (contentType && contentType.includes('application/json')) {
                const error = await response.json();
                // Show full validation details if available
                if (error.errors) {
                    const details = Object.entries(error.errors)
                        .map(([field, msgs]) => `${field}: ${Array.isArray(msgs) ? msgs.join(', ') : msgs}`)
                        .join('\n');
                    errorMessage = `${error.title || 'Validation error'}\n${details}`;
                } else {
                    errorMessage = error.title || error.detail || error.message || JSON.stringify(error);
                }
            } else {
                const text = await response.text();
                if (text) errorMessage = text;
            }
            throw new Error(errorMessage);
        }

        const saved = await response.json();
        workflowId = saved.id;
        workflow = saved;

        // Update URL if new workflow
        if (!window.location.search.includes(saved.id)) {
            window.history.replaceState({}, '', `editor.html?id=${saved.id}`);
        }

        markClean();
        showSaveStatus('Saved');
    } catch (error) {
        console.error('Error saving workflow:', error);
        alert('Failed to save: ' + error.message);
    }
}

function markDirty(skipHistory = false) {
    isDirty = true;
    showSaveStatus('Unsaved changes');
    if (!skipHistory) {
        saveState();
    }
}

function markClean() {
    isDirty = false;
}

// Undo/Redo functionality
function saveState() {
    const state = cy.json().elements;
    undoStack.push(JSON.stringify(state));
    if (undoStack.length > MAX_HISTORY) {
        undoStack.shift();
    }
    // Clear redo stack when new action is performed
    redoStack.length = 0;
}

function undo() {
    if (undoStack.length <= 1) return; // Keep at least initial state

    const currentState = undoStack.pop();
    redoStack.push(currentState);

    const previousState = undoStack[undoStack.length - 1];
    if (previousState) {
        restoreState(previousState);
        markDirty();
    }
}

function redo() {
    if (redoStack.length === 0) return;

    const nextState = redoStack.pop();
    undoStack.push(nextState);
    restoreState(nextState);
    markDirty();
}

function restoreState(stateJson) {
    const elements = JSON.parse(stateJson);
    cy.elements().remove();
    cy.add(elements);
}

function showSaveStatus(text) {
    const status = document.getElementById('saveStatus');
    status.textContent = text;
    status.classList.toggle('unsaved', isDirty);
}

// Run workflow
async function runWorkflow() {
    if (isDirty) {
        if (!confirm('You have unsaved changes. Save before running?')) return;
        await saveWorkflow();
    }

    if (!workflowId) {
        alert('Please save the workflow first');
        return;
    }

    const runBtn = document.getElementById('runBtn');
    runBtn.classList.add('running');
    runBtn.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/>
        </svg>
        Running...
    `;

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
        showExecutionBanner(data.executionId);
        startCanvasExecutionPolling(data.executionId);
    } catch (error) {
        console.error('Error executing workflow:', error);
        alert('Failed to execute: ' + error.message);
        resetRunButton();
    }
}

function resetRunButton() {
    const runBtn = document.getElementById('runBtn');
    runBtn.classList.remove('running');
    runBtn.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
            <path d="M8 5v14l11-7z"/>
        </svg>
        Run
    `;
}

// Execution Banner (shown on canvas)
function showExecutionBanner(executionId) {
    currentExecution = executionId;
    const banner = document.getElementById('executionBanner');
    banner.classList.remove('hidden', 'success', 'failed', 'waiting');
    banner.classList.add('running');
    document.getElementById('bannerStatusText').textContent = 'Executing...';
    document.getElementById('execProgress').style.width = '10%';
    updateExecutionBanner('Running', 10);
}

function hideExecutionBanner() {
    document.getElementById('executionBanner').classList.add('hidden');
}

function updateExecutionBanner(status, progress) {
    const banner = document.getElementById('executionBanner');
    const statusText = document.getElementById('bannerStatusText');
    const progressBar = document.getElementById('execProgress');
    const actions = document.getElementById('bannerActions');

    progressBar.style.width = `${progress}%`;
    banner.classList.remove('running', 'success', 'failed', 'waiting');

    const stopHtml = '<button class="btn btn-sm" onclick="stopCanvasExecutionPolling(); stopExecutionPolling(); hideExecutionBanner(); resetRunButton();">Stop</button>';
    const dismissHtml = '<button class="btn btn-sm" onclick="hideExecutionBanner(); resetRunButton();">Dismiss</button>';
    const detailsHtml = `<button class="btn btn-sm" onclick="if(currentExecution) showExecutionModal(currentExecution)">Details</button>`;

    if (status === 'Completed') {
        banner.classList.add('success');
        statusText.textContent = 'Completed';
        actions.innerHTML = `${detailsHtml} ${dismissHtml}`;
    } else if (status === 'Failed' || status === 'Rejected') {
        banner.classList.add('failed');
        statusText.textContent = status;
        actions.innerHTML = `${detailsHtml} ${dismissHtml}`;
    } else if (status === 'WaitingForApproval') {
        banner.classList.add('waiting');
        statusText.textContent = 'Waiting for Approval';
        actions.innerHTML = `<button class="btn btn-sm btn-success" onclick="approveExecution()">Approve</button>
            <button class="btn btn-sm btn-danger" onclick="rejectExecution()">Reject</button>
            ${stopHtml}`;
    } else {
        banner.classList.add('running');
        statusText.textContent = 'Executing...';
        actions.innerHTML = `${detailsHtml} ${stopHtml}`;
    }
}

// Canvas-based execution polling (updates nodes directly on canvas)
function startCanvasExecutionPolling(executionId) {
    const poll = async () => {
        try {
            const response = await authFetch(`/api/v1/workflow/executions/${executionId}/nodes`);
            if (!response.ok) return;

            const data = await response.json();
            updateCanvasNodeStates(data);

            // Calculate progress
            const totalNodes = data.nodes?.length || 0;
            const completedNodes = data.nodes?.filter(n =>
                ['Completed', 'Skipped', 'Failed'].includes(n.status)
            ).length || 0;
            const progress = totalNodes > 0 ? Math.round((completedNodes / totalNodes) * 100) : 0;

            updateExecutionBanner(data.status, progress);

            if (['Completed', 'Failed', 'Rejected'].includes(data.status)) {
                stopCanvasExecutionPolling();
                resetRunButton();
                hideExecutionBanner();

                // Auto-open execution report only if modal isn't already open
                if (!document.getElementById('executionModal').classList.contains('show')) {
                    showExecutionModal(executionId);
                }
            }

            // Handle approval waiting - open modal only once
            if (data.status === 'WaitingForApproval') {
                document.getElementById('bannerStatusText').textContent = 'Waiting for Approval...';
                if (!document.getElementById('executionModal').classList.contains('show')) {
                    showExecutionModal(executionId);
                }
            }
        } catch (error) {
            console.error('Execution polling error:', error);
        }
    };

    poll();
    executionPollingInterval = setInterval(poll, 1500);
}

function stopCanvasExecutionPolling() {
    if (executionPollingInterval) {
        clearInterval(executionPollingInterval);
        executionPollingInterval = null;
    }
}

function updateCanvasNodeStates(data) {
    if (!cy || !data.nodes) return;

    // Reset all node styles first
    cy.nodes().removeClass('cy-node-running cy-node-completed cy-node-failed cy-node-waiting');

    data.nodes.forEach(nodeState => {
        const node = cy.getElementById(nodeState.nodeId);
        if (!node || node.empty()) return;

        switch (nodeState.status) {
            case 'Running':
                node.addClass('cy-node-running');
                break;
            case 'Completed':
                node.addClass('cy-node-completed');
                break;
            case 'Failed':
                node.addClass('cy-node-failed');
                break;
            case 'WaitingForApproval':
                node.addClass('cy-node-waiting');
                break;
        }
    });
}

// Execution viewer modal (pipeline style)
async function showExecutionModal(executionId) {
    // Stop any existing modal polling to prevent duplicates
    stopExecutionPolling();
    _lastStepStatuses = {};

    currentExecution = executionId;
    document.getElementById('executionModal').classList.add('show');
    document.getElementById('pipelineSteps').innerHTML = '<div class="pipeline-loading">Loading...</div>';

    // Load current state first
    try {
        const response = await authFetch(`/api/v1/workflow/executions/${executionId}/nodes`);
        if (response.ok) {
            const data = await response.json();
            updateExecutionStatus(data);

            // If execution is finished, auto-expand all steps and stop
            if (['Completed', 'Failed', 'Rejected'].includes(data.status)) {
                autoExpandAllSteps();
                return;
            }
        }
    } catch (error) {
        console.error('Error loading execution:', error);
    }

    // Still running — start polling
    startExecutionPolling(executionId);
}

function autoExpandAllSteps() {
    document.querySelectorAll('.pipeline-step').forEach(step => {
        const nodeId = step.dataset.stepId;
        const outputEl = document.getElementById(`step-output-${nodeId}`);
        if (outputEl && !outputEl.classList.contains('expanded')) {
            outputEl.classList.add('expanded');
            step.classList.add('step-expanded');
            loadStepOutput(nodeId);
        }
    });
}

async function startExecutionPolling(executionId) {
    const poll = async () => {
        try {
            const response = await authFetch(`/api/v1/workflow/executions/${executionId}/nodes`);
            if (!response.ok) return;

            const data = await response.json();
            updateExecutionStatus(data);

            if (['Completed', 'Failed', 'Rejected'].includes(data.status)) {
                stopExecutionPolling();
            }
        } catch (error) {
            console.error('Execution polling error:', error);
        }
    };

    poll();
    executionPollingInterval = setInterval(poll, 1500);
}

function stopExecutionPolling() {
    if (executionPollingInterval) {
        clearInterval(executionPollingInterval);
        executionPollingInterval = null;
    }
}

function updateExecutionStatus(data) {
    // Update status bar
    document.getElementById('execStatus').textContent = data.status;

    // Show approval controls if needed
    const approvalFooter = document.getElementById('approvalFooter');
    if (data.pendingApproval) {
        approvalFooter.style.display = 'flex';
        document.getElementById('approvalMessage').textContent =
            `Waiting for approval: ${data.pendingApproval.approvalName || 'Approval'}`;
    } else {
        approvalFooter.style.display = 'none';
    }

    // Render pipeline steps
    renderPipelineSteps(data.nodes || []);
}

// Track last known status per step to avoid unnecessary re-renders
let _lastStepStatuses = {};

function renderPipelineSteps(nodes) {
    const container = document.getElementById('pipelineSteps');

    // Skip start/end nodes for cleaner view
    const steps = nodes.filter(n => n.nodeType !== 'Start' && n.nodeType !== 'End'
        && n.nodeType !== 'start' && n.nodeType !== 'end');

    if (steps.length === 0) {
        container.innerHTML = '<div class="pipeline-empty">No steps to display</div>';
        _lastStepStatuses = {};
        return;
    }

    // Check if we need a full render (first time or step count changed)
    const existingSteps = container.querySelectorAll('.pipeline-step');
    const needsFullRender = existingSteps.length === 0 || existingSteps.length !== steps.length;

    if (needsFullRender) {
        _lastStepStatuses = {};
        container.innerHTML = steps.map((step, index) => buildStepHtml(step, index, steps.length)).join('');

        // Initial loads
        steps.forEach(step => {
            if (['Completed', 'Failed'].includes(step.status)) {
                loadStepOutput(step.nodeId);
            }
            if (step.status === 'WaitingForApproval') {
                loadApprovalContext(step.nodeId);
            }
        });

        // Save statuses
        steps.forEach(s => _lastStepStatuses[s.nodeId] = s.status);
        return;
    }

    // Incremental update: only update steps whose status changed
    steps.forEach(step => {
        const prev = _lastStepStatuses[step.nodeId];
        if (prev === step.status) return; // No change

        _lastStepStatuses[step.nodeId] = step.status;

        const stepEl = container.querySelector(`[data-step-id="${step.nodeId}"]`);
        if (!stepEl) return;

        // Update status class
        stepEl.className = `pipeline-step ${(step.status || 'pending').toLowerCase()}`;
        if (stepEl.classList.contains('step-expanded')) stepEl.classList.add('step-expanded');

        // Update status icon
        const iconEl = stepEl.querySelector('.step-status-icon');
        if (iconEl) iconEl.innerHTML = getPipelineStatusIcon(step.status);

        // Update duration/time
        const metaEl = stepEl.querySelector('.step-meta');
        if (metaEl) {
            const duration = step.duration ? formatStepDuration(step.duration) : '';
            const startTime = step.startedAt ? new Date(step.startedAt).toLocaleTimeString() : '';
            metaEl.innerHTML = `
                ${duration ? `<span class="step-duration">${duration}</span>` : ''}
                ${startTime ? `<span class="step-time">${startTime}</span>` : ''}
            `;
        }

        // Add approval panel if newly waiting
        if (step.status === 'WaitingForApproval' && !stepEl.querySelector('.step-approval-panel')) {
            const stepContent = stepEl.querySelector('.step-content');
            const outputDiv = stepEl.querySelector('.step-output');
            const panel = document.createElement('div');
            panel.className = 'step-approval-panel';
            panel.id = `approval-panel-${step.nodeId}`;
            panel.innerHTML = `
                <div class="approval-context" id="approval-context-${step.nodeId}">
                    <div class="approval-context-loading">Loading upstream results...</div>
                </div>
                <div class="approval-editor">
                    <div class="approval-editor-label">Output for downstream nodes (editable)</div>
                    <textarea class="approval-output-editor" id="approval-output-${step.nodeId}" rows="8" placeholder="Edit the output that will be passed to downstream nodes..."></textarea>
                </div>
                <div class="step-approval-actions">
                    <span class="approval-hint">Review and decide</span>
                    <button class="btn btn-sm btn-danger" onclick="event.stopPropagation(); rejectExecution()">Reject</button>
                    <button class="btn btn-sm btn-success" onclick="event.stopPropagation(); approveWithOutput('${step.nodeId}')">Approve</button>
                </div>
            `;
            stepContent.insertBefore(panel, outputDiv);
            loadApprovalContext(step.nodeId);
        }

        // Remove approval panel if no longer waiting
        if (step.status !== 'WaitingForApproval') {
            const panel = stepEl.querySelector('.step-approval-panel');
            if (panel) panel.remove();
        }

        // Load output for newly completed/failed steps
        if (['Completed', 'Failed'].includes(step.status)) {
            loadStepOutput(step.nodeId);
        }
    });
}

function buildStepHtml(step, index, totalSteps) {
    const statusIcon = getPipelineStatusIcon(step.status);
    const statusClass = step.status ? step.status.toLowerCase() : 'pending';
    const duration = step.duration ? formatStepDuration(step.duration) : '';
    const startTime = step.startedAt ? new Date(step.startedAt).toLocaleTimeString() : '';
    const label = step.nodeLabel || step.nodeId;

    return `
        <div class="pipeline-step ${statusClass}" data-step-id="${step.nodeId}">
            <div class="step-connector">${index < totalSteps - 1 ? '<div class="connector-line"></div>' : ''}</div>
            <div class="step-content">
                <div class="step-header" onclick="toggleStepOutput('${step.nodeId}')">
                    <div class="step-status-icon">${statusIcon}</div>
                    <div class="step-info">
                        <span class="step-label">${escapeHtml(label)}</span>
                        <span class="step-type">${escapeHtml(step.nodeType)}</span>
                    </div>
                    <div class="step-meta">
                        ${duration ? `<span class="step-duration">${duration}</span>` : ''}
                        ${startTime ? `<span class="step-time">${startTime}</span>` : ''}
                    </div>
                    <span class="step-chevron">&#9654;</span>
                </div>
                ${step.status === 'WaitingForApproval' ? `
                <div class="step-approval-panel" id="approval-panel-${step.nodeId}">
                    <div class="approval-context" id="approval-context-${step.nodeId}">
                        <div class="approval-context-loading">Loading upstream results...</div>
                    </div>
                    <div class="approval-editor">
                        <div class="approval-editor-label">Output for downstream nodes (editable)</div>
                        <textarea class="approval-output-editor" id="approval-output-${step.nodeId}" rows="8" placeholder="Edit the output that will be passed to downstream nodes..."></textarea>
                    </div>
                    <div class="step-approval-actions">
                        <span class="approval-hint">Review and decide</span>
                        <button class="btn btn-sm btn-danger" onclick="event.stopPropagation(); rejectExecution()">Reject</button>
                        <button class="btn btn-sm btn-success" onclick="event.stopPropagation(); approveWithOutput('${step.nodeId}')">Approve</button>
                    </div>
                </div>
                ` : ''}
                <div class="step-output" id="step-output-${step.nodeId}">
                    <div class="step-output-content">Loading...</div>
                </div>
            </div>
        </div>
    `;
}

async function loadApprovalContext(nodeId) {
    const contextEl = document.getElementById(`approval-context-${nodeId}`);
    const editorEl = document.getElementById(`approval-output-${nodeId}`);
    if (!contextEl || !currentExecution) return;

    try {
        const response = await authFetch(`/api/v1/workflow/executions/${currentExecution}`);
        if (!response.ok) return;

        const data = await response.json();

        if (data.pendingApproval && data.pendingApproval.context && data.pendingApproval.context.length > 0) {
            // Show upstream outputs
            contextEl.innerHTML = data.pendingApproval.context.map(item => {
                let outputHtml = '';
                if (item.outputSummary) {
                    try {
                        const parsed = JSON.parse(item.outputSummary);
                        outputHtml = escapeHtml(JSON.stringify(parsed, null, 2));
                    } catch {
                        outputHtml = escapeHtml(item.outputSummary);
                    }
                }
                return `
                    <div class="context-item">
                        <div class="context-item-label">${escapeHtml(item.nodeLabel)}</div>
                        <pre class="context-item-output">${outputHtml || 'No output'}</pre>
                    </div>
                `;
            }).join('');

            // Pre-fill editor with merged upstream outputs
            if (editorEl && !editorEl.value) {
                const merged = {};
                data.pendingApproval.context.forEach(item => {
                    if (item.outputSummary) {
                        try {
                            merged[item.nodeId] = JSON.parse(item.outputSummary);
                        } catch {
                            merged[item.nodeId] = item.outputSummary;
                        }
                    }
                });
                editorEl.value = JSON.stringify(merged, null, 2);
            }
        } else {
            contextEl.innerHTML = '<div class="context-empty">No upstream outputs available</div>';
        }
    } catch (error) {
        console.error('Error loading approval context:', error);
        contextEl.innerHTML = '<div class="context-empty">Failed to load context</div>';
    }
}

async function loadStepOutput(nodeId) {
    const outputEl = document.querySelector(`[data-step-id="${nodeId}"] .step-output-content`);
    if (!outputEl || outputEl.dataset.loaded) return;

    try {
        const response = await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes/${nodeId}`);
        if (!response.ok) {
            outputEl.textContent = 'Failed to load output';
            return;
        }

        const node = await response.json();
        outputEl.dataset.loaded = 'true';

        let content = '';
        if (node.inputJson) {
            try {
                const input = JSON.parse(node.inputJson);
                content += `<div class="output-section"><div class="output-section-label">Input</div><pre>${escapeHtml(JSON.stringify(input, null, 2))}</pre></div>`;
            } catch {
                content += `<div class="output-section"><div class="output-section-label">Input</div><pre>${escapeHtml(node.inputJson)}</pre></div>`;
            }
        }

        if (node.outputJson) {
            try {
                const output = JSON.parse(node.outputJson);
                content += `<div class="output-section"><div class="output-section-label">Output</div><pre>${escapeHtml(JSON.stringify(output, null, 2))}</pre></div>`;
            } catch {
                content += `<div class="output-section"><div class="output-section-label">Output</div><pre>${escapeHtml(node.outputJson)}</pre></div>`;
            }
        }

        if (node.errorMessage) {
            content += `<div class="output-section output-error"><div class="output-section-label">Error</div><pre>${escapeHtml(node.errorMessage)}</pre></div>`;
        }

        if (!content) {
            content = '<div class="output-section"><pre>No output</pre></div>';
        }

        outputEl.innerHTML = content;
    } catch (error) {
        outputEl.textContent = 'Error loading output: ' + error.message;
    }
}

function toggleStepOutput(nodeId) {
    const outputEl = document.getElementById(`step-output-${nodeId}`);
    if (!outputEl) return;

    const isExpanding = !outputEl.classList.contains('expanded');
    outputEl.classList.toggle('expanded');

    // Load output on first expand
    if (isExpanding) {
        loadStepOutput(nodeId);
    }

    // Toggle chevron
    const step = outputEl.closest('.pipeline-step');
    step.classList.toggle('step-expanded');
}

function getPipelineStatusIcon(status) {
    switch (status) {
        case 'Completed':
            return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#27ae60" stroke-width="2.5"><path d="M20 6L9 17l-5-5"/></svg>';
        case 'Failed':
            return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#e74c3c" stroke-width="2.5"><path d="M18 6L6 18M6 6l12 12"/></svg>';
        case 'Running':
            return '<div class="step-spinner"></div>';
        case 'WaitingForApproval':
            return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#f39c12" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/></svg>';
        case 'Skipped':
            return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#95a5a6" stroke-width="2"><path d="M5 12h14"/></svg>';
        default:
            return '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#95a5a6" stroke-width="2"><circle cx="12" cy="12" r="10"/></svg>';
    }
}

function formatStepDuration(duration) {
    // duration comes as "HH:MM:SS.xxx" or milliseconds string
    if (typeof duration === 'string' && duration.includes(':')) {
        const parts = duration.split(':');
        const hours = parseInt(parts[0]);
        const mins = parseInt(parts[1]);
        const secs = parseFloat(parts[2]);
        if (hours > 0) return `${hours}h ${mins}m`;
        if (mins > 0) return `${mins}m ${Math.floor(secs)}s`;
        return `${secs.toFixed(1)}s`;
    }
    const ms = typeof duration === 'number' ? duration : parseFloat(duration);
    if (isNaN(ms)) return '';
    if (ms < 1000) return `${Math.round(ms)}ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
    return `${Math.floor(ms / 60000)}m ${Math.floor((ms % 60000) / 1000)}s`;
}

async function approveExecution() {
    if (!currentExecution) return;

    try {
        const response = await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes`);
        if (!response.ok) return;

        const data = await response.json();
        if (!data.pendingApproval) return;

        const waitingNode = data.nodes.find(n => n.status === 'WaitingForApproval');
        if (!waitingNode) return;

        await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes/${waitingNode.nodeId}/approve`, {
            method: 'POST',
            body: JSON.stringify({})
        });
    } catch (error) {
        console.error('Error approving:', error);
        alert('Failed to approve: ' + error.message);
    }
}

async function approveWithOutput(nodeId) {
    if (!currentExecution) return;

    const editor = document.getElementById(`approval-output-${nodeId}`);
    const editedOutput = editor ? editor.value.trim() : null;

    try {
        await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes/${nodeId}/approve`, {
            method: 'POST',
            body: JSON.stringify({
                editedOutput: editedOutput || null
            })
        });
    } catch (error) {
        console.error('Error approving:', error);
        alert('Failed to approve: ' + error.message);
    }
}

async function rejectExecution() {
    if (!currentExecution) return;

    try {
        const response = await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes`);
        if (!response.ok) return;

        const data = await response.json();
        const waitingNode = data.nodes.find(n => n.status === 'WaitingForApproval');
        if (!waitingNode) return;

        await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes/${waitingNode.nodeId}/reject`, {
            method: 'POST'
        });
    } catch (error) {
        console.error('Error rejecting:', error);
        alert('Failed to reject: ' + error.message);
    }
}

function closeExecutionModal() {
    stopExecutionPolling();
    document.getElementById('executionModal').classList.remove('show');
    currentExecution = null;
    _lastStepStatuses = {};
}

// Load execution (when opened from list)
async function loadExecution(executionId) {
    try {
        const response = await authFetch(`/api/v1/workflow/executions/${executionId}`);
        if (!response.ok) throw new Error('Failed to load execution');

        const execution = await response.json();

        // Load the workflow
        workflowId = execution.workflowDefinitionId;
        await loadWorkflow();

        // Show execution modal
        showExecutionModal(executionId);
    } catch (error) {
        console.error('Error loading execution:', error);
        alert('Failed to load execution: ' + error.message);
    }
}

// Schedule modal
function openScheduleModal() {
    const modal = document.getElementById('scheduleModal');
    const schedule = workflow?.schedule;

    document.getElementById('scheduleEnabled').checked = schedule?.isEnabled || false;
    document.getElementById('scheduleFrequency').value = schedule?.frequency || 'Daily';
    document.getElementById('scheduleTime').value = schedule?.timeOfDay || '09:00';
    document.getElementById('scheduleTimezone').value = schedule?.timezone || 'UTC';
    document.getElementById('dayOfMonth').value = schedule?.dayOfMonth || 1;

    // Set days of week
    if (schedule?.daysOfWeek) {
        document.querySelectorAll('#weeklyOptions input[type="checkbox"]').forEach(cb => {
            cb.checked = schedule.daysOfWeek.includes(cb.value);
        });
    }

    updateScheduleOptionsVisibility();
    modal.classList.add('show');
}

function updateScheduleOptionsVisibility() {
    const enabled = document.getElementById('scheduleEnabled').checked;
    const frequency = document.getElementById('scheduleFrequency').value;

    document.getElementById('scheduleOptions').style.display = enabled ? 'block' : 'none';
    document.getElementById('weeklyOptions').style.display = frequency === 'Weekly' ? 'block' : 'none';
    document.getElementById('monthlyOptions').style.display = frequency === 'Monthly' ? 'block' : 'none';
}

function saveSchedule() {
    const enabled = document.getElementById('scheduleEnabled').checked;

    if (!enabled) {
        workflow = workflow || {};
        workflow.schedule = null;
    } else {
        const frequency = document.getElementById('scheduleFrequency').value;
        const daysOfWeek = [];
        document.querySelectorAll('#weeklyOptions input[type="checkbox"]:checked').forEach(cb => {
            daysOfWeek.push(cb.value);
        });

        workflow = workflow || {};
        workflow.schedule = {
            isEnabled: true,
            frequency,
            timeOfDay: document.getElementById('scheduleTime').value,
            timezone: document.getElementById('scheduleTimezone').value,
            daysOfWeek: frequency === 'Weekly' ? daysOfWeek : null,
            dayOfMonth: frequency === 'Monthly' ? parseInt(document.getElementById('dayOfMonth').value) : null
        };
    }

    closeScheduleModal();
    markDirty();
}

function closeScheduleModal() {
    document.getElementById('scheduleModal').classList.remove('show');
}

// Event listeners
function setupEventListeners() {
    // Toolbar buttons
    document.getElementById('saveBtn').addEventListener('click', saveWorkflow);
    document.getElementById('runBtn').addEventListener('click', runWorkflow);
    document.getElementById('autoLayoutBtn').addEventListener('click', autoLayout);
    document.getElementById('scheduleBtn').addEventListener('click', openScheduleModal);
    document.getElementById('helpBtn').addEventListener('click', showHelp);

    // Schedule modal
    document.getElementById('closeScheduleModal').addEventListener('click', closeScheduleModal);
    document.getElementById('cancelSchedule').addEventListener('click', closeScheduleModal);
    document.getElementById('saveSchedule').addEventListener('click', saveSchedule);
    document.getElementById('scheduleEnabled').addEventListener('change', updateScheduleOptionsVisibility);
    document.getElementById('scheduleFrequency').addEventListener('change', updateScheduleOptionsVisibility);

    // Execution modal
    document.getElementById('closeExecutionModal').addEventListener('click', closeExecutionModal);
    document.getElementById('approveExecBtn').addEventListener('click', approveExecution);
    document.getElementById('rejectExecBtn').addEventListener('click', rejectExecution);

    // Keyboard shortcuts
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            closeScheduleModal();
            closeExecutionModal();
        }

        // Ctrl/Cmd + S to save
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            saveWorkflow();
        }

        // Ctrl/Cmd + Z to undo, Ctrl/Cmd + Shift + Z to redo
        if ((e.ctrlKey || e.metaKey) && e.key === 'z') {
            e.preventDefault();
            if (e.shiftKey) {
                redo();
            } else {
                undo();
            }
        }

        // Ctrl/Cmd + Y to redo (alternative)
        if ((e.ctrlKey || e.metaKey) && e.key === 'y') {
            e.preventDefault();
            redo();
        }

        // Delete key to delete selected node or edge
        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'TEXTAREA') {
                deleteSelectedElements();
            }
        }
    });

    // Warn before leaving with unsaved changes
    window.addEventListener('beforeunload', (e) => {
        if (isDirty) {
            e.preventDefault();
            e.returnValue = '';
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

function renderMarkdown(text) {
    if (!text) return '';
    // Use marked.js if available, otherwise just escape HTML
    if (typeof marked !== 'undefined') {
        // Configure marked for safe rendering
        marked.setOptions({
            breaks: true,
            gfm: true
        });
        return marked.parse(text);
    }
    return `<p>${escapeHtml(text)}</p>`;
}

// ========================================
// Onboarding
// ========================================
function setupOnboarding() {
    const overlay = document.getElementById('onboardingOverlay');
    const closeBtn = document.getElementById('closeOnboarding');
    const startBtn = document.getElementById('startEditing');
    const dontShowAgain = document.getElementById('dontShowAgain');
    // Check if user has dismissed onboarding before
    const hasSeenOnboarding = localStorage.getItem('workflow-editor-onboarding-seen');

    // Show onboarding for new workflows only (not when editing existing)
    const urlParams = new URLSearchParams(window.location.search);
    const isNewWorkflow = !urlParams.get('id');

    if (!hasSeenOnboarding && isNewWorkflow) {
        overlay.classList.remove('hidden');
    }

    closeBtn?.addEventListener('click', closeOnboarding);
    startBtn?.addEventListener('click', closeOnboarding);

    function closeOnboarding() {
        overlay.classList.add('hidden');
        if (dontShowAgain?.checked) {
            localStorage.setItem('workflow-editor-onboarding-seen', 'true');
        }
    }

    // Banner buttons are now inline onclick handlers (set by updateExecutionBanner)
}

// Show help overlay manually
function showHelp() {
    document.getElementById('onboardingOverlay').classList.remove('hidden');
}
