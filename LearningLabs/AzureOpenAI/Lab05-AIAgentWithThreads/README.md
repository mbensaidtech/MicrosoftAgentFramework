# Lab 05 - AI Agent with Threads (Conversation Persistence)

## Objective

In this lab, you will learn how to create an AI Agent with **conversation thread persistence** using the Microsoft Agents Framework with Azure OpenAI.

You will implement two scenarios:
1. **InMemory Storage** - Messages stored in memory (lost when app stops)
2. **MongoDB Storage** - Messages persisted to MongoDB (survives app restarts)

The key concept is **thread serialization and deserialization** - the ability to save a conversation state (thread ID) and restore it later to continue the conversation with full context.

## What You Will Learn

- How to create a custom `ChatMessageStore` for thread persistence
- How to use `InMemoryVectorStore` for development/testing
- How to use `MongoVectorStore` for production persistence
- How to serialize and deserialize agent threads
- How to extract thread IDs from serialized state
- How to restore a thread and continue a conversation

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- Docker installed (for MongoDB in Scenario 2)

## Project Structure

```
Lab05-AIAgentWithThreads/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── appsettings.json            <-- Update with your settings
│   ├── Configuration/
│   │   ├── AgentSettings.cs        <-- Provided (do not modify)
│   │   ├── AzureOpenAISettings.cs  <-- Provided (do not modify)
│   │   └── MongoDbConfiguration.cs <-- Provided (do not modify)
│   ├── ConfigurationHelper.cs      <-- Provided (do not modify)
│   ├── models/
│   │   └── AgentThreadState.cs     <-- Provided (do not modify)
│   ├── Stores/
│   │   ├── ChatHistoryItem.cs              <-- Provided (do not modify)
│   │   ├── InMemoryVectorChatMessageStore.cs  <-- Provided (do not modify)
│   │   └── MongoVectorChatMessageStore.cs     <-- Provided (do not modify)
│   └── MongoDB/
│       └── docker-compose.yml      <-- Run this to start MongoDB
└── Solution/                       <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 0: Start MongoDB (Required for Scenario 2)

Before running Scenario 2, you need to start MongoDB using Docker:

```bash
cd Start/MongoDB
docker compose up -d
```

This starts:
- **MongoDB** on port `27017` (database server)
- **Mongo Express** on port `8081` (web UI to view data)

You can access the Mongo Express UI at: http://localhost:8081
- Username: `admin`
- Password: `admin`

To stop MongoDB when done:
```bash
docker compose down
```

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the Azure OpenAI values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "Agents": {
      "GlobalAgent": {
        "Name": "GlobalAgent",
        "Instructions": "You are a global agent that can answer questions about any topic. The response should be very short and concise."
      }
    }
  },
  "MongoDb": {
    "ConnectionString": "mongodb://admin:password123@localhost:27017",
    "DatabaseName": "AIAgentThreads"
  }
}
```

> **Note**: The MongoDB settings are pre-configured to work with the provided docker-compose.yml. Only modify them if you have a different MongoDB setup.

### Step 2: Complete Scenario 1 (InMemory Storage)

Open `Start/Program.cs` and complete TODOs 1-5 in the Scenario 1 region:

| TODO | Description |
|------|-------------|
| TODO 1 | Create an `InMemoryVectorStore` instance |
| TODO 2 | Get agent settings using `ConfigurationHelper.GetAgent("GlobalAgent")` |
| TODO 3 | Create `ChatClientAgentOptions` with instructions, name, and `ChatMessageStoreFactory` |
| TODO 4 | Create the AI Agent using `chatClient.CreateAIAgent(agentOptions)` |
| TODO 5 | Call `RunThreadConversationTestAsync(agent)` |

### Step 3: Complete Scenario 2 (MongoDB Storage)

Complete TODOs 6-10 in the Scenario 2 region:

| TODO | Description |
|------|-------------|
| TODO 6 | Create a `MongoVectorStore` using `ConfigurationHelper.GetMongoDatabase()` |
| TODO 7 | Get agent settings using `ConfigurationHelper.GetAgent("GlobalAgent")` |
| TODO 8 | Create `ChatClientAgentOptions` with `MongoVectorChatMessageStore` factory |
| TODO 9 | Create the AI Agent |
| TODO 10 | Call `RunThreadConversationTestAsync(agent)` |

### Step 4: Implement Helper Methods

Complete the helper methods that handle the thread conversation flow:

#### `RunThreadConversationTestAsync` (TODOs 11-16)
This method orchestrates the complete conversation test:
1. Creates a new thread
2. Asks a first question
3. Extracts the thread ID
4. Restores the thread from the ID
5. Asks a follow-up question (to test context is preserved)
6. Displays token usage

#### `AskQuestionAsync` (TODOs 17-20)
- Display the question label
- Run the agent with `agent.RunAsync(question, thread)`
- Display the response
- Return the response

#### `ExtractAndDisplayThreadId` (TODOs 21-24)
- Serialize the thread using `thread.Serialize()`
- Extract the thread ID using `ExtractThreadIdFromState`
- Display the extracted ID
- Return the ID

#### `RestoreThreadFromId` (TODOs 25-28)
- Create an `AgentThreadState` with the thread ID
- Serialize it to `JsonElement`
- Deserialize the thread using `agent.DeserializeThread()`
- Return the restored thread

#### `DisplayTokenUsage` (TODOs 29-31)
- Display input, output, and total tokens

> **Note**: The `ExtractThreadIdFromState` method is already implemented for you as it contains complex JSON parsing logic.

### Step 5: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `ChatMessageStore` | Base class for storing conversation messages |
| `InMemoryVectorStore` | In-memory vector store (volatile) |
| `MongoVectorStore` | MongoDB-backed vector store (persistent) |
| `ChatClientAgentOptions` | Options for creating an agent with thread support |
| `ChatMessageStoreFactory` | Factory delegate to create message stores |
| `thread.Serialize()` | Serializes thread state to JSON |
| `agent.DeserializeThread()` | Restores a thread from serialized state |
| `AgentThreadState` | Model class containing the thread ID (StoreState) |

## How Thread Persistence Works

```
┌─────────────────────────────────────────────────────────────────┐
│                    CONVERSATION FLOW                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. CREATE NEW THREAD                                           │
│     var thread = agent.GetNewThread();                          │
│                                                                 │
│  2. ASK FIRST QUESTION                                          │
│     await agent.RunAsync("What is the capital of France?", thread);
│     → Response: "Paris is the capital of France"                │
│                                                                 │
│  3. SERIALIZE THREAD STATE                                      │
│     var state = thread.Serialize();                             │
│     → Contains: { "storeState": "abc123..." }                   │
│                                                                 │
│  4. EXTRACT THREAD ID                                           │
│     var threadId = ExtractThreadIdFromState(state);             │
│     → threadId = "abc123..."                                    │
│                                                                 │
│  5. RESTORE THREAD (simulating app restart)                     │
│     var agentThreadState = new AgentThreadState { StoreState = threadId };
│     var restoredThread = agent.DeserializeThread(...);          │
│                                                                 │
│  6. ASK FOLLOW-UP QUESTION                                      │
│     await agent.RunAsync("What is the population of that city?", restoredThread);
│     → Response: "Paris has a population of about 2.1 million"   │
│     ✅ Agent remembers "that city" = Paris!                     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## InMemory vs MongoDB: Key Differences

| Aspect | InMemory | MongoDB |
|--------|----------|---------|
| **Persistence** | Lost when app stops | Survives app restarts |
| **Use Case** | Development/Testing | Production |
| **Setup** | No setup needed | Requires MongoDB running |
| **Multi-instance** | Not shared | Shared across instances |
| **Timestamp** | Seconds precision | Milliseconds precision |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Create an AI Agent with Threads using the InMemory vector store ===
First Question: What is the capital of France?
Response: Paris is the capital of France.
Extracted thread id: a1b2c3d4e5f6...
----------------------------------------
Restoring thread from thread id to test context preservation...
Follow-up Question: What is the population of that city?
Response: Paris has a population of approximately 2.1 million people.
----------------------------------------
Token Usage (Follow-up): 
  Input tokens: 125
  Output tokens: 45
  Total tokens: 170
----------------------------------------
=== Scenario 2: Create an AI Agent with Threads using the MongoDB vector store ===
...
```

## Provided Files

The following files are provided and **should not be modified**:

| File | Description |
|------|-------------|
| `Configuration/AgentSettings.cs` | Agent configuration model |
| `Configuration/AzureOpenAISettings.cs` | Azure OpenAI configuration model |
| `Configuration/MongoDbConfiguration.cs` | MongoDB configuration model |
| `ConfigurationHelper.cs` | Helper to read configuration |
| `models/AgentThreadState.cs` | Thread state model for serialization |
| `Stores/ChatHistoryItem.cs` | Vector store item model |
| `Stores/InMemoryVectorChatMessageStore.cs` | In-memory message store implementation |
| `Stores/MongoVectorChatMessageStore.cs` | MongoDB message store implementation |
| `MongoDB/docker-compose.yml` | Docker Compose for MongoDB |
| `AIAgentWithThreads.csproj` | Project file with dependencies |

The following file **should be modified** with your values:

| File | What to modify |
|------|----------------|
| `appsettings.json` | Update `Endpoint` and `ChatDeploymentName` |

## Troubleshooting

### MongoDB Connection Failed
- Ensure Docker is running
- Run `docker compose up -d` in the MongoDB folder
- Check if ports 27017 and 8081 are available

### Port Already in Use
If you see "port is already allocated":
```bash
# Find what's using the port
docker ps -a --filter "publish=27017"
# Stop the conflicting container
docker stop <container_name>
```

### Follow-up Question Doesn't Remember Context
- Ensure you're using the restored thread, not the original
- Check that the thread ID was correctly extracted and passed

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.
