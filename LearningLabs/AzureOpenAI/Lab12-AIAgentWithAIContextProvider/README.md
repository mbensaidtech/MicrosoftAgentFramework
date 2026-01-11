# Lab 12 - AI Agent With AI Context Provider

## Objective

In this lab, you will create an AI Agent that uses an **AI Context Provider** to manage user memories. The agent will:
- Remember information about the user across conversations
- Extract new user data from messages and store it in MongoDB
- Use stored memories to personalize responses

## What You Will Learn

- How to implement a custom `AIContextProvider`
- How to use `InvokingAsync` to inject context before agent execution
- How to use `InvokedAsync` to process results after agent execution
- How to use structured output to extract user data
- How to persist user memories in MongoDB

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- Docker installed (for MongoDB)

## Project Structure

```
Lab12-AIAgentWithAIContextProvider/
├── README.md
├── Start/                              <-- Your working folder
│   ├── Program.cs                      <-- Complete the TODOs here
│   ├── UserMemoryAIContextProvider.cs  <-- Complete the TODOs here
│   ├── Configuration/
│   │   ├── AzureOpenAISettings.cs
│   │   └── MongoDbConfiguration.cs
│   ├── Models/
│   │   ├── UserMemory.cs
│   │   └── UserMemoryDocument.cs
│   ├── ConfigurationHelper.cs
│   ├── IUserMemoriesRepository.cs
│   ├── UserMemoriesRepository.cs
│   ├── MongoDB/
│   │   └── docker-compose.yml
│   ├── appsettings.json
│   └── AIAgentWithAIContextProvider.csproj
└── Solution/                           <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Start MongoDB

Navigate to the MongoDB folder and start the containers:

```bash
cd Start/MongoDB
docker-compose up -d
```

This will start:
- **MongoDB** on port `27017`
- **Mongo Express** (web UI) on port `8081` - http://localhost:8081 (admin/admin)

### Step 2: Configure your settings

Open `Start/appsettings.json` and update the Azure OpenAI values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 3: Complete the UserMemoryAIContextProvider

Open `Start/UserMemoryAIContextProvider.cs` and implement the methods.

---

#### InvokingAsync - Called BEFORE the agent runs

This method is called before each agent execution. Use it to load user memories and inject them as context.

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Get user memories from the repository | Use `_userMemoriesRepository.GetUserMemoriesAsync(_userId)` |
| **Step 2** | Return memories as instructions | Return `new AIContext { Instructions = ... }` with joined memories |

**Example for Step 2:**
```csharp
return new AIContext { Instructions = string.Join("\n", _userMemories.Select(x => x.Memory)) };
```

---

#### InvokedAsync - Called AFTER the agent runs

This method is called after each agent execution. Use it to extract user data from the conversation and save it.

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Get the last user message | Use `context.RequestMessages.LastOrDefault(x => x.Role == ChatRole.User)` |
| **Step 2** | Create messages for the extractor agent | Create a `List<ChatMessage>` with a system message and the user message |
| **Step 3** | Run the extractor with structured output | Use `_userDataExtractorAgent.RunAsync<List<UserMemory>>(messages)` |
| **Step 4** | Save extracted memories | Use `_userMemoriesRepository.AddUserMemoriesAsync(response.Result)` |

**Example for Step 2 - Creating messages:**
```csharp
List<ChatMessage> messages = [
    new(ChatRole.System, $"this is what we know about the user: {string.Join("\n", _userMemories.Select(x => x.Memory))}. Don't extract the same info again. The userId is: {_userId}"),
    lastUserMessage,
];
```

---

### Step 4: Complete the Program.cs (Scenario 1)

Open `Start/Program.cs` and implement the scenario steps.

---

#### Scenario 1: User Memory using AI Context Provider

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Create the user data extractor agent | Use `chatClient.CreateAIAgent(instructions: "...")` |
| **Step 2** | Create ChatOptions for the agent | Create `new ChatOptions { Instructions = "..." }` |
| **Step 3** | Create agent with AIContextProvider | Use `ChatClientAgentOptions` with `ChatOptions` and `AIContextProviderFactory` |
| **Step 4** | Create a new thread | Use `agentWithMemory.GetNewThread()` |
| **Step 5** | Assistant starts conversation | Use `agentWithMemory.RunAsync(thread)` without user input |
| **Step 6** | User conversation loop | Loop: read input, create ChatMessage, run agent, display response |

**Example for Step 1 - Extractor agent instructions:**
```csharp
"You are a helpful assistant that can extract user data from a given text. If the info already exists in the context, don't extract it again. Extract only data that helps us to know the user better."
```

**Example for Step 2 - Agent with memory instructions:**
```csharp
"You are a helpful assistant that can use the user memory to answer questions. When you start a new conversation, start by introducing yourself and welcome the user to the conversation using the user name from the memories if there is any."
```

**Example for Step 3 - Creating agent with AIContextProvider:**
```csharp
ChatClientAgent agentWithMemory = chatClient.CreateAIAgent(new ChatClientAgentOptions
{
    ChatOptions = chatOptions,
    AIContextProviderFactory = _ => new UserMemoryAIContextProvider(userDataExtractorAgent, userId, userMemoriesRepository)
});
```

**Example for Step 6 - Conversation loop:**
```csharp
while (true)
{
    Console.Write("> ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrEmpty(userInput) || userInput == "exit")
        break;
    ChatMessage userMessage = new(ChatRole.User, userInput);
    AgentRunResponse response = await agentWithMemory.RunAsync(userMessage, thread);
    ColoredConsole.WriteAssistantLine(response.Text);
}
```

---

### Step 5: Run and Test

**Run the Start project (your implementation):**
```bash
cd Start
dotnet run
```

**Run the Solution (reference):**
```bash
cd Solution
dotnet run
```

### Step 6: Stop MongoDB

When you're done, stop the containers:

```bash
cd Start/MongoDB
docker-compose down
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AIContextProvider` | Base class for custom context providers |
| `InvokingAsync` | Called BEFORE agent execution - inject context |
| `InvokedAsync` | Called AFTER agent execution - process results |
| `AIContext` | Contains `Instructions` to inject into the agent |
| `AIContextProviderFactory` | Factory to create context provider instances |
| `RunAsync<T>` | Structured output - agent returns typed object |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Microsoft.Agents.AI` | Provides `AIContextProvider`, `AIContext`, `ChatClientAgent` |
| `Microsoft.Extensions.AI` | Provides `ChatMessage`, `ChatRole`, `ChatOptions` |
| `MongoDB.Driver` | MongoDB .NET driver |
| `CommonUtilities` | Provides `ColoredConsole`, `MongoDbHealthCheck` |

## Expected Output

```
Checking MongoDB connectivity...
MongoDB server is running.
Database 'AIAgentContext' is accessible.

────────────────────────────────────────────────────

=== Running Scenario 1: User Memory using AI Context Provider ===
Hello! I'm your helpful assistant. How can I assist you today?
> My name is Mohammed and I'm a software developer
Nice to meet you, Mohammed! It's great to know you're a software developer...
> What's my name?
Your name is Mohammed!
> exit
```

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithAIContextProvider.csproj` - Project file
- `ConfigurationHelper.cs` - Configuration loading
- `Configuration/` - Settings classes
- `Models/` - DTOs and documents
- `IUserMemoriesRepository.cs` - Repository interface
- `UserMemoriesRepository.cs` - MongoDB repository
- `MongoDB/docker-compose.yml` - Docker setup

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete Scenario 1 steps
- `UserMemoryAIContextProvider.cs` - Implement InvokingAsync and InvokedAsync

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
