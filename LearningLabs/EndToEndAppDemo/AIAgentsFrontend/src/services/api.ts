import type { RestChatResponse, A2AResponse, DebugInfo } from '../types';
import { API_BASE_URL, getAgentById } from '../config/agents.config';

export interface ApiResultWithDebug<T> {
  data: T;
  debug: DebugInfo;
}

/**
 * Get agent paths by id
 */
function getAgentPaths(agentId: string): { rest: string; a2a: string } {
  const agent = getAgentById(agentId);
  if (!agent) {
    // Fallback for unknown agents
    return {
      rest: `/api/agents/${agentId}`,
      a2a: `/a2a/${agentId}Agent`,
    };
  }
  return {
    rest: agent.restPath,
    a2a: agent.a2aPath,
  };
}

/**
 * Send a message using REST API (standard HTTP)
 */
export async function sendRestMessage(
  agentId: string,
  message: string,
  contextId?: string
): Promise<ApiResultWithDebug<RestChatResponse>> {
  const paths = getAgentPaths(agentId);
  const url = `${API_BASE_URL}${paths.rest}/chat`;
  const requestBody = { message, contextId };
  const startTime = Date.now();

  const debug: DebugInfo = {
    timestamp: new Date(),
    method: 'POST',
    url,
    requestBody,
  };

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(requestBody),
    });

    debug.responseStatus = response.status;
    debug.duration = Date.now() - startTime;

    if (!response.ok) {
      debug.error = `HTTP error! status: ${response.status}`;
      throw new Error(debug.error);
    }

    const data = await response.json();
    debug.responseBody = data;

    return { data, debug };
  } catch (error) {
    debug.duration = Date.now() - startTime;
    debug.error = error instanceof Error ? error.message : 'Unknown error';
    throw { error, debug };
  }
}

/**
 * Send a message using REST API with SSE streaming
 */
export async function sendRestStreamingMessage(
  agentId: string,
  message: string,
  contextId: string | undefined,
  onToken: (token: string) => void,
  onComplete: (contextId: string) => void,
  onError: (error: string) => void,
  onDebug?: (debug: DebugInfo) => void
): Promise<void> {
  const paths = getAgentPaths(agentId);
  const url = `${API_BASE_URL}${paths.rest}/stream`;
  const requestBody = { message, contextId };
  const startTime = Date.now();

  const debug: DebugInfo = {
    timestamp: new Date(),
    method: 'POST (SSE)',
    url,
    requestBody,
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
      debug.error = `HTTP error! status: ${response.status}`;
      debug.duration = Date.now() - startTime;
      onDebug?.(debug);
      throw new Error(debug.error);
    }

    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error('No response body');
    }

    const decoder = new TextDecoder();
    let buffer = '';
    const streamedContent: string[] = [];

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('event:')) {
            continue;
          }
          if (line.startsWith('data:')) {
            const data = line.slice(5).trim();
            if (data) {
              try {
                const parsed = JSON.parse(data);
                if (parsed.text) {
                  onToken(parsed.text);
                  streamedContent.push(parsed.text);
                }
                if (parsed.contextId && !parsed.text) {
                  onComplete(parsed.contextId);
                }
              } catch {
                // Ignore parse errors
              }
            }
          }
        }
      }

      debug.duration = Date.now() - startTime;
      debug.responseBody = { streamedContent: streamedContent.join(''), note: 'SSE streaming response' };
      onDebug?.(debug);
    } catch (error) {
      debug.duration = Date.now() - startTime;
      debug.error = error instanceof Error ? error.message : 'Stream error';
      onDebug?.(debug);
      onError(debug.error);
    }
  } catch (error) {
    debug.duration = Date.now() - startTime;
    debug.error = error instanceof Error ? error.message : 'Unknown error';
    onDebug?.(debug);
    onError(debug.error);
  }
}

/**
 * Send a message using A2A protocol (JSON-RPC)
 */
export async function sendA2AMessage(
  agentId: string,
  message: string,
  contextId?: string
): Promise<ApiResultWithDebug<{ text: string; contextId: string }>> {
  const paths = getAgentPaths(agentId);
  const url = `${API_BASE_URL}${paths.a2a}`;
  const requestBody = {
    jsonrpc: '2.0',
    method: 'message/send',
    params: {
      message: {
        kind: 'message',
        messageId: `msg-${Date.now()}`,
        role: 'user',
        parts: [
          {
            kind: 'text',
            text: message,
          },
        ],
      },
      contextId,
    },
    id: `req-${Date.now()}`,
  };
  const startTime = Date.now();

  const debug: DebugInfo = {
    timestamp: new Date(),
    method: 'POST (JSON-RPC)',
    url,
    requestBody,
  };

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(requestBody),
    });

    debug.responseStatus = response.status;
    debug.duration = Date.now() - startTime;

    if (!response.ok) {
      debug.error = `HTTP error! status: ${response.status}`;
      throw new Error(debug.error);
    }

    const data: A2AResponse = await response.json();
    debug.responseBody = data;

    const textPart = data.result.parts.find(p => p.kind === 'text');
    const text = textPart?.text || '';

    return {
      data: { text, contextId: data.result.contextId },
      debug,
    };
  } catch (error) {
    debug.duration = Date.now() - startTime;
    debug.error = error instanceof Error ? error.message : 'Unknown error';
    throw { error, debug };
  }
}

/**
 * Send a message using A2A protocol with SSE streaming
 */
export async function sendA2AStreamingMessage(
  agentId: string,
  message: string,
  contextId: string | undefined,
  onToken: (token: string) => void,
  onComplete: (contextId: string) => void,
  onError: (error: string) => void,
  onDebug?: (debug: DebugInfo) => void
): Promise<void> {
  const paths = getAgentPaths(agentId);
  const url = `${API_BASE_URL}${paths.a2a}/v1/message:stream`;
  const requestBody = {
    message: {
      kind: 'message',
      role: 'user',
      parts: [
        {
          kind: 'text',
          text: message,
          metadata: {},
        },
      ],
      messageId: null,
      contextId,
    },
  };
  const startTime = Date.now();

  const debug: DebugInfo = {
    timestamp: new Date(),
    method: 'POST (A2A SSE)',
    url,
    requestBody,
  };

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(requestBody),
    });

    debug.responseStatus = response.status;

    if (!response.ok) {
      debug.error = `HTTP error! status: ${response.status}`;
      debug.duration = Date.now() - startTime;
      onDebug?.(debug);
      throw new Error(debug.error);
    }

    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error('No response body');
    }

    const decoder = new TextDecoder();
    let buffer = '';
    const streamedContent: string[] = [];

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data:')) {
            const data = line.slice(5).trim();
            if (data) {
              try {
                const parsed = JSON.parse(data);
                if (parsed.parts) {
                  const textPart = parsed.parts.find((p: { kind: string; text: string }) => p.kind === 'text');
                  if (textPart?.text) {
                    onToken(textPart.text);
                    streamedContent.push(textPart.text);
                  }
                }
                if (parsed.contextId) {
                  onComplete(parsed.contextId);
                }
              } catch {
                // Ignore parse errors
              }
            }
          }
        }
      }

      debug.duration = Date.now() - startTime;
      debug.responseBody = { streamedContent: streamedContent.join(''), note: 'A2A SSE streaming response' };
      onDebug?.(debug);
    } catch (error) {
      debug.duration = Date.now() - startTime;
      debug.error = error instanceof Error ? error.message : 'Stream error';
      onDebug?.(debug);
      onError(debug.error);
    }
  } catch (error) {
    debug.duration = Date.now() - startTime;
    debug.error = error instanceof Error ? error.message : 'Unknown error';
    onDebug?.(debug);
    onError(debug.error);
  }
}
