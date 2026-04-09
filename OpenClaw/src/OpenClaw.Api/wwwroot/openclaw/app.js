// DOM Elements
let messagesEl, userInputEl, sendBtn, themeToggle;
let currentConversationId = null;
let conversations = [];
let modelProviders = [];      // user's own model providers
let availableProviders = [];  // global providers available to add
let skills = [];
let appConfigs = [];

// Input history navigation (from current conversation)
let historyIndex = -1;
let tempInput = ''; // Store current input when navigating history

// Pending images for upload
let pendingImages = []; // Array of { base64Data, mimeType, previewUrl }

// AbortController for stopping inference
let currentAbortController = null;

// Token usage tracking (session-scoped)
const tokenUsage = {
    inputTokens: 0,
    outputTokens: 0,
    callCount: 0,
    history: [] // { message, inputTokens, outputTokens, timestamp }
};

// Configure mermaid
if (typeof mermaid !== 'undefined') {
    mermaid.initialize({ startOnLoad: false, theme: 'dark' });
}

// Configure marked.js with custom renderer for mermaid
const renderer = new marked.Renderer();
const originalCode = renderer.code;
renderer.code = function({ text, lang }) {
    if (lang === 'mermaid') {
        const id = 'mermaid-' + Math.random().toString(36).substr(2, 9);
        return `<div class="mermaid" id="${id}">${text}</div>`;
    }
    // Default code rendering with hljs
    if (lang && hljs.getLanguage(lang)) {
        const highlighted = hljs.highlight(text, { language: lang }).value;
        return `<pre><code class="hljs language-${lang}">${highlighted}</code></pre>`;
    }
    const highlighted = hljs.highlightAuto(text).value;
    return `<pre><code class="hljs">${highlighted}</code></pre>`;
};

marked.setOptions({
    renderer,
    breaks: true,
    gfm: true
});

document.addEventListener('DOMContentLoaded', () => {
    messagesEl = document.getElementById('messages');
    userInputEl = document.getElementById('user-input');
    sendBtn = document.getElementById('send-btn');
    themeToggle = document.getElementById('theme-toggle');

    // Initialize Chat-specific theme settings (hljs, button text)
    initChatTheme();

    // Listen for theme changes from top-header.js
    window.addEventListener('themechange', (e) => {
        updateHljsTheme(e.detail.theme);
        updateThemeButtonText(e.detail.theme);
    });

    // Show admin-only nav items based on user role
    initAdminNavItems();

    // Channel header toggle (CSP-compliant replacement for inline onclick)
    document.getElementById('telegram-channel-header').addEventListener('click', (e) => {
        // Don't toggle when clicking the toggle switch itself
        if (e.target.closest('#telegram-toggle-label')) return;
        document.getElementById('telegram-channel').classList.toggle('collapsed');
    });
    document.getElementById('telegram-toggle-label').addEventListener('click', (e) => {
        e.stopPropagation();
    });

    // Event Listeners
    sendBtn.addEventListener('click', sendMessage);
    themeToggle.addEventListener('click', toggleChatTheme);

    userInputEl.addEventListener('keydown', async (e) => {
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
                    await selectAutocompleteItem();
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

    // Edit display name
    document.getElementById('edit-name-btn').addEventListener('click', editDisplayName);

    // Image upload handling
    initImageUpload();

    // Check setup status first - this handles authentication and onboarding
    // Only load data after authentication is confirmed
    checkSetupStatus().then(() => {
        // Only load if authenticated
        if (isAuthenticated()) {
            loadConversations();
            loadSkills();
            loadAgentList();
            loadToolList();
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

async function editDisplayName() {
    const user = getCurrentUser();
    if (!user) return;

    const newName = prompt('Enter new display name:', user.name);
    if (!newName || newName.trim() === '' || newName.trim() === user.name) return;

    try {
        const res = await authFetch(`/api/v1/users/${user.id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: newName.trim() })
        });

        if (res.ok) {
            const updated = await res.json();
            // Update stored user info
            const stored = JSON.parse(localStorage.getItem('user') || '{}');
            stored.name = updated.name;
            localStorage.setItem('user', JSON.stringify(stored));
            // Update all UI elements showing the name
            updateUserProfile();
            // Update top-header user name
            const topHeaderName = document.querySelector('.user-name');
            if (topHeaderName) topHeaderName.textContent = updated.name;
            document.querySelector('.profile-section').classList.remove('open');
        } else {
            const err = await res.json().catch(() => ({}));
            alert(err.title || err.message || 'Failed to update name');
        }
    } catch (e) {
        alert('Error: ' + e.message);
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

// Theme functions for Chat page
// Uses top-header.js for core theme management, adds Chat-specific updates
function initChatTheme() {
    const savedTheme = localStorage.getItem('theme') || 'dark';
    updateHljsTheme(savedTheme);
    updateThemeButtonText(savedTheme);
}

function toggleChatTheme() {
    // Use top-header.js toggleTheme if available, otherwise handle locally
    if (typeof toggleTheme === 'function') {
        toggleTheme();
    } else {
        const currentTheme = localStorage.getItem('theme') || 'dark';
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
        if (newTheme === 'light') {
            document.documentElement.setAttribute('data-theme', 'light');
        } else {
            document.documentElement.removeAttribute('data-theme');
        }
        localStorage.setItem('theme', newTheme);
    }
    // Update Chat-specific UI
    const newTheme = localStorage.getItem('theme') || 'dark';
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
    if (hljsLink) {
        if (theme === 'light') {
            hljsLink.href = 'https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github.min.css';
        } else {
            hljsLink.href = 'https://cdn.jsdelivr.net/gh/highlightjs/cdn-release@11.9.0/build/styles/github-dark.min.css';
        }
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

function updateStatusIndicator(indicator, type, toolName, progressMessage) {
    const textEl = indicator.querySelector('.status-text');
    switch (type) {
        case 'Thinking':
            textEl.textContent = 'Thinking...';
            break;
        case 'ToolExecuting':
            textEl.textContent = `Executing: ${toolName}...`;
            break;
        case 'ToolProgress':
            textEl.textContent = progressMessage
                ? `${toolName}: ${progressMessage}`
                : `Executing: ${toolName}...`;
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
        renderMermaidDiagrams();
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
    renderMermaidDiagrams();

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

    // Render markdown and sanitize with DOMPurify to prevent XSS
    const html = marked.parse(content);
    const sanitized = typeof DOMPurify !== 'undefined'
        ? DOMPurify.sanitize(html, {
            ADD_TAGS: ['katex-display'],
            ADD_ATTR: ['class', 'style', 'id'],
            CUSTOM_ELEMENT_HANDLING: {
                tagNameCheck: /^div$/,
                attributeNameCheck: /^(class|id)$/,
                allowCustomizedBuiltInElements: true
            }
        })
        : html;
    return sanitized;
}

// Run mermaid on all pending diagrams in the messages container
function renderMermaidDiagrams() {
    if (typeof mermaid === 'undefined') return;
    try {
        const pending = document.querySelectorAll('.mermaid:not([data-processed])');
        if (pending.length > 0) {
            mermaid.run({ nodes: pending });
        }
    } catch (e) {
        console.warn('Mermaid render error:', e);
    }
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
        // Collect tool/agent tags
        const toolTags = contextTags.filter(t => t.type === 'tool').map(t => t.name);
        const agentTags = contextTags.filter(t => t.type === 'agent').map(t => t.name);

        const res = await authFetch('/api/v1/chat/stream', {
            method: 'POST',
            body: JSON.stringify({
                message: message || 'What is in this image?',
                conversationId: currentConversationId,
                language: settings.language,
                images: images,
                tools: toolTags.length > 0 ? toolTags : undefined,
                agents: agentTags.length > 0 ? agentTags : undefined
            }),
            signal: currentAbortController.signal
        });

        // Clear tags after sending
        contextTags = [];
        renderContextTags();

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

                        case 'ToolProgress':
                            if (statusIndicator) {
                                updateStatusIndicator(statusIndicator, 'ToolProgress', event.toolName, event.content);
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

                        case 'UsageReport':
                            if (event.usage) {
                                const u = event.usage;
                                tokenUsage.inputTokens += (u.inputTokens || 0);
                                tokenUsage.outputTokens += (u.outputTokens || 0);
                                tokenUsage.callCount++;
                                tokenUsage.history.push({
                                    message: accumulatedContent?.substring(0, 60) || '(tool call)',
                                    inputTokens: u.inputTokens || 0,
                                    outputTokens: u.outputTokens || 0,
                                    timestamp: new Date()
                                });
                            }
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
             data-id="${escapeHtml(c.id)}">
            <span class="title">${escapeHtml(c.title)}</span>
            <button class="delete-btn" data-action="delete-conv" data-id="${escapeHtml(c.id)}">🗑️</button>
        </div>
    `).join('');

    // Add click handlers
    list.querySelectorAll('.conversation-item').forEach(item => {
        item.addEventListener('click', () => selectConversation(item.dataset.id));
    });
    list.querySelectorAll('[data-action="delete-conv"]').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            deleteConversation(btn.dataset.id, e);
        });
    });
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
        language: 'auto'
    };
}

function saveSettings(settings) {
    localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
}

async function loadModelProviders() {
    try {
        const [userRes, availableRes] = await Promise.all([
            authFetch('/api/v1/user-model-provider'),
            authFetch('/api/v1/user-model-provider/available')
        ]);
        if (userRes.ok) {
            modelProviders = await userRes.json();
        }
        if (availableRes.ok) {
            availableProviders = await availableRes.json();
        }
    } catch (e) {
        console.error('Failed to load model providers:', e);
        modelProviders = [];
        availableProviders = [];
    }
    return modelProviders;
}


function renderModelList() {
    const listEl = document.getElementById('model-list');
    const availableListEl = document.getElementById('available-provider-list');

    // --- Render user's own providers ---
    if (modelProviders.length === 0) {
        listEl.innerHTML = '<p class="setting-hint">No model providers configured. Add a custom provider or select from the global providers below.</p>';
    } else {
        listEl.innerHTML = modelProviders.map(p => {
            const isGlobalRef = !!p.globalModelProviderId;
            const badge = isGlobalRef
                ? '<span class="provider-badge global">Global</span>'
                : '<span class="provider-badge custom">Custom</span>';
            const editBtn = isGlobalRef ? '' : `
                <button class="model-item-btn edit" data-id="${p.id}" title="Edit">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/>
                        <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/>
                    </svg>
                </button>`;
            return `
            <div class="model-item ${p.isDefault ? 'active' : ''}" data-id="${p.id}">
                <input type="radio" name="active-model" class="model-item-radio"
                       value="${p.id}" ${p.isDefault ? 'checked' : ''}>
                <div class="model-item-info">
                    <div class="model-item-name">${escapeHtml(p.name)} ${badge}</div>
                    <div class="model-item-details">${escapeHtml(p.type)} - ${escapeHtml(p.modelName)}</div>
                </div>
                <div class="model-item-actions">
                    ${editBtn}
                    <button class="model-item-btn delete" data-id="${p.id}" title="Remove">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M3 6h18M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2"/>
                        </svg>
                    </button>
                </div>
            </div>`;
        }).join('');

        // Event listeners for radio buttons (set default)
        listEl.querySelectorAll('.model-item-radio').forEach(radio => {
            radio.addEventListener('change', async (e) => {
                const id = e.target.value;
                await setDefaultModelProvider(id);
                renderModelList();
            });
        });

        // Event listeners for edit buttons (custom providers only)
        listEl.querySelectorAll('.model-item-btn.edit').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                openEditModelModal(btn.dataset.id);
            });
        });

        // Event listeners for delete buttons
        listEl.querySelectorAll('.model-item-btn.delete').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                if (confirm('Remove this model provider from your list?')) {
                    await deleteModelProvider(btn.dataset.id);
                    renderModelList();
                }
            });
        });
    }

    // --- Render available global providers ---
    // Filter out global providers already added by user
    const userGlobalIds = new Set(modelProviders.filter(p => p.globalModelProviderId).map(p => p.globalModelProviderId));
    const available = availableProviders.filter(p => !userGlobalIds.has(p.id));

    if (available.length === 0) {
        availableListEl.innerHTML = '<p class="setting-hint">All global providers have been added to your list, or none are available.</p>';
    } else {
        availableListEl.innerHTML = available.map(p => `
            <div class="model-item available-item" data-id="${p.id}">
                <div class="model-item-info">
                    <div class="model-item-name">${escapeHtml(p.name)}</div>
                    <div class="model-item-details">${escapeHtml(p.type)} - ${escapeHtml(p.modelName)}</div>
                    ${p.description ? `<div class="model-item-desc">${escapeHtml(p.description)}</div>` : ''}
                </div>
                <div class="model-item-actions">
                    <button class="model-item-btn add-global" data-id="${p.id}" data-name="${escapeHtml(p.name)}" title="Add to my list">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M12 5v14M5 12h14"/>
                        </svg>
                    </button>
                </div>
            </div>
        `).join('');

        // Event listeners for add buttons
        availableListEl.querySelectorAll('.model-item-btn.add-global').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                e.stopPropagation();
                await addGlobalProvider(btn.dataset.id, btn.dataset.name);
                renderModelList();
            });
        });
    }
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

async function setDefaultModelProvider(id) {
    try {
        await authFetch(`/api/v1/user-model-provider/${id}/set-default`, { method: 'POST' });
        await loadModelProviders();
    } catch (e) {
        console.error('Failed to set default provider:', e);
    }
}

async function deleteModelProvider(id) {
    try {
        await authFetch(`/api/v1/user-model-provider/${id}`, { method: 'DELETE' });
        await loadModelProviders();
    } catch (e) {
        console.error('Failed to delete provider:', e);
    }
}

async function addGlobalProvider(globalId, name) {
    try {
        const isFirst = modelProviders.length === 0;
        const res = await authFetch('/api/v1/user-model-provider', {
            method: 'POST',
            body: JSON.stringify({
                globalModelProviderId: globalId,
                name: name,
                isDefault: isFirst
            })
        });
        if (!res.ok) {
            const err = await res.json();
            alert(err.message || 'Failed to add provider');
            return;
        }
        await loadModelProviders();
    } catch (e) {
        console.error('Failed to add global provider:', e);
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
    await Promise.all([loadModelProviders(), loadSkills(), loadTelegramSettings(), loadAppConfigs(), loadUserPreferences(), loadAgentList(), loadToolList()]);
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

// Tools Settings Functions
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
        listEl.innerHTML = '<p class="setting-hint">No tools available.</p>';
        return;
    }

    listEl.innerHTML = skills.map(s => `
        <div class="skill-item" data-name="${escapeHtml(s.name)}">
            <div class="skill-item-info">
                <div class="skill-item-name">
                    ${escapeHtml(s.name)}
                    <code>/${escapeHtml(s.name)}</code>
                </div>
                <div class="skill-item-description collapsed" data-action="toggle-desc">${escapeHtml(s.description)}</div>
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

    // Add event listeners for description expand/collapse
    listEl.querySelectorAll('[data-action="toggle-desc"]').forEach(el => {
        el.addEventListener('click', () => el.classList.toggle('collapsed'));
    });
}

async function toggleSkill(skillName, enabled) {
    try {
        const action = enabled ? 'enable' : 'disable';
        const res = await authFetch(`/api/v1/skill-settings/${encodeURIComponent(skillName)}/${action}`, {
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
            botInfoEl.innerHTML = `<span>Validation failed: ${escapeHtml(result.message || 'Invalid token')}</span>`;
            return;
        }

        botInfoEl.style.display = 'block';
        botInfoEl.className = 'bot-info-card';
        botInfoEl.innerHTML = `
            <div class="bot-name">${escapeHtml(result.botInfo.firstName)}</div>
            <div class="bot-username">@${escapeHtml(result.botInfo.username || 'N/A')}</div>
            <div class="bot-details">
                Bot ID: ${escapeHtml(String(result.botInfo.id))}<br>
                Can join groups: ${result.botInfo.canJoinGroups ? 'Yes' : 'No'}
            </div>
        `;
    } catch (e) {
        console.error('Telegram validation error:', e);
        botInfoEl.style.display = 'block';
        botInfoEl.className = 'bot-info-card channel-validation-status error';
        botInfoEl.innerHTML = `<span>Validation failed: ${escapeHtml(e.message)}</span>`;
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
            const body = await res.json().catch(() => null);
            throw new Error(body?.message || body?.detail || 'Failed to save Telegram settings');
        }

        telegramSettings = await res.json();
        renderTelegramSettings();
        return true;
    } catch (e) {
        console.error('Failed to save Telegram settings:', e);
        alert(e.message);
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
        enabledToggle.addEventListener('change', async () => {
            const success = await saveTelegramSettings();
            if (!success) {
                // Revert toggle on failure
                enabledToggle.checked = !enabledToggle.checked;
            }
        });
    }
}

// App Config Functions
async function loadAppConfigs() {
    try {
        const res = await authFetch('/api/v1/user-config');
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
        const res = await authFetch(`/api/v1/user-config/${encodeURIComponent(key)}`, {
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
        const res = await authFetch(`/api/v1/user-config/${encodeURIComponent(key)}`, {
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

// ── Enhanced Chat Syntax Autocomplete ──
// /  → tools (add as tag)
// // → agents (add as tag)
// @  → workspace files (inline)
let autocompleteIndex = -1;
let filteredItems = [];
let autocompleteMode = null; // 'agent' | 'file' | 'tool'
let agentList = [];
let toolList = [];

// Context tags state
let contextTags = []; // { type: 'tool'|'agent', name: string }

function addContextTag(type, name) {
    if (contextTags.some(t => t.type === type && t.name === name)) return;
    contextTags.push({ type, name });
    renderContextTags();
}

function removeContextTag(index) {
    contextTags.splice(index, 1);
    renderContextTags();
}

function renderContextTags() {
    const container = document.getElementById('context-tags');
    if (contextTags.length === 0) {
        container.style.display = 'none';
        container.innerHTML = '';
        return;
    }
    container.style.display = 'flex';
    container.innerHTML = contextTags.map((tag, i) => {
        const prefix = tag.type === 'agent' ? '//' : '/';
        const cls = tag.type === 'agent' ? 'tag-agent' : 'tag-tool';
        return `<span class="context-tag ${cls}" data-index="${i}">${prefix}${escapeHtml(tag.name)}<span class="tag-remove" data-index="${i}">&times;</span></span>`;
    }).join('');
    container.querySelectorAll('.tag-remove').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            removeContextTag(parseInt(btn.dataset.index));
        });
    });
}

async function loadAgentList() {
    try {
        const res = await authFetch('/api/v1/agents');
        if (res.ok) agentList = await res.json();
    } catch {}
}

async function loadToolList() {
    try {
        const res = await authFetch('/api/v1/agents/tools');
        if (res.ok) toolList = await res.json();
    } catch {}
}

function handleAutocompleteInput() {
    const value = userInputEl.value;
    const cursorPos = userInputEl.selectionStart;

    // Detect // agent invoke (must check before /)
    if (value.startsWith('//')) {
        const inCommandArea = !value.includes(' ') || cursorPos <= value.indexOf(' ') + 1;
        if (inCommandArea) {
            const query = value.slice(2).split(' ')[0].toLowerCase();
            filteredItems = agentList
                .filter(a => a.name.toLowerCase().includes(query))
                ;
            autocompleteMode = 'agent';
            if (filteredItems.length > 0) { showAutocomplete(filteredItems); return; }
        }
    }
    // Detect / tool invoke
    else if (value.startsWith('/')) {
        const inCommandArea = !value.includes(' ') || cursorPos <= value.indexOf(' ') + 1;
        if (inCommandArea) {
            const query = value.slice(1).split(' ')[0].toLowerCase();
            filteredItems = toolList
                .filter(t => (t.name || t).toLowerCase().includes(query))
                ;
            autocompleteMode = 'tool';
            if (filteredItems.length > 0) { showAutocomplete(filteredItems); return; }
        }
    }

    // Detect @ file reference (at cursor position)
    const beforeCursor = value.slice(0, cursorPos);
    const atMatch = beforeCursor.match(/@([\w./\-]*)$/);
    if (atMatch) {
        const query = atMatch[1].toLowerCase();
        autocompleteMode = 'file';
        loadFileAutocomplete(query);
        return;
    }

    hideAutocomplete();
}

let fileAutocompleteDebounce = null;
function loadFileAutocomplete(query) {
    clearTimeout(fileAutocompleteDebounce);
    fileAutocompleteDebounce = setTimeout(() => loadFileAutocompleteNow(query), 200);
}

async function loadFileAutocompleteNow(query) {
    try {
        const dir = query.includes('/') ? query.slice(0, query.lastIndexOf('/')) : '';
        const res = await authFetch(`/api/v1/workspace-file/list?path=${encodeURIComponent(dir)}`);
        if (!res.ok) return;
        const data = await res.json();
        const prefix = query.includes('/') ? query.slice(query.lastIndexOf('/') + 1) : query;
        filteredItems = (data.entries || [])
            .filter(e => e.name.toLowerCase().includes(prefix.toLowerCase()))
            .map(e => ({ name: dir ? `${dir}/${e.name}` : e.name, description: e.isDirectory ? 'folder' : `${(e.size / 1024).toFixed(1)} KB`, isDirectory: e.isDirectory }));
        if (filteredItems.length > 0) showAutocomplete(filteredItems);
        else hideAutocomplete();
    } catch { hideAutocomplete(); }
}

function showAutocomplete(items) {
    const dropdown = document.getElementById('autocomplete-dropdown');
    autocompleteIndex = -1;

    const prefixMap = { agent: '//', file: '@', tool: '/' };
    const prefix = prefixMap[autocompleteMode] || '';

    dropdown.innerHTML = items.map((a, i) => {
        const name = typeof a === 'string' ? a : a.name;
        const desc = typeof a === 'string' ? '' : (a.description || '');
        return `
        <div class="autocomplete-item" data-index="${i}" data-name="${escapeHtml(name)}">
            <span class="autocomplete-command">${prefix}${escapeHtml(name)}</span>
            ${desc ? `<span class="autocomplete-desc">${escapeHtml(desc)}</span>` : ''}
        </div>`;
    }).join('');

    dropdown.classList.add('visible');

    dropdown.querySelectorAll('.autocomplete-item').forEach(item => {
        item.addEventListener('click', async () => {
            autocompleteIndex = parseInt(item.dataset.index);
            await selectAutocompleteItem();
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
    filteredItems = [];
    autocompleteMode = null;
}

function isAutocompleteVisible() {
    return document.getElementById('autocomplete-dropdown').classList.contains('visible');
}

function navigateAutocomplete(direction) {
    if (filteredItems.length === 0) return;

    autocompleteIndex += direction;
    if (autocompleteIndex < 0) autocompleteIndex = filteredItems.length - 1;
    if (autocompleteIndex >= filteredItems.length) autocompleteIndex = 0;

    updateAutocompleteSelection();
}

function updateAutocompleteSelection() {
    const dropdown = document.getElementById('autocomplete-dropdown');
    dropdown.querySelectorAll('.autocomplete-item').forEach((item, i) => {
        item.classList.toggle('selected', i === autocompleteIndex);
        if (i === autocompleteIndex) item.scrollIntoView({ block: 'nearest' });
    });
}

function getSelectedAutocompleteItem() {
    if (autocompleteIndex >= 0 && autocompleteIndex < filteredItems.length) {
        return filteredItems[autocompleteIndex];
    }
    return null;
}

async function selectAutocompleteItem() {
    const item = getSelectedAutocompleteItem();
    if (!item) return;

    const name = typeof item === 'string' ? item : item.name;
    const value = userInputEl.value;
    const cursorPos = userInputEl.selectionStart;

    if (autocompleteMode === 'agent') {
        // Add as tag, clear the //command from input
        addContextTag('agent', name);
        const spaceIndex = value.indexOf(' ');
        userInputEl.value = spaceIndex > 0 ? value.slice(spaceIndex + 1) : '';
    } else if (autocompleteMode === 'tool') {
        // Add as tag, clear the /command from input
        addContextTag('tool', name);
        const spaceIndex = value.indexOf(' ');
        userInputEl.value = spaceIndex > 0 ? value.slice(spaceIndex + 1) : '';
    } else if (autocompleteMode === 'file') {
        const beforeCursor = value.slice(0, cursorPos);
        const atIndex = beforeCursor.lastIndexOf('@');
        const suffix = item.isDirectory ? '/' : ' ';
        userInputEl.value = value.slice(0, atIndex) + '@' + name + suffix + value.slice(cursorPos);
        const newCursorPos = atIndex + 1 + name.length + suffix.length;
        userInputEl.setSelectionRange(newCursorPos, newCursorPos);
        userInputEl.focus();

        if (item.isDirectory) {
            // Show loading state immediately
            const dropdown = document.getElementById('autocomplete-dropdown');
            dropdown.innerHTML = '<div class="autocomplete-item"><span class="autocomplete-command">Loading...</span></div>';
            dropdown.classList.add('visible');
            autocompleteMode = 'file';
            // Fetch next level
            const dirQuery = name.endsWith('/') ? name : name + '/';
            try {
                const dir = dirQuery.slice(0, dirQuery.lastIndexOf('/'));
                const res = await authFetch(`/api/v1/workspace-file/list?path=${encodeURIComponent(dir)}`);
                if (res.ok) {
                    const data = await res.json();
                    filteredItems = (data.entries || [])
                        .map(e => ({ name: dir ? `${dir}/${e.name}` : e.name, description: e.isDirectory ? 'folder' : `${(e.size / 1024).toFixed(1)} KB`, isDirectory: e.isDirectory }));
                    if (filteredItems.length > 0) { showAutocomplete(filteredItems); }
                    else { dropdown.innerHTML = '<div class="autocomplete-item"><span class="autocomplete-desc">Empty directory</span></div>'; }
                }
            } catch { hideAutocomplete(); }
            return;
        }
    }

    userInputEl.focus();
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
        const res = await authFetch('/api/v1/user-model-provider/validate', {
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
            // Update existing user provider
            res = await authFetch(`/api/v1/user-model-provider/${providerId}`, {
                method: 'PUT',
                body: JSON.stringify(provider)
            });
            if (!res.ok) throw new Error('Failed to update provider');
        } else {
            // Create new custom user provider
            const isFirstProvider = modelProviders.length === 0;
            provider.isDefault = isFirstProvider;
            res = await authFetch('/api/v1/user-model-provider', {
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
            <button class="remove-btn" data-action="remove-image" data-index="${index}">&times;</button>
        </div>
    `).join('');

    previewArea.querySelectorAll('[data-action="remove-image"]').forEach(btn => {
        btn.addEventListener('click', () => removeImage(parseInt(btn.dataset.index)));
    });
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

// ==================== Cron Job Drawer ====================

let cronJobs = [];

function initCronJobDrawer() {
    const drawer = document.getElementById('cronjob-drawer');
    const toggleBtn = document.getElementById('cronjob-toggle-btn');
    const closeBtn = document.getElementById('drawer-close-btn');

    toggleBtn.addEventListener('click', () => {
        openCronJobDrawer();
    });

    closeBtn.addEventListener('click', () => {
        closeCronJobDrawer();
    });

    // Close on escape
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && drawer.classList.contains('open')) {
            closeCronJobDrawer();
        }
    });
}

async function openCronJobDrawer() {
    const drawer = document.getElementById('cronjob-drawer');
    const toggleBtn = document.getElementById('cronjob-toggle-btn');

    drawer.classList.add('open');
    toggleBtn.classList.add('hidden');

    // Load cron jobs when drawer opens
    await loadCronJobs();
}

function closeCronJobDrawer() {
    const drawer = document.getElementById('cronjob-drawer');
    const toggleBtn = document.getElementById('cronjob-toggle-btn');

    drawer.classList.remove('open');
    toggleBtn.classList.remove('hidden');
}

async function loadCronJobs() {
    const listEl = document.getElementById('cronjobDrawerList');
    listEl.innerHTML = '<p class="setting-hint" style="text-align: center; padding: 20px;">Loading...</p>';

    try {
        const res = await authFetch('/api/v1/cron-job');
        if (res.ok) {
            const data = await res.json();
            cronJobs = data.jobs || data || [];
            renderCronJobDrawerList(cronJobs);
        }
    } catch (e) {
        console.error('Failed to load cron jobs:', e);
        listEl.innerHTML = '<p class="setting-hint" style="text-align: center; padding: 20px;">Failed to load cron jobs</p>';
    }
}

function renderCronJobDrawerList(jobs) {
    const listEl = document.getElementById('cronjobDrawerList');

    if (!jobs || jobs.length === 0) {
        listEl.innerHTML = '<p class="setting-hint" style="text-align: center; padding: 20px;">No cron jobs available</p>';
        return;
    }

    listEl.innerHTML = jobs.map(job => {
        const isEnabled = job.enabled !== false;
        const statusClass = isEnabled ? 'active' : 'inactive';
        const statusLabel = isEnabled ? 'Active' : 'Inactive';
        const schedule = job.cronExpression || job.schedule || 'No schedule';

        return `
            <div class="cronjob-card" data-id="${escapeHtml(job.id || job.name)}">
                <div class="cronjob-card-header">
                    <div class="cronjob-card-title">
                        <span class="cronjob-status-dot ${statusClass}"></span>
                        <span class="cronjob-card-name">${escapeHtml(job.displayName || job.name)}</span>
                    </div>
                    <span class="cronjob-card-status ${statusClass}">${statusLabel}</span>
                </div>
                <div class="cronjob-card-schedule">${escapeHtml(schedule)}</div>
                <div class="cronjob-card-actions">
                    <button class="cronjob-run-btn" data-id="${escapeHtml(job.id || job.name)}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polygon points="5 3 19 12 5 21 5 3"/>
                        </svg>
                        Run
                    </button>
                </div>
            </div>
        `;
    }).join('');

    // Bind click on card to open editor
    listEl.querySelectorAll('.cronjob-card').forEach(card => {
        card.addEventListener('click', (e) => {
            // Don't navigate if clicking the Run button
            if (e.target.closest('.cronjob-run-btn')) return;
            const jobId = card.dataset.id;
            window.open(`/cronjobs/index.html?id=${encodeURIComponent(jobId)}`, '_blank');
        });
    });

    // Bind run buttons
    listEl.querySelectorAll('.cronjob-run-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const jobId = btn.dataset.id;
            await executeCronJob(jobId, btn);
        });
    });
}

async function executeCronJob(jobId, btn) {
    btn.disabled = true;
    const originalHtml = btn.innerHTML;
    btn.innerHTML = '<span class="status-dot"></span> Running...';

    try {
        const res = await authFetch(`/api/v1/cron-job/${encodeURIComponent(jobId)}/execute`, {
            method: 'POST'
        });

        if (!res.ok) {
            throw new Error('Failed to execute cron job');
        }

        ToastManager.show('success', 'Job Triggered', `${jobId} execution started`, { duration: 3000 });
    } catch (e) {
        console.error('Failed to execute cron job:', e);
        ToastManager.show('error', 'Execution Failed', e.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = originalHtml;
    }
}

// Initialize cron job drawer on DOM ready
document.addEventListener('DOMContentLoaded', () => {
    initCronJobDrawer();
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

// ===== Channel Account Linking =====
const BINDING_API = '/api/v1/channel-binding';

async function loadLinkedAccounts() {
    const list = document.getElementById('linked-accounts-list');
    if (!list) return;

    try {
        const res = await authFetch(BINDING_API);
        if (!res.ok) throw new Error();
        const bindings = await res.json();

        if (bindings.length === 0) {
            list.innerHTML = '<p class="setting-hint">No linked accounts. Use /link in Telegram to get a verification code.</p>';
            return;
        }

        list.innerHTML = bindings.map(b => `
            <div class="linked-account-item">
                <div class="linked-account-info">
                    <span class="linked-platform">${b.platform}</span>
                    <span class="linked-user">${b.displayName || b.externalUserId}</span>
                    <span class="linked-date">${new Date(b.createdAt).toLocaleDateString()}</span>
                </div>
                <button class="btn btn-outline btn-sm unlink-btn" data-platform="${b.platform}" data-external-id="${b.externalUserId}">Unlink</button>
            </div>
        `).join('');

        list.querySelectorAll('.unlink-btn').forEach(btn => {
            btn.addEventListener('click', async () => {
                if (!confirm('Unlink this account?')) return;
                const res = await authFetch(`${BINDING_API}/${btn.dataset.platform}/${btn.dataset.externalId}`, { method: 'DELETE' });
                if (res.ok) loadLinkedAccounts();
                else alert('Failed to unlink');
            });
        });
    } catch {
        list.innerHTML = '<p class="setting-hint">Failed to load linked accounts.</p>';
    }
}

document.getElementById('verify-link-btn')?.addEventListener('click', async () => {
    const codeInput = document.getElementById('link-code-input');
    const code = codeInput.value.trim();
    if (!code || code.length !== 6) {
        alert('Please enter a 6-digit code.');
        return;
    }

    try {
        const res = await authFetch(`${BINDING_API}/verify`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code })
        });

        if (res.ok) {
            const data = await res.json();
            codeInput.value = '';
            alert(`Linked ${data.platform} account: ${data.displayName || data.externalUserId}`);
            loadLinkedAccounts();
        } else {
            const err = await res.json().catch(() => ({}));
            alert(err.detail || err.message || 'Invalid or expired code.');
        }
    } catch {
        alert('Failed to verify code.');
    }
});

// Load linked accounts when channels tab is shown
document.querySelectorAll('[data-settings-tab]')?.forEach(tab => {
    tab.addEventListener('click', () => {
        if (tab.dataset.settingsTab === 'channels') {
            loadLinkedAccounts();
        }
    });
});

// ── Token Usage Modal ──

function formatTokenCount(n) {
    if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
    if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
    return n.toLocaleString();
}

function openTokenUsageModal() {
    const modal = document.getElementById('token-usage-modal');
    if (!modal) return;

    // Update stats
    document.getElementById('usage-input-tokens').textContent = formatTokenCount(tokenUsage.inputTokens);
    document.getElementById('usage-output-tokens').textContent = formatTokenCount(tokenUsage.outputTokens);
    document.getElementById('usage-total-tokens').textContent = formatTokenCount(tokenUsage.inputTokens + tokenUsage.outputTokens);
    document.getElementById('usage-call-count').textContent = tokenUsage.callCount;

    // Rough cost estimate (GPT-4o pricing: $2.50/1M input, $10/1M output)
    const cost = (tokenUsage.inputTokens * 2.5 + tokenUsage.outputTokens * 10) / 1_000_000;
    document.getElementById('usage-estimated-cost').textContent = cost < 0.01 && cost > 0 ? '< $0.01' : `$${cost.toFixed(2)}`;

    // Render history
    const historyList = document.getElementById('usage-history-list');
    if (tokenUsage.history.length === 0) {
        historyList.innerHTML = '<div class="usage-empty">No usage data yet. Send a message to start tracking.</div>';
    } else {
        historyList.innerHTML = tokenUsage.history.map((h, i) => `
            <div class="usage-history-item">
                <span class="usage-history-msg" title="${h.message}">#${i + 1} ${h.message || '...'}</span>
                <span class="usage-history-tokens">${formatTokenCount(h.inputTokens)} in / ${formatTokenCount(h.outputTokens)} out</span>
            </div>
        `).reverse().join('');
    }

    modal.classList.add('active');
}

function closeTokenUsageModal() {
    const modal = document.getElementById('token-usage-modal');
    if (modal) modal.classList.remove('active');
}

function resetTokenUsage() {
    tokenUsage.inputTokens = 0;
    tokenUsage.outputTokens = 0;
    tokenUsage.callCount = 0;
    tokenUsage.history = [];
    openTokenUsageModal(); // refresh UI
}

// Wire up modal buttons
document.getElementById('token-usage-close')?.addEventListener('click', closeTokenUsageModal);
document.getElementById('token-usage-done')?.addEventListener('click', closeTokenUsageModal);
document.getElementById('token-usage-reset')?.addEventListener('click', resetTokenUsage);
document.getElementById('token-usage-modal')?.querySelector('.modal-overlay')?.addEventListener('click', closeTokenUsageModal);