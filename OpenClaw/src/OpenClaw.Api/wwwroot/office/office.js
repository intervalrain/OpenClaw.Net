/**
 * Agent Office Floor Plan Visualization
 * Real-time + Replay mode for agent activity monitoring
 */

const API_BASE = '/api/v1/agent-activity';
const DESK_WIDTH = 160;
const DESK_HEIGHT = 120;
const DESK_GAP_X = 40;
const DESK_GAP_Y = 50;
const COLS = 5;
const OFFSET_X = 60;
const OFFSET_Y = 60;

// State
let agents = {};         // userId -> { name, status, type, detail, deskEl, bubbleEl, history[] }
let selectedAgentId = null;
let isLiveMode = true;
let replayEvents = [];
let replayIndex = 0;
let replayTimer = null;
let replayPlaying = false;

// ─── Initialization ────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', async () => {
    initTopHeader('office');
    initEventListeners();
    await loadUsers();
    await loadCurrentState();
    startSSE();
});

function initEventListeners() {
    document.getElementById('liveModeBtn').addEventListener('click', () => switchMode('live'));
    document.getElementById('replayModeBtn').addEventListener('click', () => switchMode('replay'));
    document.getElementById('detailClose').addEventListener('click', closeDetailPanel);
    document.getElementById('loadReplayBtn').addEventListener('click', loadReplayData);
    document.getElementById('replayPlayBtn').addEventListener('click', toggleReplayPlayback);
    document.getElementById('replayScrubber').addEventListener('input', onScrubberChange);
    document.getElementById('replaySpeed').addEventListener('change', updateReplaySpeed);

    // Set default replay time range (last 1 hour)
    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
    document.getElementById('replayTo').value = formatDateTimeLocal(now);
    document.getElementById('replayFrom').value = formatDateTimeLocal(oneHourAgo);
}

// ─── Data Loading ──────────────────────────────────────────────────

async function loadUsers() {
    try {
        const token = localStorage.getItem('authToken');
        const res = await fetch(`${API_BASE}/users`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (res.status === 403) {
            // Not admin — load self only
            const meRes = await fetch('/api/v1/users/me', {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            if (meRes.ok) {
                const me = await meRes.json();
                addAgent(me.id, me.name || me.email);
            }
            return;
        }

        if (!res.ok) return;
        const users = await res.json();
        users.filter(u => u.status === 'Active').forEach((u, i) => {
            addAgent(u.id, u.name || u.email);
        });
    } catch (e) {
        console.error('Failed to load users:', e);
    }
}

async function loadCurrentState() {
    try {
        const token = localStorage.getItem('authToken');
        const res = await fetch(`${API_BASE}/current`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!res.ok) return;
        const activities = await res.json();
        activities.forEach(a => updateAgentState(a));
    } catch (e) {
        console.error('Failed to load current state:', e);
    }
}

// ─── Agent Management ──────────────────────────────────────────────

function addAgent(userId, name) {
    if (agents[userId]) return;

    const index = Object.keys(agents).length;
    const col = index % COLS;
    const row = Math.floor(index / COLS);
    const x = OFFSET_X + col * (DESK_WIDTH + DESK_GAP_X);
    const y = OFFSET_Y + row * (DESK_HEIGHT + DESK_GAP_Y);

    const initials = getInitials(name);

    const deskEl = createDeskSVG(userId, x, y, name, initials);
    document.getElementById('desksGroup').appendChild(deskEl);

    agents[userId] = {
        name,
        status: 'idle',
        type: null,
        detail: null,
        deskEl,
        bubbleEl: null,
        x, y,
        history: []
    };

    updateAgentCount();

    // Update SVG viewBox to fit all desks
    const totalRows = Math.floor(Object.keys(agents).length / COLS) + 1;
    const svgHeight = Math.max(800, OFFSET_Y + totalRows * (DESK_HEIGHT + DESK_GAP_Y) + 40);
    document.getElementById('officeSvg').setAttribute('viewBox', `0 0 1200 ${svgHeight}`);
}

function createDeskSVG(userId, x, y, name, initials) {
    const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    g.setAttribute('class', 'desk-group idle');
    g.setAttribute('data-user-id', userId);
    g.setAttribute('transform', `translate(${x},${y})`);
    g.addEventListener('click', () => selectAgent(userId));

    // Desk background
    const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    rect.setAttribute('class', 'desk-bg');
    rect.setAttribute('width', DESK_WIDTH);
    rect.setAttribute('height', DESK_HEIGHT);
    rect.setAttribute('rx', '8');
    g.appendChild(rect);

    // Avatar circle
    const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
    circle.setAttribute('class', 'desk-avatar');
    circle.setAttribute('cx', DESK_WIDTH / 2);
    circle.setAttribute('cy', 38);
    circle.setAttribute('r', 22);
    g.appendChild(circle);

    // Avatar initials
    const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    text.setAttribute('class', 'desk-avatar-text');
    text.setAttribute('x', DESK_WIDTH / 2);
    text.setAttribute('y', 38);
    text.textContent = initials;
    g.appendChild(text);

    // Name label
    const nameText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    nameText.setAttribute('class', 'desk-name');
    nameText.setAttribute('x', DESK_WIDTH / 2);
    nameText.setAttribute('y', 75);
    nameText.textContent = name.length > 14 ? name.substring(0, 12) + '..' : name;
    g.appendChild(nameText);

    // Status icon
    const statusIcon = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    statusIcon.setAttribute('class', 'desk-status-icon');
    statusIcon.setAttribute('x', DESK_WIDTH / 2);
    statusIcon.setAttribute('y', 100);
    statusIcon.setAttribute('data-role', 'status-icon');
    g.appendChild(statusIcon);

    return g;
}

// ─── State Updates ─────────────────────────────────────────────────

function updateAgentState(event) {
    const userId = event.userId;

    // Auto-add agents we haven't seen yet
    if (!agents[userId] && event.userName) {
        addAgent(userId, event.userName);
    }

    const agent = agents[userId];
    if (!agent) return;

    const cssStatus = statusToCss(event.status);
    agent.status = cssStatus;
    agent.type = event.type;
    agent.detail = event.detail;

    // Update desk CSS class
    agent.deskEl.className.baseVal = `desk-group ${cssStatus}${selectedAgentId === userId ? ' selected' : ''}`;

    // Update status icon
    const iconEl = agent.deskEl.querySelector('[data-role="status-icon"]');
    if (iconEl) {
        iconEl.textContent = statusToIcon(event.status);
    }

    // Update speech bubble
    updateBubble(userId, event);

    // Add to history
    agent.history.unshift({
        time: new Date(event.createdAt),
        status: event.status,
        type: event.type,
        detail: event.detail
    });
    if (agent.history.length > 50) agent.history.pop();

    // Update detail panel if this agent is selected
    if (selectedAgentId === userId) {
        refreshDetailPanel(userId);
    }

    // Auto-clear completed/failed after 5s
    if (cssStatus === 'completed' || cssStatus === 'failed') {
        setTimeout(() => {
            if (agent.status === cssStatus) {
                agent.status = 'idle';
                agent.deskEl.className.baseVal = `desk-group idle${selectedAgentId === userId ? ' selected' : ''}`;
                iconEl.textContent = '';
                removeBubble(userId);
            }
        }, 5000);
    }
}

function updateBubble(userId, event) {
    const agent = agents[userId];
    if (!agent) return;

    removeBubble(userId);

    const cssStatus = statusToCss(event.status);
    if (cssStatus === 'idle') return;

    const bubble = document.createElement('div');
    bubble.className = `speech-bubble ${cssStatus}`;
    bubble.dataset.userId = userId;

    const icon = statusToIcon(event.status);
    let text = statusToLabel(event.status);
    if (event.detail) text += `: ${event.detail}`;
    if (event.sourceName) text = `[${event.sourceName}] ${text}`;

    bubble.innerHTML = `<span class="bubble-icon">${icon}</span>${escapeHtml(text)}`;
    bubble.title = text;

    // Position relative to the SVG desk
    const svg = document.getElementById('officeSvg');
    const svgRect = svg.getBoundingClientRect();
    const viewBox = svg.viewBox.baseVal;
    const scaleX = svgRect.width / viewBox.width;
    const scaleY = svgRect.height / viewBox.height;

    const bubbleLeft = svgRect.left + (agent.x + DESK_WIDTH / 2 - 60) * scaleX;
    const bubbleTop = svgRect.top + (agent.y - 10) * scaleY;

    bubble.style.left = `${bubbleLeft - svgRect.left}px`;
    bubble.style.top = `${bubbleTop - svgRect.top - 30}px`;

    document.getElementById('bubblesOverlay').appendChild(bubble);
    agent.bubbleEl = bubble;
}

function removeBubble(userId) {
    const agent = agents[userId];
    if (agent?.bubbleEl) {
        agent.bubbleEl.remove();
        agent.bubbleEl = null;
    }
}

// ─── SSE Live Mode ─────────────────────────────────────────────────

let sseAbortController = null;

function startSSE() {
    stopSSE();
    sseAbortController = new AbortController();

    const token = localStorage.getItem('authToken');

    fetch(`${API_BASE}/stream`, {
        headers: { 'Authorization': `Bearer ${token}` },
        signal: sseAbortController.signal
    }).then(async response => {
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop(); // Keep incomplete line in buffer

            for (const line of lines) {
                if (line.startsWith('data: ')) {
                    try {
                        const event = JSON.parse(line.substring(6));
                        updateAgentState(event);
                    } catch (err) {
                        console.error('SSE parse error:', err);
                    }
                }
            }
        }
    }).catch(err => {
        if (err.name === 'AbortError') return;
        console.warn('SSE connection lost, reconnecting in 5s...', err);
        setTimeout(() => {
            if (isLiveMode) startSSE();
        }, 5000);
    });
}

function stopSSE() {
    if (sseAbortController) {
        sseAbortController.abort();
        sseAbortController = null;
    }
}

// ─── Replay Mode ───────────────────────────────────────────────────

async function loadReplayData() {
    const from = document.getElementById('replayFrom').value;
    const to = document.getElementById('replayTo').value;
    if (!from || !to) return;

    try {
        const token = localStorage.getItem('authToken');
        const res = await fetch(
            `${API_BASE}/history?from=${new Date(from).toISOString()}&to=${new Date(to).toISOString()}`,
            { headers: { 'Authorization': `Bearer ${token}` } }
        );
        if (!res.ok) return;
        replayEvents = await res.json();
        replayIndex = 0;
        document.getElementById('replayScrubber').max = Math.max(1, replayEvents.length - 1);
        document.getElementById('replayScrubber').value = 0;
        updateReplayTimeDisplay();

        // Reset all agents to idle
        Object.keys(agents).forEach(id => {
            agents[id].status = 'idle';
            agents[id].deskEl.className.baseVal = 'desk-group idle';
            removeBubble(id);
        });
    } catch (e) {
        console.error('Failed to load replay data:', e);
    }
}

function toggleReplayPlayback() {
    if (replayPlaying) {
        pauseReplay();
    } else {
        playReplay();
    }
}

function playReplay() {
    if (replayIndex >= replayEvents.length) {
        replayIndex = 0; // Loop
    }
    replayPlaying = true;
    updatePlayButton();
    scheduleNextReplayEvent();
}

function pauseReplay() {
    replayPlaying = false;
    if (replayTimer) clearTimeout(replayTimer);
    updatePlayButton();
}

function scheduleNextReplayEvent() {
    if (!replayPlaying || replayIndex >= replayEvents.length) {
        pauseReplay();
        return;
    }

    const event = replayEvents[replayIndex];
    updateAgentState(event);
    document.getElementById('replayScrubber').value = replayIndex;
    updateReplayTimeDisplay();

    replayIndex++;

    if (replayIndex < replayEvents.length) {
        const current = new Date(event.createdAt).getTime();
        const next = new Date(replayEvents[replayIndex].createdAt).getTime();
        const speed = parseInt(document.getElementById('replaySpeed').value) || 5;
        const delay = Math.max(50, Math.min(2000, (next - current) / speed));
        replayTimer = setTimeout(scheduleNextReplayEvent, delay);
    } else {
        pauseReplay();
    }
}

function onScrubberChange(e) {
    replayIndex = parseInt(e.target.value);
    if (replayIndex < replayEvents.length) {
        // Reset all to idle, then replay up to current position
        Object.keys(agents).forEach(id => {
            agents[id].status = 'idle';
            agents[id].deskEl.className.baseVal = 'desk-group idle';
            removeBubble(id);
        });

        // Apply the last event per user up to current index
        const latestPerUser = {};
        for (let i = 0; i <= replayIndex; i++) {
            latestPerUser[replayEvents[i].userId] = replayEvents[i];
        }
        Object.values(latestPerUser).forEach(evt => updateAgentState(evt));
        updateReplayTimeDisplay();
    }
}

function updateReplaySpeed() {
    // Speed change takes effect on the next scheduled event
}

function updatePlayButton() {
    const btn = document.getElementById('replayPlayBtn');
    btn.innerHTML = replayPlaying
        ? '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>'
        : '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="5 3 19 12 5 21 5 3"/></svg>';
}

function updateReplayTimeDisplay() {
    const el = document.getElementById('replayTime');
    if (replayIndex < replayEvents.length) {
        const d = new Date(replayEvents[replayIndex].createdAt);
        el.textContent = d.toLocaleTimeString();
    } else {
        el.textContent = '--:--:--';
    }
}

// ─── Mode Switching ────────────────────────────────────────────────

function switchMode(mode) {
    isLiveMode = mode === 'live';

    document.getElementById('liveModeBtn').classList.toggle('active', isLiveMode);
    document.getElementById('replayModeBtn').classList.toggle('active', !isLiveMode);
    document.getElementById('replayControls').classList.toggle('hidden', isLiveMode);

    if (isLiveMode) {
        pauseReplay();
        startSSE();
        loadCurrentState();
    } else {
        stopSSE();
    }
}

// ─── Detail Panel ──────────────────────────────────────────────────

function selectAgent(userId) {
    // Deselect previous
    if (selectedAgentId && agents[selectedAgentId]) {
        agents[selectedAgentId].deskEl.classList.remove('selected');
    }

    selectedAgentId = userId;
    agents[userId].deskEl.classList.add('selected');
    refreshDetailPanel(userId);
    document.getElementById('detailPanel').classList.remove('hidden');
}

function closeDetailPanel() {
    if (selectedAgentId && agents[selectedAgentId]) {
        agents[selectedAgentId].deskEl.classList.remove('selected');
    }
    selectedAgentId = null;
    document.getElementById('detailPanel').classList.add('hidden');
}

function refreshDetailPanel(userId) {
    const agent = agents[userId];
    if (!agent) return;

    document.getElementById('detailAvatar').textContent = getInitials(agent.name);
    document.getElementById('detailName').textContent = agent.name;

    const statusEl = document.getElementById('detailStatus');
    statusEl.textContent = statusToLabel(agent.status === 'idle' ? 'idle' : agent.history[0]?.status || 'idle');
    statusEl.className = `detail-status ${agent.status}`;

    const activityText = agent.detail
        ? `${agent.type || ''}: ${agent.detail}`
        : (agent.status === 'idle' ? 'No current activity' : `${agent.type || 'Working'}...`);
    document.getElementById('detailActivity').textContent = activityText;

    // Render history
    const historyEl = document.getElementById('detailHistory');
    historyEl.innerHTML = agent.history.slice(0, 20).map(h => `
        <div class="history-item">
            <span class="time">${h.time.toLocaleTimeString()}</span>
            <span class="desc">${statusToIcon(h.status)} ${escapeHtml(h.detail || h.type || h.status)}</span>
        </div>
    `).join('');
}

// ─── Helpers ───────────────────────────────────────────────────────

function getInitials(name) {
    return name.split(/[\s@]+/).filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('');
}

function statusToCss(status) {
    const map = {
        'Started': 'thinking',
        'Thinking': 'thinking',
        'ToolExecuting': 'tool-executing',
        'Completed': 'completed',
        'Failed': 'failed'
    };
    return map[status] || 'idle';
}

function statusToIcon(status) {
    const map = {
        'idle': '',
        'Started': '\u{1F4AD}',
        'Thinking': '\u{1F914}',
        'ToolExecuting': '\u{1F527}',
        'Completed': '\u2705',
        'Failed': '\u274C',
        'thinking': '\u{1F914}',
        'tool-executing': '\u{1F527}',
        'completed': '\u2705',
        'failed': '\u274C'
    };
    return map[status] || '';
}

function statusToLabel(status) {
    const map = {
        'idle': 'Idle',
        'Started': 'Starting',
        'Thinking': 'Thinking',
        'ToolExecuting': 'Executing Tool',
        'Completed': 'Completed',
        'Failed': 'Failed',
        'thinking': 'Thinking',
        'tool-executing': 'Executing Tool',
        'completed': 'Completed',
        'failed': 'Failed'
    };
    return map[status] || status;
}

function updateAgentCount() {
    const count = Object.keys(agents).length;
    document.getElementById('agentCount').textContent = `${count} agent${count !== 1 ? 's' : ''}`;
}

function formatDateTimeLocal(date) {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    const h = String(date.getHours()).padStart(2, '0');
    const min = String(date.getMinutes()).padStart(2, '0');
    return `${y}-${m}-${d}T${h}:${min}`;
}

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}
