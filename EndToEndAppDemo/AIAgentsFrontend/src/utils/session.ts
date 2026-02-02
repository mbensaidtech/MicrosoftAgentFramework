const SESSION_KEY = 'aiagent_user_session';
const CONTEXT_ID_KEY = 'aiagent_context_id';
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

// Context ID Management
export function saveContextId(contextId: string): void {
  localStorage.setItem(CONTEXT_ID_KEY, contextId);
}

export function getContextId(): string | null {
  return localStorage.getItem(CONTEXT_ID_KEY);
}

export function clearContextId(): void {
  localStorage.removeItem(CONTEXT_ID_KEY);
}
