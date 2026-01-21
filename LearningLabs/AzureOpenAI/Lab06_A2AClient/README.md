# Lab 06 - A2A Client (Agent-to-Agent Communication)

## Objective

In this lab, you will learn how to create an **A2A (Agent-to-Agent) Client** using the Microsoft Agents Framework with Azure OpenAI.

The A2A Client connects to an A2A Server and enables communication with remote agents. You'll learn how to discover remote agent capabilities, send requests, and receive responses - enabling distributed agent systems where specialized agents can collaborate to solve complex tasks.

You will implement:
- **Scenario 1**: Connect to a remote Auth Agent and interact with it (generate and validate API keys)

## What You Will Learn

- How to create an A2A client that connects to remote agents
- How to use `A2AClient` to communicate with A2A servers
- How to get agent information using `GetAIAgent()` method
- How to send requests to remote agents using natural language
- How to interact with remote agent tools without knowing their implementation

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- **Lab06_A2AServer running** on http://localhost:5000

## Project Structure

```
Lab06_A2AClient/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── appsettings.json            <-- Update with your settings
│   ├── AzureOpenAISettings.cs      <-- Provided (do not modify)
│   ├── RemoteAuthAgentSettings.cs  <-- Provided (do not modify)
│   └── ConfigurationHelper.cs      <-- Provided (do not modify)
└── Solution/                       <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 0: Start the A2A Server

**IMPORTANT**: Before running the client, you must start the A2A Server from Lab06_A2AServer:

```bash
# Open a new terminal window
cd ../Lab06_A2AServer/Start   # or Solution
dotnet run
```

The server should display:
```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

**Keep this terminal running!** The client needs the server to be active.

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the Azure OpenAI values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "APIKey": ""
  },
  "RemoteAuthAgentSettings": {
    "Name": "AuthAgent",
    "Description": "An authentication agent specialized in generating and validating API keys. Only handles authentication-related tasks.",
    "Url": "http://localhost:5000/a2a/authAgent"
  }
}
```

#### Authentication Options

The labs support two authentication methods:

1. **API Key Authentication** (recommended for local development):
   - Set `APIKey` in `appsettings.json` with your Azure OpenAI API key

2. **DefaultAzureCredential** (for Azure-hosted apps):
   - Leave `APIKey` empty
   - Requires Azure CLI login (`az login`) or managed identity

The Solution folder includes the conditional logic to handle both methods.

> **Note**: The `RemoteAuthAgentSettings.Url` should point to the A2A Server endpoint. If you changed the server port or path, update it accordingly.

### Step 2: Complete Scenario 1 - Connect to Remote Agent

Open `Start/Program.cs` and complete the TODOs to create an A2A client that connects to the remote Auth Agent:

| TODO | Description | Hint |
|------|-------------|------|
| TODO 1 | Create a remote agent using the A2AClient | Use `CreateRemoteAgentUsingUrlAsync()` with `remoteAuthAgentSettings` |
| TODO 2 | Display the remote agent name | Use `remoteAuthAgent.Name` |
| TODO 3 | Generate an API key using the remote agent | Call `GenerateApiKeyAsync(remoteAuthAgent)` |
| TODO 4 | Validate the generated API key | Call `ValidateApiKeyAsync(remoteAuthAgent, apiKey)` |

### Step-by-Step Guide

The key to understanding this lab is recognizing two ways to connect to remote agents:

#### Method 1: Using A2AClient with Direct URL

This is the simplest method - directly connect to the agent URL:

```csharp
var a2aClient = new A2A.A2AClient(new Uri(remoteAuthAgentSettings.Url));
return a2aClient.GetAIAgent();
```

#### Method 2: Using A2ACardResolver (Alternative)

This method fetches the agent card first, then creates the client:

```csharp
var agentCardResolver = new A2ACardResolver(
    new Uri(remoteAuthAgentSettings.Url), 
    new HttpClient { Timeout = TimeSpan.FromSeconds(60) }
);
return await agentCardResolver.GetAIAgentAsync();
```

Both methods return an `AIAgent` that you can interact with using natural language!

#### TODO 1-2: Create and Display Remote Agent

The agent is created using one of the methods above. Once you have the `AIAgent` instance, you can access its properties:

```csharp
ColoredConsole.WriteInfoLine($"Remote Auth Agent: {remoteAuthAgent.Name}");
```

#### TODO 3: Generate an API Key

To interact with the remote agent, simply use `RunAsync()` with natural language:

```csharp
var agentRunResponse = await remoteAuthAgent.RunAsync("Generate a new API key");
```

The remote agent will execute its `GenerateAPIKey` tool and return the result. Extract the response:

```csharp
string apiKey = agentRunResponse.Messages
    .Where(m => !string.IsNullOrWhiteSpace(m.Text))
    .Last()
    .Text;
```

#### TODO 4: Validate the API Key

Similarly, you can validate the API key by asking the agent:

```csharp
var agentRunResponse = await remoteAuthAgent.RunAsync($"Validate this API key: {apiKey}");
```

The remote agent will execute its `ValidateAPIKey` tool and tell you if the key is valid.

### Step 3: Run and Test

```bash
cd Start
dotnet run
```

## Expected Output

When you run the client successfully, you should see:

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
Remote Auth Agent: AuthAgent
Remote Auth Agent Description: An authentication agent specialized in generating and validating API keys...
Remote Auth Agent URL: http://localhost:5000/a2a/authAgent
----------------------------------------
Remote Auth Agent: AuthAgent
Scenario 1: Generate an API key using the remote agent
Generated API Key: Meknes_A3f8Kp2...
Scenario 2: Validate the API key using the remote agent
Validated API Key: The API key is valid.
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `A2AClient` | Client for connecting to A2A server endpoints |
| `A2ACardResolver` | Fetches agent card and creates client from it |
| `GetAIAgent()` | Returns an `AIAgent` instance representing the remote agent |
| `RunAsync()` | Sends a natural language request to the remote agent |
| `RemoteAuthAgentSettings` | Configuration for connecting to a remote agent |

## How A2A Communication Works

```
┌─────────────────────────────────────────────────────────────────────┐
│                        A2A COMMUNICATION FLOW                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  CLIENT (Lab06_A2AClient)                                          │
│  ┌───────────────────────────────────────────────────────────┐     │
│  │  1. Create A2AClient with remote URL                      │     │
│  │     var a2aClient = new A2AClient(remoteUrl);             │     │
│  │                                                            │     │
│  │  2. Get AIAgent instance                                  │     │
│  │     var agent = a2aClient.GetAIAgent();                   │     │
│  │                                                            │     │
│  │  3. Send natural language request                         │     │
│  │     var response = await agent.RunAsync("Generate key");  │     │
│  └───────────────────────────────────────────────────────────┘     │
│                              │                                      │
│                              │ HTTP Request                         │
│                              ▼                                      │
│  SERVER (Lab06_A2AServer)                                          │
│  ┌───────────────────────────────────────────────────────────┐     │
│  │  1. Receives request at /a2a/authAgent                    │     │
│  │                                                            │     │
│  │  2. AuthAgent processes the request                       │     │
│  │     - Understands "Generate key" intent                   │     │
│  │     - Executes GenerateAPIKey tool                        │     │
│  │     - Returns: "Meknes_A3f8Kp2..."                        │     │
│  └───────────────────────────────────────────────────────────┘     │
│                              │                                      │
│                              │ HTTP Response                        │
│                              ▼                                      │
│  CLIENT receives response and displays it                          │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Two Ways to Create Remote Agents

This lab demonstrates two methods for connecting to remote agents:

### Method 1: Direct URL Connection (Simpler)

```csharp
async Task<AIAgent> CreateRemoteAgentUsingUrlAsync(RemoteAuthAgentSettings settings)
{
    var a2aClient = new A2A.A2AClient(new Uri(settings.Url));
    return a2aClient.GetAIAgent();
}
```

**Pros:**
- Simple and direct
- Fewer lines of code
- Synchronous creation

**Use when:** You know the exact agent URL

### Method 2: Agent Card Resolver (More Flexible)

```csharp
async Task<AIAgent> CreateRemoteAgentUsingResolverAsync(RemoteAuthAgentSettings settings)
{
    var agentCardResolver = new A2ACardResolver(
        new Uri(settings.Url), 
        new HttpClient { Timeout = TimeSpan.FromSeconds(60) }
    );
    return await agentCardResolver.GetAIAgentAsync();
}
```

**Pros:**
- Fetches agent card with capabilities and skills
- Can inspect agent metadata before connecting
- More control over HTTP client configuration

**Use when:** You need to discover agent capabilities first

## The Power of A2A

Notice how the client doesn't need to know:
- ✅ The implementation details of `GenerateAPIKey` or `ValidateAPIKey`
- ✅ The cryptographic algorithms used (HMAC-SHA256)
- ✅ The secret key for signing

The client simply:
1. Connects to the remote agent
2. Sends natural language requests
3. Receives results

This is the power of **Agent-to-Agent communication** - agents can use other agents' capabilities without understanding their internal implementation!

## Provided Files

The following files are provided and **should not be modified**:

| File | Description |
|------|-------------|
| `AzureOpenAISettings.cs` | Azure OpenAI configuration model |
| `RemoteAuthAgentSettings.cs` | Settings for connecting to remote agent |
| `ConfigurationHelper.cs` | Helper to read configuration |
| `A2AClient.csproj` | Project file with all required dependencies |

The following file **should be modified** with your values:

| File | What to modify |
|------|----------------|
| `appsettings.json` | Update `Endpoint` and `ChatDeploymentName` |

## Troubleshooting

### Connection Refused / Cannot Connect

**Symptom:** `HttpRequestException: Connection refused`

**Solution:** 
- Make sure Lab06_A2AServer is running
- Check that it's listening on http://localhost:5000
- Verify the URL in `appsettings.json` matches the server URL

### Server Not Running

**Symptom:** The client hangs or times out

**Solution:**
```bash
# In a separate terminal
cd ../Lab06_A2AServer/Start
dotnet run
```

### Wrong Agent URL

**Symptom:** 404 Not Found

**Solution:** Check that the URL in `RemoteAuthAgentSettings.Url` matches the server endpoint:
- Correct: `http://localhost:5000/a2a/authAgent`
- Wrong: `http://localhost:5000/authAgent` (missing `/a2a/`)

### Authentication Error

**Solution:**
- Ensure you're logged in with Azure CLI: `az login`
- Verify your Azure OpenAI endpoint is correct

## Testing Agent Card Discovery

You can manually test agent card discovery using curl:

```bash
# Get the agent card
curl http://localhost:5000/.well-known/a2a/authAgent/agent.json
```

This returns the agent's capabilities, skills, and metadata in JSON format.

## Next Steps

After completing this lab, you can:
1. Create your own custom agents with different tools
2. Build multi-agent systems where agents collaborate
3. Implement agent orchestration patterns
4. Create client applications that consume multiple A2A agents

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.
