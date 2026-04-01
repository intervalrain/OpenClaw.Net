// Shared Approval UI Component
// Can be used in Chat, Pipeline UI, and other contexts

const ApprovalUI = {
    // Create inline approval buttons for chat messages
    createInlineApproval(executionId, approvalRequest, onApprove, onReject) {
        const container = document.createElement('div');
        container.className = 'approval-inline';
        container.dataset.executionId = executionId;

        const message = document.createElement('div');
        message.className = 'approval-inline-message';
        message.textContent = approvalRequest.message || 'Approval required to continue';

        const details = document.createElement('div');
        details.className = 'approval-inline-details';
        if (approvalRequest.details) {
            const detailsContent = typeof approvalRequest.details === 'string'
                ? approvalRequest.details
                : JSON.stringify(approvalRequest.details, null, 2);
            details.innerHTML = `<pre>${this.escapeHtml(detailsContent)}</pre>`;
        }

        const actions = document.createElement('div');
        actions.className = 'approval-inline-actions';

        const rejectBtn = document.createElement('button');
        rejectBtn.className = 'btn btn-danger btn-sm';
        rejectBtn.textContent = 'Reject';
        rejectBtn.onclick = () => {
            this.disableButtons(container);
            onReject(executionId);
        };

        const approveBtn = document.createElement('button');
        approveBtn.className = 'btn btn-success btn-sm';
        approveBtn.textContent = 'Approve';
        approveBtn.onclick = () => {
            this.disableButtons(container);
            onApprove(executionId);
        };

        actions.appendChild(rejectBtn);
        actions.appendChild(approveBtn);

        container.appendChild(message);
        if (approvalRequest.details) {
            container.appendChild(details);
        }
        container.appendChild(actions);

        return container;
    },

    // Update approval UI state after decision
    markDecision(executionId, approved) {
        const container = document.querySelector(`.approval-inline[data-execution-id="${executionId}"]`);
        if (!container) return;

        const actions = container.querySelector('.approval-inline-actions');
        if (actions) {
            actions.innerHTML = `
                <span class="approval-decision ${approved ? 'approved' : 'rejected'}">
                    ${approved ? '✓ Approved' : '✗ Rejected'}
                </span>
            `;
        }
    },

    // Disable buttons while processing
    disableButtons(container) {
        const buttons = container.querySelectorAll('button');
        buttons.forEach(btn => {
            btn.disabled = true;
            btn.style.opacity = '0.5';
        });
    },

    // Utility to escape HTML
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
};

// CSS for inline approval (can be added to page styles)
const approvalStyles = `
.approval-inline {
    background: var(--bg-tertiary, #f8f9fa);
    border: 1px solid var(--border-color, #e1e5e9);
    border-left: 4px solid var(--primary-color, #3498db);
    border-radius: 8px;
    padding: 16px;
    margin: 12px 0;
}

.approval-inline-message {
    font-weight: 500;
    margin-bottom: 12px;
    color: var(--text-primary, #333);
}

.approval-inline-details {
    background: var(--bg-secondary, #fff);
    border-radius: 4px;
    padding: 12px;
    margin-bottom: 12px;
    max-height: 200px;
    overflow-y: auto;
}

.approval-inline-details pre {
    margin: 0;
    white-space: pre-wrap;
    word-break: break-word;
    font-size: 0.85rem;
    font-family: 'SF Mono', Monaco, 'Cascadia Code', monospace;
}

.approval-inline-actions {
    display: flex;
    gap: 12px;
    justify-content: flex-end;
}

.approval-decision {
    padding: 8px 16px;
    border-radius: 4px;
    font-weight: 500;
}

.approval-decision.approved {
    background: #d4edda;
    color: #155724;
}

.approval-decision.rejected {
    background: #f8d7da;
    color: #721c24;
}

.btn-success {
    background: var(--success-color, #27ae60);
    color: white;
    border: none;
    padding: 8px 16px;
    border-radius: 6px;
    cursor: pointer;
    font-weight: 500;
}

.btn-success:hover {
    background: #219a52;
}

.btn-danger {
    background: var(--error-color, #e74c3c);
    color: white;
    border: none;
    padding: 8px 16px;
    border-radius: 6px;
    cursor: pointer;
    font-weight: 500;
}

.btn-danger:hover {
    background: #c0392b;
}

.btn-sm {
    padding: 6px 12px;
    font-size: 0.9rem;
}
`;

// Inject styles if not already present
if (!document.getElementById('approval-styles')) {
    const styleEl = document.createElement('style');
    styleEl.id = 'approval-styles';
    styleEl.textContent = approvalStyles;
    document.head.appendChild(styleEl);
}
