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

// AbortController for stopping inference
let currentAbortController = null;

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

    // Show admin-only nav items based on user role
    initAdminNavItems();

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

        // ESC to stop inference (when not in autocomplete)
        if (e.key === 'Escape' && currentAbortController) {
            e.preventDefault();
            stopInference();
            return;
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

    // Initialize conversations button (but don't load yet)
    document.getElementById('new-chat-btn').addEventListener('click', createNewConversation);

    // Logout button
    document.getElementById('logout-btn').addEventListener('click', logout);

    // Image upload handling
    initImageUpload();

    // Check setup status first - this handles authentication and onboarding
    // Only load data after authentication is confirmed
    checkSetupStatus().then(() => {
        // Only load if authenticated
        if (isAuthenticated()) {
            loadConversations();
            loadSkills();
            updateUserProfile();
        }
    });
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

// Logout and show login modal
function logout() {
    clearAuth();
    showLoginModal(() => {
        window.location.reload();
    });
}

// Setup check - onboarding flow
// Returns a promise that resolves when authentication is confirmed
async function checkSetupStatus() {
    return new Promise(async (resolve) => {
        try {
            const res = await fetch('/api/v1/setup/status');
            if (!res.ok) {
                resolve();
                return;
            }

            const status = await res.json();

            if (!status.hasUser) {
                window.location.href = '/setup.html';
                return; // Never resolves - page is redirecting
            }

            if (!isAuthenticated()) {
                showLoginModal(() => {
                    window.location.reload();
                });
                return; // Never resolves - waiting for login then reload
            }

            if (!status.hasModelProvider) {
                showOnboardingModal();
            }

            resolve(); // Authenticated, continue
        } catch (e) {
            console.error('Failed to check setup status:', e);
            resolve(); // Resolve anyway to not block
        }
    });
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
    const wasNearBottom = isNearBottom(150);

    const indicator = document.createElement('div');
    indicator.className = 'message assistant status-indicator';
    indicator.innerHTML = '<span class="status-dot"></span><span class="status-text">Thinking...</span>';
    messagesEl.appendChild(indicator);

    if (wasNearBottom) {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }
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

// Stop inference function
function stopInference() {
    if (currentAbortController) {
        currentAbortController.abort();
    }
}

// Toggle button between send and stop modes
function setButtonMode(mode) {
    if (mode === 'stop') {
        sendBtn.textContent = 'Stop';
        sendBtn.classList.add('stop-mode');
        sendBtn.disabled = false;
        sendBtn.onclick = stopInference;
    } else {
        sendBtn.textContent = 'Send';
        sendBtn.classList.remove('stop-mode');
        sendBtn.disabled = false;
        sendBtn.onclick = sendMessage;
    }
}

// Chat Functions
function addMessage(content, role) {
    const wasNearBottom = isNearBottom(150);

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

    // Only auto-scroll if user was near bottom
    if (wasNearBottom) {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }
    return messageEl;
}

function addMessageWithImages(content, role, images) {
    const wasNearBottom = isNearBottom(150);

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

    // Only auto-scroll if user was near bottom
    if (wasNearBottom) {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }
    return messageEl;
}

function createStreamingMessage() {
    const messageEl = document.createElement('div');
    messageEl.className = 'message assistant';
    messagesEl.appendChild(messageEl);
    return messageEl;
}

// Check if user is near the bottom of the chat (within threshold)
function isNearBottom(threshold = 100) {
    const scrollBottom = messagesEl.scrollHeight - messagesEl.scrollTop - messagesEl.clientHeight;
    return scrollBottom <= threshold;
}

// Smooth scroll to bottom only if user was already near bottom
function scrollToBottomIfNeeded() {
    if (isNearBottom(150)) {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }
}

function updateStreamingMessage(messageEl, content) {
    // Remember scroll position before update
    const wasNearBottom = isNearBottom(150);

    messageEl.innerHTML = renderMarkdown(content);
    messageEl.querySelectorAll('pre code').forEach(block => {
        hljs.highlightElement(block);
    });

    // Only auto-scroll if user was already near the bottom
    if (wasNearBottom) {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }
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

    // Switch to stop mode
    setButtonMode('stop');

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

    // Create AbortController for this request
    currentAbortController = new AbortController();

    try {
        const settings = getSettings();
        const res = await authFetch('/api/v1/chat/stream', {
            method: 'POST',
            body: JSON.stringify({
                message: message || 'What is in this image?',
                conversationId: currentConversationId,
                language: settings.language,
                images: images
            }),
            signal: currentAbortController.signal
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

                        case 'ApprovalRequired':
                            if (statusIndicator) {
                                removeStatusIndicator(statusIndicator);
                                statusIndicator = null;
                            }
                            // Create approval UI inline in chat
                            handleApprovalRequired(event.executionId, event.approvalRequest);
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
        // Only show error if not aborted by user
        if (error.name !== 'AbortError') {
            addMessage(`Error: ${error.message}`, 'assistant');
        } else {
            // User stopped the inference - show stopped message if there was partial content
            if (streamingMessage && accumulatedContent) {
                // Add a visual indicator that response was stopped
                accumulatedContent += '\n\n*[Response stopped]*';
                updateStreamingMessage(streamingMessage, accumulatedContent);
            }
        }
    } finally {
        currentAbortController = null;
        setButtonMode('send');
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

    // Load providers, skills, channel settings, app configs, and preferences from backend
    await Promise.all([loadModelProviders(), loadSkills(), loadTelegramSettings(), loadAppConfigs(), loadUserPreferences()]);
    renderModelList();
    renderSkillsList();
    renderTelegramSettings();
    renderConfigList();
    renderPreferenceList();

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

// User Preferences Functions
let userPreferences = [];

async function loadUserPreferences() {
    try {
        const res = await authFetch('/api/v1/user-preference');
        if (res.ok) {
            userPreferences = await res.json();
        }
    } catch (e) {
        console.error('Failed to load user preferences:', e);
        userPreferences = [];
    }
    return userPreferences;
}

function renderPreferenceList() {
    const listEl = document.getElementById('preference-list');

    if (userPreferences.length === 0) {
        listEl.innerHTML = `
            <div class="config-empty">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M12 20h9"/>
                    <path d="M16.5 3.5a2.121 2.121 0 013 3L7 19l-4 1 1-4L16.5 3.5z"/>
                </svg>
                <p>No preferences set</p>
                <p style="font-size: 0.8rem; opacity: 0.7;">Add preferences like ado.assignedTo, user.language, etc.</p>
            </div>
        `;
        return;
    }

    listEl.innerHTML = userPreferences.map(p => `
        <div class="config-item" data-key="${escapeHtml(p.key)}">
            <div class="config-item-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M12 20h9"/>
                    <path d="M16.5 3.5a2.121 2.121 0 013 3L7 19l-4 1 1-4L16.5 3.5z"/>
                </svg>
            </div>
            <div class="config-item-info">
                <span class="config-item-key">${escapeHtml(p.key)}</span>
                <span class="config-item-value">${escapeHtml(p.value || '(empty)')}</span>
            </div>
            <div class="config-item-actions">
                <button class="config-item-btn edit" data-key="${escapeHtml(p.key)}" title="Edit">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
                        <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                </button>
                <button class="config-item-btn delete" data-key="${escapeHtml(p.key)}" title="Delete">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2"/>
                    </svg>
                </button>
            </div>
        </div>
    `).join('');

    // Add event listeners for edit buttons
    listEl.querySelectorAll('.config-item-btn.edit').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const key = btn.dataset.key;
            openEditPreferenceModal(key);
        });
    });

    // Add event listeners for delete buttons
    listEl.querySelectorAll('.config-item-btn.delete').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const key = btn.dataset.key;
            if (confirm(`Delete preference "${key}"?`)) {
                await deleteUserPreference(key);
                await loadUserPreferences();
                renderPreferenceList();
            }
        });
    });
}

function openEditPreferenceModal(key) {
    const pref = userPreferences.find(p => p.key === key);
    const currentValue = pref ? pref.value : '';
    const newValue = prompt(`Set new value for "${key}":`, currentValue);
    if (newValue !== null) {
        saveUserPreference(key, newValue);
    }
}

async function addUserPreference() {
    const keyInput = document.getElementById('pref-new-key');
    const valueInput = document.getElementById('pref-new-value');

    const key = keyInput.value.trim().toLowerCase();
    const value = valueInput.value;

    if (!key) {
        alert('Please enter a key');
        return;
    }

    try {
        await saveUserPreference(key, value);

        // Clear form
        keyInput.value = '';
        valueInput.value = '';

        await loadUserPreferences();
        renderPreferenceList();
    } catch (e) {
        console.error('Failed to add preference:', e);
        alert('Failed to add preference');
    }
}

async function saveUserPreference(key, value) {
    try {
        const res = await authFetch(`/api/v1/user-preference/${encodeURIComponent(key)}`, {
            method: 'PUT',
            body: JSON.stringify({ value })
        });

        if (!res.ok) {
            throw new Error('Failed to save preference');
        }

        await loadUserPreferences();
        renderPreferenceList();
        return true;
    } catch (e) {
        console.error('Failed to save preference:', e);
        alert('Failed to save preference');
        return false;
    }
}

async function deleteUserPreference(key) {
    try {
        const res = await authFetch(`/api/v1/user-preference/${encodeURIComponent(key)}`, {
            method: 'DELETE'
        });

        if (!res.ok) {
            throw new Error('Failed to delete preference');
        }
        return true;
    } catch (e) {
        console.error('Failed to delete preference:', e);
        alert('Failed to delete preference');
        return false;
    }
}

function initPreferenceManagement() {
    const addBtn = document.getElementById('add-pref-btn');
    if (addBtn) {
        addBtn.addEventListener('click', addUserPreference);
    }

    // Allow Enter key to add preference
    const keyInput = document.getElementById('pref-new-key');
    const valueInput = document.getElementById('pref-new-value');

    const handleEnter = (e) => {
        if (e.key === 'Enter') {
            addUserPreference();
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

    // Note: Model providers are loaded when settings modal opens (openSettingsModal)
    // Don't load them here to avoid triggering auth before checkSetupStatus

    // Initialize Telegram channel event handlers (not data)
    initTelegramChannel();

    // Initialize Config Management
    initConfigManagement();

    // Initialize Preference Management
    initPreferenceManagement();
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

// ==================== Pipeline Approval ====================

function handleApprovalRequired(executionId, approvalRequest) {
    if (!approvalRequest) return;

    // Create approval UI using the shared ApprovalUI component
    const approvalEl = ApprovalUI.createInlineApproval(
        executionId,
        approvalRequest,
        async (execId) => {
            await submitApprovalDecision(execId, true);
        },
        async (execId) => {
            await submitApprovalDecision(execId, false);
        }
    );

    // Wrap in a message container
    const messageDiv = document.createElement('div');
    messageDiv.className = 'message assistant';
    messageDiv.appendChild(approvalEl);

    messagesEl.appendChild(messageDiv);
    messagesEl.scrollTop = messagesEl.scrollHeight;
}

async function submitApprovalDecision(executionId, approved) {
    const action = approved ? 'approve' : 'reject';

    try {
        const response = await authFetch(`/api/v1/pipeline/executions/${executionId}/${action}`, {
            method: 'POST'
        });

        if (!response.ok) {
            throw new Error(`Failed to ${action} execution`);
        }

        // Update the UI to show the decision
        ApprovalUI.markDecision(executionId, approved);

        // Add a follow-up message
        addMessage(approved ? 'Approved. Continuing execution...' : 'Rejected. Execution cancelled.', 'assistant');

        // Refresh executions list
        await loadExecutions();

    } catch (error) {
        console.error('Approval error:', error);
        addMessage(`Error: Failed to submit approval - ${error.message}`, 'assistant');
    }
}

// ==================== Toast Notifications ====================

const ToastManager = {
    show(type, title, message, options = {}) {
        const container = document.getElementById('toast-container');
        const toast = document.createElement('div');
        toast.className = 'toast';

        const iconHtml = this.getIcon(type);
        const duration = options.duration || 5000;
        const actions = options.actions || [];

        let actionsHtml = '';
        if (actions.length > 0) {
            actionsHtml = '<div class="toast-actions">' +
                actions.map((action, i) =>
                    `<button class="toast-btn ${action.type || ''}" data-action="${i}">${action.label}</button>`
                ).join('') +
                '</div>';
        }

        toast.innerHTML = `
            <div class="toast-icon ${type}">${iconHtml}</div>
            <div class="toast-content">
                <div class="toast-title">${escapeHtml(title)}</div>
                <div class="toast-message">${escapeHtml(message)}</div>
                ${actionsHtml}
            </div>
            <button class="toast-close">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M18 6L6 18M6 6l12 12"/>
                </svg>
            </button>
        `;

        container.appendChild(toast);

        // Bind action handlers
        actions.forEach((action, i) => {
            const btn = toast.querySelector(`[data-action="${i}"]`);
            if (btn && action.onClick) {
                btn.addEventListener('click', () => {
                    action.onClick();
                    this.dismiss(toast);
                });
            }
        });

        // Bind close button
        toast.querySelector('.toast-close').addEventListener('click', () => {
            this.dismiss(toast);
        });

        // Auto-dismiss (unless has actions)
        if (actions.length === 0 && duration > 0) {
            setTimeout(() => this.dismiss(toast), duration);
        }

        return toast;
    },

    dismiss(toast) {
        toast.classList.add('toast-exit');
        setTimeout(() => toast.remove(), 200);
    },

    getIcon(type) {
        switch (type) {
            case 'success':
                return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg>';
            case 'error':
                return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M15 9l-6 6M9 9l6 6"/></svg>';
            case 'warning':
            case 'approval':
                return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>';
            case 'info':
            default:
                return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>';
        }
    }
};

// ==================== Pipeline Drawer ====================

let pipelines = [];
let executions = [];
let executionPollingInterval = null;

function initPipelineDrawer() {
    const drawer = document.getElementById('pipeline-drawer');
    const toggleBtn = document.getElementById('pipeline-toggle-btn');
    const closeBtn = document.getElementById('drawer-close-btn');

    toggleBtn.addEventListener('click', () => {
        openPipelineDrawer();
    });

    closeBtn.addEventListener('click', () => {
        closePipelineDrawer();
    });

    // Close on escape
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && drawer.classList.contains('open')) {
            closePipelineDrawer();
        }
    });

    // Note: Pipelines are loaded when drawer is opened (openPipelineDrawer)
    // Don't load them here to avoid triggering auth before checkSetupStatus
}

function openPipelineDrawer() {
    const drawer = document.getElementById('pipeline-drawer');
    const toggleBtn = document.getElementById('pipeline-toggle-btn');

    drawer.classList.add('open');
    toggleBtn.classList.add('hidden');

    // Load pipelines and executions when drawer opens
    loadPipelines();
    loadExecutions();
    startExecutionPolling();
}

function closePipelineDrawer() {
    const drawer = document.getElementById('pipeline-drawer');
    const toggleBtn = document.getElementById('pipeline-toggle-btn');

    drawer.classList.remove('open');
    toggleBtn.classList.remove('hidden');

    // Stop polling
    stopExecutionPolling();
}

async function loadPipelines() {
    try {
        const res = await authFetch('/api/v1/pipeline');
        if (res.ok) {
            const data = await res.json();
            // API returns { pipelines: [...] }
            pipelines = data.pipelines || data || [];
            renderPipelineList();
        }
    } catch (e) {
        console.error('Failed to load pipelines:', e);
    }
}

function renderPipelineList() {
    const listEl = document.getElementById('pipeline-list');

    if (pipelines.length === 0) {
        listEl.innerHTML = '<p class="setting-hint" style="text-align: center; padding: 20px;">No pipelines available</p>';
        return;
    }

    listEl.innerHTML = pipelines.map(p => `
        <div class="pipeline-card" data-name="${escapeHtml(p.name)}">
            <div class="pipeline-card-header">
                <span class="pipeline-card-name">${escapeHtml(p.displayName || p.name)}</span>
                <span class="pipeline-card-status idle">Ready</span>
            </div>
            <div class="pipeline-card-desc">${escapeHtml(p.description || 'No description')}</div>
            <div class="pipeline-card-actions">
                <button class="pipeline-run-btn" data-pipeline="${escapeHtml(p.name)}">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polygon points="5 3 19 12 5 21 5 3"/>
                    </svg>
                    Run
                </button>
            </div>
        </div>
    `).join('');

    // Bind run buttons
    listEl.querySelectorAll('.pipeline-run-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const pipelineName = btn.dataset.pipeline;
            await executePipeline(pipelineName, btn);
        });
    });
}

async function executePipeline(pipelineName, btn) {
    btn.disabled = true;
    btn.innerHTML = '<span class="status-dot"></span> Running...';

    const card = btn.closest('.pipeline-card');
    const statusEl = card.querySelector('.pipeline-card-status');
    statusEl.textContent = 'Running';
    statusEl.className = 'pipeline-card-status running';

    try {
        const res = await authFetch(`/api/v1/pipeline/${pipelineName}/execute`, {
            method: 'POST',
            body: JSON.stringify({})
        });

        if (!res.ok) {
            throw new Error('Failed to start pipeline');
        }

        const execution = await res.json();
        ToastManager.show('info', 'Pipeline Started', `${pipelineName} is now running`, { duration: 3000 });

        // Start polling for this execution
        pollExecution(execution.executionId, pipelineName, btn, card);

    } catch (e) {
        console.error('Failed to execute pipeline:', e);
        ToastManager.show('error', 'Pipeline Error', e.message);

        btn.disabled = false;
        btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="5 3 19 12 5 21 5 3"/></svg> Run';
        statusEl.textContent = 'Ready';
        statusEl.className = 'pipeline-card-status idle';
    }
}

async function pollExecution(executionId, pipelineName, btn, card) {
    const statusEl = card.querySelector('.pipeline-card-status');

    const poll = async () => {
        try {
            const res = await authFetch(`/api/v1/pipeline/executions/${executionId}`);
            if (!res.ok) return;

            const execution = await res.json();
            // Normalize status to lowercase for comparison
            const status = (execution.status || '').toString().toLowerCase();

            console.log(`Pipeline ${executionId} status: ${status}`);

            // Check if waiting for approval
            if (status === 'waitingforapproval' && execution.approvalInfo) {
                statusEl.textContent = 'Approval';
                statusEl.className = 'pipeline-card-status waiting';

                // Show approval toast
                showApprovalToast(executionId, pipelineName, execution.approvalInfo);

                // Refresh executions
                await loadExecutions();
                return; // Stop polling, will be updated via approval action
            }

            // Check if completed
            if (status === 'completed') {
                statusEl.textContent = 'Ready';
                statusEl.className = 'pipeline-card-status idle';
                btn.disabled = false;
                btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="5 3 19 12 5 21 5 3"/></svg> Run';

                ToastManager.show('success', 'Pipeline Completed', `${pipelineName} finished successfully`);
                await loadExecutions();
                return;
            }

            // Check if failed
            if (status === 'failed' || status === 'rejected') {
                statusEl.textContent = 'Ready';
                statusEl.className = 'pipeline-card-status idle';
                btn.disabled = false;
                btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="5 3 19 12 5 21 5 3"/></svg> Run';

                ToastManager.show('error', 'Pipeline Failed', execution.summary || `${pipelineName} execution failed`);
                await loadExecutions();
                return;
            }

            // Continue polling
            setTimeout(poll, 2000);

        } catch (e) {
            console.error('Polling error:', e);
            setTimeout(poll, 5000);
        }
    };

    poll();
}

function showApprovalToast(executionId, pipelineName, approvalInfo) {
    ToastManager.show('approval', 'Approval Required', approvalInfo.message || `${pipelineName} requires approval`, {
        duration: 0, // Don't auto-dismiss
        actions: [
            {
                label: 'Approve',
                type: 'approve',
                onClick: async () => {
                    await submitDrawerApproval(executionId, true, pipelineName);
                }
            },
            {
                label: 'Reject',
                type: 'reject',
                onClick: async () => {
                    await submitDrawerApproval(executionId, false, pipelineName);
                }
            }
        ]
    });
}

async function submitDrawerApproval(executionId, approved, pipelineName) {
    const action = approved ? 'approve' : 'reject';

    try {
        const response = await authFetch(`/api/v1/pipeline/executions/${executionId}/${action}`, {
            method: 'POST'
        });

        if (!response.ok) {
            throw new Error(`Failed to ${action} execution`);
        }

        ToastManager.show(
            approved ? 'success' : 'info',
            approved ? 'Approved' : 'Rejected',
            `${pipelineName} ${approved ? 'will continue' : 'was cancelled'}`
        );

        // Refresh executions and pipeline status
        await loadExecutions();
        await loadPipelines();

    } catch (error) {
        console.error('Drawer approval error:', error);
        ToastManager.show('error', 'Approval Failed', error.message);
    }
}

async function loadExecutions() {
    try {
        const res = await authFetch('/api/v1/pipeline/executions?limit=10');
        if (res.ok) {
            executions = await res.json();
            renderExecutionList();
            updateBadge();
        }
    } catch (e) {
        console.error('Failed to load executions:', e);
    }
}

function renderExecutionList() {
    const listEl = document.getElementById('execution-list');

    if (executions.length === 0) {
        listEl.innerHTML = '<p class="setting-hint" style="text-align: center; padding: 16px; font-size: 0.8rem;">No recent executions</p>';
        return;
    }

    listEl.innerHTML = executions.map(e => {
        const statusClass = e.status.toLowerCase().replace(' ', '-');
        const statusIcon = getStatusIconClass(e.status);
        const timeAgo = formatTimeAgo(e.startedAt);

        let actionsHtml = '';
        if ((e.status || '').toLowerCase() === 'waitingforapproval') {
            actionsHtml = `
                <div class="execution-actions">
                    <button class="execution-action-btn approve" data-id="${e.id}">Approve</button>
                    <button class="execution-action-btn reject" data-id="${e.id}">Reject</button>
                </div>
            `;
        }

        return `
            <div class="execution-item" data-id="${e.id}">
                <div class="execution-status-icon ${statusIcon}"></div>
                <div class="execution-info">
                    <div class="execution-name">${escapeHtml(e.pipelineName)}</div>
                    <div class="execution-time">${timeAgo}</div>
                </div>
                ${actionsHtml}
            </div>
        `;
    }).join('');

    // Bind approval buttons
    listEl.querySelectorAll('.execution-action-btn.approve').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = btn.dataset.id;
            const exec = executions.find(e => e.id === id);
            await submitDrawerApproval(id, true, exec?.pipelineName || 'Pipeline');
        });
    });

    listEl.querySelectorAll('.execution-action-btn.reject').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = btn.dataset.id;
            const exec = executions.find(e => e.id === id);
            await submitDrawerApproval(id, false, exec?.pipelineName || 'Pipeline');
        });
    });
}

function getStatusIconClass(status) {
    const s = (status || '').toLowerCase();
    switch (s) {
        case 'running': return 'running';
        case 'waitingforapproval': return 'waiting';
        case 'completed': return 'completed';
        case 'failed':
        case 'rejected': return 'failed';
        default: return 'idle';
    }
}

function formatTimeAgo(dateStr) {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now - date;
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);

    if (diffSec < 60) return 'Just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    if (diffHour < 24) return `${diffHour}h ago`;
    return date.toLocaleDateString();
}

function updateBadge() {
    const badge = document.getElementById('pipeline-badge');
    const waitingCount = executions.filter(e => (e.status || '').toLowerCase() === 'waitingforapproval').length;

    if (waitingCount > 0) {
        badge.textContent = waitingCount;
        badge.style.display = 'flex';
    } else {
        badge.style.display = 'none';
    }
}

function startExecutionPolling() {
    stopExecutionPolling();
    executionPollingInterval = setInterval(loadExecutions, 5000);
}

function stopExecutionPolling() {
    if (executionPollingInterval) {
        clearInterval(executionPollingInterval);
        executionPollingInterval = null;
    }
}

// Initialize pipeline drawer on DOM ready
document.addEventListener('DOMContentLoaded', () => {
    initPipelineDrawer();
});

// Show admin-only navigation items based on user role
function initAdminNavItems() {
    const user = getCurrentUser();
    if (!user || !user.roles) return;

    const isAdmin = user.roles.some(role =>
        role.toLowerCase() === 'admin' || role.toLowerCase() === 'superadmin'
    );

    if (isAdmin) {
        document.querySelectorAll('.admin-only').forEach(el => {
            el.style.display = '';
        });
    }
}