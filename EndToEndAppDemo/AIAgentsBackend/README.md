# AI Agents Backend

A .NET Web API backend for AI Agents management system with Azure OpenAI integration.

## 📋 Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Configuration](#configuration)
- [Environment Variables Setup](#environment-variables-setup)
- [Running the Application](#running-the-application)
- [API Endpoints](#api-endpoints)
- [Tech Stack](#tech-stack)

---

## 🎯 Overview

This backend provides RESTful APIs for managing AI agents including:
- **Translation Agent** - Multi-language translation services
- **Customer Support Agent** - Customer inquiry handling
- **History Agent** - Historical knowledge with conversation memory

The application uses:
- Azure OpenAI for AI capabilities
- MongoDB for conversation history storage
- ASP.NET Core Web API for RESTful services

---

## 📦 Prerequisites

- **.NET 8.0 SDK** or later
- **Azure OpenAI** resource with:
  - Chat completion deployment (e.g., GPT-4o-mini)
  - Text embedding deployment (e.g., text-embedding-3-small)
  - API Key
- **MongoDB** (optional, for conversation history)
  - Can run locally via Docker
  - See `MongoDB/docker-compose.yml`

---

## ⚙️ Configuration

The application reads configuration from `appsettings.json` and **environment variables**.

### Configuration Structure

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "DefaultChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "APIKey": "YOUR-API-KEY",
    "DefaultEmbeddingDeploymentName": "YOUR-EMBEDDING-DEPLOYMENT-NAME"
  },
  "MongoDB": {
    "ConnectionString": "mongodb://admin:password123@localhost:27017",
    "DatabaseName": "aiagents_db",
    "ChatMessageStoreCollectionName": "chat_history"
  }
}
```

> ⚠️ **Security Note**: Never commit API keys to version control. Use environment variables instead!

---

## 🔐 Environment Variables Setup

Environment variables **override** values in `appsettings.json`. This is the recommended approach for sensitive data like API keys.

### .NET Configuration Format

.NET uses **double underscores (`__`)** to map environment variables to nested JSON properties:

```
AzureOpenAI__Endpoint          → AzureOpenAI.Endpoint
AzureOpenAI__APIKey            → AzureOpenAI.APIKey
MongoDB__ConnectionString      → MongoDB.ConnectionString
```

---

## 🐧 Linux / macOS Setup

### Option 1: Using `.bashrc` (Persistent)

This method keeps environment variables available across all terminal sessions.

#### Step 1: Edit `.bashrc`

Open your bash configuration file:

```bash
nano ~/.bashrc
# or
vim ~/.bashrc
```

#### Step 2: Add Environment Variables

Add these lines at the end of the file:

```bash
# ============================================================
# AI Agents Backend Configuration
# ============================================================
# Azure OpenAI Configuration
export AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com/"
export AzureOpenAI__DefaultChatDeploymentName="gpt-4o-mini-paris-chat"
export AzureOpenAI__APIKey="YOUR-API-KEY-HERE"
export AzureOpenAI__DefaultEmbeddingDeploymentName="text-embedding-3-small-paris"

# MongoDB Configuration (optional)
export MongoDB__ConnectionString="mongodb://admin:password123@localhost:27017"
export MongoDB__DatabaseName="aiagents_db"
export MongoDB__ChatMessageStoreCollectionName="chat_history"
```

**Replace the placeholder values with your actual configuration:**
- `YOUR-RESOURCE` - Your Azure OpenAI resource name
- `YOUR-API-KEY-HERE` - Your Azure OpenAI API key
- Deployment names as per your Azure setup

#### Step 3: Apply Changes

**Important:** After editing `.bashrc`, you need to reload it to apply changes.

**Option A: Reload in current terminal** (recommended for immediate use)
```bash
source ~/.bashrc
```

**Option B: Open a new terminal**  
New terminal windows automatically load `.bashrc`, so the changes will be available without running `source`.

> 💡 **Why source?** The `source` command re-executes `.bashrc` in the current shell session, making the new environment variables immediately available without closing the terminal.

#### Step 4: Verify Environment Variables

```bash
echo $AzureOpenAI__Endpoint
echo $AzureOpenAI__DefaultChatDeploymentName
echo $AzureOpenAI__APIKey
```

### Option 2: Using `.zshrc` (for Zsh users)

If you're using Zsh shell (common on macOS):

```bash
# Edit .zshrc instead
nano ~/.zshrc

# Add the same environment variables as shown in Option 1

# Then reload to apply changes
source ~/.zshrc
```

> 💡 Like `.bashrc`, you only need to run `source` for the current terminal. New terminals will automatically load the updated `.zshrc`.

### Option 3: Temporary Session (Non-Persistent)

For testing or temporary use:

```bash
export AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com/"
export AzureOpenAI__DefaultChatDeploymentName="gpt-4o-mini"
export AzureOpenAI__APIKey="YOUR-API-KEY"
export AzureOpenAI__DefaultEmbeddingDeploymentName="text-embedding-3-small"

# Then run the application in the same terminal
dotnet run
```

> ⚠️ These variables are lost when the terminal is closed.

### Option 4: Using `.env` File with `dotnet-user-secrets`

For development, use .NET User Secrets:

```bash
# Initialize user secrets
dotnet user-secrets init

# Set individual secrets
dotnet user-secrets set "AzureOpenAI:APIKey" "YOUR-API-KEY"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DefaultChatDeploymentName" "gpt-4o-mini"
dotnet user-secrets set "AzureOpenAI:DefaultEmbeddingDeploymentName" "text-embedding-3-small"

# List all secrets
dotnet user-secrets list
```

> 💡 User secrets are stored outside the project directory and never committed to source control.

---

## 🪟 Windows Setup

### Option 1: Using Environment Variables GUI (Persistent - System Wide)

#### Step 1: Open System Properties

1. Press `Win + R`
2. Type `sysdm.cpl` and press Enter
3. Go to the **Advanced** tab
4. Click **Environment Variables**

#### Step 2: Add User Environment Variables

In the **User variables** section, click **New** for each variable:

| Variable Name | Value |
|---------------|-------|
| `AzureOpenAI__Endpoint` | `https://YOUR-RESOURCE.openai.azure.com/` |
| `AzureOpenAI__DefaultChatDeploymentName` | `gpt-4o-mini-paris-chat` |
| `AzureOpenAI__APIKey` | `YOUR-API-KEY-HERE` |
| `AzureOpenAI__DefaultEmbeddingDeploymentName` | `text-embedding-3-small-paris` |
| `MongoDB__ConnectionString` | `mongodb://admin:password123@localhost:27017` |
| `MongoDB__DatabaseName` | `aiagents_db` |

#### Step 3: Apply Changes

1. Click **OK** on all dialogs
2. **Restart** your terminal/IDE for changes to take effect

#### Step 4: Verify Environment Variables

Open Command Prompt or PowerShell:

```cmd
# Command Prompt
echo %AzureOpenAI__Endpoint%
echo %AzureOpenAI__APIKey%

# PowerShell
$env:AzureOpenAI__Endpoint
$env:AzureOpenAI__APIKey
```

### Option 2: Using PowerShell Profile (Persistent - User)

#### Step 1: Open PowerShell Profile

```powershell
# Check if profile exists
Test-Path $PROFILE

# Create profile if it doesn't exist
if (!(Test-Path $PROFILE)) {
    New-Item -Path $PROFILE -ItemType File -Force
}

# Edit the profile
notepad $PROFILE
```

#### Step 2: Add Environment Variables

Add these lines to your PowerShell profile:

```powershell
# ============================================================
# AI Agents Backend Configuration
# ============================================================
$env:AzureOpenAI__Endpoint = "https://YOUR-RESOURCE.openai.azure.com/"
$env:AzureOpenAI__DefaultChatDeploymentName = "gpt-4o-mini-paris-chat"
$env:AzureOpenAI__APIKey = "YOUR-API-KEY-HERE"
$env:AzureOpenAI__DefaultEmbeddingDeploymentName = "text-embedding-3-small-paris"

# MongoDB Configuration (optional)
$env:MongoDB__ConnectionString = "mongodb://admin:password123@localhost:27017"
$env:MongoDB__DatabaseName = "aiagents_db"
$env:MongoDB__ChatMessageStoreCollectionName = "chat_history"
```

#### Step 3: Reload Profile

**Important:** After editing your PowerShell profile, you need to reload it to apply changes.

**Option A: Reload in current session** (recommended for immediate use)
```powershell
. $PROFILE
```

**Option B: Restart PowerShell**  
New PowerShell windows automatically load the profile, so the changes will be available without reloading.

> 💡 **Why reload?** The `. $PROFILE` command re-executes your profile script in the current PowerShell session, making the new environment variables immediately available.

### Option 3: Using Command Prompt (Temporary Session)

```cmd
set AzureOpenAI__Endpoint=https://YOUR-RESOURCE.openai.azure.com/
set AzureOpenAI__DefaultChatDeploymentName=gpt-4o-mini
set AzureOpenAI__APIKey=YOUR-API-KEY
set AzureOpenAI__DefaultEmbeddingDeploymentName=text-embedding-3-small

dotnet run
```

> ⚠️ Variables are lost when Command Prompt is closed.

### Option 4: Using PowerShell (Temporary Session)

```powershell
$env:AzureOpenAI__Endpoint = "https://YOUR-RESOURCE.openai.azure.com/"
$env:AzureOpenAI__DefaultChatDeploymentName = "gpt-4o-mini"
$env:AzureOpenAI__APIKey = "YOUR-API-KEY"
$env:AzureOpenAI__DefaultEmbeddingDeploymentName = "text-embedding-3-small"

dotnet run
```

> ⚠️ Variables are lost when PowerShell is closed.

### Option 5: Using .NET User Secrets (Recommended for Development)

```powershell
# Initialize user secrets
dotnet user-secrets init

# Set individual secrets
dotnet user-secrets set "AzureOpenAI:APIKey" "YOUR-API-KEY"
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:DefaultChatDeploymentName" "gpt-4o-mini"
dotnet user-secrets set "AzureOpenAI:DefaultEmbeddingDeploymentName" "text-embedding-3-small"

# List all secrets
dotnet user-secrets list
```

---

## 🚀 Running the Application

### Prerequisites Check

Before running, ensure:

1. ✅ Environment variables are set (or user secrets configured)
2. ✅ MongoDB is running (if using conversation history feature)
3. ✅ Azure OpenAI resource is accessible

### Start MongoDB (Optional)

If using conversation history:

```bash
# Navigate to MongoDB folder
cd MongoDB

# Start MongoDB with Docker Compose
docker-compose up -d

# Check status
docker-compose ps
```

### Run the Backend

```bash
# Navigate to backend directory
cd /path/to/AIAgentsBackend

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

Or with watch mode (auto-restart on file changes):

```bash
dotnet watch run
```

### Expected Output

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5016
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Verify the Application

Open your browser and navigate to:
- **Swagger UI**: http://localhost:5016/swagger
- **Health Check**: http://localhost:5016/health

---

## 📡 API Endpoints

### Base URL
```
http://localhost:5016
```

### Agent Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/agents` | List all available agents |
| `POST` | `/api/agents/{agentId}/chat` | Chat with a specific agent |
| `GET` | `/api/agents/{agentId}/history` | Get conversation history |

### A2A (Agent-to-Agent) Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/a2a/translationAgent` | Translation agent endpoint |
| `POST` | `/a2a/customerSupportAgent` | Customer support agent endpoint |
| `POST` | `/a2a/historyAgent` | History agent endpoint |

### Example Request

```bash
curl -X POST "http://localhost:5016/api/agents/translation/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Translate 'Hello World' to French"
  }'
```

---

## 🛠️ Tech Stack

- **ASP.NET Core 8.0** - Web API Framework
- **Azure OpenAI** - AI Services
- **MongoDB** - Conversation History Storage
- **Swagger/OpenAPI** - API Documentation

---

## 🐛 Troubleshooting

### Issue: "AzureOpenAI configuration is missing"

**Solution:** Ensure environment variables are set correctly or appsettings.json is configured.

```bash
# Verify variables (Linux/macOS)
env | grep AzureOpenAI

# Verify variables (Windows PowerShell)
Get-ChildItem Env:AzureOpenAI*
```

### Issue: "MongoDB connection failed"

**Solution:** 
1. Check if MongoDB is running: `docker ps`
2. Verify connection string in environment variables
3. Or disable conversation history if not needed

### Issue: "Unauthorized" or "401" errors

**Solution:** 
1. Verify your Azure OpenAI API key is correct
2. Check if the API key has the correct permissions
3. Ensure the endpoint URL is correct

### Issue: Environment variables not loading

**Solution:**
- **Linux/macOS:** Run `source ~/.bashrc` or restart terminal
- **Windows:** Restart terminal/IDE after setting environment variables
- Verify with `echo` or `$env:` commands

---

## 📄 Configuration Priority

.NET loads configuration in this order (later overrides earlier):

1. `appsettings.json`
2. `appsettings.{Environment}.json` (e.g., `appsettings.Development.json`)
3. **Environment Variables** (highest priority)
4. User Secrets (Development only)
5. Command-line arguments

---

## 🔒 Security Best Practices

1. ✅ **Never commit API keys** to version control
2. ✅ **Use environment variables** or User Secrets for sensitive data
3. ✅ **Use different keys** for development and production
4. ✅ **Rotate API keys** periodically
5. ✅ **Restrict key permissions** in Azure to minimum required
6. ✅ **Add `.env` files** to `.gitignore`

---

## 📚 Additional Resources

- [ASP.NET Core Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
- [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Environment Variables in Windows](https://learn.microsoft.com/windows/win32/procthread/environment-variables)
- [Linux Environment Variables](https://wiki.archlinux.org/title/Environment_variables)

---

## 📄 License

This project is part of the Microsoft Agent Framework learning materials.

