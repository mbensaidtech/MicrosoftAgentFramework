const SESSION_KEY = 'antalia_user_session';
const CONTEXT_ID_KEY = 'antalia_context_id';
const CONVERSATION_ID_KEY = 'antalia_conversation_id';
const SESSION_DURATION_MS = 15 * 60 * 1000; // 15 minutes

interface UserSession {
  username: string;
  expiresAt: number;
}

export function saveSession(username: string): void {
  const session: UserSession = {
    username,
    expiresAt: Date.now() + SESSION_DURATION_MS,
  };
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function getSession(): UserSession | null {
  const stored = localStorage.getItem(SESSION_KEY);
  if (!stored) return null;

  try {
    const session: UserSession = JSON.parse(stored);
    
    // Check if session has expired
    if (Date.now() > session.expiresAt) {
      clearSession();
      return null;
    }
    
    return session;
  } catch {
    clearSession();
    return null;
  }
}

export function getUsername(): string | null {
  const session = getSession();
  return session?.username ?? null;
}

export function isLoggedIn(): boolean {
  return getSession() !== null;
}

export function clearSession(): void {
  localStorage.removeItem(SESSION_KEY);
}

export function extendSession(): void {
  const session = getSession();
  if (session) {
    saveSession(session.username);
  }
}

export function getSessionTimeRemaining(): number {
  const session = getSession();
  if (!session) return 0;
  return Math.max(0, session.expiresAt - Date.now());
}

// Context ID Management (for AI assistant thread - temporary)
export function saveContextId(contextId: string): void {
  localStorage.setItem(CONTEXT_ID_KEY, contextId);
}

export function getContextId(): string | null {
  return localStorage.getItem(CONTEXT_ID_KEY);
}

export function clearContextId(): void {
  localStorage.removeItem(CONTEXT_ID_KEY);
}

// Conversation ID Management (for customer-seller conversation - persistent)
export function saveConversationId(conversationId: string): void {
  localStorage.setItem(CONVERSATION_ID_KEY, conversationId);
}

export function getConversationId(): string | null {
  return localStorage.getItem(CONVERSATION_ID_KEY);
}

export function clearConversationId(): void {
  localStorage.removeItem(CONVERSATION_ID_KEY);
}

// Generate a new conversation ID based on username
export function generateConversationId(username: string): string {
  const sanitizedUsername = username.toLowerCase().replace(/[^a-z0-9]/g, '');
  const timestamp = Date.now().toString(36);
  const random = Math.random().toString(36).substring(2, 8);
  return `conv-${sanitizedUsername}-${timestamp}-${random}`;
}
