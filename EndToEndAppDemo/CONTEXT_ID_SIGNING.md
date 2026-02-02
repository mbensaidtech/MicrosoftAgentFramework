# Signed Context ID Implementation

This document explains the signed context ID mechanism implemented to ensure data integrity and authenticity between the frontend and backend.

## 🎯 Purpose

The signed context ID mechanism ensures that:
- ✅ Context IDs are based on the authenticated username
- ✅ Context IDs cannot be tampered with during transmission
- ✅ Only requests from the legitimate frontend are accepted
- ✅ Protects against man-in-the-middle attacks

## 🔐 How It Works

### Architecture

```
┌─────────────────────────────────────────────┐
│             Frontend (React)                 │
│                                              │
│  1. User logs in: "john.doe"                │
│  2. Generate contextId: "john.doe|timestamp"│
│  3. Sign with HMAC-SHA256                   │
│  4. Send { contextId, signature }           │
└────────────────┬────────────────────────────┘
                 │
                 │ HTTP POST (JSON)
                 │ { contextId, signature }
                 ▼
┌────────────────────────────────────────────┐
│           Backend (.NET)                    │
│                                             │
│  1. Receive contextId + signature          │
│  2. Verify signature with same secret key  │
│  3. If valid → proceed                     │
│  4. If invalid → return 403 Forbidden      │
└─────────────────────────────────────────────┘
```

### Signing Process

**Frontend (JavaScript/TypeScript):**
```typescript
// 1. Generate contextId
const contextId = `${username}|${Date.now()}`;
// Example: "john.doe|1705847234123"

// 2. Sign with HMAC-SHA256
const signature = await crypto.subtle.sign(
  'HMAC',
  secretKey,
  contextIdBytes
);

// 3. Convert to Base64
const signatureBase64 = btoa(signature);

// 4. Send both to backend
fetch('/a2a/agent', {
  body: JSON.stringify({
    params: {
      contextId: "john.doe|1705847234123",
      signature: "k7fY2s9..."
    }
  })
});
```

**Backend (.NET):**
```csharp
// 1. Extract contextId and signature from request
var contextId = "john.doe|1705847234123";
var signature = "k7fY2s9...";

// 2. Generate expected signature
using var hmac = new HMACSHA256(secretKeyBytes);
var expectedSignature = Convert.ToBase64String(
    hmac.ComputeHash(Encoding.UTF8.GetBytes(contextId))
);

// 3. Compare signatures
if (signature == expectedSignature) {
    // ✅ Valid - proceed
} else {
    // ❌ Invalid - reject with 403
}
```

## 🚀 Setup Instructions

### Backend Setup

#### 1. Configure the Secret Key

**Option A: appsettings.json**
```json
{
  "Security": {
    "ContextIdSigningKey": "your-strong-secret-key-min-32-chars-12345678"
  }
}
```

**Option B: Environment Variables (Recommended for Production)**
```bash
# Linux/macOS
export Security__ContextIdSigningKey="your-strong-secret-key-min-32-chars-12345678"

# Windows PowerShell
$env:Security__ContextIdSigningKey = "your-strong-secret-key-min-32-chars-12345678"
```

#### 2. Generate a Strong Secret Key

Use a cryptographically secure random string generator:

**PowerShell:**
```powershell
# Generate 32-byte random key as Base64
$bytes = New-Object Byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

**Bash:**
```bash
# Generate 32-byte random key as Base64
openssl rand -base64 32
```

**Online:** https://randomkeygen.com/ (use "Fort Knox Passwords")

#### 3. Services are Auto-Registered

The services are already registered in `ServiceCollectionExtensions.cs`:
```csharp
services.Configure<SecuritySettings>(configuration.GetSection("Security"));
services.AddScoped<IContextIdValidator, ContextIdValidator>();
```

### Frontend Setup

#### 1. Install Dependencies

No additional dependencies needed - uses Web Crypto API (built into modern browsers).

#### 2. Configure the Secret Key

Create `.env.local` file in the frontend root:

```env
# .env.local
VITE_CONTEXT_ID_SIGNING_KEY=your-strong-secret-key-min-32-chars-12345678
```

⚠️ **Important:** 
- The secret key MUST match the backend key
- Add `.env.local` to `.gitignore`
- Never commit the actual secret key to version control

#### 3. Use the Utility

```typescript
import { generateSignedContextId } from './utils/contextIdSigner';

// In your component or service
const username = 'john.doe'; // From your auth system
const { contextId, signature } = await generateSignedContextId(username);

// Send to backend
const response = await fetch('/a2a/historyAgent', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    messages: [{ role: 'user', content: 'Hello' }],
    params: {
      contextId: contextId,
      signature: signature
    }
  })
});
```

## 📋 API Request Format

### Valid Request

```json
{
  "messages": [
    {
      "role": "user",
      "content": "What is the capital of France?"
    }
  ],
  "params": {
    "contextId": "john.doe|1705847234123",
    "signature": "k7fY2s9XpQmN3vB8wL4eR6tY7uI9oP0aS1dF2gH3jK5l="
  }
}
```

### Backend Response Scenarios

**✅ Valid Signature (200 OK):**
```json
{
  "message": "Paris is the capital of France",
  "contextId": "john.doe|1705847234123"
}
```

**❌ Invalid Signature (403 Forbidden):**
```json
{
  "error": "Invalid contextId signature"
}
```

**⚠️ No Signature (200 OK - Backward Compatible):**
Currently allows requests without signature but logs a warning.

## 🔒 Security Considerations

### Secret Key Management

✅ **DO:**
- Use environment variables for production
- Use a minimum of 32 characters
- Use cryptographically random keys
- Rotate keys periodically
- Different keys for dev/staging/production

❌ **DON'T:**
- Hardcode keys in source code
- Commit keys to version control
- Share keys publicly
- Use weak or predictable keys
- Reuse keys across different systems

### Production Recommendations

1. **Use Azure Key Vault** (or similar secret management):
   ```csharp
   // In production, retrieve from Key Vault
   var keyVaultUrl = configuration["KeyVault:Url"];
   var secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
   var secret = await secretClient.GetSecretAsync("ContextIdSigningKey");
   ```

2. **Implement Key Rotation**:
   - Support multiple keys with versioning
   - Gradual rollout of new keys
   - Grace period for old keys

3. **Add Timestamp Validation**:
   - Reject contextIds older than X minutes
   - Prevents replay attacks

4. **Rate Limiting**:
   - Limit requests per contextId
   - Prevent brute force attacks

## 🧪 Testing

### Backend Test

Create a test endpoint to verify signing (remove in production):

```csharp
[HttpPost("test-signature")]
public IActionResult TestSignature([FromBody] SignatureTestRequest request)
{
    var isValid = _contextIdValidator.ValidateSignature(
        request.ContextId,
        request.Signature
    );
    
    return Ok(new { 
        contextId = request.ContextId,
        signature = request.Signature,
        isValid = isValid,
        username = _contextIdValidator.ExtractUsername(request.ContextId)
    });
}
```

### Frontend Test

```typescript
import { generateSignedContextId, signContextId } from './utils/contextIdSigner';

async function testSigning() {
  const username = 'test.user';
  const { contextId, signature } = await generateSignedContextId(username);
  
  console.log('Context ID:', contextId);
  console.log('Signature:', signature);
  
  // Test with backend
  const response = await fetch('/api/test-signature', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ contextId, signature })
  });
  
  const result = await response.json();
  console.log('Valid:', result.isValid); // Should be true
}
```

## 📊 Monitoring & Logging

The middleware logs validation attempts:

```
[A2AMiddleware] Valid signed contextId for user: john.doe
[A2AMiddleware] Invalid signature for contextId: john.doe|1705847234123
[A2AMiddleware] No signature provided for contextId: john.doe|1705847234123
```

Monitor these logs for:
- Failed validation attempts (potential attacks)
- Missing signatures (clients not upgraded)
- Unusual patterns

## 🔄 Migration from Unsigned to Signed

### Phase 1: Soft Launch (Current)
- Accept both signed and unsigned contextIds
- Log warnings for unsigned requests
- Monitor adoption rate

### Phase 2: Hard Requirement
- Update middleware to require signatures
- Return 403 for unsigned requests
- Update all clients

```csharp
// Phase 2: Require signature
if (string.IsNullOrWhiteSpace(signature))
{
    logger.LogWarning("Missing signature for contextId");
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    await context.Response.WriteAsJsonAsync(new { error = "Signature required" });
    return;
}
```

## 📚 Files Created/Modified

### Backend
- ✅ `Services/IContextIdValidator.cs` - Interface for validation
- ✅ `Services/ContextIdValidator.cs` - HMAC-SHA256 implementation
- ✅ `Configuration/SecuritySettings.cs` - Security configuration
- ✅ `Agents/Middleware/A2AContextMiddleware.cs` - Signature validation
- ✅ `Agents/Extensions/ServiceCollectionExtensions.cs` - Service registration
- ✅ `appsettings.json` - Security configuration section

### Frontend
- ✅ `src/utils/contextIdSigner.ts` - Signing utility
- ✅ `src/utils/contextIdSigner.example.ts` - Usage examples
- 📝 `.env.local` (create manually) - Secret key configuration

## 🤝 Support

For questions or issues with signed context IDs:
1. Check the logs for validation failures
2. Verify secret keys match between frontend and backend
3. Test with the provided examples
4. Check that signatures are properly base64 encoded

## 📄 License

This is part of the Microsoft Agent Framework learning materials.

