// DOM Elements
let messagesEl, userInputEl, sendBtn, themeToggle;
let currentConversationId = null;
let conversations = [];
let modelProviders = [];
let skills = [];
let appConfigs = [];

// Input history navigation (from current conversation)
let historyIndex = -1;
let tempInput = ''; // Store current input when navigating history

// Pending images for upload
let pendingImages = []; // Array of { base64Data, mimeType, previewUrl }

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

    // Logout button
    document.getElementById('logout-btn').addEventListener('click', logout);

    // Image upload handling
    initImageUpload();

    // Check setup status - show onboarding if no provider configured
    checkSetupStatus();

    // Update user profile display
    updateUserProfile();
});

// Update user profile display from localStorage
function updateUserProfile() {
    const user = getCurrentUser();
    if (!user) return;

    const avatarEl = document.querySelector('.profile-avatar');
    const nameEl = document.querySelector('.profile-name');

    if (avatarEl && user.name) {
        avatarEl.textContent = user.name.charAt(0).toUpperCase();
    }
    if (nameEl && user.name) {
        nameEl.textContent = user.name;
    }
}

// Logout and redirect to login
function logout() {
    clearAuth();
    window.location.href = '/login.html';
}

// Setup check - onboarding flow
async function checkSetupStatus() {
    try {
        const res = await fetch('/api/v1/setup/status');
        if (!res.ok) return;

        const status = await res.json();
        
        if (!status.hasUser) {
            window.location.href = '/setup.html';
            return;
        }

        if (!isAuthenticated()) {
            window.location.href = '/login.html'
            return;
        }

        if (!status.hasModelProvider) {
            showOnboardingModal();
        }
    } catch (e) {
        console.error('Failed to check setup status:', e);
    }
}

function showOnboardingModal() {
    // Directly open Add Model Modal for first-time setup
    openAddModelModal();

    // Update the modal title to indicate first-time setup
    const modalHeader = document.querySelector('#add-model-modal .modal-header h3');
    if (modalHeader) {
        modalHeader.textContent = 'Welcome! Configure your first Model Provider';
    }

    // Pre-fill with Ollama defaults for easy local setup
    document.getElementById('provider-type').value = 'ollama';
    document.getElementById('provider-name').value = 'Local Ollama';
    document.getElementById('provider-url').value = 'http://localhost:11434';
    document.getElementById('provider-model').value = 'qwen2.5:7b';
    updateProviderTypeUI();
}

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

function addMessageWithImages(content, role, images) {
    const messageEl = document.createElement('div');
    messageEl.className = `message ${role}`;

    // Add images first if present
    if (images && images.length > 0) {
        const imagesContainer = document.createElement('div');
        imagesContainer.className = 'message-images';
        images.forEach(img => {
            const imgEl = document.createElement('img');
            imgEl.src = img.previewUrl;
            imgEl.alt = 'Uploaded image';
            imgEl.className = 'message-image';
            imagesContainer.appendChild(imgEl);
        });
        messageEl.appendChild(imagesContainer);
    }

    // Add text content
    if (content && content !== '[Image]') {
        const textEl = document.createElement('div');
        textEl.className = 'message-text';
        textEl.textContent = content;
        messageEl.appendChild(textEl);
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
    const hasImages = pendingImages.length > 0;

    // Require either message or images
    if (!message && !hasImages) return;

    // Reset history navigation
    historyIndex = -1;
    tempInput = '';

    // Add user message with image previews if any
    addMessageWithImages(message || '[Image]', 'user', pendingImages);
    userInputEl.value = '';
    sendBtn.disabled = true;

    // Prepare images for API
    const images = hasImages ? pendingImages.map(img => ({
        base64Data: img.base64Data,
        mimeType: img.mimeType
    })) : null;

    // Clear pending images
    clearImages();

    let statusIndicator = null;
    let streamingMessage = null;
    let accumulatedContent = '';

    try {
        const settings = getSettings();
        const res = await authFetch('/api/v1/chat/stream', {
            method: 'POST',
            body: JSON.stringify({
                message: message || 'What is in this image?',
                conversationId: currentConversationId,
                language: settings.language,
                images: images
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
        const response = await authFetch(`/api/v1/conversation/${currentConversationId}`);
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
    const response = await authFetch('/api/v1/conversation');
    if (!response.ok) return;
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
    const response = await authFetch('/api/v1/conversation', {
        method: 'POST',
        body: JSON.stringify({ title: 'New Chat' })
    });
    if (!response.ok) return;
    const { id, title } = await response.json();
    conversations.unshift({ id, title, messageCount: 0 });
    selectConversation(id);
    renderConversationList();
}

async function selectConversation(id) {
    currentConversationId = id;
    renderConversationList();

    // Load messages
    const response = await authFetch(`/api/v1/conversation/${id}`);
    if (!response.ok) return;
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

    await authFetch(`/api/v1/conversation/${id}`, { method: 'DELETE' });
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
        const res = await authFetch('/api/v1/model-provider');
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
                <button class="model-item-btn edit" data-id="${p.id}" title="Edit">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
                        <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                </button>
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

    // Add event listeners for edit buttons
    listEl.querySelectorAll('.model-item-btn.edit').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const id = btn.dataset.id;
            openEditModelModal(id);
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

// Edit Model Provider
function openEditModelModal(id) {
    const provider = modelProviders.find(p => p.id === id);
    if (!provider) return;

    // Set modal title for edit mode
    document.getElementById('model-modal-title').textContent = 'Edit Model Provider';
    document.getElementById('provider-id').value = id;

    // Fill form with existing values
    document.getElementById('provider-type').value = provider.type.toLowerCase();
    document.getElementById('provider-name').value = provider.name;
    document.getElementById('provider-url').value = provider.url || '';
    document.getElementById('provider-api-key').value = ''; // Don't show existing API key
    document.getElementById('provider-model').value = provider.modelName;

    updateProviderTypeUI();

    // Reset validation status
    document.getElementById('validation-status').textContent = '';
    document.getElementById('validation-status').className = 'validation-status';
    document.getElementById('save-model-btn').disabled = false; // Allow save for edit

    document.getElementById('add-model-modal').classList.add('active');
}

async function activateModelProvider(id) {
    try {
        await authFetch(`/api/v1/model-provider/${id}/activate`, { method: 'POST' });
        await loadModelProviders();
    } catch (e) {
        console.error('Failed to activate provider:', e);
    }
}

async function deleteModelProvider(id) {
    try {
        await authFetch(`/api/v1/model-provider/${id}`, { method: 'DELETE' });
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

    // Load providers, skills, channel settings, and app configs from backend
    await Promise.all([loadModelProviders(), loadSkills(), loadTelegramSettings(), loadAppConfigs()]);
    renderModelList();
    renderSkillsList();
    renderTelegramSettings();
    renderConfigList();

    // Update account info
    const user = getCurrentUser();
    if (user) {
        const avatarEl = document.getElementById('settings-avatar');
        const nameEl = document.getElementById('settings-name');
        const emailEl = document.getElementById('settings-email');
        if (avatarEl) avatarEl.textContent = user.name ? user.name.charAt(0).toUpperCase() : 'U';
        if (nameEl) nameEl.textContent = user.name || 'User';
        if (emailEl) emailEl.textContent = user.email || '';
    }

    // Initialize tab switching
    initSettingsTabs();

    modal.classList.add('active');
    document.querySelector('.profile-section').classList.remove('open');
}

// Settings Tab Switching
function initSettingsTabs() {
    const tabs = document.querySelectorAll('.settings-tab');
    const panels = document.querySelectorAll('.settings-panel');

    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            const targetPanel = tab.dataset.tab;

            // Update active tab
            tabs.forEach(t => t.classList.remove('active'));
            tab.classList.add('active');

            // Update active panel
            panels.forEach(p => p.classList.remove('active'));
            document.querySelector(`.settings-panel[data-panel="${targetPanel}"]`).classList.add('active');
        });
    });

    // Logout button
    const logoutBtn = document.getElementById('settings-logout-btn');
    if (logoutBtn) {
        logoutBtn.onclick = () => {
            logout();
        };
    }
}

// Skills Functions
async function loadSkills() {
    try {
        const res = await authFetch('/api/v1/skill-settings');
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

// Channel Settings Functions
let telegramSettings = null;

async function loadTelegramSettings() {
    try {
        const res = await authFetch('/api/v1/channel-settings/telegram');
        if (res.ok) {
            telegramSettings = await res.json();
        }
    } catch (e) {
        console.error('Failed to load Telegram settings:', e);
        telegramSettings = null;
    }
    return telegramSettings;
}

function renderTelegramSettings() {
    const enabledToggle = document.getElementById('telegram-enabled');
    const statusEl = document.getElementById('telegram-status');
    const tokenInput = document.getElementById('telegram-bot-token');
    const webhookInput = document.getElementById('telegram-webhook-url');
    const secretInput = document.getElementById('telegram-secret-token');
    const allowedUsersInput = document.getElementById('telegram-allowed-users');

    if (!telegramSettings) {
        statusEl.textContent = 'Not configured';
        statusEl.className = 'channel-status';
        enabledToggle.checked = false;
        tokenInput.value = '';
        webhookInput.value = '';
        secretInput.value = '';
        allowedUsersInput.value = '';
        return;
    }

    enabledToggle.checked = telegramSettings.enabled;
    tokenInput.value = ''; // Don't show actual token
    tokenInput.placeholder = telegramSettings.botTokenMasked || 'Enter bot token...';
    webhookInput.value = telegramSettings.webhookUrl || '';
    secretInput.value = telegramSettings.secretToken || '';
    allowedUsersInput.value = (telegramSettings.allowedUserIds || []).join(', ');

    if (telegramSettings.enabled && telegramSettings.botTokenMasked) {
        statusEl.textContent = 'Connected';
        statusEl.className = 'channel-status connected';
    } else if (telegramSettings.botTokenMasked) {
        statusEl.textContent = 'Configured (disabled)';
        statusEl.className = 'channel-status';
    } else {
        statusEl.textContent = 'Not configured';
        statusEl.className = 'channel-status';
    }
}

async function validateTelegramBot() {
    const tokenInput = document.getElementById('telegram-bot-token');
    const botInfoEl = document.getElementById('telegram-bot-info');
    const validateBtn = document.getElementById('validate-telegram-btn');

    const token = tokenInput.value.trim();
    if (!token) {
        botInfoEl.style.display = 'none';
        botInfoEl.innerHTML = '';
        return;
    }

    validateBtn.disabled = true;
    validateBtn.textContent = 'Validating...';

    try {
        const res = await authFetch('/api/v1/channel-settings/telegram/validate', {
            method: 'POST',
            body: JSON.stringify({ botToken: token })
        });

        const result = await res.json();

        if (!res.ok || !result.success) {
            botInfoEl.style.display = 'block';
            botInfoEl.className = 'bot-info-card channel-validation-status error';
            botInfoEl.innerHTML = `<span>Validation failed: ${result.message || 'Invalid token'}</span>`;
            return;
        }

        botInfoEl.style.display = 'block';
        botInfoEl.className = 'bot-info-card';
        botInfoEl.innerHTML = `
            <div class="bot-name">${escapeHtml(result.botInfo.firstName)}</div>
            <div class="bot-username">@${escapeHtml(result.botInfo.username || 'N/A')}</div>
            <div class="bot-details">
                Bot ID: ${result.botInfo.id}<br>
                Can join groups: ${result.botInfo.canJoinGroups ? 'Yes' : 'No'}
            </div>
        `;
    } catch (e) {
        console.error('Telegram validation error:', e);
        botInfoEl.style.display = 'block';
        botInfoEl.className = 'bot-info-card channel-validation-status error';
        botInfoEl.innerHTML = `<span>Validation failed: ${e.message}</span>`;
    } finally {
        validateBtn.disabled = false;
        validateBtn.textContent = 'Validate';
    }
}

async function saveTelegramSettings() {
    const enabled = document.getElementById('telegram-enabled').checked;
    const token = document.getElementById('telegram-bot-token').value.trim();
    const webhookUrl = document.getElementById('telegram-webhook-url').value.trim();
    const secretToken = document.getElementById('telegram-secret-token').value.trim();
    const allowedUsersText = document.getElementById('telegram-allowed-users').value.trim();

    // Parse allowed user IDs
    const allowedUserIds = allowedUsersText
        ? allowedUsersText.split(',')
            .map(s => s.trim())
            .filter(s => /^\d+$/.test(s))
            .map(s => parseInt(s, 10))
        : [];

    const settings = {
        enabled: enabled,
        botToken: token || null,
        webhookUrl: webhookUrl || null,
        secretToken: secretToken || null,
        allowedUserIds: allowedUserIds
    };

    try {
        const res = await authFetch('/api/v1/channel-settings/telegram', {
            method: 'PUT',
            body: JSON.stringify(settings)
        });

        if (!res.ok) {
            throw new Error('Failed to save Telegram settings');
        }

        telegramSettings = await res.json();
        renderTelegramSettings();
        return true;
    } catch (e) {
        console.error('Failed to save Telegram settings:', e);
        alert('Failed to save Telegram settings');
        return false;
    }
}

function initTelegramChannel() {
    const validateBtn = document.getElementById('validate-telegram-btn');
    if (validateBtn) {
        validateBtn.addEventListener('click', validateTelegramBot);
    }

    const enabledToggle = document.getElementById('telegram-enabled');
    if (enabledToggle) {
        enabledToggle.addEventListener('change', () => {
            // Auto-save when toggling
            saveTelegramSettings();
        });
    }
}

// App Config Functions
async function loadAppConfigs() {
    try {
        const res = await authFetch('/api/v1/app-config');
        if (res.ok) {
            appConfigs = await res.json();
        }
    } catch (e) {
        console.error('Failed to load app configs:', e);
        appConfigs = [];
    }
    return appConfigs;
}

function renderConfigList() {
    const listEl = document.getElementById('config-list');

    if (appConfigs.length === 0) {
        listEl.innerHTML = `
            <div class="config-empty">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"/>
                    <path d="M7 11V7a5 5 0 0110 0v4"/>
                </svg>
                <p>No configuration values set</p>
                <p style="font-size: 0.8rem; opacity: 0.7;">Add secrets like GH_TOKEN, API keys, etc.</p>
            </div>
        `;
        return;
    }

    listEl.innerHTML = appConfigs.map(c => `
        <div class="config-item" data-key="${escapeHtml(c.key)}">
            <div class="config-item-icon ${c.isSecret ? 'secret' : ''}">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"/>
                    <path d="M7 11V7a5 5 0 0110 0v4"/>
                </svg>
            </div>
            <div class="config-item-info">
                <span class="config-item-key">${escapeHtml(c.key)}</span>
                <span class="config-item-badge ${c.isSecret ? 'secret' : 'plain'}">
                    ${c.isSecret ? 'ENCRYPTED' : 'PLAIN'}
                </span>
            </div>
            <div class="config-item-actions">
                <button class="config-item-btn edit" data-key="${escapeHtml(c.key)}" title="Edit">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
                        <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                </button>
                <button class="config-item-btn delete" data-key="${escapeHtml(c.key)}" title="Delete">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2"/>
                    </svg>
                </button>
            </div>
        </div>
    `).join('');

    // Add event listeners for reveal buttons
    // Add event listeners for edit buttons
    listEl.querySelectorAll('.config-item-btn.edit').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const key = btn.dataset.key;
            openEditConfigModal(key);
        });
    });

    // Add event listeners for delete buttons
    listEl.querySelectorAll('.config-item-btn.delete').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const key = btn.dataset.key;
            if (confirm(`Delete config "${key}"?`)) {
                await deleteAppConfig(key);
                await loadAppConfigs();
                renderConfigList();
            }
        });
    });
}

function openEditConfigModal(key) {
    // Don't show the current value - just prompt for new value
    const newValue = prompt(`Set new value for "${key}":`);
    if (newValue !== null && newValue !== '') {
        saveAppConfig(key, newValue, true); // Always encrypted
    }
}

async function addAppConfig() {
    const keyInput = document.getElementById('config-new-key');
    const valueInput = document.getElementById('config-new-value');

    const key = keyInput.value.trim().toUpperCase(); // Normalize to uppercase
    const value = valueInput.value;

    if (!key) {
        alert('Please enter a key');
        return;
    }

    if (!value) {
        alert('Please enter a value');
        return;
    }

    try {
        await saveAppConfig(key, value, true); // Always encrypted

        // Clear form
        keyInput.value = '';
        valueInput.value = '';

        await loadAppConfigs();
        renderConfigList();
    } catch (e) {
        console.error('Failed to add config:', e);
        alert('Failed to add config');
    }
}

async function saveAppConfig(key, value, isSecret) {
    try {
        const res = await authFetch(`/api/v1/app-config/${encodeURIComponent(key)}`, {
            method: 'PUT',
            body: JSON.stringify({ value, isSecret })
        });

        if (!res.ok) {
            throw new Error('Failed to save config');
        }

        await loadAppConfigs();
        renderConfigList();
        return true;
    } catch (e) {
        console.error('Failed to save config:', e);
        alert('Failed to save config');
        return false;
    }
}

async function deleteAppConfig(key) {
    try {
        const res = await authFetch(`/api/v1/app-config/${encodeURIComponent(key)}`, {
            method: 'DELETE'
        });

        if (!res.ok) {
            throw new Error('Failed to delete config');
        }
        return true;
    } catch (e) {
        console.error('Failed to delete config:', e);
        alert('Failed to delete config');
        return false;
    }
}

function initConfigManagement() {
    const addBtn = document.getElementById('add-config-btn');
    if (addBtn) {
        addBtn.addEventListener('click', addAppConfig);
    }

    // Allow Enter key to add config
    const keyInput = document.getElementById('config-new-key');
    const valueInput = document.getElementById('config-new-value');

    const handleEnter = (e) => {
        if (e.key === 'Enter') {
            addAppConfig();
        }
    };

    if (keyInput) keyInput.addEventListener('keydown', handleEnter);
    if (valueInput) valueInput.addEventListener('keydown', handleEnter);
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

async function saveSettingsAndClose() {
    const settings = getSettings();
    settings.language = document.getElementById('language-select').value;
    saveSettings(settings);

    // Save Telegram settings
    await saveTelegramSettings();

    closeSettingsModal();
}

// Add Model Modal
function openAddModelModal() {
    const modal = document.getElementById('add-model-modal');
    // Reset form for add mode
    document.getElementById('model-modal-title').textContent = 'Add Model Provider';
    document.getElementById('provider-id').value = '';
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
        // Validate through backend API to avoid CORS issues
        const res = await authFetch('/api/v1/model-provider/validate', {
            method: 'POST',
            body: JSON.stringify({
                type: type,
                url: url,
                modelName: model,
                apiKey: apiKey || null
            })
        });

        const result = await res.json();

        if (!res.ok || !result.success) {
            statusEl.className = 'validation-status error';
            statusEl.textContent = result.message || 'Validation failed';
            saveBtn.disabled = true;
            return;
        }

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
    const providerId = document.getElementById('provider-id').value;
    const isEditMode = !!providerId;

    const type = document.getElementById('provider-type').value;
    const name = document.getElementById('provider-name').value.trim();

    // Set URL based on provider type
    let url = document.getElementById('provider-url').value.trim();
    if (type === 'openai') {
        url = 'https://api.openai.com';
    } else if (type === 'anthropic') {
        url = 'https://api.anthropic.com';
    }

    const apiKey = document.getElementById('provider-api-key').value.trim();

    const provider = {
        type: type,
        name: name || getDefaultProviderName(type),
        url: url,
        modelName: document.getElementById('provider-model').value.trim(),
        apiKey: apiKey || null
    };

    try {
        let res;
        if (isEditMode) {
            // Update existing provider
            res = await authFetch(`/api/v1/model-provider/${providerId}`, {
                method: 'PUT',
                body: JSON.stringify(provider)
            });
            if (!res.ok) throw new Error('Failed to update provider');
        } else {
            // Create new provider
            const isFirstProvider = modelProviders.length === 0;
            provider.isActive = isFirstProvider;
            res = await authFetch('/api/v1/model-provider', {
                method: 'POST',
                body: JSON.stringify(provider)
            });
            if (!res.ok) throw new Error('Failed to create provider');
        }

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

    // Initialize Telegram channel
    initTelegramChannel();

    // Initialize Config Management
    initConfigManagement();
});

// ==================== Image Upload Functions ====================

function initImageUpload() {
    const attachBtn = document.getElementById('attach-btn');
    const imageInput = document.getElementById('image-input');

    // Attach button click
    attachBtn.addEventListener('click', () => {
        imageInput.click();
    });

    // File input change
    imageInput.addEventListener('change', (e) => {
        handleFileSelect(e.target.files);
        e.target.value = ''; // Reset input to allow selecting same file again
    });

    // Paste event on textarea
    userInputEl.addEventListener('paste', handlePaste);

    // Drag and drop on input area
    const inputArea = document.querySelector('.input-area');
    inputArea.addEventListener('dragover', (e) => {
        e.preventDefault();
        inputArea.classList.add('drag-over');
    });
    inputArea.addEventListener('dragleave', () => {
        inputArea.classList.remove('drag-over');
    });
    inputArea.addEventListener('drop', (e) => {
        e.preventDefault();
        inputArea.classList.remove('drag-over');
        if (e.dataTransfer.files.length > 0) {
            handleFileSelect(e.dataTransfer.files);
        }
    });
}

function handlePaste(e) {
    const items = e.clipboardData?.items;
    if (!items) return;

    const imageItems = Array.from(items).filter(item => item.type.startsWith('image/'));
    if (imageItems.length === 0) return;

    e.preventDefault();

    for (const item of imageItems) {
        const file = item.getAsFile();
        if (file) {
            processImageFile(file);
        }
    }
}

function handleFileSelect(files) {
    for (const file of files) {
        if (file.type.startsWith('image/')) {
            processImageFile(file);
        }
    }
}

function processImageFile(file) {
    // Limit file size (10MB)
    const maxSize = 10 * 1024 * 1024;
    if (file.size > maxSize) {
        alert(`Image "${file.name}" is too large. Maximum size is 10MB.`);
        return;
    }

    const reader = new FileReader();
    reader.onload = (e) => {
        const dataUrl = e.target.result;
        // Extract base64 data and mime type
        const [header, base64Data] = dataUrl.split(',');
        const mimeMatch = header.match(/data:(image\/\w+);/);
        const mimeType = mimeMatch ? mimeMatch[1] : 'image/png';

        const imageData = {
            base64Data: base64Data,
            mimeType: mimeType,
            previewUrl: dataUrl
        };

        pendingImages.push(imageData);
        renderImagePreviews();
    };
    reader.readAsDataURL(file);
}

function renderImagePreviews() {
    const previewArea = document.getElementById('image-preview-area');

    if (pendingImages.length === 0) {
        previewArea.style.display = 'none';
        previewArea.innerHTML = '';
        return;
    }

    previewArea.style.display = 'flex';
    previewArea.innerHTML = pendingImages.map((img, index) => `
        <div class="image-preview-item" data-index="${index}">
            <img src="${img.previewUrl}" alt="Preview ${index + 1}">
            <button class="remove-btn" onclick="removeImage(${index})">&times;</button>
        </div>
    `).join('');
}

function removeImage(index) {
    pendingImages.splice(index, 1);
    renderImagePreviews();
}

function clearImages() {
    pendingImages = [];
    renderImagePreviews();
}