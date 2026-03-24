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
            // Execution status styles
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
        const response = await authFetch('/api/v1/workflow/skills');
        if (!response.ok) throw new Error('Failed to load skills');
        skills = await response.json();
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

    // Simple, compact skill list - just name with tooltip for description
    container.innerHTML = skills.map(skill => `
        <div class="skill-item" draggable="true" data-type="skill" data-skill="${escapeHtml(skill.name)}" title="${escapeHtml(skill.description || '')}">
            ${escapeHtml(skill.name)}
        </div>
    `).join('');

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

        const base = {
            id: data.id,
            type: data.type,
            position: { x: Math.round(pos.x), y: Math.round(pos.y) },
            label: data.label
        };

        if (data.type === 'skill') {
            return {
                ...base,
                skillName: data.skillName || '',
                args: data.args || {},
                timeoutSeconds: data.timeoutSeconds || 300
            };
        }

        if (data.type === 'approval') {
            return {
                ...base,
                approvalName: data.approvalName || data.label || 'Approval',
                description: data.description || '',
                scheduledBehavior: data.scheduledBehavior || 'WaitForApproval'
            };
        }

        if (data.type === 'wait') {
            return {
                ...base,
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
        edges,
        variables: workflow?.definition?.variables || {}
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
        `<option value="${escapeHtml(s.name)}" ${s.name === data.skillName ? 'selected' : ''}>${escapeHtml(s.name)}</option>`
    ).join('');

    const skill = skills.find(s => s.name === data.skillName);

    let html = `
        <div class="property-group">
            <label>Skill</label>
            <select id="propSkillName" onchange="updateNodeProperty('skillName', this.value); updateArgsEditor()">
                <option value="">Select a skill...</option>
                ${skillOptions}
            </select>
        </div>
    `;

    // Show skill description if available (supports markdown)
    if (skill && skill.description) {
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

    // Args section
    if (skill && skill.parameters && Object.keys(skill.parameters).length > 0) {
        html += `
            <div class="args-section">
                <h4>Arguments</h4>
                ${renderArgsEditor(skill.parameters, data.args || {})}
            </div>
        `;
    } else if (skill) {
        html += `
            <div class="args-section">
                <h4>Arguments</h4>
                <p class="text-muted">This skill has no parameters</p>
            </div>
        `;
    }

    return html;
}

function renderArgsEditor(parameters, currentArgs) {
    if (!parameters || Object.keys(parameters).length === 0) {
        return '<p class="text-muted">This skill has no parameters</p>';
    }

    return Object.entries(parameters).map(([name, param]) => {
        const argSource = currentArgs[name] || {};
        const activeSource = getActiveArgSource(argSource);
        const sourceHelp = getArgSourceHelp(activeSource, name);

        return `
            <div class="arg-item">
                <div class="arg-header">
                    <span class="arg-name">${escapeHtml(name)} ${param.required ? '<span class="required-mark">*</span>' : ''}</span>
                    <span class="arg-type">${escapeHtml(param.type || 'string')}</span>
                </div>
                ${param.description ? `<div class="arg-description">${escapeHtml(param.description)}</div>` : ''}
                <div class="arg-source-tabs">
                    <button class="arg-source-tab ${activeSource === 'filled' ? 'active' : ''}"
                            onclick="setArgSource('${name}', 'filled')"
                            title="直接填入固定值">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                        </svg>
                        Value
                    </button>
                    <button class="arg-source-tab ${activeSource === 'config' ? 'active' : ''}"
                            onclick="setArgSource('${name}', 'config')"
                            title="使用 Workflow 變數">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="12" cy="12" r="3"/>
                            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"/>
                        </svg>
                        Config
                    </button>
                    <button class="arg-source-tab ${activeSource === 'input' ? 'active' : ''}"
                            onclick="setArgSource('${name}', 'input')"
                            title="從上游節點取值">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="12" cy="5" r="3"/>
                            <line x1="12" y1="8" x2="12" y2="21"/>
                            <path d="M8 17l4 4 4-4"/>
                        </svg>
                        Input
                    </button>
                </div>
                <div class="arg-source-help">${sourceHelp}</div>
                <input type="text" class="arg-value-input"
                       id="arg-${name}"
                       value="${escapeHtml(getArgValue(argSource, activeSource))}"
                       placeholder="${getArgPlaceholder(activeSource, param)}"
                       onchange="updateArgValue('${name}', this.value)">
                ${renderArgExamples(activeSource, param, name)}
            </div>
        `;
    }).join('');
}

function getArgSourceHelp(source, argName) {
    switch (source) {
        case 'filled':
            return '直接輸入固定值，執行時使用此值';
        case 'config':
            return '引用 Workflow 變數，可在不同執行間共享';
        case 'input':
            return '從上游節點的輸出取值 (格式: nodeId.path)';
        default:
            return '';
    }
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
        case 'config':
            examples = ['myVariable', `${argName}_config`];
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
            <span class="examples-label">例:</span>
            ${examples.map(ex => `<code class="example-value" onclick="setArgExample('${argName}', '${escapeHtml(ex)}')">${escapeHtml(ex)}</code>`).join('')}
        </div>
    `;
}

function getUpstreamNodeExamples() {
    if (!selectedNode || !cy) return [];

    const examples = [];
    const currentId = selectedNode.id();

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
    if (argSource.filledValue !== undefined && argSource.filledValue !== null) return 'filled';
    if (argSource.configKey) return 'config';
    if (argSource.inputMapping) return 'input';
    return 'filled';
}

function getArgValue(argSource, activeSource) {
    switch (activeSource) {
        case 'filled': return argSource.filledValue || '';
        case 'config': return argSource.configKey || '';
        case 'input': return argSource.inputMapping || '';
        default: return '';
    }
}

function getArgPlaceholder(activeSource, param) {
    switch (activeSource) {
        case 'filled': return param.default || 'Enter value...';
        case 'config': return 'Variable name (e.g., myVar)';
        case 'input': return 'Node output (e.g., nodeId.output)';
        default: return '';
    }
}

function setArgSource(argName, source) {
    if (!selectedNode) return;

    const args = selectedNode.data('args') || {};
    const currentArg = args[argName] || {};
    const value = document.getElementById(`arg-${argName}`).value;

    // Clear all sources and set the new one
    const newArg = {};
    switch (source) {
        case 'filled':
            newArg.filledValue = value;
            break;
        case 'config':
            newArg.configKey = value;
            break;
        case 'input':
            newArg.inputMapping = value;
            break;
    }

    args[argName] = newArg;
    selectedNode.data('args', args);
    markDirty();

    // Re-render to update tabs
    renderProperties(selectedNode);
}

function updateArgValue(argName, value) {
    if (!selectedNode) return;

    const args = selectedNode.data('args') || {};
    const currentArg = args[argName] || {};
    const activeSource = getActiveArgSource(currentArg);

    switch (activeSource) {
        case 'filled':
            currentArg.filledValue = value;
            break;
        case 'config':
            currentArg.configKey = value;
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
            const error = await response.json();
            throw new Error(error.title || 'Failed to save workflow');
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
        showExecutionModal(data.executionId);
    } catch (error) {
        console.error('Error executing workflow:', error);
        alert('Failed to execute: ' + error.message);
    }
}

// Execution viewer
async function showExecutionModal(executionId) {
    currentExecution = executionId;
    document.getElementById('executionModal').classList.add('show');

    // Initialize execution graph (copy of main graph)
    initExecutionGraph();

    // Start polling
    startExecutionPolling(executionId);
}

function initExecutionGraph() {
    const container = document.getElementById('executionGraph');

    // Create a mini cytoscape instance for execution visualization
    const execCy = cytoscape({
        container,
        elements: cy.elements().clone(),
        style: cy.style().json(),
        layout: { name: 'preset' },
        userZoomingEnabled: false,
        userPanningEnabled: false,
        boxSelectionEnabled: false
    });

    execCy.fit(30);

    // Store reference
    container._cy = execCy;

    // Click handler for nodes
    execCy.on('tap', 'node', async function(evt) {
        const nodeId = evt.target.id();
        await loadNodeOutput(currentExecution, nodeId);
    });
}

async function startExecutionPolling(executionId) {
    const poll = async () => {
        try {
            const response = await authFetch(`/api/v1/workflow/executions/${executionId}/nodes`);
            if (!response.ok) return;

            const data = await response.json();
            updateExecutionStatus(data);

            if (['Completed', 'Failed', 'Rejected'].includes(data.Status)) {
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
    document.getElementById('execStatus').textContent = data.Status;

    // Update node statuses in execution graph
    const execCy = document.getElementById('executionGraph')._cy;
    if (!execCy) return;

    data.Nodes.forEach(node => {
        const cyNode = execCy.$(`#${node.NodeId}`);
        cyNode.removeClass('pending running completed failed waiting');

        switch (node.Status) {
            case 'Running':
                cyNode.addClass('running');
                break;
            case 'Completed':
                cyNode.addClass('completed');
                break;
            case 'Failed':
                cyNode.addClass('failed');
                break;
            case 'WaitingForApproval':
                cyNode.addClass('waiting');
                break;
        }
    });

    // Show approval controls if needed
    const approvalFooter = document.getElementById('approvalFooter');
    if (data.PendingApproval) {
        approvalFooter.style.display = 'flex';
        document.getElementById('approvalMessage').textContent =
            `Waiting for approval: ${data.PendingApproval.approvalName || 'Approval'}`;
    } else {
        approvalFooter.style.display = 'none';
    }
}

async function loadNodeOutput(executionId, nodeId) {
    try {
        const response = await authFetch(`/api/v1/workflow/executions/${executionId}/nodes/${nodeId}`);
        if (!response.ok) return;

        const node = await response.json();
        const content = document.getElementById('outputContent');

        if (node.outputJson) {
            try {
                const output = JSON.parse(node.outputJson);
                content.textContent = JSON.stringify(output, null, 2);
            } catch {
                content.textContent = node.outputJson;
            }
        } else if (node.errorMessage) {
            content.textContent = `Error: ${node.errorMessage}`;
        } else {
            content.textContent = 'No output available';
        }
    } catch (error) {
        console.error('Error loading node output:', error);
    }
}

async function approveExecution() {
    if (!currentExecution) return;

    // Get the pending approval node from the current execution data
    try {
        const response = await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes`);
        if (!response.ok) return;

        const data = await response.json();
        if (!data.PendingApproval) return;

        // Find the waiting node
        const waitingNode = data.Nodes.find(n => n.Status === 'WaitingForApproval');
        if (!waitingNode) return;

        await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes/${waitingNode.NodeId}/approve`, {
            method: 'POST'
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
        const waitingNode = data.Nodes.find(n => n.Status === 'WaitingForApproval');
        if (!waitingNode) return;

        await authFetch(`/api/v1/workflow/executions/${currentExecution}/nodes/${waitingNode.NodeId}/reject`, {
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
