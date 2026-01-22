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
import './ChatPage.css';

// Message sent to seller (after AI assistant approval)
interface SentMessage {
  id: string;
  content: string;
  timestamp: Date;
  from: 'customer' | 'seller';
}

export function ChatPage() {
  const navigate = useNavigate();
  const username = getUsername();
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  // In some TS setups (DOM+Node typings), setTimeout return types differ (number vs Timeout).
  // We store it as number (browser) to avoid build errors.
  const feedbackDismissTimeoutRef = useRef<number | null>(null);

  // Draft messages (conversation with AI assistant - temporary)
  const [draftMessages, setDraftMessages] = useState<ChatMessage[]>([]);
  // Sent messages (actual customer-seller conversation - persistent)
  const [sentMessages, setSentMessages] = useState<SentMessage[]>([]);
  // Store the last proposed message for extraction
  const [lastProposedMessage, setLastProposedMessage] = useState<string>('');
  
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingConversation, setIsLoadingConversation] = useState(true);
  const [showFeedback, setShowFeedback] = useState(false);
  // Persistent conversation ID (for customer-seller conversation)
  const [conversationId, setConversationId] = useState<string>('');
  // Temporary context ID (for AI assistant thread)
  const [contextId, setContextId] = useState<string>('');

  // Generate a new context ID for AI assistant thread
  // Uses conversationId as prefix so we can delete all related threads together
  const generateContextId = useCallback((convId?: string): string => {
    const baseConvId = convId || conversationId;
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `${baseConvId}-ai-${timestamp}-${random}`;
  }, [conversationId]);

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
      
      // Get or create conversation ID
      let convId = getConversationId();
      const isExistingConversation = !!convId;
      
      if (!convId) {
        convId = generateConversationId(username);
        saveConversationId(convId);
        console.log(`[ChatPage] Created new conversation: ${convId}`);
      } else {
        console.log(`[ChatPage] Found existing conversation: ${convId}`);
      }
      
      setConversationId(convId);

      // Load existing conversation from backend
      if (isExistingConversation) {
        try {
          console.log(`[ChatPage] Fetching conversation from backend: ${convId}`);
          const response = await getConversation(convId);
          console.log(`[ChatPage] Backend response:`, response);
          
          if (response.messages && response.messages.length > 0) {
            const loadedMessages: SentMessage[] = response.messages.map((msg: ApiConversationMessage, index: number) => ({
              id: `sent-${index}-${Date.now()}`,
              content: msg.content,
              timestamp: new Date(msg.timestamp),
              from: msg.from as 'customer' | 'seller',
            }));
            setSentMessages(loadedMessages);
            console.log(`[ChatPage] Loaded ${loadedMessages.length} messages from conversation ${convId}`);
          } else {
            console.log(`[ChatPage] No messages found in conversation ${convId}`);
          }
        } catch (error) {
          console.error('[ChatPage] Error loading conversation:', error);
        }
      }

      // Generate initial context ID for AI assistant (using the convId we just set)
      setContextId(generateContextId(convId));
      setIsLoadingConversation(false);
    };

    initConversation();
  }, [username, generateContextId]);

  // Scroll to bottom when messages change
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [draftMessages, sentMessages]);

  const startOrRestartFeedbackAutoDismiss = useCallback(() => {
    if (!showFeedback) return;
    if (feedbackDismissTimeoutRef.current) {
      window.clearTimeout(feedbackDismissTimeoutRef.current);
    }
    feedbackDismissTimeoutRef.current = window.setTimeout(() => {
      setShowFeedback(false);
      inputRef.current?.focus();
    }, 2 * 60 * 1000); // 2 minutes
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

  // (generateContextId moved near state declarations)

  // Extract proposed message from AI response (only the message, not seller info)
  const extractProposedMessage = (content: string): string => {
    const replaceSignaturePlaceholders = (text: string): string => {
      if (!username) return text;
      return text
        .replace(/\[Votre\s+nom\]/gi, username)
        .replace(/\[Votre\s+Nom\]/g, username)
        .replace(/\[VOTRE\s+NOM\]/g, username);
    };

    const stripOptionalProvideBlock = (text: string): string => {
      const lower = text.toLowerCase();
      const idx = lower.indexOf('je peux fournir');
      if (idx === -1) return text;

      const before = text.substring(0, idx).trim();
      const after = text.substring(idx);
      // keep "Merci..." if present after the provide block
      const merciMatch = after.match(/(^|\n)\s*Merci[^\n]*.*$/im);
      const merciLine = merciMatch ? merciMatch[0].trim() : '';
      const combined = `${before}\n\n${merciLine}`.trim();
      return combined;
    };

    const proposedStart = content.indexOf('📝');
    
    // Find where the seller info section starts (💡 Le vendeur pourrait...)
    let sellerInfoStart = content.indexOf('💡');
    if (sellerInfoStart === -1) {
      sellerInfoStart = content.indexOf('Le vendeur pourrait');
    }
    
    // Find where the approval text starts
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
      // End at seller info section OR approval text, whichever comes first
      let endIndex = content.length;
      if (sellerInfoStart !== -1 && sellerInfoStart > proposedStart) {
        endIndex = sellerInfoStart;
      }
      if (approvalStart !== -1 && approvalStart > proposedStart && approvalStart < endIndex) {
        endIndex = approvalStart;
      }
      
      const rawProposed = content.substring(proposedStart, endIndex);
      const messageHeaderEnd = rawProposed.indexOf(':');
      if (messageHeaderEnd !== -1) {
        let message = rawProposed.substring(messageHeaderEnd + 1).trim();
        message = message.replace(/\*\*/g, '').trim();
        message = stripOptionalProvideBlock(message);
        message = replaceSignaturePlaceholders(message);
        return message;
      }
    }

    // Fallback format (older agent output)
    // "Voici un message que vous pourriez envoyer au vendeur :\n\n--\n<message>\n--\nSouhaitez-vous..."
    const marker = 'Voici un message que vous pourriez envoyer au vendeur';
    const markerIndex = content.toLowerCase().indexOf(marker.toLowerCase());
    if (markerIndex !== -1) {
      const lines = content.split(/\r?\n/);
      const delimiterIndexes: number[] = [];
      for (let i = 0; i < lines.length; i++) {
        if (lines[i].trim() === '--') delimiterIndexes.push(i);
      }

      let messageBlock = '';
      if (delimiterIndexes.length >= 2) {
        messageBlock = lines.slice(delimiterIndexes[0] + 1, delimiterIndexes[1]).join('\n').trim();
      } else {
        // try after marker
        messageBlock = content.substring(markerIndex).trim();
      }

      messageBlock = stripOptionalProvideBlock(messageBlock);
      messageBlock = replaceSignaturePlaceholders(messageBlock);
      return messageBlock.trim();
    }

    return '';
  };

  // Check if AI response contains a proposed message
  const hasProposedMessage = (content: string): boolean => {
    return (
      content.includes('Message proposé au vendeur') ||
      content.includes('Voici un message que vous pourriez envoyer au vendeur')
    );
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
      
      // Generate new conversation ID
      const newConvId = generateConversationId(username || 'anonymous');
      saveConversationId(newConvId);
      setConversationId(newConvId);
      
      // Generate new context ID
      setContextId(generateContextId());
      
      console.log('[ChatPage] Conversation cleared');
    } catch (error) {
      console.error('[ChatPage] Error clearing conversation:', error);
    }
  };

  const addDraftMessage = (
    role: 'customer' | 'seller',
    content: string,
    options: { isStreaming?: boolean } = {}
  ): string => {
    const id = `msg-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    setDraftMessages((prev) => [
      ...prev,
      {
        id,
        role,
        content,
        timestamp: new Date(),
        isTyping: options.isStreaming,
      },
    ]);
    return id;
  };

  const updateDraftMessage = (
    id: string,
    content: string,
    options: { isStreaming?: boolean } = {}
  ) => {
    setDraftMessages((prev) =>
      prev.map((msg) =>
        msg.id === id
          ? {
              ...msg,
              content,
              isTyping: options.isStreaming,
            }
          : msg
      )
    );

    // Check if this message contains a proposed message
    if (!options.isStreaming && hasProposedMessage(content)) {
      const proposed = extractProposedMessage(content);
      if (proposed) {
        setLastProposedMessage(proposed);
      }
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const message = inputValue.trim();
    if (!message || isLoading || !conversationId) return;

    setInputValue('');
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
    } catch (error) {
      addDraftMessage('seller', `Erreur: ${error instanceof Error ? error.message : 'Erreur inconnue'}`);
    } finally {
      setIsLoading(false);
      inputRef.current?.focus();
    }
  };

  // Handle approval button click - directly save and show feedback
  const handleApprove = async () => {
    if (isLoading || !lastProposedMessage || !conversationId) return;
    
    setIsLoading(true);

    try {
      // Save to backend
      await saveMessageToConversation(conversationId, lastProposedMessage, username || undefined);
      
      // Add to sent messages
      const sentMsg: SentMessage = {
        id: `sent-${Date.now()}`,
        content: lastProposedMessage,
        timestamp: new Date(),
        from: 'customer',
      };
      setSentMessages((prev) => [...prev, sentMsg]);
      
      // Clear draft messages
      setDraftMessages([]);
      setLastProposedMessage('');
      
      // Show feedback
      setShowFeedback(true);
      
      // Generate new context ID for next AI assistant conversation
      setContextId(generateContextId());
      
      console.log('[ChatPage] Message approved and saved');
    } catch (error) {
      console.error('[ChatPage] Error saving message:', error);
      addDraftMessage('seller', `Erreur lors de l'envoi: ${error instanceof Error ? error.message : 'Erreur inconnue'}`);
    } finally {
      setIsLoading(false);
    }
  };

  // Handle feedback submission
  const handleFeedbackSubmit = (ratings: { id: string; label: string; rating: number }[]) => {
    console.log('[ChatPage] Feedback submitted:', ratings);
    // TODO: Send feedback to backend
    setTimeout(() => {
      setShowFeedback(false);
      inputRef.current?.focus();
    }, 2000);
  };

  // Handle feedback skip
  const handleFeedbackSkip = () => {
    setShowFeedback(false);
    inputRef.current?.focus();
  };

  const formatTime = (date: Date): string => {
    return date.toLocaleTimeString('fr-FR', {
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  // Check if the last agent message has a proposed message (for showing approve button)
  const lastAgentMessage = draftMessages.filter(m => m.role === 'seller').pop();
  const showApproveButton = lastAgentMessage && 
    !lastAgentMessage.isTyping && 
    hasProposedMessage(lastAgentMessage.content) &&
    !isLoading;

  // Are we currently in a drafting session?
  const isDrafting = draftMessages.length > 0;

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
                className="view-link active"
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
                className="view-link"
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

      {/* Main Chat Area */}
      <main className="chat-main">
        {/* Conversation Info - Dynamic based on drafting mode */}
        <div className={`conversation-header ${isDrafting ? 'drafting-mode' : 'conversation-mode'}`}>
          <div className="participant-info">
            <div className="participant customer">
              <span className="participant-avatar">{username.charAt(0).toUpperCase()}</span>
              <div className="participant-details">
                <span className="participant-name">{username}</span>
                <span className="participant-role">Client</span>
              </div>
            </div>
            <div className="conversation-separator">
              <span className="separator-text">
                {isDrafting ? 'Rédaction assistée' : 'Conversation'}
              </span>
            </div>
            <div className={`participant ${isDrafting ? 'assistant' : 'seller'}`}>
              <span className="participant-avatar">{isDrafting ? 'IA' : 'V'}</span>
              <div className="participant-details">
                <span className="participant-name">{isDrafting ? 'Assistant IA' : 'Vendeur'}</span>
                <span className="participant-role">{isDrafting ? 'Aide à la rédaction' : 'Support'}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Messages Container */}
        <div className="messages-container">
          {/* Show sent messages (final customer-seller conversation) */}
          {sentMessages.map((msg) => (
            <div
              key={msg.id}
              className={`message ${msg.from}`}
            >
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

          {/* Show draft messages (AI assistant conversation - temporary) */}
          {draftMessages.map((msg) => {
            const isLastAgentWithProposal = 
              msg.role === 'seller' && 
              msg.id === lastAgentMessage?.id && 
              showApproveButton;
            
            return (
              <div
                key={msg.id}
                className={`message ${msg.role} draft`}
              >
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
                        customerName={username}
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

          {/* Loading state */}
          {isLoadingConversation && (
            <div className="empty-state">
              <p>Chargement de la conversation...</p>
            </div>
          )}

          {/* Empty state - only when no messages at all */}
          {!isLoadingConversation && sentMessages.length === 0 && draftMessages.length === 0 && (
            <div className="empty-state">
              <p>Bienvenue sur MBS Store</p>
              <p className="hint">
                Écrivez votre message ci-dessous.<br/>
                Notre assistant vous aidera à le formuler clairement avant l'envoi.
              </p>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input Area */}
        <div className="input-area">
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
                  placeholder={isDrafting ? "Répondez à l'assistant..." : "Écrivez votre message au vendeur..."}
                  disabled={isLoading}
                  autoFocus
                />
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

              {(sentMessages.length > 0 || draftMessages.length > 0) && (
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
        <p>MBS Store - Chat avec assistance IA</p>
      </footer>
    </div>
  );
}
