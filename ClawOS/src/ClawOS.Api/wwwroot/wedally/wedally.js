// Wedally - NATS EventController Testing UI (Swagger-like)

// API Endpoints
const API_SPEC = '/api/v1/wedally/spec';
const API_PUBLISH = '/api/v1/wedally/publish';
const API_FIRE = '/api/v1/wedally/fire';

// State
let spec = null;
let selectedVersion = '';
let currentEndpoint = null;

// Action colors (for non-Request actions) - distinct from HTTP method colors
const ACTION_COLORS = {
    Request: { bg: '#49cc90', text: '#fff' },  // Default for Request (rarely used, HTTP method takes precedence)
    Publish: { bg: '#7d8492', text: '#fff' },  // Gray - fire-and-forget
    Consume: { bg: '#e040fb', text: '#fff' },  // Magenta - continuous processing
    Fetch: { bg: '#9c27b0', text: '#fff' }     // Purple - batch processing
};

// HTTP Method colors (Swagger-style)
const HTTP_METHOD_COLORS = {
    GET: { bg: '#61affe', text: '#fff' },
    POST: { bg: '#49cc90', text: '#fff' },
    PUT: { bg: '#fca130', text: '#fff' },
    PATCH: { bg: '#50e3c2', text: '#fff' },
    DELETE: { bg: '#f93e3e', text: '#fff' }
};

// DOM Elements
const versionSelect = document.getElementById('versionSelect');
const tagList = document.getElementById('tagList');
const definitionList = document.getElementById('definitionList');
const endpointsSection = document.getElementById('endpointsSection');
const definitionsSection = document.getElementById('definitionsSection');
const endpointCount = document.getElementById('endpointCount');
const definitionCount = document.getElementById('definitionCount');
const filterInput = document.getElementById('filterInput');

// Initialize
document.addEventListener('DOMContentLoaded', async () => {
    await loadSpec();
    setupEventListeners();
});

// Load spec from API
async function loadSpec() {
    try {
        tagList.innerHTML = '<div class="loading-spinner"></div>';

        const response = await fetch(API_SPEC);
        if (!response.ok) throw new Error('Failed to load spec');

        spec = await response.json();

        // Update info banner
        document.getElementById('infoTitle').textContent = spec.info.title;
        document.getElementById('infoDescription').textContent = spec.info.description;
        document.getElementById('infoVersion').textContent = `v${spec.info.version}`;

        // Update servers
        if (spec.servers && spec.servers.length > 0) {
            document.getElementById('infoServers').textContent =
                `Connections: ${spec.servers.map(s => s.name).join(', ')}`;
        }

        // Populate version select
        versionSelect.innerHTML = '<option value="">All</option>';
        spec.versions.forEach(v => {
            const opt = document.createElement('option');
            opt.value = v;
            opt.textContent = v;
            versionSelect.appendChild(opt);
        });

        // Render
        renderAll();

    } catch (error) {
        console.error('Error loading spec:', error);
        tagList.innerHTML = `<div class="error-message">Failed to load: ${error.message}</div>`;
    }
}

// Render all sections
function renderAll(filter = '') {
    renderTags(filter);
    renderEndpoints(filter);
    renderDefinitions(filter);
}

// Render tag list (sidebar)
function renderTags(filter = '') {
    tagList.innerHTML = '';

    const filterLower = filter.toLowerCase();
    let totalEndpoints = 0;

    for (const tag of spec.tags) {
        const endpoints = spec.paths[tag.name] || [];

        // Filter by version and search
        const filteredEndpoints = endpoints.filter(e => {
            if (selectedVersion && e.version !== selectedVersion) return false;
            if (filter) {
                return e.method.toLowerCase().includes(filterLower) ||
                    e.subject.toLowerCase().includes(filterLower) ||
                    e.action.toLowerCase().includes(filterLower);
            }
            return true;
        });

        if (filteredEndpoints.length === 0) continue;
        totalEndpoints += filteredEndpoints.length;

        const tagEl = document.createElement('div');
        tagEl.className = 'tag-group';

        const header = document.createElement('div');
        header.className = 'tag-header';
        header.innerHTML = `
            <span class="tag-toggle">▼</span>
            <span class="tag-name">${tag.name}</span>
            <span class="tag-count">${filteredEndpoints.length}</span>
        `;
        header.onclick = () => {
            header.classList.toggle('collapsed');
            items.classList.toggle('hidden');
        };

        const items = document.createElement('div');
        items.className = 'tag-items';

        for (const endpoint of filteredEndpoints) {
            const item = document.createElement('div');
            item.className = 'tag-item';
            item.dataset.operationId = endpoint.operationId;

            // For Request action, show HTTP method; for others, show action type
            const badgeInfo = getBadgeInfo(endpoint);

            item.innerHTML = `
                <span class="action-badge-small" style="background: ${badgeInfo.bg}; color: ${badgeInfo.text};">
                    ${badgeInfo.label}
                </span>
                <span class="item-method">${endpoint.method}</span>
            `;

            item.onclick = () => scrollToEndpoint(endpoint.operationId);
            items.appendChild(item);
        }

        tagEl.appendChild(header);
        tagEl.appendChild(items);
        tagList.appendChild(tagEl);
    }

    endpointCount.textContent = totalEndpoints;
}

// Render endpoints section
function renderEndpoints(filter = '') {
    endpointsSection.innerHTML = '';

    const filterLower = filter.toLowerCase();

    for (const tag of spec.tags) {
        const endpoints = spec.paths[tag.name] || [];

        // Filter
        const filteredEndpoints = endpoints.filter(e => {
            if (selectedVersion && e.version !== selectedVersion) return false;
            if (filter) {
                return e.method.toLowerCase().includes(filterLower) ||
                    e.subject.toLowerCase().includes(filterLower) ||
                    e.action.toLowerCase().includes(filterLower);
            }
            return true;
        });

        if (filteredEndpoints.length === 0) continue;

        // Tag section
        const tagSection = document.createElement('div');
        tagSection.className = 'tag-section';
        tagSection.id = `tag-${tag.name}`;

        const tagHeader = document.createElement('div');
        tagHeader.className = 'tag-section-header';
        tagHeader.innerHTML = `
            <span class="section-toggle">▼</span>
            <h2>${tag.name}</h2>
            <span class="tag-count-badge">${filteredEndpoints.length}</span>
            <span class="tag-description">${tag.description || ''}</span>
        `;

        const tagContent = document.createElement('div');
        tagContent.className = 'tag-section-content';

        tagHeader.onclick = () => {
            tagHeader.classList.toggle('collapsed');
            tagContent.classList.toggle('hidden');
        };

        tagSection.appendChild(tagHeader);
        tagSection.appendChild(tagContent);

        // Endpoints
        for (const endpoint of filteredEndpoints) {
            const card = createEndpointCard(endpoint);
            tagContent.appendChild(card);
        }

        endpointsSection.appendChild(tagSection);
    }
}

// Get badge info based on action type and HTTP method
function getBadgeInfo(endpoint) {
    if (endpoint.action === 'Request' && endpoint.httpMethod) {
        const httpColor = HTTP_METHOD_COLORS[endpoint.httpMethod] || HTTP_METHOD_COLORS.POST;
        return { bg: httpColor.bg, text: httpColor.text, label: endpoint.httpMethod };
    }
    const actionColor = ACTION_COLORS[endpoint.action] || ACTION_COLORS.Request;
    return { bg: actionColor.bg, text: actionColor.text, label: endpoint.action.substring(0, 3).toUpperCase() };
}

// Create endpoint card
function createEndpointCard(endpoint) {
    const card = document.createElement('div');
    card.className = 'endpoint-card';
    card.id = `endpoint-${endpoint.operationId}`;

    const badgeInfo = getBadgeInfo(endpoint);
    const canTest = endpoint.canTest;

    card.innerHTML = `
        <div class="endpoint-header" style="border-left: 4px solid ${badgeInfo.bg};">
            <div class="endpoint-header-left">
                <span class="action-badge" style="background: ${badgeInfo.bg}; color: ${badgeInfo.text};">
                    ${endpoint.action === 'Request' && endpoint.httpMethod ? endpoint.httpMethod : endpoint.action}
                </span>
                <span class="endpoint-subject">${endpoint.resolvedSubject}</span>
                <span class="endpoint-summary">${endpoint.summary || ''}</span>
            </div>
            <div class="endpoint-header-right">
                <span class="endpoint-version">${endpoint.version}</span>
                <button class="btn btn-small btn-cli" data-operation-id="${endpoint.operationId}" title="Copy NATS CLI command">
                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="4 17 10 11 4 5"></polyline>
                        <line x1="12" y1="19" x2="20" y2="19"></line>
                    </svg>
                    CLI
                </button>
                <button class="btn btn-small btn-try" data-operation-id="${endpoint.operationId}">Try it</button>
            </div>
        </div>
        <div class="endpoint-body">
            <div class="endpoint-meta">
                <span class="meta-item"><strong>Subject Pattern:</strong> ${endpoint.subject}</span>
                <span class="meta-item"><strong>Connection:</strong> ${endpoint.connection}</span>
                ${endpoint.stream ? `<span class="meta-item"><strong>Stream:</strong> ${endpoint.stream}</span>` : ''}
                ${endpoint.consumer ? `<span class="meta-item"><strong>Consumer:</strong> ${endpoint.consumer}</span>` : ''}
            </div>

            ${endpoint.parameters.length > 0 ? `
                <div class="endpoint-section">
                    <h4>Parameters</h4>
                    <table class="params-table">
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>In</th>
                                <th>Type</th>
                                <th>Required</th>
                                <th>Description</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${endpoint.parameters.map(p => `
                                <tr>
                                    <td><code>${p.name}</code></td>
                                    <td>${p.in}</td>
                                    <td>${p.type}</td>
                                    <td>${p.required ? '✓' : ''}</td>
                                    <td>${p.description || ''}</td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                </div>
            ` : ''}

            ${endpoint.requestBody ? `
                <div class="endpoint-section">
                    <h4>Request Body</h4>
                    <div class="schema-ref">
                        <a href="#definition-${endpoint.requestBody.schema}" class="schema-link">
                            ${endpoint.requestBody.schema}
                        </a>
                    </div>
                </div>
            ` : ''}

            ${endpoint.response ? `
                <div class="endpoint-section">
                    <h4>Response</h4>
                    <div class="schema-ref">
                        ${endpoint.response.ref
            ? `<a href="#definition-${endpoint.response.schema}" class="schema-link">${endpoint.response.schema}</a>`
            : `<code>${endpoint.response.schema}</code>`
        }
                    </div>
                </div>
            ` : ''}

            ${!canTest ? `
                <div class="endpoint-info">
                    <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="12" y1="16" x2="12" y2="12"></line>
                        <line x1="12" y1="8" x2="12.01" y2="8"></line>
                    </svg>
                    <span>This endpoint is ${endpoint.action} mode (fire-and-forget).</span>
                </div>
            ` : ''}
        </div>
    `;

    // Collapse/expand
    const header = card.querySelector('.endpoint-header');
    const body = card.querySelector('.endpoint-body');
    header.addEventListener('click', (e) => {
        if (e.target.closest('.btn-try') || e.target.closest('.btn-cli')) return;
        body.classList.toggle('hidden');
        card.classList.toggle('collapsed');
    });

    // Try it button
    const tryBtn = card.querySelector('.btn-try');
    if (tryBtn) {
        tryBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            openTryItModal(endpoint);
        });
    }

    // CLI button
    const cliBtn = card.querySelector('.btn-cli');
    if (cliBtn) {
        cliBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            copyNatsCliCommand(endpoint, cliBtn);
        });
    }

    return card;
}

// Render definitions section
function renderDefinitions(filter = '') {
    definitionList.innerHTML = '';
    definitionsSection.innerHTML = '';

    const definitions = Object.entries(spec.definitions);
    const filterLower = filter.toLowerCase();

    let count = 0;
    const filteredDefs = [];

    for (const [name, def] of definitions) {
        if (filter && !name.toLowerCase().includes(filterLower)) continue;
        count++;
        filteredDefs.push([name, def]);

        // Sidebar item
        const item = document.createElement('div');
        item.className = 'definition-item';
        item.textContent = name;
        item.onclick = () => scrollToDefinition(name);
        definitionList.appendChild(item);
    }

    // Create header (same structure as tag-section-header)
    const defHeader = document.createElement('div');
    defHeader.className = 'tag-section-header';
    defHeader.id = 'definitionsHeader';
    defHeader.innerHTML = `
            <span class="section-toggle">▼</span>
            <h2>Definitions</h2>
            <span class="tag-count-badge">${count}</span>
            <span class="tag-description"></span>
        `;

    // Create content container
    const defContent = document.createElement('div');
    defContent.className = 'definitions-section-content';
    defContent.id = 'definitionsContent';

    // Add click handler for collapse/expand
    defHeader.onclick = () => {
        defHeader.classList.toggle('collapsed');
        defContent.classList.toggle('hidden');
    };

    // Add cards to content
    for (const [name, def] of filteredDefs) {
        const card = createDefinitionCard(name, def);
        defContent.appendChild(card);
    }

    definitionsSection.appendChild(defHeader);
    definitionsSection.appendChild(defContent);

    definitionCount.textContent = count;
}

// Create definition card
function createDefinitionCard(name, def) {
    const card = document.createElement('div');
    card.className = 'definition-card';
    card.id = `definition-${name}`;

    let content = '';

    if (def.kind === 'enum') {
        content = `
            <div class="definition-enum">
                <h4>Enum Values</h4>
                <ul>
                    ${def.enumValues.map(v => `<li><code>${v}</code></li>`).join('')}
                </ul>
            </div>
        `;
    } else {
        content = `
            <table class="props-table">
                <thead>
                    <tr>
                        <th>Property</th>
                        <th>Type</th>
                        <th>Required</th>
                        <th>Description</th>
                    </tr>
                </thead>
                <tbody>
                    ${def.properties.map(p => `
                        <tr>
                            <td><code>${p.name}</code></td>
                            <td>${p.ref
                ? `<a href="#definition-${p.type}" class="schema-link">${p.type}</a>`
                : `<code>${p.type}</code>`
            }</td>
                            <td>${p.required ? '✓' : ''}</td>
                            <td>${p.description || ''}</td>
                        </tr>
                    `).join('')}
                </tbody>
            </table>
            ${def.example ? `
                <div class="definition-example">
                    <h4>Example</h4>
                    <pre><code>${escapeHtml(def.example)}</code></pre>
                </div>
            ` : ''}
        `;
    }

    card.innerHTML = `
        <div class="definition-header">
            <h3>${name}</h3>
            <span class="definition-kind">${def.kind}</span>
        </div>
        <div class="definition-body">
            ${content}
        </div>
    `;

    // Collapse/expand
    const header = card.querySelector('.definition-header');
    const body = card.querySelector('.definition-body');
    header.addEventListener('click', () => {
        body.classList.toggle('hidden');
        card.classList.toggle('collapsed');
    });

    return card;
}

// Open Try It modal
function openTryItModal(endpoint) {
    currentEndpoint = endpoint;

    const modal = document.getElementById('tryItModal');
    const actionBadge = document.getElementById('modalAction');
    const methodTitle = document.getElementById('modalMethod');
    const subjectBuilder = document.getElementById('modalSubjectBuilder');
    const bodySection = document.getElementById('modalBodySection');
    const requestBody = document.getElementById('modalRequestBody');
    const responseSection = document.getElementById('modalResponseSection');

    // Set header
    const badgeInfo = getBadgeInfo(endpoint);
    actionBadge.textContent = badgeInfo.label;
    actionBadge.style.background = badgeInfo.bg;
    actionBadge.style.color = badgeInfo.text;
    methodTitle.textContent = endpoint.method;

    // Build subject builder
    buildSubjectBuilder(subjectBuilder, endpoint);

    // Request body
    if (endpoint.requestBody) {
        bodySection.style.display = 'block';
        const def = spec.definitions[endpoint.requestBody.schema];
        requestBody.value = def?.example || '{}';
    } else {
        bodySection.style.display = 'none';
        requestBody.value = '{}';
    }

    // Hide response
    responseSection.style.display = 'none';

    // Show modal
    modal.classList.add('show');
}

// Build subject builder
function buildSubjectBuilder(container, endpoint) {
    container.innerHTML = '';

    const subject = endpoint.resolvedSubject;
    const parts = subject.split('.');
    const params = endpoint.parameters.filter(p => p.in === 'subject');

    let paramIndex = 0;

    for (let i = 0; i < parts.length; i++) {
        const part = parts[i];

        if (part === '*' && paramIndex < params.length) {
            const param = params[paramIndex];
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'subject-input';
            input.placeholder = `{${param.name}}`;
            input.dataset.param = param.name;
            container.appendChild(input);
            paramIndex++;
        } else {
            const span = document.createElement('span');
            span.className = 'subject-static';
            span.textContent = part;
            container.appendChild(span);
        }

        if (i < parts.length - 1) {
            const dot = document.createElement('span');
            dot.className = 'subject-dot';
            dot.textContent = '.';
            container.appendChild(dot);
        }
    }
}

// Build subject from modal inputs
function buildSubjectFromModal() {
    const container = document.getElementById('modalSubjectBuilder');
    const parts = [];

    container.querySelectorAll('.subject-static, .subject-input').forEach(el => {
        if (el.classList.contains('subject-input')) {
            const value = el.value.trim() || el.placeholder.replace(/[{}]/g, '');
            parts.push(value);
        } else {
            parts.push(el.textContent);
        }
    });

    return parts.join('.');
}

// Execute request from modal
async function executeModalRequest() {
    if (!currentEndpoint) return;

    const executeBtn = document.getElementById('modalExecuteBtn');
    const responseSection = document.getElementById('modalResponseSection');
    const responseViewer = document.getElementById('modalResponseViewer');
    const responseTime = document.getElementById('modalResponseTime');
    const responseStatus = document.getElementById('modalResponseStatus');

    executeBtn.disabled = true;
    executeBtn.textContent = 'Sending...';

    const startTime = performance.now();

    try {
        const subject = buildSubjectFromModal();

        let payload = null;
        const bodyText = document.getElementById('modalRequestBody').value.trim();
        if (bodyText && bodyText !== '{}') {
            try {
                payload = JSON.parse(bodyText);
            } catch (e) {
                throw new Error('Invalid JSON: ' + e.message);
            }
        }

        // Use different API based on action type
        const isFireAndForget = ['Publish', 'Consume', 'Fetch'].includes(currentEndpoint.action);
        const apiEndpoint = isFireAndForget ? API_FIRE : API_PUBLISH;

        const response = await fetch(apiEndpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                endpointId: currentEndpoint.operationId,
                subject: subject,
                payload: payload,
                timeoutMs: 5000
            })
        });

        const elapsedMs = Math.round(performance.now() - startTime);
        const result = await response.json();

        responseSection.style.display = 'block';
        responseTime.textContent = `${result.elapsedMs || elapsedMs}ms`;

        if (result.success) {
            responseStatus.textContent = 'Success';
            responseStatus.className = 'response-status success';
            if (isFireAndForget) {
                responseViewer.innerHTML = `<pre>${syntaxHighlight(JSON.stringify({ message: 'Message published successfully', subject: subject }, null, 2))}</pre>`;
            } else {
                responseViewer.innerHTML = `<pre>${syntaxHighlight(JSON.stringify(result.responseData, null, 2))}</pre>`;
            }
        } else {
            responseStatus.textContent = `Error ${result.errorCode || ''}`;
            responseStatus.className = 'response-status error';
            responseViewer.innerHTML = `<pre class="error-text">${escapeHtml(result.errorMessage || 'Unknown error')}</pre>`;
        }

    } catch (error) {
        responseSection.style.display = 'block';
        responseTime.textContent = `${Math.round(performance.now() - startTime)}ms`;
        responseStatus.textContent = 'Error';
        responseStatus.className = 'response-status error';
        responseViewer.innerHTML = `<pre class="error-text">${escapeHtml(error.message)}</pre>`;
    } finally {
        executeBtn.disabled = false;
        executeBtn.textContent = 'Execute';
    }
}

// Clear modal response
function clearModalResponse() {
    document.getElementById('modalResponseSection').style.display = 'none';
    document.getElementById('modalRequestBody').value = currentEndpoint?.requestBody
        ? (spec.definitions[currentEndpoint.requestBody.schema]?.example || '{}')
        : '{}';
}

// Scroll helpers
function scrollToEndpoint(operationId) {
    const el = document.getElementById(`endpoint-${operationId}`);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        el.classList.add('highlight');
        setTimeout(() => el.classList.remove('highlight'), 1500);
    }
}

function scrollToDefinition(name) {
    const el = document.getElementById(`definition-${name}`);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        el.classList.add('highlight');
        setTimeout(() => el.classList.remove('highlight'), 1500);
    }
}

// Syntax highlight JSON
function syntaxHighlight(json) {
    if (!json) return '';
    json = escapeHtml(json);
    return json.replace(/("(\\u[a-zA-Z0-9]{4}|\\[^u]|[^\\"])*"(\s*:)?|\b(true|false|null)\b|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?)/g, function (match) {
        let cls = 'number';
        if (/^"/.test(match)) {
            if (/:$/.test(match)) {
                cls = 'key';
            } else {
                cls = 'string';
            }
        } else if (/true|false/.test(match)) {
            cls = 'boolean';
        } else if (/null/.test(match)) {
            cls = 'null';
        }
        return '<span style="color: ' + getHighlightColor(cls) + '">' + match + '</span>';
    });
}

function getHighlightColor(cls) {
    switch (cls) {
        case 'key': return '#7dd3fc';
        case 'string': return '#86efac';
        case 'number': return '#fcd34d';
        case 'boolean': return '#c4b5fd';
        case 'null': return '#94a3b8';
        default: return '#e2e8f0';
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Generate NATS CLI command text for an endpoint
function generateNatsCliCommand(endpoint) {
    const subject = endpoint.resolvedSubject;
    const subjectWithPlaceholder = subject.replace(/\*/g, '<id>');

    // Get example payload if available
    let examplePayload = null;
    if (endpoint.requestBody && spec.definitions[endpoint.requestBody.schema]) {
        const def = spec.definitions[endpoint.requestBody.schema];
        if (def.example) {
            try {
                examplePayload = JSON.parse(def.example);
            } catch (e) {
                examplePayload = null;
            }
        }
    }

    // Determine command based on action type
    let command = '';

    switch (endpoint.action) {
        case 'Request':
            // Request-Reply: use nats req - response is JSON text
            if (examplePayload) {
                const formattedPayload = JSON.stringify(examplePayload, null, 2);
                command = `nats req '${subjectWithPlaceholder}' '\n${formattedPayload}\n'`;
            } else {
                command = `nats req '${subjectWithPlaceholder}' ''`;
            }
            break;

        case 'Publish':
        case 'Consume':
        case 'Fetch':
        default:
            // Pub-Sub and JetStream: use nats pub
            if (examplePayload) {
                const formattedPayload = JSON.stringify(examplePayload, null, 2);
                command = `nats pub '${subjectWithPlaceholder}' '\n${formattedPayload}\n'`;
            } else {
                command = `nats pub '${subjectWithPlaceholder}' '{}'`;
            }
            break;
    }

    return command;
}

// Copy NATS CLI command to clipboard
function copyNatsCliCommand(endpoint, button) {
    const command = generateNatsCliCommand(endpoint);

    navigator.clipboard.writeText(command).then(() => {
        // Show feedback
        const originalHtml = button.innerHTML;
        button.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
            Copied!
        `;
        button.classList.add('copied');

        setTimeout(() => {
            button.innerHTML = originalHtml;
            button.classList.remove('copied');
        }, 1500);
    }).catch(err => {
        console.error('Failed to copy:', err);
    });
}

// Copy to clipboard helper (for other uses)
function copyToClipboard(elementId, button) {
    const element = document.getElementById(elementId);
    const text = element.textContent;

    navigator.clipboard.writeText(text).then(() => {
        // Show feedback
        const originalHtml = button.innerHTML;
        button.innerHTML = `
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
        `;
        button.classList.add('copied');

        setTimeout(() => {
            button.innerHTML = originalHtml;
            button.classList.remove('copied');
        }, 1500);
    }).catch(err => {
        console.error('Failed to copy:', err);
    });
}

// Format example value for display in table
function formatExampleValue(value) {
    if (value === null || value === undefined) return '';
    if (typeof value === 'object') {
        return `<code>${escapeHtml(JSON.stringify(value))}</code>`;
    }
    if (typeof value === 'string') {
        return `<code>"${escapeHtml(value)}"</code>`;
    }
    return `<code>${escapeHtml(String(value))}</code>`;
}

// Event listeners
function setupEventListeners() {
    // Version select
    versionSelect.addEventListener('change', () => {
        selectedVersion = versionSelect.value;
        renderAll(filterInput.value);
    });

    // Filter input
    filterInput.addEventListener('input', (e) => {
        renderAll(e.target.value);
    });

    // Close modal
    document.getElementById('closeModal').addEventListener('click', () => {
        document.getElementById('tryItModal').classList.remove('show');
    });

    // Modal backdrop click
    document.getElementById('tryItModal').addEventListener('click', (e) => {
        if (e.target.id === 'tryItModal') {
            e.target.classList.remove('show');
        }
    });

    // Execute button
    document.getElementById('modalExecuteBtn').addEventListener('click', executeModalRequest);

    // Clear button
    document.getElementById('modalClearBtn').addEventListener('click', clearModalResponse);

    // Schema links
    document.addEventListener('click', (e) => {
        if (e.target.classList.contains('schema-link')) {
            e.preventDefault();
            const href = e.target.getAttribute('href');
            if (href && href.startsWith('#definition-')) {
                const name = href.replace('#definition-', '');
                scrollToDefinition(name);
            }
        }
    });
}
