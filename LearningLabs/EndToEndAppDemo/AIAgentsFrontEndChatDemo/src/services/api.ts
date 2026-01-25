export interface ChatResponse {
  message: string;
  contextId: string;
  agent: string;
}

export interface DebugInfo {
  method: string;
  url: string;
  requestBody: unknown;
  responseStatus?: number;
  responseBody?: unknown;
  duration: number;
  error?: string;
  timestamp: Date;
}

export interface ConversationMessage {
  from: 'customer' | 'seller';
  content: string;
  timestamp: string;
  customerName?: string;
}

export interface ConversationResponse {
  conversationId: string;
  messages: ConversationMessage[];
}

// Updated to use the Orchestrator Agent that coordinates specialized agents
const API_BASE_URL = '/api/agents/orchestrator';

/**
 * Send a message to the Message Formulator Agent with SSE streaming
 * Now supports both contextId (AI assistant thread) and conversationId (customer-seller conversation)
 */
export async function sendStreamingMessage(
  message: string,
  contextId: string,
  conversationId: string,
  customerName: string,
  onToken: (token: string) => void,
  onComplete: () => void,
  onError: (error: string) => void,
  onDebug?: (debug: DebugInfo) => void
): Promise<void> {
  const startTime = Date.now();
  const url = `${API_BASE_URL}/stream`;
  const requestBody = { 
    message, 
    contextId,
    conversationId,
    customerName
  };

  const debug: DebugInfo = {
    method: 'POST (SSE)',
    url,
    requestBody,
    timestamp: new Date(),
    duration: 0,
  };

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'text/event-stream',
      },
      body: JSON.stringify(requestBody),
    });

    debug.responseStatus = response.status;

    if (!response.ok) {
      debug.duration = Date.now() - startTime;
      debug.error = `HTTP ${response.status}`;
      onDebug?.(debug);
      onError(`HTTP error: ${response.status}`);
      return;
    }

    const reader = response.body?.getReader();
    if (!reader) {
      debug.duration = Date.now() - startTime;
      debug.error = 'No response body';
      onDebug?.(debug);
      onError('No response body');
      return;
    }

    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop() || '';

      for (const line of lines) {
        if (line.startsWith('data: ')) {
          try {
            const data = JSON.parse(line.slice(6));
            if (data.text) {
              onToken(data.text);
            }
          } catch {
            // Ignore parse errors for non-JSON data lines
          }
        }
      }
    }

    debug.duration = Date.now() - startTime;
    onDebug?.(debug);
    onComplete();
  } catch (error) {
    debug.duration = Date.now() - startTime;
    debug.error = error instanceof Error ? error.message : 'Unknown error';
    onDebug?.(debug);
    onError(debug.error);
  }
}

/**
 * Save an approved customer message to the customer-seller conversation
 */
export async function saveMessageToConversation(
  conversationId: string,
  content: string,
  customerName?: string
): Promise<{ success: boolean; messageId?: string; timestamp?: string }> {
  const url = `${API_BASE_URL}/conversation/message`;
  
  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      conversationId,
      content,
      customerName,
    }),
  });

  if (!response.ok) {
    throw new Error(`Failed to save message: ${response.status}`);
  }

  return response.json();
}

/**
 * Get the customer-seller conversation history
 */
export async function getConversation(
  conversationId: string
): Promise<ConversationResponse> {
  const url = `${API_BASE_URL}/conversation/${encodeURIComponent(conversationId)}`;
  
  const response = await fetch(url, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to get conversation: ${response.status}`);
  }

  return response.json();
}

/**
 * Clear a customer-seller conversation
 */
export async function clearConversation(
  conversationId: string
): Promise<{ success: boolean }> {
  const url = `${API_BASE_URL}/conversation/${encodeURIComponent(conversationId)}`;
  
  const response = await fetch(url, {
    method: 'DELETE',
    headers: {
      'Content-Type': 'application/json',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to clear conversation: ${response.status}`);
  }

  return response.json();
}
