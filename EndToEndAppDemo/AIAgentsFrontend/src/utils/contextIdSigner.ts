/**
 * Utility for generating and signing context IDs using HMAC-SHA256.
 * The signature ensures the backend can verify the contextId hasn't been tampered with.
 */

import { SECURITY_CONFIG } from '../config/security.config';

/**
 * Secret key shared between frontend and backend.
 * For this demo, it's configured in security.config.ts
 * In production, use environment variables and proper secret management.
 */
const SECRET_KEY = SECURITY_CONFIG.CONTEXT_ID_SIGNING_KEY;

/**
 * Generates a context ID based on username and current timestamp.
 * Format: "username|timestamp"
 * 
 * @param username The logged-in user's username
 * @returns The generated context ID
 */
export function generateContextId(username: string): string {
  if (!username || username.trim() === '') {
    throw new Error('Username cannot be empty');
  }

  const timestamp = Date.now();
  return `${username}|${timestamp}`;
}

/**
 * Computes HMAC-SHA256 signature for a context ID.
 * Uses the Web Crypto API for secure hashing.
 * 
 * @param contextId The context ID to sign
 * @returns Promise resolving to the Base64-encoded signature
 */
export async function signContextId(contextId: string): Promise<string> {
  if (!contextId || contextId.trim() === '') {
    throw new Error('ContextId cannot be empty');
  }

  try {
    // Convert secret key to bytes
    const encoder = new TextEncoder();
    const keyData = encoder.encode(SECRET_KEY);

    // Import the key for HMAC
    const cryptoKey = await crypto.subtle.importKey(
      'raw',
      keyData,
      { name: 'HMAC', hash: 'SHA-256' },
      false,
      ['sign']
    );

    // Sign the context ID
    const contextIdData = encoder.encode(contextId);
    const signature = await crypto.subtle.sign('HMAC', cryptoKey, contextIdData);

    // Convert to Base64
    const signatureArray = new Uint8Array(signature);
    return btoa(String.fromCharCode(...signatureArray));
  } catch (error) {
    console.error('Error signing contextId:', error);
    throw new Error('Failed to sign contextId');
  }
}

/**
 * Generates a signed context ID for the given username.
 * Returns both the contextId and its signature.
 * 
 * @param username The logged-in user's username
 * @returns Promise resolving to { contextId, signature }
 */
export async function generateSignedContextId(username: string): Promise<{
  contextId: string;
  signature: string;
}> {
  const contextId = generateContextId(username);
  const signature = await signContextId(contextId);

  return { contextId, signature };
}

/**
 * Extracts the username from a context ID.
 * 
 * @param contextId The context ID in format "username|timestamp"
 * @returns The username or null if invalid format
 */
export function extractUsername(contextId: string): string | null {
  if (!contextId) {
    return null;
  }

  const parts = contextId.split('|');
  if (parts.length < 1) {
    return null;
  }

  const username = parts[0].trim();
  return username || null;
}

/**
 * Extracts the timestamp from a context ID.
 * 
 * @param contextId The context ID in format "username|timestamp"
 * @returns The timestamp in milliseconds or null if invalid format
 */
export function extractTimestamp(contextId: string): number | null {
  if (!contextId) {
    return null;
  }

  const parts = contextId.split('|');
  if (parts.length < 2) {
    return null;
  }

  const timestamp = parseInt(parts[1], 10);
  return isNaN(timestamp) ? null : timestamp;
}

