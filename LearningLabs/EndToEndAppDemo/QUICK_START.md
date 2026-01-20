# Quick Start Guide - AI Agents Demo

A simplified guide to get the demo project running quickly.

## 🚀 Prerequisites

- ✅ .NET 10.0 SDK
- ✅ Node.js 20.19+ or 22.12+
- ✅ MongoDB (optional, for conversation history)

## 📦 Setup

### 1. Backend Setup

#### Configure Azure OpenAI

Edit `AIAgentsBackend/appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://mbensaid-project-alpha-resource.openai.azure.com/",
    "DefaultChatDeploymentName": "gpt-4o-mini-paris-chat",
    "APIKey": "YOUR-API-KEY",
    "DefaultEmbeddingDeploymentName": "text-embedding-3-small-paris"
  }
}
```

Or use environment variables (already configured in your `.bashrc`):

```bash
export AzureOpenAI__Endpoint="https://mbensaid-project-alpha-resource.openai.azure.com/"
export AzureOpenAI__DefaultChatDeploymentName="gpt-4o-mini-paris-chat"
export AzureOpenAI__APIKey="YOUR-API-KEY"
export AzureOpenAI__DefaultEmbeddingDeploymentName="text-embedding-3-small-paris"
```

#### Secret Key (Demo - Already Configured)

The signing key is already set in `appsettings.json`:
```json
{
  "Security": {
    "ContextIdSigningKey": "Demo-Secret-Key-2026-For-AI-Agents-Project-Min-32-Chars-Required!"
  }
}
```

✅ **No changes needed** - this is a demo key shared between frontend and backend.

#### Run the Backend

```bash
cd AIAgentsBackend
dotnet run
```

Backend will start at: **http://localhost:5016**

### 2. Frontend Setup

#### Install Dependencies

```bash
cd AIAgentsFrontend
npm install
```

#### Secret Key (Demo - Already Configured)

The signing key is already set in `src/config/security.config.ts`:
```typescript
export const SECURITY_CONFIG = {
  CONTEXT_ID_SIGNING_KEY: 'Demo-Secret-Key-2026-For-AI-Agents-Project-Min-32-Chars-Required!',
};
```

✅ **No changes needed** - matches the backend key automatically.

#### Run the Frontend

```bash
npm run dev
```

Frontend will start at: **http://localhost:3000**

## 🎯 Test the Application

### 1. Access the Frontend

Open http://localhost:3000 in your browser.

### 2. Test Signed Context ID

The frontend automatically signs all requests with the configured key.

**Example Request (Auto-generated):**
```json
{
  "params": {
    "contextId": "username|1705847234123",
    "signature": "k7fY2s9XpQmN3vB8wL4eR6tY7uI9oP0aS1dF2gH3jK5l="
  }
}
```

**Backend validates the signature automatically** ✅

### 3. Monitor Backend Logs

You'll see validation logs:
```
[A2AMiddleware] Valid signed contextId for user: john.doe
```

## 🔐 Security Configuration Summary

| Component | Configuration File | Key Value |
|-----------|-------------------|-----------|
| **Backend** | `appsettings.json` | `Demo-Secret-Key-2026-For-AI-Agents-Project-Min-32-Chars-Required!` |
| **Frontend** | `src/config/security.config.ts` | `Demo-Secret-Key-2026-For-AI-Agents-Project-Min-32-Chars-Required!` |

✅ **Keys match** - No manual configuration needed for demo!

## 🧪 Testing Signature Validation

### Manual Test with cURL

```bash
# This will work (valid signature)
curl -X POST http://localhost:5016/a2a/historyAgent \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [{"role": "user", "content": "Hello"}],
    "params": {
      "contextId": "testuser|1705847234123",
      "signature": "VALID_SIGNATURE_HERE"
    }
  }'

# This will fail (invalid signature)
curl -X POST http://localhost:5016/a2a/historyAgent \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [{"role": "user", "content": "Hello"}],
    "params": {
      "contextId": "testuser|1705847234123",
      "signature": "INVALID_SIGNATURE"
    }
  }'
# Response: {"error": "Invalid contextId signature"} - 403 Forbidden
```

## 📁 Project Structure

```
EndToEndAppDemo/
├── AIAgentsBackend/
│   ├── appsettings.json                    ← Azure OpenAI + Security config
│   ├── Services/
│   │   ├── IContextIdValidator.cs          ← Signature validation interface
│   │   └── ContextIdValidator.cs           ← HMAC-SHA256 implementation
│   ├── Configuration/
│   │   └── SecuritySettings.cs             ← Security configuration model
│   └── Agents/Middleware/
│       └── A2AContextMiddleware.cs         ← Validates signatures
│
├── AIAgentsFrontend/
│   ├── src/
│   │   ├── config/
│   │   │   └── security.config.ts          ← Secret key configuration
│   │   └── utils/
│   │       ├── contextIdSigner.ts          ← Signing utility
│   │       └── contextIdSigner.example.ts  ← Usage examples
│   └── package.json
│
├── QUICK_START.md                          ← This file
├── CONTEXT_ID_SIGNING.md                   ← Complete documentation
└── IMPLEMENTATION_SUMMARY.md               ← Technical overview
```

## 🛠️ Features Implemented

✅ **Signed Context IDs** - HMAC-SHA256 signature validation  
✅ **ThreadRepository** - MongoDB data access layer  
✅ **Separate Collections** - `threadMessages` for repository, `chat_history` for agents  
✅ **Simplified Configuration** - Demo keys in config files  
✅ **Automatic Validation** - Middleware validates all requests  
✅ **Comprehensive Logging** - Monitor validation attempts  

## 🔄 Workflow

```
User → Frontend → Generate contextId (username|timestamp)
                ↓
                Sign with HMAC-SHA256
                ↓
                Send { contextId, signature }
                ↓
Backend → Validate signature
        ↓
        ✅ Valid → Process request
        ❌ Invalid → Return 403 Forbidden
```

## 📚 Documentation

- **[QUICK_START.md](QUICK_START.md)** - This file - Quick setup guide
- **[CONTEXT_ID_SIGNING.md](CONTEXT_ID_SIGNING.md)** - Complete implementation details
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Technical overview
- **[Backend README](AIAgentsBackend/README.md)** - Backend configuration guide
- **[Frontend README](AIAgentsFrontend/README.md)** - Frontend configuration guide

## ⚠️ Demo vs Production

### Demo (Current Setup)
- ✅ Secret key in config files
- ✅ Simple to run and test
- ✅ Perfect for learning and development

### Production Recommendations
- 🔐 Move keys to environment variables
- 🔐 Use Azure Key Vault or similar
- 🔐 Different keys for each environment
- 🔐 Implement key rotation
- 🔐 Add timestamp validation (prevent replay attacks)
- 🔐 Require signatures (remove backward compatibility)

## 🎓 Learning Objectives

By running this demo, you learn:
1. ✅ HMAC-SHA256 message authentication
2. ✅ Web Crypto API usage
3. ✅ ASP.NET Core middleware patterns
4. ✅ TypeScript utility development
5. ✅ Frontend-Backend security integration

## 🐛 Troubleshooting

### Backend won't start
- Check Azure OpenAI configuration
- Verify API key is set
- Check port 5016 is available

### Frontend won't start
- Run `npm install`
- Check Node.js version (20.19+)
- Check port 3000 is available

### Signature validation fails
- Verify keys match in both frontend and backend
- Check console logs for validation errors
- Ensure contextId format is correct

## 📄 License

This is part of the Microsoft Agent Framework learning materials.

