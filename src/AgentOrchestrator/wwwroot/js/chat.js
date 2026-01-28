// Chat module - Updated for M365 Agents SDK
const Chat = {
    messagesContainer: null,
    input: null,
    sendBtn: null,
    isProcessing: false,
    currentAssistantMessage: null,
    conversationId: null,

    init() {
        this.messagesContainer = document.getElementById('chatMessages');
        this.input = document.getElementById('chatInput');
        this.sendBtn = document.getElementById('sendBtn');

        // Generate a unique conversation ID for this session
        this.conversationId = 'conv-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);

        this.sendBtn.addEventListener('click', () => this.sendMessage());
        this.input.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                this.sendMessage();
            }
        });
    },

    async sendMessage() {
        const message = this.input.value.trim();
        if (!message || this.isProcessing || !Auth.isAuthenticated) return;

        // Clear welcome message on first send
        const welcomeMsg = this.messagesContainer.querySelector('.welcome-message');
        if (welcomeMsg) {
            welcomeMsg.remove();
        }

        // Add user message
        this.addMessage(message, 'user');
        this.input.value = '';

        // Start processing
        this.isProcessing = true;
        this.sendBtn.disabled = true;
        this.input.disabled = true;

        // Clear trace and start new one
        Trace.startNewTrace();
        Trace.addStep({
            stepId: 1,
            agent: 'user',
            action: 'message_sent',
            status: 'completed',
            message: message
        });

        // Create assistant message placeholder
        this.currentAssistantMessage = this.addMessage('Thinking...', 'assistant', true);

        try {
            const response = await this.sendActivity(message);
            this.updateAssistantMessage(response);

            Trace.addStep({
                stepId: 2,
                agent: 'orchestrator',
                action: 'response_received',
                status: 'completed'
            });
        } catch (error) {
            console.error('Error:', error);
            this.updateAssistantMessage('Error: ' + error.message);

            Trace.addStep({
                stepId: 2,
                agent: 'orchestrator',
                action: 'error',
                status: 'failed',
                error: error.message
            });
        } finally {
            this.isProcessing = false;
            this.sendBtn.disabled = false;
            this.input.disabled = false;
            this.input.focus();

            // Remove streaming indicator
            if (this.currentAssistantMessage) {
                this.currentAssistantMessage.classList.remove('message-streaming');
            }
        }
    },

    async sendActivity(message) {
        Trace.addStep({
            stepId: 1,
            agent: 'web_client',
            action: 'sending_message',
            status: 'started'
        });

        // Use the simple chat endpoint for web UI
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ message: message }),
            // SECURITY: Required for session cookies to be sent with the request
            // Without this, the server won't receive the authentication session
            credentials: 'include'
        });

        if (!response.ok) {
            let errorMessage = 'Request failed';
            try {
                const errorBody = await response.text();
                errorMessage = errorBody || `HTTP ${response.status}`;
            } catch (e) {
                errorMessage = `HTTP ${response.status}`;
            }
            throw new Error(errorMessage);
        }

        const result = await response.json();
        return result.text || 'No response received.';
    },

    addMessage(content, role, isStreaming = false) {
        const messageEl = document.createElement('div');
        messageEl.className = `message message-${role}${isStreaming ? ' message-streaming' : ''}`;

        const contentEl = document.createElement('div');
        contentEl.className = 'message-content';
        contentEl.innerHTML = this.formatContent(content);

        messageEl.appendChild(contentEl);
        this.messagesContainer.appendChild(messageEl);

        // Scroll to bottom
        this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;

        return messageEl;
    },

    updateAssistantMessage(content) {
        if (this.currentAssistantMessage) {
            const contentEl = this.currentAssistantMessage.querySelector('.message-content');
            if (contentEl) {
                contentEl.innerHTML = this.formatContent(content);
            }
            // Scroll to bottom
            this.messagesContainer.scrollTop = this.messagesContainer.scrollHeight;
        }
    },

    formatContent(content) {
        if (!content) return '';

        // Basic markdown-like formatting
        let formatted = content
            // Escape HTML
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            // Bold
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            // Italic
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            // Code blocks
            .replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>')
            // Inline code
            .replace(/`(.*?)`/g, '<code>$1</code>')
            // Line breaks
            .replace(/\n/g, '<br>');

        return `<p>${formatted}</p>`;
    }
};
