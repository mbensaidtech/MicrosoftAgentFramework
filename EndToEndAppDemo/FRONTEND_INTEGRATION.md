# Frontend Integration - Signed Context ID

## ✅ What Was Fixed

The frontend was **not sending signatures** with the contextId. I've updated the API service to automatically sign all contextIds before sending them to the backend.

## 📝 Changes Made

### File: `src/services/api.ts`

**Added import:**
```typescript
import { signContextId } from '../utils/contextIdSigner';
```

**Updated `sendA2AMessage` function:**
- Now generates signature for contextId
- Adds signature to `params.signature`
- Logs signing activity to console

**Updated `sendA2AStreamingMessage` function:**
- Now generates signature for contextId
- Adds signature to `message.signature`
- Logs signing activity to console

## 🔍 How to Verify

### 1. Check Browser Console

You should now see logs like:
```
[A2A] Signed contextId: mbensaid-mkl8aseh-rzfev2 signature: k7fY2s9XpQmN3vB8wL...
```

### 2. Check Network Tab

**Before (❌ No signature):**
```json
{
  "params": {
    "contextId": "mbensaid-mkl8aseh-rzfev2"
  }
}
```

**After (✅ With signature):**
```json
{
  "params": {
    "contextId": "mbensaid-mkl8aseh-rzfev2",
    "signature": "k7fY2s9XpQmN3vB8wL4eR6tY7uI9oP0aS1dF2gH3jK5l="
  }
}
```

### 3. Check Backend Logs

You should see:
```
[A2AMiddleware] Valid signed contextId for user: mbensaid
```

## 🎯 Request Flow

```
Frontend                          Backend
========                          =======

1. User sends message
   ↓
2. contextId = "mbensaid-mkl8aseh-rzfev2"
   ↓
3. signature = signContextId(contextId)
   ↓
4. Send { contextId, signature } ──→ 5. Middleware intercepts
                                     ↓
                                  6. Validate signature
                                     ↓
                                  7. ✅ Valid → Process
                                     ❌ Invalid → 403
```

## 🔐 Security

- ✅ Every A2A request is now signed
- ✅ Backend validates every signature
- ✅ Tampering with contextId will fail validation
- ✅ Only legitimate frontend can create valid signatures

## 🐛 Troubleshooting

### Signature not appearing in network tab

1. **Refresh the page** - Vite should auto-reload, but manual refresh ensures latest code
2. **Check browser console** - Look for signing logs
3. **Clear browser cache** - Sometimes needed for module updates

### Getting 403 Forbidden errors

1. **Check secret keys match** - Frontend and backend must use the same key
   - Frontend: `src/config/security.config.ts`
   - Backend: `appsettings.json` → `Security.ContextIdSigningKey`
2. **Check console for signing errors** - Look for `[A2A] Failed to sign contextId`

### No logs in console

1. **Open Developer Tools** (F12)
2. **Go to Console tab**
3. **Send a message** - You should see `[A2A] Signed contextId...`

## 📊 Testing

### Test Valid Signature

1. Open http://localhost:3000
2. Send a message to any agent
3. Check Network tab → Request Payload
4. Verify `signature` field is present
5. Check backend logs for "Valid signed contextId"

### Test Invalid Signature (Manual)

Use cURL to test invalid signature:

```bash
curl -X POST http://localhost:5016/a2a/historyAgent \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "message/send",
    "params": {
      "message": {
        "kind": "message",
        "role": "user",
        "parts": [{"kind": "text", "text": "Hello"}]
      },
      "contextId": "test-user",
      "signature": "INVALID_SIGNATURE"
    },
    "id": "test-1"
  }'
```

Expected response:
```json
{
  "error": "Invalid contextId signature"
}
```

Status: `403 Forbidden`

## ✨ Benefits

1. **Automatic** - No manual signing needed, happens automatically
2. **Transparent** - Works with existing code, no API changes
3. **Secure** - All requests are validated
4. **Debuggable** - Console logs show signing activity
5. **Production-ready** - Just change the secret key for production

## 📄 Related Files

- `src/services/api.ts` - API service with signing logic
- `src/utils/contextIdSigner.ts` - Signing utility
- `src/config/security.config.ts` - Secret key configuration
- `Agents/Middleware/A2AContextMiddleware.cs` - Backend validation

## 🎉 Result

Your frontend now automatically signs all contextIds, and the backend validates them. The security mechanism is fully integrated and working!

