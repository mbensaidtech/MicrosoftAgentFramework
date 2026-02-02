import { useState, useRef, useEffect, useCallback } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import type { ChatMessage } from '../types';
import { 
  getUsername, 
  clearSession, 
  extendSession, 
  getConversationId, 
  saveConversationId, 
  clearConversationId,
  generateConversationId 
} from '../utils/session';
import { 
  sendStreamingMessage, 
  saveMessageToConversation, 
  getConversation, 
  clearConversation,
  type ConversationMessage as ApiConversationMessage
} from '../services/api';
import { MessageFormatter } from '../components/MessageFormatter';
import { FeedbackRating } from '../components/FeedbackRating';
import { DemoQuestionsModal } from '../components/DemoQuestionsModal';
import './ChatPageSideBySide.css';

// Message sent to seller (after AI assistant approval)
interface SentMessage {
  id: string;
  content: string;
  timestamp: Date;
  from: 'customer' | 'seller';
}

export function ChatPageSideBySide() {
  const navigate = useNavigate();
  const username = getUsername();
  const sentMessagesEndRef = useRef<HTMLDivElement>(null);
  const draftMessagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const feedbackDismissTimeoutRef = useRef<number | null>(null);
  const columnsContainerRef = useRef<HTMLDivElement>(null);

  // Draft messages (conversation with AI assistant)
  const [draftMessages, setDraftMessages] = useState<ChatMessage[]>([]);
  // Sent messages (actual customer-seller conversation)
  const [sentMessages, setSentMessages] = useState<SentMessage[]>([]);
  // Store the last proposed message for extraction
  const [lastProposedMessage, setLastProposedMessage] = useState<string>('');
  
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingConversation, setIsLoadingConversation] = useState(true);
  const [showFeedback, setShowFeedback] = useState(false);
  const [showDemoModal, setShowDemoModal] = useState(false);
  const [showAssistant, setShowAssistant] = useState(false);
  
  // Resizable columns state
  const [leftColumnWidth, setLeftColumnWidth] = useState(50); // percentage
  const [isResizing, setIsResizing] = useState(false);
  
  // Persistent conversation ID (for customer-seller conversation)
  const [conversationId, setConversationId] = useState<string>('');
  // Temporary context ID (for AI assistant thread)
  const [contextId, setContextId] = useState<string>('');

  // Redirect to login if not authenticated
  useEffect(() => {
    if (!username) {
      navigate('/login');
    }
  }, [username, navigate]);

  // Initialize or load conversation on mount
  useEffect(() => {
    if (!username) return;

    const initConversation = async () => {
      setIsLoadingConversation(true);
      
      let convId = getConversationId();
      const isExistingConversation = !!convId;
      
      if (!convId) {
        convId = generateConversationId(username);
        saveConversationId(convId);
      }
      
      setConversationId(convId);

      if (isExistingConversation) {
        try {
          const response = await getConversation(convId);
          if (response.messages && response.messages.length > 0) {
            const loadedMessages: SentMessage[] = response.messages.map((msg: ApiConversationMessage, index: number) => ({
              id: `sent-${index}-${Date.now()}`,
              content: msg.content,
              timestamp: new Date(msg.timestamp),
              from: msg.from as 'customer' | 'seller',
            }));
            setSentMessages(loadedMessages);
          }
        } catch {
          // ignore load errors
        }
      }

      setContextId(generateContextId(convId));
      setIsLoadingConversation(false);
    };

    initConversation();
  }, [username, navigate]);

  // Scroll to bottom when messages change
  useEffect(() => {
    sentMessagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [sentMessages]);

  useEffect(() => {
    draftMessagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [draftMessages]);

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

  // Handle column resizing
  const handleResizeStart = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    setIsResizing(true);
  }, []);

  useEffect(() => {
    const handleResizeMove = (e: MouseEvent) => {
      if (!isResizing || !columnsContainerRef.current) return;
      
      const container = columnsContainerRef.current;
      const containerRect = container.getBoundingClientRect();
      const newLeftWidth = ((e.clientX - containerRect.left) / containerRect.width) * 100;
      
      // Clamp between 20% and 80%
      const clampedWidth = Math.min(Math.max(newLeftWidth, 20), 80);
      setLeftColumnWidth(clampedWidth);
    };

    const handleResizeEnd = () => {
      setIsResizing(false);
    };

    if (isResizing) {
      document.addEventListener('mousemove', handleResizeMove);
      document.addEventListener('mouseup', handleResizeEnd);
      document.body.style.cursor = 'col-resize';
      document.body.style.userSelect = 'none';
    }

    return () => {
      document.removeEventListener('mousemove', handleResizeMove);
      document.removeEventListener('mouseup', handleResizeEnd);
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };
  }, [isResizing]);

  // Reset column width when assistant is hidden
  useEffect(() => {
    if (!showAssistant) {
      setLeftColumnWidth(50);
    }
  }, [showAssistant]);

  // Generate a new context ID for AI assistant thread
  const generateContextId = useCallback((convId?: string): string => {
    const baseConvId = convId || conversationId;
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `${baseConvId}-ai-${timestamp}-${random}`;
  }, [conversationId]);

  const startOrRestartFeedbackAutoDismiss = useCallback(() => {
    if (!showFeedback) return;
    if (feedbackDismissTimeoutRef.current) {
      window.clearTimeout(feedbackDismissTimeoutRef.current);
    }
    feedbackDismissTimeoutRef.current = window.setTimeout(() => {
      setShowFeedback(false);
      inputRef.current?.focus();
    }, 2 * 60 * 1000);
  }, [showFeedback]);

  // Auto-dismiss feedback after 2 minutes
  useEffect(() => {
    if (showFeedback) {
      startOrRestartFeedbackAutoDismiss();
    } else if (feedbackDismissTimeoutRef.current) {
      window.clearTimeout(feedbackDismissTimeoutRef.current);
      feedbackDismissTimeoutRef.current = null;
    }

    return () => {
      if (feedbackDismissTimeoutRef.current) {
        window.clearTimeout(feedbackDismissTimeoutRef.current);
        feedbackDismissTimeoutRef.current = null;
      }
    };
  }, [showFeedback, startOrRestartFeedbackAutoDismiss]);

  const formatTime = (date: Date): string => {
    return date.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' });
  };

  const handleLogout = () => {
    clearSession();
    clearConversationId();
    navigate('/login');
  };

  const handleClearConversation = async () => {
    if (!conversationId) return;
    try {
      await clearConversation(conversationId);
      setSentMessages([]);
      setDraftMessages([]);
      setLastProposedMessage('');
      setShowFeedback(false);
      setShowAssistant(false);

      const newConvId = generateConversationId(username || 'anonymous');
      saveConversationId(newConvId);
      setConversationId(newConvId);
      setContextId(generateContextId(newConvId));
    } catch {
      // ignore
    }
  };

  const addDraftMessage = (role: 'customer' | 'seller', content: string, options?: { isStreaming?: boolean }) => {
    const msg: ChatMessage = {
      id: `draft-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
      role,
      content,
      timestamp: new Date(),
      isTyping: options?.isStreaming ?? false,
    };
    setDraftMessages((prev) => [...prev, msg]);
    return msg.id;
  };

  const hasProposedMessage = (content: string): boolean => {
    return content.includes('📝') || content.includes('📜');
  };

  const extractProposedMessage = (content: string): string => {
    const replaceSignaturePlaceholders = (text: string): string => {
      if (!username) return text;
      return text
        .replace(/\[Votre\s+nom\]/gi, username)
        .replace(/\[Votre\s+Nom\]/g, username)
        .replace(/\[VOTRE\s+NOM\]/g, username);
    };

    // Look for either 📝 or 📜 emoji (agent might use either)
    const proposedStart = content.indexOf('📝') !== -1 ? content.indexOf('📝') : content.indexOf('📜');
    
    let sellerInfoStart = content.indexOf('💡');
    if (sellerInfoStart === -1) {
      sellerInfoStart = content.indexOf('Le vendeur pourrait');
    }
    
    const approvalPatterns = [
      'Cliquez sur le bouton',
      'Si ce message vous convient',
      'Approuvez-vous ce message',
      'Vous pouvez approuver',
      'Souhaitez-vous apporter',
    ];
    
    let approvalStart = -1;
    for (const pattern of approvalPatterns) {
      const index = content.indexOf(pattern);
      if (index !== -1 && (approvalStart === -1 || index < approvalStart)) {
        approvalStart = index;
      }
    }
    
    if (proposedStart !== -1) {
      let endIndex = content.length;
      if (sellerInfoStart !== -1 && sellerInfoStart > proposedStart) endIndex = sellerInfoStart;
      if (approvalStart !== -1 && approvalStart > proposedStart && approvalStart < endIndex) endIndex = approvalStart;

      const rawProposed = content.substring(proposedStart, endIndex);
      const messageHeaderEnd = rawProposed.indexOf(':');
      if (messageHeaderEnd !== -1) {
        let message = rawProposed.substring(messageHeaderEnd + 1).trim();
        message = message.replace(/\*\*/g, '').trim();
        message = replaceSignaturePlaceholders(message);
        return message;
      }
    }
    return '';
  };

  const updateDraftMessage = (id: string, content: string, options?: { isStreaming?: boolean }) => {
    setDraftMessages((prev) =>
      prev.map((msg) =>
        msg.id === id
          ? { ...msg, content, isTyping: options?.isStreaming ?? false }
          : msg
      )
    );

    if (!options?.isStreaming && hasProposedMessage(content)) {
      const proposed = extractProposedMessage(content);
      if (proposed) setLastProposedMessage(proposed);
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const message = inputValue.trim();
    if (!message || isLoading || !conversationId) return;

    setInputValue('');
    setShowAssistant(true);
    addDraftMessage('customer', message);
    setIsLoading(true);

    try {
      const agentMsgId = addDraftMessage('seller', '', { isStreaming: true });
      let fullContent = '';

      await sendStreamingMessage(
        message,
        contextId,
        conversationId,
        username || 'Client',
        (token) => {
          fullContent += token;
          updateDraftMessage(agentMsgId, fullContent, { isStreaming: true });
        },
        () => {
          updateDraftMessage(agentMsgId, fullContent, { isStreaming: false });
        },
        (error) => {
          updateDraftMessage(agentMsgId, `Erreur: ${error}`, { isStreaming: false });
        }
      );
    } finally {
      setIsLoading(false);
      inputRef.current?.focus();
    }
  };

  const handleApprove = async () => {
    if (isLoading || !lastProposedMessage || !conversationId) return;

    setIsLoading(true);

    try {
      await saveMessageToConversation(conversationId, lastProposedMessage, username || undefined);
      
      const sentMsg: SentMessage = {
        id: `sent-${Date.now()}`,
        content: lastProposedMessage,
        timestamp: new Date(),
        from: 'customer',
      };
      setSentMessages((prev) => [...prev, sentMsg]);
      
      setDraftMessages([]);
      setLastProposedMessage('');
      setShowAssistant(false);
      setShowFeedback(true);
      setContextId(generateContextId());
      
      console.log('[ChatPageSideBySide] Message approved and saved');
    } catch (error) {
      console.error('[ChatPageSideBySide] Error saving message:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleFeedbackSubmit = (ratings: { id: string; label: string; rating: number }[]) => {
    console.log('[ChatPageSideBySide] Feedback submitted:', ratings);
    setTimeout(() => {
      setShowFeedback(false);
      inputRef.current?.focus();
    }, 2000);
  };

  const handleFeedbackSkip = () => {
    setShowFeedback(false);
    inputRef.current?.focus();
  };

  const handleDemoSelect = (message: string) => {
    setInputValue(message);
    setShowDemoModal(false);
    inputRef.current?.focus();
  };

  const lastAgentMessage = draftMessages.filter(m => m.role === 'seller').pop();
  const showApproveButton = lastAgentMessage && 
    !lastAgentMessage.isTyping && 
    hasProposedMessage(lastAgentMessage.content) &&
    !isLoading;

  if (!username) return null;

  return (
    <div className="chat-container">
      {/* Header */}
      <header className="chat-header">
        <div className="header-content">
          <div className="header-left">
            <div className="logo">
              <span className="logo-text">MBS Store</span>
            </div>
            <nav className="view-switch">
              <button
                onClick={() => navigate('/chat')}
                className="view-link"
              >
                Vue classique
              </button>
              <button
                onClick={() => navigate('/chat-modal')}
                className="view-link"
              >
                Vue modal
              </button>
              <button
                onClick={() => navigate('/chat-side-by-side')}
                className="view-link active"
              >
                Vue côte à côte
              </button>
            </nav>
            <h1>Chat avec le vendeur</h1>
          </div>
          <div className="header-right">
            <span className="user-badge">Bonjour, {username}</span>
            <button onClick={handleLogout} className="logout-button">
              Déconnexion
            </button>
          </div>
        </div>
      </header>

      {/* Main Chat Area - Two Columns */}
      <main className="chat-main-side-by-side">
        <div 
          ref={columnsContainerRef}
          className={`columns-container ${showAssistant ? 'show-both' : 'show-single'} ${isResizing ? 'resizing' : ''}`}
        >
          {/* Left Column: Customer-Seller Conversation */}
          <div 
            className="conversation-column"
            style={showAssistant ? { width: `${leftColumnWidth}%` } : undefined}
          >
            <div className="column-header seller-column-header">
              <span className="column-title">Conversation avec le vendeur</span>
            </div>
            <div className="messages-container">
              {sentMessages.map((msg) => (
                <div key={msg.id} className={`message ${msg.from}`}>
                  <div className="message-avatar">
                    {msg.from === 'customer' ? username.charAt(0).toUpperCase() : 'V'}
                  </div>
                  <div className="message-bubble">
                    <div className="message-header">
                      <span className="message-sender">
                        {msg.from === 'customer' ? username : 'Vendeur'}
                      </span>
                      <span className="message-time">{formatTime(msg.timestamp)}</span>
                    </div>
                    <div className="message-content sent-message-content">
                      {msg.content}
                    </div>
                  </div>
                </div>
              ))}

              {isLoadingConversation && (
                <div className="empty-state">
                  <p>Chargement de la conversation...</p>
                </div>
              )}

              {!isLoadingConversation && sentMessages.length === 0 && (
                <div className="empty-state">
                  <p>Aucun message envoyé</p>
                  <p className="hint">
                    Utilisez l'assistant IA pour rédiger votre premier message
                  </p>
                </div>
              )}

              <div ref={sentMessagesEndRef} />
            </div>
          </div>

          {/* Resizer Handle */}
          {showAssistant && (
            <div 
              className="column-resizer"
              onMouseDown={handleResizeStart}
              title="Glisser pour redimensionner"
            >
              <div className="resizer-handle">
                <span className="resizer-dots">⋮</span>
              </div>
              {isResizing && (
                <div className="resize-indicator">
                  {Math.round(leftColumnWidth)}% / {Math.round(100 - leftColumnWidth)}%
                </div>
              )}
            </div>
          )}

          {/* Right Column: AI Assistant - Only shown when drafting */}
          {showAssistant && (
            <div 
              className="assistant-column"
              style={{ width: `${100 - leftColumnWidth}%` }}
            >
              <div className="column-header assistant-column-header">
                <div className="column-header-content">
                  <span className="column-title">Assistant de rédaction</span>
                  <span className="column-subtitle">L'assistant vous aide à rédiger votre demande avant de l'envoyer au vendeur</span>
                </div>
              </div>
            <div className="messages-container">
              {draftMessages.map((msg) => {
                const isLastAgentWithProposal = 
                  msg.role === 'seller' && 
                  msg.id === lastAgentMessage?.id && 
                  showApproveButton;
                
                return (
                  <div key={msg.id} className={`message ${msg.role} draft`}>
                    <div className="message-avatar">
                      {msg.role === 'customer' ? username.charAt(0).toUpperCase() : 'IA'}
                    </div>
                    <div className="message-bubble">
                      <div className="message-header">
                        <span className="message-sender">
                          {msg.role === 'customer' ? username : 'Assistant IA'}
                        </span>
                        <span className="message-time">{formatTime(msg.timestamp)}</span>
                      </div>
                      <div className="message-content">
                        {msg.role === 'seller' && !msg.isTyping ? (
                          <MessageFormatter 
                            content={msg.content} 
                            hideSellerInfo={sentMessages.length > 0}
                            customerName={username || undefined}
                          />
                        ) : (
                          msg.content
                        )}
                        {msg.isTyping && <span className="typing-cursor">|</span>}
                      </div>
                      {isLastAgentWithProposal && (
                        <div className="approve-action">
                          <button 
                            className="approve-button"
                            onClick={handleApprove}
                            disabled={isLoading}
                          >
                            Approuver et envoyer au vendeur
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}

              {draftMessages.length === 0 && (
                <div className="empty-state">
                  <p>Assistant de rédaction</p>
                  <p className="hint">
                    Commencez à écrire votre message ci-dessous.<br/>
                    L'assistant vous aidera à le formuler clairement.
                  </p>
                </div>
              )}

              <div ref={draftMessagesEndRef} />
            </div>
            </div>
          )}
        </div>

        {/* Shared Input Area */}
        <div className="input-area-shared">
          {showFeedback ? (
            <FeedbackRating
              onSubmit={handleFeedbackSubmit}
              onSkip={handleFeedbackSkip}
              onActivity={startOrRestartFeedbackAutoDismiss}
            />
          ) : (
            <>
              <form onSubmit={handleSubmit} className="input-form">
                <input
                  ref={inputRef}
                  type="text"
                  value={inputValue}
                  onChange={(e) => setInputValue(e.target.value)}
                  placeholder="Écrivez votre message..."
                  disabled={isLoading}
                  autoFocus
                />
                <button
                  type="button"
                  className="demo-button"
                  onClick={() => setShowDemoModal(true)}
                  disabled={isLoading}
                  title="Questions de démo"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                    <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z"/>
                  </svg>
                  <span>Démo</span>
                </button>
                <button
                  type="submit"
                  disabled={!inputValue.trim() || isLoading}
                  title="Envoyer"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                    <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                  </svg>
                  <span>{isLoading ? 'Envoi...' : 'Envoyer'}</span>
                </button>
              </form>

              {sentMessages.length > 0 && (
                <div className="action-buttons">
                  <button
                    className="action-button danger"
                    onClick={handleClearConversation}
                    disabled={isLoading}
                    title="Effacer la conversation"
                  >
                    Effacer la conversation
                  </button>
                </div>
              )}
            </>
          )}
        </div>
      </main>

      {/* Footer */}
      <footer className="chat-footer">
        <p>MBS Store - Chat avec assistance IA (Vue côte à côte)</p>
      </footer>

      {/* Demo Questions Modal */}
      <DemoQuestionsModal
        isOpen={showDemoModal}
        onClose={() => setShowDemoModal(false)}
        onSelect={handleDemoSelect}
      />
    </div>
  );
}
