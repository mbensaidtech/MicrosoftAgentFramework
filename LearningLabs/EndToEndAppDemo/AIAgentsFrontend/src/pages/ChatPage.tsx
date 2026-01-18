import { useState, useRef, useEffect } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { getUsername, clearSession, extendSession } from '../utils/session';
import {
  sendRestMessage,
  sendRestStreamingMessage,
  sendA2AMessage,
  sendA2AStreamingMessage,
} from '../services/api';
import type {
  ChatMessage,
  ChatSettings,
  Protocol,
  StreamingMode,
  DebugInfo,
} from '../types';
import { AGENTS_CONFIG, getAgentById } from '../config/agents.config';
import './ChatPage.css';

export function ChatPage() {
  const navigate = useNavigate();
  const username = getUsername();
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [contextId, setContextId] = useState<string | undefined>();
  
  const [settings, setSettings] = useState<ChatSettings>({
    protocol: 'rest',
    streaming: 'http',
    agentId: AGENTS_CONFIG[0]?.id || 'history',
  });

  const [showQuestionsPopup, setShowQuestionsPopup] = useState(false);
  const [showDebugPanel, setShowDebugPanel] = useState(false);
  const [debugHistory, setDebugHistory] = useState<DebugInfo[]>([]);
  const [expandedDebugEntries, setExpandedDebugEntries] = useState<Set<number>>(new Set());

  // Redirect to login if not authenticated
  useEffect(() => {
    if (!username) {
      navigate('/login');
    }
  }, [username, navigate]);

  // Scroll to bottom when messages change
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Extend session on activity
  useEffect(() => {
    const handleActivity = () => extendSession();
    window.addEventListener('click', handleActivity);
    window.addEventListener('keypress', handleActivity);
    return () => {
      window.removeEventListener('click', handleActivity);
      window.removeEventListener('keypress', handleActivity);
    };
  }, []);

  const handleLogout = () => {
    clearSession();
    navigate('/login');
  };

  const clearChat = () => {
    setMessages([]);
    setContextId(undefined);
    setDebugHistory([]);
    setExpandedDebugEntries(new Set());
  };

  const handleSelectQuestion = (question: string) => {
    setInputValue(question);
    setShowQuestionsPopup(false);
    inputRef.current?.focus();
  };

  const addDebugInfo = (debug: DebugInfo) => {
    setDebugHistory((prev) => [debug, ...prev].slice(0, 20)); // Keep last 20
    // Auto-expand the newest entry (index 0)
    setExpandedDebugEntries((prev) => new Set([0, ...Array.from(prev).map(i => i + 1)]));
  };

  const clearDebugHistory = () => {
    setDebugHistory([]);
    setExpandedDebugEntries(new Set());
  };

  const toggleDebugEntry = (index: number) => {
    setExpandedDebugEntries((prev) => {
      const newSet = new Set(prev);
      if (newSet.has(index)) {
        newSet.delete(index);
      } else {
        newSet.add(index);
      }
      return newSet;
    });
  };

  const collapseAllDebugEntries = () => {
    setExpandedDebugEntries(new Set());
  };

  const addMessage = (
    role: 'user' | 'agent',
    content: string,
    options: { isStreaming?: boolean; isLoading?: boolean } = {}
  ): string => {
    const id = `msg-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    setMessages((prev) => [
      ...prev,
      {
        id,
        role,
        content,
        timestamp: new Date(),
        isStreaming: options.isStreaming ?? false,
        isLoading: options.isLoading ?? false,
      },
    ]);
    return id;
  };

  const updateMessage = (
    id: string,
    content: string,
    options: { isStreaming?: boolean; isLoading?: boolean } = {}
  ) => {
    setMessages((prev) =>
      prev.map((msg) =>
        msg.id === id
          ? {
              ...msg,
              content,
              isStreaming: options.isStreaming ?? false,
              isLoading: options.isLoading ?? false,
            }
          : msg
      )
    );
  };


  // Generate a new contextId with username prefix
  const generateContextId = (): string => {
    const sanitizedUsername = (username || 'anonymous')
      .toLowerCase()
      .replace(/[^a-z0-9]/g, '');
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `${sanitizedUsername}-${timestamp}-${random}`;
  };

  // Get current contextId or generate a new one
  const getOrCreateContextId = (): string => {
    if (contextId) return contextId;
    const newContextId = generateContextId();
    setContextId(newContextId);
    return newContextId;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const message = inputValue.trim();
    if (!message || isLoading) return;

    setInputValue('');
    addMessage('user', message);
    setIsLoading(true);

    // Get or create contextId with username
    const currentContextId = getOrCreateContextId();

    try {
      const { protocol, streaming, agentId } = settings;

      if (protocol === 'rest') {
        if (streaming === 'sse') {
          // REST API with SSE streaming
          const agentMsgId = addMessage('agent', '', { isStreaming: true });
          let fullContent = '';
          
          await sendRestStreamingMessage(
            agentId,
            message,
            currentContextId,
            (token) => {
              fullContent += token;
              updateMessage(agentMsgId, fullContent, { isStreaming: true });
            },
            () => {
              updateMessage(agentMsgId, fullContent);
            },
            (error) => {
              updateMessage(agentMsgId, `Error: ${error}`);
            },
            addDebugInfo
          );
        } else {
          // REST API standard HTTP - show skeleton while waiting
          const loadingMsgId = addMessage('agent', '', { isLoading: true });
          const { data, debug } = await sendRestMessage(agentId, message, currentContextId);
          addDebugInfo(debug);
          updateMessage(loadingMsgId, data.message);
        }
      } else {
        // A2A protocol
        if (streaming === 'sse') {
          // A2A with SSE
          const agentMsgId = addMessage('agent', '', { isStreaming: true });
          let fullContent = '';
          
          await sendA2AStreamingMessage(
            agentId,
            message,
            currentContextId,
            (token) => {
              fullContent += token;
              updateMessage(agentMsgId, fullContent, { isStreaming: true });
            },
            () => {
              updateMessage(agentMsgId, fullContent);
            },
            (error) => {
              updateMessage(agentMsgId, `Error: ${error}`);
            },
            addDebugInfo
          );
        } else {
          // A2A standard HTTP (JSON-RPC) - show skeleton while waiting
          const loadingMsgId = addMessage('agent', '', { isLoading: true });
          const { data, debug } = await sendA2AMessage(agentId, message, currentContextId);
          addDebugInfo(debug);
          updateMessage(loadingMsgId, data.text);
        }
      }
    } catch (error) {
      addMessage('agent', `Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setIsLoading(false);
      inputRef.current?.focus();
    }
  };

  const selectedAgent = getAgentById(settings.agentId);

  if (!username) return null;

  return (
    <div className="chat-container">
      {/* Header */}
      <header className="chat-header">
        <div className="header-left">
          <h1>AI Agents Chat</h1>
          <span className="user-badge">Hi, {username}!</span>
        </div>
        <button onClick={handleLogout} className="logout-button">
          Logout
        </button>
      </header>

      <div className="chat-main">
        {/* Control Panel */}
        <aside className="control-panel">
          <h2>Settings</h2>

          <div className="control-group">
            <label>Protocol</label>
            <div className="radio-group">
              <label className={`radio-option ${settings.protocol === 'rest' ? 'selected' : ''}`}>
                <input
                  type="radio"
                  name="protocol"
                  value="rest"
                  checked={settings.protocol === 'rest'}
                  onChange={(e) =>
                    setSettings((s) => ({ ...s, protocol: e.target.value as Protocol }))
                  }
                />
                <span>REST API</span>
              </label>
              <label className={`radio-option ${settings.protocol === 'a2a' ? 'selected' : ''}`}>
                <input
                  type="radio"
                  name="protocol"
                  value="a2a"
                  checked={settings.protocol === 'a2a'}
                  onChange={(e) =>
                    setSettings((s) => ({ ...s, protocol: e.target.value as Protocol }))
                  }
                />
                <span>A2A Protocol</span>
              </label>
            </div>
          </div>

          <div className="control-group">
            <label>Streaming</label>
            <div className="radio-group">
              <label className={`radio-option ${settings.streaming === 'http' ? 'selected' : ''}`}>
                <input
                  type="radio"
                  name="streaming"
                  value="http"
                  checked={settings.streaming === 'http'}
                  onChange={(e) =>
                    setSettings((s) => ({ ...s, streaming: e.target.value as StreamingMode }))
                  }
                />
                <span>Non-Streaming</span>
              </label>
              <label className={`radio-option ${settings.streaming === 'sse' ? 'selected' : ''}`}>
                <input
                  type="radio"
                  name="streaming"
                  value="sse"
                  checked={settings.streaming === 'sse'}
                  onChange={(e) =>
                    setSettings((s) => ({ ...s, streaming: e.target.value as StreamingMode }))
                  }
                />
                <span>SSE Streaming</span>
              </label>
            </div>
          </div>

          <div className="control-group">
            <label>Agent</label>
            <div className="agent-list">
              {AGENTS_CONFIG.map((agent) => (
                <label
                  key={agent.id}
                  className={`agent-option ${settings.agentId === agent.id ? 'selected' : ''}`}
                >
                  <input
                    type="radio"
                    name="agent"
                    value={agent.id}
                    checked={settings.agentId === agent.id}
                    onChange={(e) => {
                      const newAgentId = e.target.value;
                      if (newAgentId !== settings.agentId) {
                        setSettings((s) => ({ ...s, agentId: newAgentId }));
                        clearChat(); // Start new conversation when changing agent
                      }
                    }}
                  />
                  <div className="agent-info">
                    <span className="agent-name">{agent.name}</span>
                    <span className="agent-desc">{agent.description}</span>
                    {agent.supportsStreaming && (
                      <span className="agent-badge">Streaming</span>
                    )}
                  </div>
                </label>
              ))}
            </div>
          </div>

          <button
            className="debug-toggle-sidebar"
            onClick={() => setShowDebugPanel(!showDebugPanel)}
            title="Toggle Debug Panel"
          >
            {showDebugPanel ? 'Hide Debug' : 'Show Debug'}
          </button>
        </aside>

        {/* Chat Area */}
        <main className="chat-area">
          <div className="agent-header">
            <h3>{selectedAgent?.name}</h3>
            <div className="header-info">
              <span className="mode-badge">
                {settings.protocol.toUpperCase()} • {settings.streaming === 'sse' ? 'STREAMING' : 'NON-STREAMING'}
              </span>
              <span className="context-badge" title={contextId || 'New conversation'}>
                {contextId ? `ID: ${contextId.substring(0, 12)}...` : 'New conversation'}
              </span>
            </div>
          </div>

          <div className="messages-container">
            {messages.length === 0 ? (
              <div className="empty-state">
                <p>Start a conversation with {selectedAgent?.name}</p>
                <p className="hint">{selectedAgent?.description}</p>
              </div>
            ) : (
              messages.map((msg) => (
                <div key={msg.id} className={`message ${msg.role}`}>
                  <div className="message-header">
                    <span className="message-role">
                      {msg.role === 'user' ? username : selectedAgent?.name}
                    </span>
                    <span className="message-time">
                      {msg.timestamp.toLocaleTimeString()}
                    </span>
                  </div>
                  <div className="message-content">
                    {msg.isLoading ? (
                      <div className="skeleton-container">
                        <div className="skeleton-line skeleton-line-1"></div>
                        <div className="skeleton-line skeleton-line-2"></div>
                        <div className="skeleton-line skeleton-line-3"></div>
                      </div>
                    ) : (
                      <>
                        {msg.content}
                        {msg.isStreaming && <span className="typing-cursor">|</span>}
                      </>
                    )}
                  </div>
                </div>
              ))
            )}
            <div ref={messagesEndRef} />
          </div>

          <form onSubmit={handleSubmit} className="input-form">
            <input
              ref={inputRef}
              type="text"
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              placeholder={`Message ${selectedAgent?.name}...`}
              disabled={isLoading}
              autoFocus
            />
            <button type="submit" disabled={isLoading || !inputValue.trim()} title="Send Message">
              {isLoading ? 'Sending...' : 'Send'}
            </button>
            <button
              type="button"
              className="questions-button"
              onClick={() => setShowQuestionsPopup(true)}
              title="Preset Questions"
            >
              ?
            </button>
            <button
              type="button"
              className="clear-chat-button"
              onClick={clearChat}
              title="Clear Chat"
              disabled={messages.length === 0}
            >
              ⟲
            </button>
          </form>

          {/* Preset Questions Popup */}
          {showQuestionsPopup && (() => {
            const questions = selectedAgent?.presetQuestions || [];
            return (
              <div className="popup-overlay" onClick={() => setShowQuestionsPopup(false)}>
                <div className="popup-content" onClick={(e) => e.stopPropagation()}>
                  <div className="popup-header">
                    <h3>Sample Questions</h3>
                    <button
                      className="popup-close"
                      onClick={() => setShowQuestionsPopup(false)}
                    >
                      ×
                    </button>
                  </div>
                  <div className="popup-body">
                    {questions.length > 0 ? (
                      <div className="question-list-direct">
                        {questions.map((question, index) => (
                          <button
                            key={index}
                            className="question-item"
                            onClick={() => handleSelectQuestion(question)}
                          >
                            {question}
                          </button>
                        ))}
                      </div>
                    ) : (
                      <p className="no-questions">No preset questions available</p>
                    )}
                  </div>
                  <div className="popup-footer">
                    <p>Questions are linked to test conversation continuity</p>
                  </div>
                </div>
              </div>
            );
          })()}
        </main>
      </div>

      {/* Debug Panel */}
      {showDebugPanel && (
        <div className="debug-panel">
          <div className="debug-panel-header">
            <h3>Debug Panel</h3>
            <div className="debug-header-actions">
              <button onClick={collapseAllDebugEntries} className="debug-action-btn" title="Collapse All">
                ▲
              </button>
              <button onClick={clearDebugHistory} className="debug-action-btn" title="Clear All">
                ✕
              </button>
              <button onClick={() => setShowDebugPanel(false)} className="debug-close-btn" title="Close Panel">
                ←
              </button>
            </div>
          </div>
          <div className="debug-panel-content">
            {debugHistory.length === 0 ? (
              <p className="debug-empty">No requests yet. Send a message to see debug info.</p>
            ) : (
              debugHistory.map((debug, index) => (
                <div key={index} className={`debug-entry ${expandedDebugEntries.has(index) ? 'expanded' : 'collapsed'}`}>
                  <button
                    className="debug-entry-header"
                    onClick={() => toggleDebugEntry(index)}
                  >
                    <span className="debug-expand-icon">
                      {expandedDebugEntries.has(index) ? '▼' : '▶'}
                    </span>
                    <span className="debug-method">{debug.method}</span>
                    <span className={`debug-status ${debug.error ? 'error' : 'success'}`}>
                      {debug.responseStatus || 'N/A'}
                    </span>
                    <span className="debug-duration">{debug.duration}ms</span>
                    <span className="debug-time-short">{debug.timestamp.toLocaleTimeString()}</span>
                  </button>
                  {expandedDebugEntries.has(index) && (
                    <div className="debug-entry-body">
                      <div className="debug-url">{debug.url}</div>
                      <div className="debug-section">
                        <div className="debug-section-title">Request Body</div>
                        <pre className="debug-json">{JSON.stringify(debug.requestBody, null, 2)}</pre>
                      </div>
                      {debug.responseBody !== undefined && (
                        <div className="debug-section">
                          <div className="debug-section-title">Response Body</div>
                          <pre className="debug-json">{JSON.stringify(debug.responseBody, null, 2)}</pre>
                        </div>
                      )}
                      {debug.error && (
                        <div className="debug-section">
                          <div className="debug-section-title debug-error-title">Error</div>
                          <pre className="debug-json debug-error-text">{debug.error}</pre>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
