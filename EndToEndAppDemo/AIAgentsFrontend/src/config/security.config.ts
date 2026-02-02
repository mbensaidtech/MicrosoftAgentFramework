/**
 * Security Configuration
 * 
 * ⚠️ NOTE: This is a DEMO configuration with hardcoded secret key.
 * In production, use environment variables and proper secret management.
 */

export const SECURITY_CONFIG = {
  /**
   * Secret key used for signing context IDs.
   * Must match the backend configuration: Security.ContextIdSigningKey
   */
  CONTEXT_ID_SIGNING_KEY: 'Demo-Secret-Key-2026-For-AI-Agents-Project-Min-32-Chars-Required!',
} as const;

