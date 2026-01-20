# End-to-End App Demo

A complete full-stack application demonstrating AI Agents management with both **backend** and **frontend** components.

## 🚀 Quick Start

**New to this project?** Start here:
- **[QUICK_START.md](./QUICK_START.md)** ⭐ - Get running in 5 minutes!

## 📋 Overview

This demo showcases how to build a full-stack application integrating:

- **[Backend](./AIAgentsBackend/README.md)** - .NET Web API with Azure OpenAI integration
- **[Frontend](./AIAgentsFrontend/README.md)** - React + TypeScript + Vite application

## 🔐 Security Features

- **Signed Context IDs** - HMAC-SHA256 signature validation ensures request integrity
- **ThreadRepository** - Clean MongoDB data access layer
- See **[CONTEXT_ID_SIGNING.md](./CONTEXT_ID_SIGNING.md)** for complete documentation

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Frontend (React)                     │
│         http://localhost:3000                           │
│  • Translation Agent UI                                 │
│  • Customer Support UI                                  │
│  • History Agent UI                                     │
└─────────────────┬───────────────────────────────────────┘
                  │
                  │ REST API
                  │
┌─────────────────▼───────────────────────────────────────┐
│              Backend (.NET Web API)                     │
│         http://localhost:5016                           │
│  • Agent Management APIs                                │
│  • Azure OpenAI Integration                             │
│  • MongoDB Conversation History                         │
└─────────────────┬───────────────────────────────────────┘
                  │
        ┌─────────┴──────────┐
        │                    │
┌───────▼──────┐   ┌────────▼─────────┐
│ Azure OpenAI │   │     MongoDB      │
│   Service    │   │  (Conversations) │
└──────────────┘   └──────────────────┘
```

## 🚀 Quick Start

### 1. Start the Backend

See [Backend README](./AIAgentsBackend/README.md) for detailed instructions.

**Quick steps:**

```bash
# Configure environment variables (Linux/macOS)
export AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com/"
export AzureOpenAI__DefaultChatDeploymentName="gpt-4o-mini"
export AzureOpenAI__APIKey="YOUR-API-KEY"
export AzureOpenAI__DefaultEmbeddingDeploymentName="text-embedding-3-small"

# Navigate to backend
cd AIAgentsBackend

# Run the backend
dotnet run
```

Backend will be available at: **http://localhost:5016**

### 2. Start the Frontend

See [Frontend README](./AIAgentsFrontend/README.md) for detailed instructions.

**Quick steps:**

```bash
# Navigate to frontend
cd AIAgentsFrontend

# Install dependencies (first time only)
npm install

# Run the development server
npm run dev
```

Frontend will be available at: **http://localhost:3000**

### 3. Access the Application

- **Frontend UI**: http://localhost:3000
- **Backend API**: http://localhost:5016
- **API Documentation (Swagger)**: http://localhost:5016/swagger

## 📚 Documentation

### Backend Documentation
**[→ Backend README](./AIAgentsBackend/README.md)**

Covers:
- Environment variables setup (Linux/Windows)
- Azure OpenAI configuration
- MongoDB setup
- API endpoints
- Running the backend

### Frontend Documentation
**[→ Frontend README](./AIAgentsFrontend/README.md)**

Covers:
- Node.js installation (NVM, direct, package managers)
- Project setup
- Running the development server
- Building for production
- Troubleshooting

## 🎯 Features

### AI Agents

1. **Translation Agent**
   - Multi-language translation
   - Context-aware translations
   - Preserves tone and style

2. **Customer Support Agent**
   - Handles customer inquiries
   - Provides product information
   - Troubleshooting assistance

3. **History Agent**
   - Historical knowledge expert
   - Conversation memory
   - Detailed historical information

## 📦 Prerequisites

### Backend
- .NET 8.0 SDK or later
- Azure OpenAI resource
- MongoDB (optional, for conversation history)

### Frontend
- Node.js 20.19+ or 22.12+
- npm, yarn, or pnpm

## 🔧 Configuration

### Backend Configuration

Environment variables (recommended):

```bash
# Linux/macOS (.bashrc)
export AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com/"
export AzureOpenAI__DefaultChatDeploymentName="gpt-4o-mini"
export AzureOpenAI__APIKey="YOUR-API-KEY"
export AzureOpenAI__DefaultEmbeddingDeploymentName="text-embedding-3-small"
```

```powershell
# Windows (PowerShell Profile)
$env:AzureOpenAI__Endpoint = "https://YOUR-RESOURCE.openai.azure.com/"
$env:AzureOpenAI__DefaultChatDeploymentName = "gpt-4o-mini"
$env:AzureOpenAI__APIKey = "YOUR-API-KEY"
$env:AzureOpenAI__DefaultEmbeddingDeploymentName = "text-embedding-3-small"
```

See [Backend README](./AIAgentsBackend/README.md) for complete configuration guide.

### Frontend Configuration

The frontend automatically connects to the backend at `http://localhost:5016`.

To change the API endpoint, update the configuration in the frontend source code.

## 🐛 Troubleshooting

### Backend not starting

1. Check environment variables are set correctly
2. Verify Azure OpenAI endpoint and API key
3. Check if port 5016 is available
4. See [Backend README - Troubleshooting](./AIAgentsBackend/README.md#-troubleshooting)

### Frontend not starting

1. Check Node.js version: `node --version` (should be 20.19+ or 22.12+)
2. Clear cache: `rm -rf node_modules package-lock.json && npm install`
3. Check if port 3000 is available
4. See [Frontend README - Troubleshooting](./AIAgentsFrontend/README.md#-troubleshooting)

### Connection issues

1. Ensure backend is running before starting frontend
2. Check CORS configuration in backend
3. Verify API URLs in frontend configuration

## 🔒 Security Notes

- **Never commit API keys** to version control
- Use environment variables for sensitive configuration
- Different keys for development and production
- Rotate API keys periodically
- Add `.env` files to `.gitignore`

## 📄 Project Structure

```
EndToEndAppDemo/
├── README.md                    # This file
├── AIAgentsBackend/            # .NET Web API Backend
│   ├── README.md               # Backend documentation
│   ├── Program.cs
│   ├── appsettings.json
│   ├── Controllers/
│   ├── Agents/
│   ├── Configuration/
│   └── MongoDB/
│       └── docker-compose.yml  # MongoDB setup
└── AIAgentsFrontend/           # React Frontend
    ├── README.md               # Frontend documentation
    ├── package.json
    ├── vite.config.ts
    ├── src/
    │   ├── App.tsx
    │   ├── components/
    │   └── ...
    └── public/
```

## 📚 Learning Path

1. **Start with Backend** - Configure and run the API
2. **Then Frontend** - Install Node.js and run the UI
3. **Test Integration** - Use the UI to interact with agents
4. **Explore APIs** - Check Swagger documentation
5. **Customize** - Modify agents and add new features

## 📄 License

This project is part of the Microsoft Agent Framework learning materials.
