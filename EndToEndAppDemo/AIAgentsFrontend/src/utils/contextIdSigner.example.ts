/**
 * Example usage of the contextIdSigner utility.
 */

import { generateSignedContextId, extractUsername, extractTimestamp } from './contextIdSigner';

// Example 1: Generate a signed context ID for a user
async function example1() {
  const username = 'john.doe';
  
  const { contextId, signature } = await generateSignedContextId(username);
  
  console.log('Generated Context ID:', contextId);
  // Output: "john.doe|1234567890123"
  
  console.log('Signature:', signature);
  // Output: "base64encodedHMAC..."
}

// Example 2: Use in an API call
async function sendMessageToAgent(username: string, message: string) {
  // Generate signed contextId
  const { contextId, signature } = await generateSignedContextId(username);
  
  // Send to backend with signature
  const response = await fetch('http://localhost:5016/a2a/historyAgent', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      messages: [
        {
          role: 'user',
          content: message,
        },
      ],
      params: {
        contextId: contextId,
        signature: signature,  // Backend will validate this
      },
    }),
  });
  
  return await response.json();
}

// Example 3: Extract information from contextId
function example3() {
  const contextId = 'john.doe|1234567890123';
  
  const username = extractUsername(contextId);
  console.log('Username:', username);
  // Output: "john.doe"
  
  const timestamp = extractTimestamp(contextId);
  console.log('Timestamp:', timestamp);
  // Output: 1234567890123
  
  const date = timestamp ? new Date(timestamp) : null;
  console.log('Generated at:', date?.toISOString());
}

// Example 4: React Hook for managing signed contextId
import { useState, useEffect } from 'react';

function useSignedContextId(username: string) {
  const [contextData, setContextData] = useState<{
    contextId: string;
    signature: string;
  } | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    if (!username) {
      setContextData(null);
      return;
    }

    setLoading(true);
    generateSignedContextId(username)
      .then((data) => {
        setContextData(data);
        setError(null);
      })
      .catch((err) => {
        setError(err);
        setContextData(null);
      })
      .finally(() => {
        setLoading(false);
      });
  }, [username]);

  return { contextData, loading, error };
}

export { example1, sendMessageToAgent, example3, useSignedContextId };

