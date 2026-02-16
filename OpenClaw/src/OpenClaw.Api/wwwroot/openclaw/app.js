// DOM Elements
let messagesEl, userInputEl, sendBtn, navLinks, pages, themeToggle;

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
    navLinks = document.querySelectorAll('.nav-link');
    pages = document.querySelectorAll('.page');
    themeToggle = document.getElementById('theme-toggle');

    // Initialize theme
    initTheme();

    // Event Listeners
    sendBtn.addEventListener('click', sendMessage);
    themeToggle.addEventListener('click', toggleTheme);

    userInputEl.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    navLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            const hash = e.target.getAttribute('href') || '#chat';
            navigate(hash);
            history.pushState(null, '', hash);
        });
    });

    // Initial navigation
    navigate(location.hash || '#chat');
});

// Theme functions
function initTheme() {
    const savedTheme = localStorage.getItem('theme') || 'dark';
    document.documentElement.setAttribute('data-theme', savedTheme);
    updateHljsTheme(savedTheme);
}

function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
    updateHljsTheme(newTheme);
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

async function sendMessage() {
    const message = userInputEl.value.trim();
    if (!message) return;

    addMessage(message, 'user');
    userInputEl.value = '';
    sendBtn.disabled = true;

    let statusIndicator = null;
    let streamingMessage = null;
    let accumulatedContent = '';

    try {
        const res = await fetch('/api/v1/chat/stream', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message })
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
    }
}

// Navigation
function navigate(hash) {
    navLinks.forEach(link => {
        link.classList.toggle('active', link.getAttribute('href') === hash);
    });
    pages.forEach(page => {
        const pageId = page.id.replace('-page', '');
        page.classList.toggle('active', `#${pageId}` === hash);
    });
}
