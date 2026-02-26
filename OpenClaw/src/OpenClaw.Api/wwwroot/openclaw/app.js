// DOM Elements
let messagesEl, userInputEl, sendBtn, themeToggle;
let currentConversationId = null;
let conversations = [];
let modelProviders = [];
let skills = [];

// Input history navigation (from current conversation)
let historyIndex = -1;
let tempInput = ''; // Store current input when navigating history

// Configure marked.js
marked.setOptions({
    highlight: function(code, lang) {
        if (lang && hljs.getLanguage(lang)) {
            return hljs.highlight(code, { language: lang }).value;
        }
        return hljs.highlightAuto(code).value;
    },
    breaks: true,
    gfm: true
});

document.addEventListener('DOMContentLoaded', () => {
    messagesEl = document.getElementById('messages');
    userInputEl = document.getElementById('user-input');
    sendBtn = document.getElementById('send-btn');
    themeToggle = document.getElementById('theme-toggle');

    // Initialize theme
    initTheme();

    // Event Listeners
    sendBtn.addEventListener('click', sendMessage);
    themeToggle.addEventListener('click', toggleTheme);

    userInputEl.addEventListener('keydown', (e) => {
        // Handle autocomplete navigation
        if (isAutocompleteVisible()) {
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                navigateAutocomplete(1);
                return;
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                navigateAutocomplete(-1);
                return;
            } else if (e.key === 'Tab' || e.key === 'Enter') {
                if (getSelectedAutocompleteItem()) {
                    e.preventDefault();
                    selectAutocompleteItem();
                    return;
                }
            } else if (e.key === 'Escape') {
                e.preventDefault();
                hideAutocomplete();
                return;
            }
        }

        // Handle input history navigation (↑↓ keys)
        // Only navigate history when:
        // - Input is empty, OR
        // - Already navigating history (historyIndex >= 0)
        // AND cursor is at the end (not editing middle of text)
        const isAtEnd = userInputEl.selectionStart === userInputEl.value.length;
        const canNavigateHistory = (userInputEl.value === '' || historyIndex >= 0) && isAtEnd;

        // ↑ = go to older messages (direction +1), ↓ = go to newer (direction -1)
        if (e.key === 'ArrowUp' && canNavigateHistory) {
            e.preventDefault();
            navigateInputHistory(1);
            return;
        } else if (e.key === 'ArrowDown' && historyIndex >= 0 && isAtEnd) {
            e.preventDefault();
            navigateInputHistory(-1);
            return;
        }
        // Otherwise, let ↑↓ keys work normally (cursor movement in textarea)

        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    // Autocomplete on input
    userInputEl.addEventListener('input', handleAutocompleteInput);

    // Sidebar toggle
    const sidebarToggle = document.getElementById('sidebar-toggle');
    const sidebar = document.querySelector('.sidebar');
    sidebarToggle.addEventListener('click', () => {
        sidebar.classList.toggle('collapsed');
    });

    // Profile dropdown toggle
    const profileBtn = document.getElementById('profile-btn');
    const profileSection = document.querySelector('.profile-section');
    profileBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        profileSection.classList.toggle('open');
    });

    // Close dropdown when clicking outside
    document.addEventListener('click', (e) => {
        if (!profileSection.contains(e.target)) {
            profileSection.classList.remove('open');
        }
    });

    // Initialize conversations
    document.getElementById('new-chat-btn').addEventListener('click', createNewConversation);
    loadConversations();

    // Preload skills for autocomplete
    loadSkills();
});

// Theme functions
function initTheme() {
    const savedTheme = localStorage.getItem('theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);
    updateHljsTheme(savedTheme);
    updateThemeButtonText(savedTheme);
}

function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
    updateHljsTheme(newTheme);
    updateThemeButtonText(newTheme);
}

function updateThemeButtonText(theme) {
    const themeText = document.querySelector('.theme-text');
    if (themeText) {
        themeText.textContent = theme === 'dark' ? 'Light Mode' : 'Dark Mode';
    }
}

function updateHljsTheme(theme) {
    const hljsLink = document.getElementById('hljs-theme');
    if (theme === 'light') {
        hljsLink.href = 'https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github.min.css';
    } else {
        hljsLink.href = 'https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github-dark.min.css';
    }
}

// Status indicator
function createStatusIndicator() {
    const indicator = document.createElement('div');
    indicator.className = 'message assistant status-indicator';
    indicator.innerHTML = '<span class="status-dot"></span><span class="status-text">Thinking...</span>';
    messagesEl.appendChild(indicator);
    messagesEl.scrollTop = messagesEl.scrollHeight;
    return indicator;
}

function updateStatusIndicator(indicator, type, toolName) {
    const textEl = indicator.querySelector('.status-text');
    switch (type) {
        case 'Thinking':
            textEl.textContent = 'Thinking...';
            break;
        case 'ToolExecuting':
            textEl.textContent = `Executing: ${toolName}...`;
            break;
        case 'ToolCompleted':
            textEl.textContent = `Completed: ${toolName}`;
            break;
    }
}

function removeStatusIndicator(indicator) {
    if (indicator && indicator.parentNode) {
        indicator.parentNode.removeChild(indicator);
    }
}

// Chat Functions
function addMessage(content, role) {
    const messageEl = document.createElement('div');
    messageEl.className = `message ${role}`;

    if (role === 'assistant') {
        messageEl.innerHTML = renderMarkdown(content);
        // Apply syntax highlighting to code blocks
        messageEl.querySelectorAll('pre code').forEach(block => {
            hljs.highlightElement(block);
        });
    } else {
        messageEl.textContent = content;
    }

    messagesEl.appendChild(messageEl);
    messagesEl.scrollTop = messagesEl.scrollHeight;
    return messageEl;
}

function createStreamingMessage() {
    const messageEl = document.createElement('div');
    messageEl.className = 'message assistant';
    messagesEl.appendChild(messageEl);
    return messageEl;
}

function updateStreamingMessage(messageEl, content) {
    messageEl.innerHTML = renderMarkdown(content);
    messageEl.querySelectorAll('pre code').forEach(block => {
        hljs.highlightElement(block);
    });
    messagesEl.scrollTop = messagesEl.scrollHeight;
}

// Render markdown with LaTeX support
function renderMarkdown(content) {
    // Process LaTeX before markdown
    // Block LaTeX: $$...$$ or \[...\]
    content = content.replace(/\$\$([\s\S]*?)\$\$/g, (match, latex) => {
        try {
            return `<div class="katex-display">${katex.renderToString(latex.trim(), { displayMode: true })}</div>`;
        } catch (e) {
            return match;
        }
    });

    content = content.replace(/\\\[([\s\S]*?)\\\]/g, (match, latex) => {
        try {
            return `<div class="katex-display">${katex.renderToString(latex.trim(), { displayMode: true })}</div>`;
        } catch (e) {
            return match;
        }
    });

    // Inline LaTeX: $...$ or \(...\)
    content = content.replace(/\$([^\$\n]+?)\$/g, (match, latex) => {
        try {
            return katex.renderToString(latex.trim(), { displayMode: false });
        } catch (e) {
            return match;
        }
    });

    content = content.replace(/\\\(([\s\S]*?)\\\)/g, (match, latex) => {
        try {
            return katex.renderToString(latex.trim(), { displayMode: false });
        } catch (e) {
            return match;
        }
    });

    // Render markdown
    return marked.parse(content);
}

// Input history navigation - get user messages from current conversation
function getInputHistory() {
    // Get user messages from DOM (role === 'user')
    const userMessages = Array.from(messagesEl.querySelectorAll('.message.user'))
        .map(el => el.textContent.trim())
        .filter(text => text.length > 0);
    return userMessages;
}

function navigateInputHistory(direction) {
    const inputHistory = getInputHistory();
    if (inputHistory.length === 0) return;

    // Save current input when starting to navigate
    if (historyIndex === -1 && direction === 1) {
        tempInput = userInputEl.value;
    }

    // direction: 1 = up (older), -1 = down (newer)
    const newIndex = historyIndex + direction;

    // Going down past current input
    if (newIndex < 0) {
        historyIndex = -1;
        userInputEl.value = tempInput;
        return;
    }

    // Going up past oldest message
    if (newIndex >= inputHistory.length) {
        return; // Stay at oldest
    }

    historyIndex = newIndex;

    // Set value from history (most recent is at end of array)
    userInputEl.value = inputHistory[inputHistory.length - 1 - historyIndex];

    // Move cursor to end
    userInputEl.setSelectionRange(userInputEl.value.length, userInputEl.value.length);
}

async function sendMessage() {
    const message = userInputEl.value.trim();
    if (!message) return;

    // Reset history navigation
    historyIndex = -1;
    tempInput = '';

    addMessage(message, 'user');
    userInputEl.value = '';
    sendBtn.disabled = true;

    let statusIndicator = null;
    let streamingMessage = null;
    let accumulatedContent = '';

    try {
        const settings = getSettings();
        const res = await fetch('/api/v1/chat/stream', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                message,
                conversationId: currentConversationId,
                language: settings.language
            })
        });

        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop() || '';

            for (const line of lines) {
                if (!line.startsWith('data: ')) continue;

                const jsonStr = line.slice(6);
                if (!jsonStr) continue;

                try {
                    const event = JSON.parse(jsonStr);

                    switch (event.type) {
                        case 'Thinking':
                            if (!statusIndicator) {
                                statusIndicator = createStatusIndicator();
                            }
                            updateStatusIndicator(statusIndicator, 'Thinking');
                            break;

                        case 'ToolExecuting':
                            if (statusIndicator) {
                                updateStatusIndicator(statusIndicator, 'ToolExecuting', event.toolName);
                            }
                            // Hide streaming message during tool execution
                            if (streamingMessage) {
                                streamingMessage.style.display = 'none';
                            }
                            break;

                        case 'ToolCompleted':
                            if (statusIndicator) {
                                updateStatusIndicator(statusIndicator, 'ToolCompleted', event.toolName);
                            }
                            break;

                        case 'ContentDelta':
                            // Remove status indicator when content starts
                            if (statusIndicator) {
                                removeStatusIndicator(statusIndicator);
                                statusIndicator = null;
                            }
                            // Create or show streaming message
                            if (!streamingMessage) {
                                streamingMessage = createStreamingMessage();
                            } else {
                                streamingMessage.style.display = '';
                            }
                            accumulatedContent += event.content || '';
                            updateStreamingMessage(streamingMessage, accumulatedContent);
                            break;

                        case 'Completed':
                            if (statusIndicator) {
                                removeStatusIndicator(statusIndicator);
                                statusIndicator = null;
                            }
                            // Final update with full content
                            if (streamingMessage && accumulatedContent) {
                                updateStreamingMessage(streamingMessage, accumulatedContent);
                            } else if (!streamingMessage && event.content) {
                                addMessage(event.content, 'assistant');
                            }
                            break;

                        case 'Error':
                            if (statusIndicator) {
                                removeStatusIndicator(statusIndicator);
                                statusIndicator = null;
                            }
                            addMessage(`Error: ${event.content}`, 'assistant');
                            break;
                    }
                } catch (parseError) {
                    console.error('Failed to parse SSE event:', parseError);
                }
            }
        }
    } catch (error) {
        if (statusIndicator) {
            removeStatusIndicator(statusIndicator);
        }
        addMessage(`Error: ${error.message}`, 'assistant');
    } finally {
        sendBtn.disabled = false;
        userInputEl.focus();

        // Refresh conversation title (may have been updated by backend on first message)
        await refreshCurrentConversationTitle();
    }
}

async function refreshCurrentConversationTitle() {
    if (!currentConversationId) return;

    try {
        const response = await fetch(`/api/v1/conversation/${currentConversationId}`);
        if (!response.ok) return;

        const conversation = await response.json();
        const idx = conversations.findIndex(c => c.id === currentConversationId);
        if (idx >= 0 && conversations[idx].title !== conversation.title) {
            conversations[idx].title = conversation.title;
            renderConversationList();
        }
    } catch (e) {
        // Ignore errors, title refresh is not critical
    }
}


// Load conversations on startup
async function loadConversations() {
    const response = await fetch('/api/v1/conversation');
    conversations = await response.json();

    // Auto-create or select first conversation
    if (conversations.length === 0) {
        await createNewConversation();
    } else {
        selectConversation(conversations[0].id);
    }

    renderConversationList();
}

function renderConversationList() {
    const list = document.getElementById('conversation-list');
    list.innerHTML = conversations.map(c => `
        <div class="conversation-item ${c.id === currentConversationId ? 'active' : ''}" 
             data-id="${c.id}">
            <span class="title">${c.title}</span>
            <button class="delete-btn" onclick="deleteConversation('${c.id}', event)">🗑️</button>
        </div>
    `).join('');

    // Add click handlers
    list.querySelectorAll('.conversation-item').forEach(item => {
        item.addEventListener('click', () => selectConversation(item.dataset.id));
    })
}

async function createNewConversation() {
    const response = await fetch('/api/v1/conversation', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ title: 'New Chat' })
    });
    const { id, title } = await response.json();
    conversations.unshift({ id, title, messageCount: 0 });
    selectConversation(id);
    renderConversationList();
}

async function selectConversation(id) {
    currentConversationId = id;
    renderConversationList();

    // Load messages
    const response = await fetch(`/api/v1/conversation/${id}`);
    const conversation = await response.json();

    // Clear and render messages
    const messagesDiv = document.getElementById('messages');
    messagesDiv.innerHTML = '';
    conversation.messages.forEach(msg => {
        addMessage(msg.content, msg.role === 1 ? 'user' : 'assistant');
    });
}

async function deleteConversation(id, event) {
    event.stopPropagation();
    if (!confirm('Are you sure to delete this conversation?')) return;

    await fetch(`/api/v1/conversation/${id}`, { method: 'DELETE' });
    conversations = conversations.filter(c => c.id !== id);

    if (currentConversationId === id) {
        currentConversationId = null;
        document.getElementById('messages').innerHTML = '';
    }
    renderConversationList();
}

// Settings Modal
const SETTINGS_KEY = 'openclaw_settings';
const MODELS_KEY = 'openclaw_models';

function getSettings() {
    const saved = localStorage.getItem(SETTINGS_KEY);
    return saved ? JSON.parse(saved) : {
        language: 'auto',
        activeModelId: null
    };
}

function saveSettings(settings) {
    localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
}

async function loadModelProviders() {
    try {
        const res = await fetch('/api/v1/model-provider');
        if (res.ok) {
            modelProviders = await res.json();
        }
    } catch (e) {
        console.error('Failed to load model providers:', e);
        modelProviders = [];
    }
    return modelProviders;
}

function getActiveModelProviderId() {
    const active = modelProviders.find(p => p.isActive);
    return active?.id || null;
}

function renderModelList() {
    const listEl = document.getElementById('model-list');

    if (modelProviders.length === 0) {
        listEl.innerHTML = '<p class="setting-hint">No model providers configured.</p>';
        return;
    }

    listEl.innerHTML = modelProviders.map(p => `
        <div class="model-item ${p.isActive ? 'active' : ''}" data-id="${p.id}">
            <input type="radio" name="active-model" class="model-item-radio"
                   value="${p.id}" ${p.isActive ? 'checked' : ''}>
            <div class="model-item-info">
                <div class="model-item-name">${escapeHtml(p.name)}</div>
                <div class="model-item-details">${escapeHtml(p.type)} - ${escapeHtml(p.modelName)}</div>
            </div>
            <div class="model-item-actions">
                <button class="model-item-btn delete" data-id="${p.id}" title="Delete">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2"/>
                    </svg>
                </button>
            </div>
        </div>
    `).join('');

    // Add event listeners for radio buttons
    listEl.querySelectorAll('.model-item-radio').forEach(radio => {
        radio.addEventListener('change', async (e) => {
            const id = e.target.value;
            await activateModelProvider(id);
            renderModelList();
        });
    });

    // Add event listeners for delete buttons
    listEl.querySelectorAll('.model-item-btn.delete').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const id = btn.dataset.id;
            if (confirm('Delete this model provider?')) {
                await deleteModelProvider(id);
                renderModelList();
            }
        });
    });
}

async function activateModelProvider(id) {
    try {
        await fetch(`/api/v1/model-provider/${id}/activate`, { method: 'POST' });
        await loadModelProviders();
    } catch (e) {
        console.error('Failed to activate provider:', e);
    }
}

async function deleteModelProvider(id) {
    try {
        await fetch(`/api/v1/model-provider/${id}`, { method: 'DELETE' });
        await loadModelProviders();
    } catch (e) {
        console.error('Failed to delete provider:', e);
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

async function openSettingsModal() {
    const modal = document.getElementById('settings-modal');
    const settings = getSettings();

    document.getElementById('language-select').value = settings.language;

    // Load providers and skills from backend
    await Promise.all([loadModelProviders(), loadSkills()]);
    renderModelList();
    renderSkillsList();

    modal.classList.add('active');
    document.querySelector('.profile-section').classList.remove('open');
}

// Skills Functions
async function loadSkills() {
    try {
        const res = await fetch('/api/v1/skill-settings');
        if (res.ok) {
            skills = await res.json();
        }
    } catch (e) {
        console.error('Failed to load skills:', e);
        skills = [];
    }
    return skills;
}

function renderSkillsList() {
    const listEl = document.getElementById('skills-list');

    if (skills.length === 0) {
        listEl.innerHTML = '<p class="setting-hint">No skills available.</p>';
        return;
    }

    listEl.innerHTML = skills.map(s => `
        <div class="skill-item" data-name="${escapeHtml(s.name)}">
            <div class="skill-item-info">
                <div class="skill-item-name">
                    ${escapeHtml(s.name)}
                    <code>/${escapeHtml(s.name)}</code>
                </div>
                <div class="skill-item-description">${escapeHtml(s.description)}</div>
            </div>
            <label class="toggle-switch">
                <input type="checkbox" ${s.isEnabled ? 'checked' : ''} data-skill="${escapeHtml(s.name)}">
                <span class="toggle-slider"></span>
            </label>
        </div>
    `).join('');

    // Add event listeners for toggle switches
    listEl.querySelectorAll('.toggle-switch input').forEach(toggle => {
        toggle.addEventListener('change', async (e) => {
            const skillName = e.target.dataset.skill;
            const enabled = e.target.checked;
            e.target.disabled = true;
            await toggleSkill(skillName, enabled);
            e.target.disabled = false;
        });
    });
}

async function toggleSkill(skillName, enabled) {
    try {
        const action = enabled ? 'enable' : 'disable';
        const res = await fetch(`/api/v1/skill-settings/${encodeURIComponent(skillName)}/${action}`, {
            method: 'POST'
        });
        if (!res.ok) throw new Error('Failed to update skill');

        // Update local state
        const skill = skills.find(s => s.name === skillName);
        if (skill) skill.isEnabled = enabled;
    } catch (e) {
        console.error('Failed to toggle skill:', e);
        // Revert on error
        await loadSkills();
        renderSkillsList();
    }
}

function closeSettingsModal() {
    document.getElementById('settings-modal').classList.remove('active');
}

// Slash Command Autocomplete
let autocompleteIndex = -1;
let filteredSkills = [];

function handleAutocompleteInput() {
    const value = userInputEl.value;
    const cursorPos = userInputEl.selectionStart;

    // Check if we're typing a slash command at the start
    if (value.startsWith('/') && cursorPos <= value.indexOf(' ') + 1 || (value.startsWith('/') && !value.includes(' '))) {
        const query = value.slice(1).split(' ')[0].toLowerCase();

        // Filter enabled skills
        filteredSkills = skills
            .filter(s => s.isEnabled && s.name.toLowerCase().includes(query))
            .slice(0, 6); // Max 6 suggestions

        if (filteredSkills.length > 0) {
            showAutocomplete(filteredSkills);
        } else {
            hideAutocomplete();
        }
    } else {
        hideAutocomplete();
    }
}

function showAutocomplete(skillList) {
    const dropdown = document.getElementById('autocomplete-dropdown');
    autocompleteIndex = -1;

    dropdown.innerHTML = skillList.map((s, i) => `
        <div class="autocomplete-item" data-index="${i}" data-name="${escapeHtml(s.name)}">
            <span class="autocomplete-command">/${escapeHtml(s.name)}</span>
            <span class="autocomplete-desc">${escapeHtml(s.description)}</span>
        </div>
    `).join('');

    dropdown.classList.add('visible');

    // Add click handlers
    dropdown.querySelectorAll('.autocomplete-item').forEach(item => {
        item.addEventListener('click', () => {
            autocompleteIndex = parseInt(item.dataset.index);
            selectAutocompleteItem();
        });
        item.addEventListener('mouseenter', () => {
            autocompleteIndex = parseInt(item.dataset.index);
            updateAutocompleteSelection();
        });
    });
}

function hideAutocomplete() {
    const dropdown = document.getElementById('autocomplete-dropdown');
    dropdown.classList.remove('visible');
    autocompleteIndex = -1;
    filteredSkills = [];
}

function isAutocompleteVisible() {
    return document.getElementById('autocomplete-dropdown').classList.contains('visible');
}

function navigateAutocomplete(direction) {
    if (filteredSkills.length === 0) return;

    autocompleteIndex += direction;
    if (autocompleteIndex < 0) autocompleteIndex = filteredSkills.length - 1;
    if (autocompleteIndex >= filteredSkills.length) autocompleteIndex = 0;

    updateAutocompleteSelection();
}

function updateAutocompleteSelection() {
    const dropdown = document.getElementById('autocomplete-dropdown');
    dropdown.querySelectorAll('.autocomplete-item').forEach((item, i) => {
        item.classList.toggle('selected', i === autocompleteIndex);
    });
}

function getSelectedAutocompleteItem() {
    if (autocompleteIndex >= 0 && autocompleteIndex < filteredSkills.length) {
        return filteredSkills[autocompleteIndex];
    }
    return null;
}

function selectAutocompleteItem() {
    const skill = getSelectedAutocompleteItem();
    if (!skill) return;

    // Replace the current slash command with selected one
    const currentValue = userInputEl.value;
    const spaceIndex = currentValue.indexOf(' ');
    const args = spaceIndex > 0 ? currentValue.slice(spaceIndex) : ' ';

    userInputEl.value = `/${skill.name}${args}`;
    userInputEl.focus();

    // Move cursor after the command
    const newCursorPos = skill.name.length + 2; // +2 for '/' and space
    userInputEl.setSelectionRange(newCursorPos, newCursorPos);

    hideAutocomplete();
}

function saveSettingsAndClose() {
    const settings = getSettings();
    settings.language = document.getElementById('language-select').value;
    saveSettings(settings);
    closeSettingsModal();
}

// Add Model Modal
function openAddModelModal() {
    const modal = document.getElementById('add-model-modal');
    // Reset form
    document.getElementById('provider-type').value = 'ollama';
    document.getElementById('provider-name').value = '';
    document.getElementById('provider-url').value = '';
    document.getElementById('provider-api-key').value = '';
    document.getElementById('provider-model').value = '';
    document.getElementById('validation-status').className = 'validation-status';
    document.getElementById('validation-status').textContent = '';
    document.getElementById('save-model-btn').disabled = true;
    updateProviderTypeUI();
    modal.classList.add('active');
}

function closeAddModelModal() {
    document.getElementById('add-model-modal').classList.remove('active');
}

function updateProviderTypeUI() {
    const type = document.getElementById('provider-type').value;
    const apiKeyGroup = document.querySelector('.api-key-group');
    const urlGroup = document.querySelector('.url-group');
    const urlHint = document.querySelector('.provider-url-hint');
    const urlInput = document.getElementById('provider-url');

    if (type === 'ollama') {
        apiKeyGroup.style.display = 'none';
        urlGroup.style.display = 'block';
        urlHint.textContent = 'Ollama default: http://localhost:11434';
        urlInput.placeholder = 'http://localhost:11434';
    } else if (type === 'openai') {
        apiKeyGroup.style.display = 'block';
        urlGroup.style.display = 'none'; // OpenAI has fixed endpoint
    } else if (type === 'anthropic') {
        apiKeyGroup.style.display = 'block';
        urlGroup.style.display = 'none'; // Anthropic has fixed endpoint
    } else {
        // Custom OpenAI-compatible
        apiKeyGroup.style.display = 'block';
        urlGroup.style.display = 'block';
        urlHint.textContent = 'Enter your OpenAI-compatible API endpoint';
        urlInput.placeholder = 'https://your-api-endpoint.com';
    }
}

async function validateModel() {
    const statusEl = document.getElementById('validation-status');
    const saveBtn = document.getElementById('save-model-btn');
    const type = document.getElementById('provider-type').value;
    const url = document.getElementById('provider-url').value.trim();
    const model = document.getElementById('provider-model').value.trim();
    const apiKey = document.getElementById('provider-api-key').value.trim();

    // Validation requirements differ by provider type
    if (type === 'ollama' && (!url || !model)) {
        statusEl.className = 'validation-status error';
        statusEl.textContent = 'Please enter URL and model name';
        return;
    }
    if ((type === 'openai' || type === 'anthropic') && (!apiKey || !model)) {
        statusEl.className = 'validation-status error';
        statusEl.textContent = 'Please enter API Key and model name';
        return;
    }
    if (type === 'custom' && (!url || !model)) {
        statusEl.className = 'validation-status error';
        statusEl.textContent = 'Please enter URL and model name';
        return;
    }

    statusEl.className = 'validation-status validating';
    statusEl.innerHTML = '<div class="validation-spinner"></div><span>Validating connection...</span>';
    saveBtn.disabled = true;

    try {
        if (type === 'ollama') {
            // Check if Ollama model exists
            const res = await fetch(`${url}/api/tags`);
            if (!res.ok) throw new Error('Cannot connect to Ollama');
            const data = await res.json();
            const models = data.models || [];
            const modelExists = models.some(m => m.name === model || m.name.startsWith(model + ':'));
            if (!modelExists) {
                statusEl.className = 'validation-status error';
                statusEl.textContent = `Model "${model}" not found. Available: ${models.slice(0, 3).map(m => m.name).join(', ')}...`;
                return;
            }
        } else if (type === 'openai') {
            // Validate OpenAI API key by listing models
            const res = await fetch('https://api.openai.com/v1/models', {
                headers: { 'Authorization': `Bearer ${apiKey}` }
            });
            if (!res.ok) throw new Error('Invalid API Key');
        } else if (type === 'anthropic') {
            // Anthropic doesn't have a simple validation endpoint
            // Just check key format
            if (!apiKey.startsWith('sk-ant-')) {
                throw new Error('Invalid Anthropic API Key format (should start with sk-ant-)');
            }
        }
        // For custom providers, skip validation (user responsibility)

        statusEl.className = 'validation-status success';
        statusEl.textContent = 'Validation successful!';
        saveBtn.disabled = false;
    } catch (err) {
        statusEl.className = 'validation-status error';
        statusEl.textContent = `Validation failed: ${err.message}`;
        saveBtn.disabled = true;
    }
}

async function saveModelProvider() {
    const type = document.getElementById('provider-type').value;
    const name = document.getElementById('provider-name').value.trim();

    // Set URL based on provider type
    let url = document.getElementById('provider-url').value.trim();
    if (type === 'openai') {
        url = 'https://api.openai.com';
    } else if (type === 'anthropic') {
        url = 'https://api.anthropic.com';
    }

    const isFirstProvider = modelProviders.length === 0;

    const provider = {
        type: type,
        name: name || getDefaultProviderName(type),
        url: url,
        modelName: document.getElementById('provider-model').value.trim(),
        apiKey: document.getElementById('provider-api-key').value.trim() || null,
        isActive: isFirstProvider
    };

    try {
        const res = await fetch('/api/v1/model-provider', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(provider)
        });

        if (!res.ok) throw new Error('Failed to create provider');

        await loadModelProviders();
        closeAddModelModal();
        renderModelList();
    } catch (e) {
        console.error('Failed to save provider:', e);
        alert('Failed to save model provider');
    }
}

function getDefaultProviderName(type) {
    switch (type) {
        case 'ollama': return 'Local Ollama';
        case 'openai': return 'OpenAI';
        case 'anthropic': return 'Anthropic';
        default: return 'Custom Provider';
    }
}

// Initialize settings modal events
document.addEventListener('DOMContentLoaded', () => {
    const settingsLink = document.getElementById('settings-link');
    const modal = document.getElementById('settings-modal');
    const addModelModal = document.getElementById('add-model-modal');

    if (settingsLink) {
        settingsLink.addEventListener('click', (e) => {
            e.preventDefault();
            openSettingsModal();
        });
    }

    if (modal) {
        modal.querySelector('.modal-overlay').addEventListener('click', closeSettingsModal);
        modal.querySelector('.modal-close').addEventListener('click', closeSettingsModal);
        modal.querySelector('.modal-cancel').addEventListener('click', closeSettingsModal);
        modal.querySelector('.modal-save').addEventListener('click', saveSettingsAndClose);
        document.getElementById('add-model-btn').addEventListener('click', openAddModelModal);
    }

    if (addModelModal) {
        addModelModal.querySelector('.modal-overlay').addEventListener('click', closeAddModelModal);
        addModelModal.querySelector('.modal-close').addEventListener('click', closeAddModelModal);
        document.getElementById('cancel-add-model').addEventListener('click', closeAddModelModal);
        document.getElementById('validate-model-btn').addEventListener('click', validateModel);
        document.getElementById('save-model-btn').addEventListener('click', saveModelProvider);
        document.getElementById('provider-type').addEventListener('change', updateProviderTypeUI);
    }

    // Initialize model providers from backend
    loadModelProviders();
});