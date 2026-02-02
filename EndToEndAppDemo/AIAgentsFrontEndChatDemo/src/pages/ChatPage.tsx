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
  const [showDemoModal, setShowDemoModal] = useState(false);
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

    // Look for either 📝 or 📜 emoji (agent might use either)
    const proposedStart = content.indexOf('📝') !== -1 ? content.indexOf('📝') : content.indexOf('📜');
    
    if (proposedStart === -1) {
      console.log('[ChatPage] extractProposedMessage: No 📝 or 📜 emoji found');
      // Fallback format (older agent output)
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
    }
    
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
    
    // End at seller info section OR approval text, whichever comes first
    let endIndex = content.length;
    if (sellerInfoStart !== -1 && sellerInfoStart > proposedStart) {
      endIndex = Math.min(endIndex, sellerInfoStart);
    }
    if (approvalStart !== -1 && approvalStart > proposedStart) {
      endIndex = Math.min(endIndex, approvalStart);
    }
    
    const rawProposed = content.substring(proposedStart, endIndex);
    console.log('[ChatPage] extractProposedMessage: rawProposed preview:', rawProposed.substring(0, 200));
    
    // Look for the colon after "Message proposé au vendeur:" or similar patterns
    // The format is: 📝 or 📜 **Message proposé au vendeur:**\n\n[message]
    const colonIndex = rawProposed.indexOf(':');
    if (colonIndex === -1) {
      console.warn('[ChatPage] extractProposedMessage: No colon found after emoji');
      // Try to extract message directly after emoji, skipping any header text
      // Look for double newline which typically separates header from message
      const doubleNewlineIndex = rawProposed.indexOf('\n\n');
      if (doubleNewlineIndex !== -1) {
        let message = rawProposed.substring(doubleNewlineIndex + 2).trim();
        message = message.replace(/\*\*/g, '').trim();
        message = stripOptionalProvideBlock(message);
        message = replaceSignaturePlaceholders(message);
        return message;
      }
      // Last resort: extract everything after the emoji, removing markdown formatting
      let message = rawProposed.substring(1).trim(); // Skip emoji
      message = message.replace(/^\*\*.*?\*\*:?\s*/g, ''); // Remove **header**: pattern
      message = message.replace(/\*\*/g, '').trim();
      message = stripOptionalProvideBlock(message);
      message = replaceSignaturePlaceholders(message);
      return message;
    }
    
    // Extract message after the colon
    let message = rawProposed.substring(colonIndex + 1).trim();
    
    // Remove markdown formatting (**)
    message = message.replace(/\*\*/g, '').trim();
    
    // Remove any remaining header text that might be on the same line
      // Sometimes the format is: 📝 or 📜 **Message proposé au vendeur:** [message]
    // We want to skip any text before the actual message starts
    const lines = message.split('\n');
    if (lines.length > 0 && lines[0].trim().length > 0) {
      // Check if first line looks like a continuation of the header
      const firstLine = lines[0].trim();
      if (firstLine.toLowerCase().includes('message proposé') || 
          firstLine.toLowerCase().includes('au vendeur')) {
        // Skip this line, start from next line
        message = lines.slice(1).join('\n').trim();
      }
    }
    
    // Clean up the message
    message = stripOptionalProvideBlock(message);
    message = replaceSignaturePlaceholders(message);
    
    console.log('[ChatPage] extractProposedMessage: extracted message length:', message.length);
    return message;
  };

  // Check if AI response contains a proposed message
  const hasProposedMessage = (content: string): boolean => {
    // Check for emoji (most reliable indicator) - support both 📝 and 📜
    if (content.includes('📝') || content.includes('📜')) return true;
    // Check for text patterns
    if (content.includes('Message proposé au vendeur')) return true;
    if (content.includes('Voici un message que vous pourriez envoyer au vendeur')) return true;
    // Check for "Cliquez sur le bouton Approuver" which indicates a message proposal
    if (content.includes('Cliquez sur le bouton Approuver') || content.includes('Approuver pour envoyer')) return true;
    return false;
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
    if (!options.isStreaming) {
      if (hasProposedMessage(content)) {
        const proposed = extractProposedMessage(content);
        console.log('[ChatPage] updateDraftMessage - hasProposedMessage:', {
          hasProposed: true,
          extractedLength: proposed?.length || 0,
          extractedPreview: proposed?.substring(0, 50) || 'empty',
          contentPreview: content.substring(0, 200)
        });
        if (proposed && proposed.trim().length > 0) {
          setLastProposedMessage(proposed);
          console.log('[ChatPage] Set lastProposedMessage:', proposed.substring(0, 50) + '...');
        } else {
          console.warn('[ChatPage] hasProposedMessage returned true but extractProposedMessage returned empty', {
            contentHasEmoji: content.includes('📝') || content.includes('📜'),
            contentLength: content.length
          });
          // Try to extract again with more lenient logic
          // Sometimes the message might be formatted differently
          const retryExtracted = extractProposedMessage(content);
          if (retryExtracted && retryExtracted.trim().length > 0) {
            console.log('[ChatPage] Retry extraction succeeded');
            setLastProposedMessage(retryExtracted);
          }
        }
      } else {
        // Clear lastProposedMessage if this message doesn't have a proposal
        // (but only if this is the last agent message)
        const isLastAgentMessage = draftMessages.filter(m => m.role === 'seller').pop()?.id === id;
        if (isLastAgentMessage) {
          console.log('[ChatPage] Last agent message does not contain proposed message, clearing lastProposedMessage');
          setLastProposedMessage('');
        }
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
    console.log('[ChatPage] handleApprove called', {
      isLoading,
      lastProposedMessage: lastProposedMessage ? 'exists' : 'missing',
      conversationId: conversationId || 'missing',
      lastProposedMessageLength: lastProposedMessage?.length || 0,
      draftMessagesCount: draftMessages.length,
      lastAgentMessageId: draftMessages.filter(m => m.role === 'seller').pop()?.id
    });

    if (isLoading) {
      console.log('[ChatPage] Approve blocked: isLoading is true');
      return;
    }
    
    if (!conversationId) {
      console.error('[ChatPage] Approve blocked: conversationId is missing');
      return;
    }
    
    // Determine the message to send (use lastProposedMessage or extract from last agent message)
    let messageToSend = lastProposedMessage;
    
    if (!messageToSend || messageToSend.trim().length === 0) {
      console.log('[ChatPage] lastProposedMessage is empty, trying to extract from last agent message');
      // Try to extract from last agent message as fallback
      const lastAgentMsg = draftMessages.filter(m => m.role === 'seller').pop();
      if (lastAgentMsg) {
        console.log('[ChatPage] Found last agent message:', {
          id: lastAgentMsg.id,
          contentLength: lastAgentMsg.content.length,
          contentPreview: lastAgentMsg.content.substring(0, 200),
          hasProposed: hasProposedMessage(lastAgentMsg.content)
        });
        
        if (hasProposedMessage(lastAgentMsg.content)) {
          const extracted = extractProposedMessage(lastAgentMsg.content);
          console.log('[ChatPage] Extraction result:', {
            extractedLength: extracted?.length || 0,
            extractedPreview: extracted?.substring(0, 100) || 'empty'
          });
          
          if (extracted && extracted.trim().length > 0) {
            console.log('[ChatPage] Extracted message from last agent message as fallback');
            messageToSend = extracted;
            // Also update state for future reference
            setLastProposedMessage(extracted);
          } else {
            console.error('[ChatPage] Could not extract proposed message from content', {
              contentHasEmoji: lastAgentMsg.content.includes('📝') || lastAgentMsg.content.includes('📜'),
              contentHasMarker: lastAgentMsg.content.includes('Message proposé'),
              contentLength: lastAgentMsg.content.length
            });
            addDraftMessage('seller', 'Erreur: Impossible d\'extraire le message proposé. Veuillez réessayer.');
            return;
          }
        } else {
          console.error('[ChatPage] Last agent message does not contain a proposed message', {
            contentPreview: lastAgentMsg.content.substring(0, 200)
          });
          addDraftMessage('seller', 'Erreur: Aucun message proposé trouvé. Veuillez réessayer.');
          return;
        }
      } else {
        console.error('[ChatPage] No last agent message found');
        addDraftMessage('seller', 'Erreur: Aucun message proposé trouvé. Veuillez réessayer.');
        return;
      }
    }
    
    if (!messageToSend || messageToSend.trim().length === 0) {
      console.error('[ChatPage] Message to send is empty after extraction');
      addDraftMessage('seller', 'Erreur: Le message est vide. Veuillez réessayer.');
      return;
    }
    
    setIsLoading(true);

    try {
      console.log('[ChatPage] Saving message to conversation:', {
        conversationId,
        messageLength: messageToSend.length,
        messagePreview: messageToSend.substring(0, 50) + '...'
      });
      
      // Save to backend
      await saveMessageToConversation(conversationId, messageToSend, username || undefined);
      
      // Add to sent messages
      const sentMsg: SentMessage = {
        id: `sent-${Date.now()}`,
        content: messageToSend,
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

  // Handle demo question selection
  const handleDemoSelect = (message: string) => {
    setInputValue(message);
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
  const hasProposed = lastAgentMessage ? hasProposedMessage(lastAgentMessage.content) : false;
  const showApproveButton = lastAgentMessage && 
    !lastAgentMessage.isTyping && 
    hasProposed &&
    !isLoading;
  
  // Debug logging for approve button visibility
  useEffect(() => {
    if (lastAgentMessage) {
      console.log('[ChatPage] Approve button visibility check:', {
        hasLastAgentMessage: !!lastAgentMessage,
        isTyping: lastAgentMessage.isTyping,
        hasProposedMessage: hasProposed,
        isLoading,
        showApproveButton,
        lastProposedMessage: lastProposedMessage ? 'set' : 'not set',
        conversationId: conversationId || 'missing'
      });
    }
  }, [lastAgentMessage, hasProposed, isLoading, showApproveButton, lastProposedMessage, conversationId]);

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

      {/* Demo Questions Modal */}
      <DemoQuestionsModal
        isOpen={showDemoModal}
        onClose={() => setShowDemoModal(false)}
        onSelect={handleDemoSelect}
      />
    </div>
  );
}
