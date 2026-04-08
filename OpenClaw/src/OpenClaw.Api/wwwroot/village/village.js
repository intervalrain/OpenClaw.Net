/**
 * Agent Village — Phaser 3 + Tiled Map + A* Pathfinding + Real-time SSE
 */

const API_BASE = '/api/v1/agent-activity';
const TILE = 32;
const ASSET_PATH = '/village/assets';

// Sector display names (friendly labels)
const ZONE_LABELS = {
    'Hobbs Cafe':                          'Cafe',
    'The Rose and Crown Pub':              'Pub',
    'Oak Hill College':                    'College',
    'Dorm for Oak Hill College':           'Library',
    'Harvey Oak Supply Store':             'Workshop',
    'The Willows Market and Pharmacy':     'Market',
    'Johnson Park':                        'Park',
    "Arthur Burton's apartment":           'Post Office',
    "artist's co-living space":            'Studio',
    "Isabella Rodriguez's apartment":      'Home',
    "Ryan Park's apartment":              'Office',
    "Carlos Gomez's apartment":           'Lounge',
    "Giorgio Rossi's apartment":          'Lab',
    "Adam Smith's house":                 'Archives',
    "Lin family's house":                 'Garden',
    "Moore family's house":               'Cabin',
    "Moreno family's house":              'Quarters',
    "Tamara Taylor and Carmen Ortiz's house": 'Commons',
    "Yuriko Yamamoto's house":            'Retreat',
};

function getZoneLabel(sectorName) {
    return ZONE_LABELS[sectorName] || sectorName;
}

// Activity type → sector name mapping
const ACTIVITY_ZONES = {
    chat:          ['Hobbs Cafe', 'The Rose and Crown Pub'],
    cronjob:       ['Oak Hill College', 'Harvey Oak Supply Store'],
    toolexecution: ['The Willows Market and Pharmacy', 'Harvey Oak Supply Store'],
};

// Tool name → specific sector (more granular than activity type)
const TOOL_ZONES = {
    git:             'Oak Hill College',         // College — coding/study
    github:          'Oak Hill College',
    azure_devops:    'Oak Hill College',
    execute_command: 'Harvey Oak Supply Store',   // Workshop — building/running
    read_file:       'Dorm for Oak Hill College', // Library — reading
    write_file:      'Dorm for Oak Hill College',
    list_directory:  'Dorm for Oak Hill College',
    web_search:      'Oak Hill College',          // College — research
    http_request:    'The Willows Market and Pharmacy', // Market — fetching data
    send_email:      "Arthur Burton's apartment", // Post Office — sending mail
    manage_cronjob:  'Harvey Oak Supply Store',   // Workshop — scheduling
    manage_agent:    "artist's co-living space",  // Studio — creating agents
    preference:      "Isabella Rodriguez's apartment", // Home — personal settings
    image_gen:       "artist's co-living space",  // Studio — creative work
    pdf:             'Dorm for Oak Hill College',  // Library — documents
    notion:          "Ryan Park's apartment",     // Office — note-taking
};

const IDLE_ZONES = ['Johnson Park', 'Hobbs Cafe', 'The Rose and Crown Pub'];
const CHARACTERS = ['misa', 'alex', 'bob', 'carol', 'dave', 'eve'];
let myCharacter = 'misa'; // loaded from user preference

// ===== State =====
let agents = {};
let selectedAgentId = null;
let sseAbortController = null;
let gameScene = null;
let villageData = null; // { width, height, collision[][], sectors{} }

// ===== Init =====
document.addEventListener('DOMContentLoaded', async () => {
    initTopHeader('office');
    document.getElementById('detailClose').addEventListener('click', closeDetailPanel);
    document.getElementById('change-character-btn').addEventListener('click', showCharacterPicker);

    // Load village data first (collision grid + room positions)
    try {
        const res = await fetch(`${ASSET_PATH}/village_data.json`);
        villageData = await res.json();
        console.log(`Village data loaded: ${villageData.width}x${villageData.height}, ${Object.keys(villageData.sectors).length} sectors`);
    } catch (e) {
        console.error('Failed to load village data:', e);
    }

    startPhaser();
});

// ===== Phaser =====
let cursors, player, map;

function startPhaser() {
    const container = document.getElementById('game-container');
    new Phaser.Game({
        type: Phaser.AUTO,
        width: container.clientWidth || 1200,
        height: (window.innerHeight - 90) || 700,
        parent: 'game-container',
        pixelArt: true,
        physics: { default: 'arcade', arcade: { gravity: { y: 0 } } },
        scene: { preload, create, update }
    });
}

function preload() {
    this.load.tilemapTiledJSON('map', `${ASSET_PATH}/the_ville.json`);

    const rpg = `${ASSET_PATH}/map_assets/cute_rpg_word_VXAce/tilesets`;
    ['CuteRPG_Field_B','CuteRPG_Field_C','CuteRPG_Harbor_C','CuteRPG_Village_B',
     'CuteRPG_Forest_B','CuteRPG_Desert_C','CuteRPG_Mountains_B','CuteRPG_Desert_B',
     'CuteRPG_Forest_C'].forEach(n => this.load.image(n, `${rpg}/${n}.png`));

    ['Room_Builder_32x32','interiors_pt1','interiors_pt2','interiors_pt3','interiors_pt4','interiors_pt5']
        .forEach(n => this.load.image(n, `${ASSET_PATH}/map_assets/v1/${n}.png`));

    ['blocks_1','blocks_2','blocks_3']
        .forEach(n => this.load.image(n, `${ASSET_PATH}/map_assets/blocks/${n}.png`));

    this.load.atlas('atlas', `${ASSET_PATH}/atlas.png`, `${ASSET_PATH}/atlas.json`);
}

function create() {
    gameScene = this;
    map = this.make.tilemap({ key: 'map' });

    // Add all tilesets
    const tsNames = [
        'CuteRPG_Field_B','CuteRPG_Field_C','CuteRPG_Harbor_C','Room_Builder_32x32',
        'CuteRPG_Village_B','CuteRPG_Forest_B','CuteRPG_Desert_C','CuteRPG_Mountains_B',
        'CuteRPG_Desert_B','CuteRPG_Forest_C',
        'interiors_pt1','interiors_pt2','interiors_pt3','interiors_pt4','interiors_pt5',
        'blocks_1','blocks_2','blocks_3'
    ];
    const allTS = tsNames.map(n => map.addTilesetImage(n, n));

    // Create visible layers
    ['Bottom Ground','Exterior Ground','Exterior Decoration L1','Exterior Decoration L2',
     'Interior Ground','Wall','Interior Furniture L1','Interior Furniture L2 ']
        .forEach(name => map.createLayer(name, allTS, 0, 0));

    const fg1 = map.createLayer('Foreground L1', allTS, 0, 0);
    const fg2 = map.createLayer('Foreground L2', allTS, 0, 0);
    if (fg1) fg1.setDepth(10);
    if (fg2) fg2.setDepth(10);

    // Animations for all characters
    const anims = this.anims;
    CHARACTERS.forEach(char => {
        ['left','right','front','back'].forEach(dir => {
            anims.create({
                key: `${char}-${dir}-walk`,
                frames: anims.generateFrameNames('atlas', { prefix: `${char}-${dir}-walk.`, start: 0, end: 3, zeroPad: 3 }),
                frameRate: 6, repeat: -1
            });
        });
    });

    // Camera control sprite (invisible)
    player = this.physics.add.sprite(2200, 1500, 'atlas', 'misa-front').setAlpha(0).setDepth(-1);
    const camera = this.cameras.main;
    camera.startFollow(player);
    camera.setBounds(0, 0, map.widthInPixels, map.heightInPixels);
    cursors = this.input.keyboard.createCursorKeys();

    loadUsersAndStart();
}

function update() {
    if (!cursors || !player) return;
    const speed = 500;
    player.body.setVelocity(0);
    if (cursors.left.isDown) player.body.setVelocityX(-speed);
    if (cursors.right.isDown) player.body.setVelocityX(speed);
    if (cursors.up.isDown) player.body.setVelocityY(-speed);
    if (cursors.down.isDown) player.body.setVelocityY(speed);
}

// ===== Data Loading =====
async function loadUsersAndStart() {
    // Load character preferences for all users
    const charPrefs = {};
    try {
        const meRes = await authFetch('/api/v1/users/me');
        if (meRes.ok) {
            const me = await meRes.json();
            // Load my character preference
            const prefRes = await authFetch('/api/v1/user-config/village_character');
            if (prefRes.ok) {
                const pref = await prefRes.json();
                if (pref.value && CHARACTERS.includes(pref.value)) {
                    myCharacter = pref.value;
                    charPrefs[me.id] = pref.value;
                }
            }
        }
    } catch {}

    try {
        const res = await authFetch(`${API_BASE}/users`);
        if (res.status === 403) {
            const meRes = await authFetch('/api/v1/users/me');
            if (meRes.ok) { const me = await meRes.json(); addAgent(me.id, me.name || me.email, charPrefs[me.id]); }
        } else if (res.ok) {
            (await res.json()).filter(u => u.status === 'Active').forEach(u => addAgent(u.id, u.name || u.email, charPrefs[u.id]));
        }
    } catch (e) { console.error('loadUsers error:', e); }

    try {
        const res = await authFetch(`${API_BASE}/current`);
        if (res.ok) (await res.json()).forEach(a => updateAgentState(a));
    } catch (e) { console.error('loadCurrentState error:', e); }

    startSSE();
    updateAgentCount();
}

// ===== Agent Management =====
function addAgent(userId, name, character) {
    if (agents[userId] || !gameScene || !villageData) return;

    const char = character || CHARACTERS[Object.keys(agents).length % CHARACTERS.length];
    const index = Object.keys(agents).length;
    const zoneName = IDLE_ZONES[index % IDLE_ZONES.length];
    const pos = getRandomWalkableTile(zoneName);
    const px = pos[0] * TILE, py = pos[1] * TILE;

    const sprite = gameScene.physics.add.sprite(px, py, 'atlas', `${char}-front`)
        .setSize(30, 40).setOffset(0, 32).setDepth(5);

    const label = gameScene.add.text(px, py + 20, name.split(' ')[0], {
        font: '11px monospace', fill: '#ffffff',
        stroke: '#000000', strokeThickness: 3
    }).setOrigin(0.5).setDepth(6);

    const bubble = gameScene.add.text(px - 6, py - 74, '💤', {
        font: '18px monospace', fill: '#000',
        padding: { x: 6, y: 4 }, backgroundColor: '#ffffffdd'
    }).setDepth(7);

    sprite.setInteractive();
    sprite.on('pointerdown', () => selectAgent(userId));

    agents[userId] = {
        name, character: char, initials: getInitials(name), sprite, label, bubble,
        currentZone: zoneName, status: 'idle', detail: '', history: [],
        walkTimer: null, walkQueue: []
    };
    updateAgentCount();
}

function getRandomWalkableTile(sectorName) {
    if (!villageData) return [70, 50];
    const center = villageData.sectors[sectorName];
    if (!center) return [70, 50];
    // Find a random walkable tile within the sector area (radius up to 8)
    const candidates = [];
    for (let dy = -8; dy <= 8; dy++) {
        for (let dx = -8; dx <= 8; dx++) {
            const x = center[0] + dx, y = center[1] + dy;
            if (isWalkable(x, y)) candidates.push([x, y]);
        }
    }
    if (candidates.length > 0) return candidates[Math.floor(Math.random() * candidates.length)];
    return center;
}

// ===== Activity → Movement =====
function updateAgentState(activity) {
    const userId = activity.userId;
    if (!agents[userId]) addAgent(userId, activity.userName || 'Unknown');
    const agent = agents[userId];
    if (!agent) return;

    const statusRaw = (activity.status || '').toLowerCase();
    const typeRaw = (activity.type || '').toLowerCase();
    // For ToolExecution events, the tool name is in the detail field
    const toolName = (typeRaw === 'toolexecution' ? (activity.detail || '') : '').toLowerCase();
    const detail = activity.detail || activity.sourceName || '';

    // Choose target sector: tool-specific > activity-type > idle
    let targetZone;
    if (toolName && TOOL_ZONES[toolName]) {
        targetZone = TOOL_ZONES[toolName];
    } else if (ACTIVITY_ZONES[typeRaw]) {
        const sectors = ACTIVITY_ZONES[typeRaw];
        targetZone = sectors[Object.keys(agents).indexOf(userId) % sectors.length];
    } else {
        targetZone = IDLE_ZONES[Object.keys(agents).indexOf(userId) % IDLE_ZONES.length];
    }

    if (statusRaw === 'completed' || statusRaw === 'failed') {
        moveAgentToSector(userId, targetZone);
        setTimeout(() => {
            const idleZone = IDLE_ZONES[Math.floor(Math.random() * IDLE_ZONES.length)];
            moveAgentToSector(userId, idleZone);
        }, 8000);
    } else {
        moveAgentToSector(userId, targetZone);
    }

    // Update bubble
    const icons = { thinking: '💭', started: '💭', toolexecuting: '🔧', completed: '✅', failed: '❌' };
    const icon = icons[statusRaw] || '💤';
    const bubbleText = detail ? `${icon} ${detail}` : icon;
    if (agent.bubble) agent.bubble.setText(bubbleText.length > 22 ? bubbleText.substring(0, 22) + '..' : bubbleText);

    agent.status = statusRaw;
    agent.detail = detail;
    agent.history.unshift({ time: new Date(activity.createdAt || Date.now()), status: statusRaw, type: typeRaw, detail });
    if (agent.history.length > 20) agent.history.pop();
    if (selectedAgentId === userId) updateDetailPanel(userId);
}

function moveAgentToSector(userId, sectorName) {
    const agent = agents[userId];
    if (!agent || !gameScene || !villageData) return;
    if (agent.currentZone === sectorName && !agent.walkQueue.length) return;

    // Cancel current walk
    if (agent.walkTimer) { agent.walkTimer.remove(); agent.walkTimer = null; }

    const startX = Math.round(agent.sprite.x / TILE);
    const startY = Math.round(agent.sprite.y / TILE);
    const dest = getRandomWalkableTile(sectorName);

    // A* pathfinding
    const path = findPath(startX, startY, dest[0], dest[1]);
    if (path.length === 0) return;

    agent.currentZone = sectorName;
    agent.walkQueue = path;

    // Walk tile by tile
    let stepIdx = 0;
    const STEP_MS = 180;

    function step() {
        if (stepIdx >= agent.walkQueue.length) {
            agent.sprite.anims.stop();
            agent.sprite.setTexture('atlas', `${agent.character}-front`);
            agent.walkTimer = null;
            agent.walkQueue = [];
            return;
        }

        const tile = agent.walkQueue[stepIdx];
        const tx = tile[0] * TILE, ty = tile[1] * TILE;
        const dx = tx - agent.sprite.x, dy = ty - agent.sprite.y;

        // Direction
        let dir = 'front';
        if (Math.abs(dx) >= Math.abs(dy)) dir = dx > 0 ? 'right' : 'left';
        else dir = dy > 0 ? 'front' : 'back';

        agent.sprite.anims.play(`${agent.character}-${dir}-walk`, true);

        gameScene.tweens.add({
            targets: agent.sprite,
            x: tx, y: ty,
            duration: STEP_MS,
            ease: 'Linear',
            onUpdate: () => {
                agent.label.x = agent.sprite.x;
                agent.label.y = agent.sprite.y + 20;
                agent.bubble.x = agent.sprite.x - 6;
                agent.bubble.y = agent.sprite.y - 74;
            }
        });
        stepIdx++;
    }

    step();
    agent.walkTimer = gameScene.time.addEvent({
        delay: STEP_MS,
        callback: step,
        repeat: path.length - 1
    });
}

// ===== A* Pathfinding =====
function isWalkable(x, y) {
    if (!villageData) return false;
    if (x < 0 || y < 0 || x >= villageData.width || y >= villageData.height) return false;
    return villageData.collision[y][x] === 0;
}

function findPath(sx, sy, ex, ey) {
    if (!villageData) return [];
    if (sx === ex && sy === ey) return [];
    if (!isWalkable(ex, ey)) {
        // Find nearest walkable tile to target
        for (let r = 1; r < 8; r++) {
            for (let dx = -r; dx <= r; dx++) {
                for (let dy = -r; dy <= r; dy++) {
                    if (isWalkable(ex + dx, ey + dy)) { ex += dx; ey += dy; break; }
                }
                if (isWalkable(ex, ey)) break;
            }
            if (isWalkable(ex, ey)) break;
        }
    }

    const W = villageData.width, H = villageData.height;
    const key = (x, y) => y * W + x;
    const heuristic = (x, y) => Math.abs(x - ex) + Math.abs(y - ey);

    const open = new Map();
    const closed = new Set();
    const gScore = new Map();
    const parent = new Map();

    const startKey = key(sx, sy);
    gScore.set(startKey, 0);
    open.set(startKey, heuristic(sx, sy));

    const dirs = [[0,1],[0,-1],[1,0],[-1,0]];
    let iterations = 0;
    const MAX_ITER = 5000;

    while (open.size > 0 && iterations++ < MAX_ITER) {
        // Find lowest f-score in open set
        let bestKey = null, bestF = Infinity;
        for (const [k, f] of open) {
            if (f < bestF) { bestF = f; bestKey = k; }
        }

        const cx = bestKey % W, cy = Math.floor(bestKey / W);
        if (cx === ex && cy === ey) {
            // Reconstruct path
            const path = [];
            let cur = bestKey;
            while (cur !== undefined && cur !== startKey) {
                path.unshift([cur % W, Math.floor(cur / W)]);
                cur = parent.get(cur);
            }
            return path;
        }

        open.delete(bestKey);
        closed.add(bestKey);

        const g = gScore.get(bestKey);

        for (const [ddx, ddy] of dirs) {
            const nx = cx + ddx, ny = cy + ddy;
            if (!isWalkable(nx, ny)) continue;
            const nk = key(nx, ny);
            if (closed.has(nk)) continue;

            const ng = g + 1;
            if (!gScore.has(nk) || ng < gScore.get(nk)) {
                gScore.set(nk, ng);
                parent.set(nk, bestKey);
                open.set(nk, ng + heuristic(nx, ny));
            }
        }
    }

    // Fallback: direct path ignoring collisions
    return buildDirectPath(sx, sy, ex, ey);
}

function buildDirectPath(sx, sy, ex, ey) {
    const path = [];
    let x = sx, y = sy;
    while (x !== ex) { x += x < ex ? 1 : -1; path.push([x, y]); }
    while (y !== ey) { y += y < ey ? 1 : -1; path.push([x, y]); }
    return path;
}

// ===== SSE =====
function startSSE() {
    if (sseAbortController) sseAbortController.abort();
    sseAbortController = new AbortController();
    fetch(`${API_BASE}/stream`, { headers: getAuthHeaders(), signal: sseAbortController.signal })
        .then(async response => {
            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';
            while (true) {
                const { done, value } = await reader.read();
                if (done) break;
                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split('\n');
                buffer = lines.pop();
                for (const line of lines) {
                    if (line.startsWith('data: ')) {
                        try { updateAgentState(JSON.parse(line.substring(6))); } catch {}
                    }
                }
            }
        })
        .catch(err => {
            if (err.name === 'AbortError') return;
            setTimeout(startSSE, 5000);
        });
}

// ===== Detail Panel =====
function selectAgent(userId) {
    selectedAgentId = userId;
    updateDetailPanel(userId);
    document.getElementById('detailPanel').classList.remove('hidden');
    // Pan camera to agent
    const agent = agents[userId];
    if (agent && player) {
        player.x = agent.sprite.x;
        player.y = agent.sprite.y;
    }
}

function closeDetailPanel() {
    selectedAgentId = null;
    document.getElementById('detailPanel').classList.add('hidden');
}

function updateDetailPanel(userId) {
    const a = agents[userId];
    if (!a) return;
    document.getElementById('detailAvatar').textContent = a.initials;
    document.getElementById('detailName').textContent = a.name;
    document.getElementById('detailStatus').textContent = `${a.status} — ${getZoneLabel(a.currentZone)}`;
    document.getElementById('detailActivity').textContent = a.detail || 'Idle';
    document.getElementById('detailHistory').innerHTML = a.history.length === 0
        ? '<div style="font-size:12px;color:var(--text-muted)">No activity</div>'
        : a.history.slice(0, 10).map(h => `
            <div class="history-item">
                <span class="time">${fmtTime(h.time)}</span>
                <span class="desc">${esc(h.type)}/${esc(h.status)}${h.detail ? ' — ' + esc(h.detail) : ''}</span>
            </div>`).join('');
}

// ===== Helpers =====
function getInitials(n) { return n.split(' ').map(w => w[0]).join('').substring(0,2).toUpperCase(); }
function fmtTime(d) { return (d instanceof Date ? d : new Date(d)).toLocaleTimeString([],{hour:'2-digit',minute:'2-digit',second:'2-digit'}); }
function updateAgentCount() { document.getElementById('agentCount').textContent = `${Object.keys(agents).length} agents`; }
function esc(t) { if(!t)return''; const d=document.createElement('div'); d.textContent=t; return d.innerHTML; }

// ===== Character Picker =====
const CHAR_COLORS = { misa: '#e74c3c', alex: '#e67e22', bob: '#27ae60', carol: '#2980b9', dave: '#8e44ad', eve: '#e84393' };

function showCharacterPicker() {
    const existing = document.getElementById('character-picker');
    if (existing) { existing.remove(); return; }

    const picker = document.createElement('div');
    picker.id = 'character-picker';
    picker.className = 'character-picker';

    const items = CHARACTERS.map(c => {
        const color = CHAR_COLORS[c] || '#888';
        const sel = c === myCharacter ? ' selected' : '';
        return `<div class="picker-item${sel}" data-char="${c}">` +
            `<div class="picker-dot" style="background:${color}"></div>` +
            `<span>${c}</span></div>`;
    }).join('');

    picker.innerHTML = `<div class="picker-title">Choose Character</div><div class="picker-grid">${items}</div>`;
    document.querySelector('.village-toolbar').appendChild(picker);

    // Use event delegation on picker
    picker.addEventListener('click', async (e) => {
        const item = e.target.closest('.picker-item');
        if (!item) return;

        const char = item.dataset.char;
        if (!char) return;
        myCharacter = char;

        // Save preference
        try {
            await authFetch('/api/v1/user-config/village_character', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ value: char, isSecret: false })
            });
        } catch (err) { console.error('Save character error:', err); }

        // Update sprite
        const user = getCurrentUser();
        if (user && agents[user.id]) {
            agents[user.id].character = char;
            agents[user.id].sprite.setTexture('atlas', `${char}-front`);
        }

        picker.remove();
    });

    // Close on click outside (deferred)
    setTimeout(() => {
        const handler = (e) => {
            if (!picker.contains(e.target) && e.target.id !== 'change-character-btn') {
                picker.remove();
                document.removeEventListener('click', handler);
            }
        };
        document.addEventListener('click', handler);
    }, 50);
}
