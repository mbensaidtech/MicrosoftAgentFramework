/**
 * Agents Configuration
 * 
 * Add, remove or modify agents here.
 * Each agent requires:
 * - id: Unique identifier (used in API paths)
 * - name: Display name
 * - description: Short description
 * - supportsStreaming: Whether the agent supports SSE streaming
 * - restPath: REST API endpoint path
 * - a2aPath: A2A protocol endpoint path
 * - presetQuestions: Array of sample questions for testing (optional)
 */

export interface AgentConfig {
  id: string;
  name: string;
  description: string;
  supportsStreaming: boolean;
  restPath: string;
  a2aPath: string;
  presetQuestions?: string[];
}

export const API_BASE_URL = ''; // Empty for Vite proxy, or set to 'http://localhost:5016' for direct

export const AGENTS_CONFIG: AgentConfig[] = [
  {
    id: 'translation',
    name: 'Translation Agent',
    description: 'Translates text between languages',
    supportsStreaming: false, // AIAgent - no RunStreamingAsync
    restPath: '/api/agents/translation',
    a2aPath: '/a2a/translationAgent',
    presetQuestions: [
      'Translate "Hello, how are you today?" to French',
      'Now translate that same phrase to Spanish',
      'How would you say "Thank you very much" in the same language?',
      'Translate "I love learning new languages" to German',
      'Can you also translate that to Italian?',
    ],
  },
  {
    id: 'customer-support',
    name: 'Customer Support Agent',
    description: 'Handles customer inquiries',
    supportsStreaming: true, // ChatClientAgent - has RunStreamingAsync
    restPath: '/api/agents/customer-support',
    a2aPath: '/a2a/customerSupportAgent',
    presetQuestions: [
      "I ordered a product 5 days ago but haven't received it yet",
      'Can you check the tracking status for me?',
      "What are my options if it's lost?",
      'How do I request a refund for this order?',
      'What is your return policy timeframe?',
    ],
  },
  {
    id: 'history',
    name: 'History Agent',
    description: 'Expert historian with conversation memory',
    supportsStreaming: true, // ChatClientAgent - has RunStreamingAsync
    restPath: '/api/agents/history',
    a2aPath: '/a2a/historyAgent',
    presetQuestions: [
      'Which country was the first to land a human on the Moon?',
      'Who were the astronauts on that historic mission?',
      'What year did that Moon landing happen?',
      'What was the name of the spacecraft they used?',
      'How long did they stay on the lunar surface?',
    ],
  },
];

/**
 * Helper function to get agent by id
 */
export function getAgentById(id: string): AgentConfig | undefined {
  return AGENTS_CONFIG.find(agent => agent.id === id);
}

/**
 * Helper function to get all agent ids
 */
export function getAgentIds(): string[] {
  return AGENTS_CONFIG.map(agent => agent.id);
}
