# Lab 03 - AI Agent With Function Tools

## Objective

In this lab, you will create an AI Agent that uses **Function Tools** (also known as Function Calling) using the **Microsoft Agents Framework** with **Azure OpenAI**.

You will learn how to give an agent access to external data and systems by defining function tools that the agent can call during its reasoning process.

## What You Will Learn

- How to configure an Azure OpenAI client with managed identity authentication
- How to create function tools using `AIFunctionFactory.Create`
- How to register tools with an agent (basic approach)
- How to discover and register tools using reflection
- How to use dependency injection with static tool methods
- How to pass a `ServiceProvider` to tools for resolving dependencies

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab03-AIAgentWithFunctionTools/
├── README.md
├── Start/                      <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── Tools/
│   │   ├── CompanyTools.cs     <-- Provided: employee & meeting room tools
│   │   └── NotificationTools.cs <-- Provided: notification tools (static)
│   ├── Repositories/
│   │   ├── INotificationRepository.cs
│   │   └── InMemoryNotificationRepository.cs
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithFunctionTools.csproj
└── Solution/                   <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "APIKey": ""
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

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

| Step | Description | Hints |
|------|-------------|-------|
| **Step 2** | Create `AzureOpenAIClient` with managed identity authentication | • Constructor: `new AzureOpenAIClient(Uri endpoint, TokenCredential credential)` <br> • Use `new Uri(settings.Endpoint)` for the endpoint <br> • Use `new DefaultAzureCredential()` for authentication |
| **Step 3** | Get a `ChatClient` for the deployment | • Method: `client.GetChatClient(string deploymentName)` <br> • Use `settings.ChatDeploymentName` |

---

#### Scenario 1: Function Tools Calling - Basic

In this scenario, you manually create each tool using `AIFunctionFactory.Create` and pass them to the agent.

**First**, open `Tools/CompanyTools.cs` and add `[Description]` attributes to the `GetEmployeeInfo` method:

| Step | Description | Value to Use |
|------|-------------|--------------|
| **Step 0.1** | Add `[Description]` attribute on the method | `"Retrieves detailed information about an employee using their employee ID (e.g., EMP001, EMP002)."` |
| **Step 0.2** | Add `[Description]` attribute on the `employeeId` parameter | `"The employee ID (e.g., EMP001)"` |

**Example:**
```csharp
[Description("Retrieves detailed information about an employee using their employee ID (e.g., EMP001, EMP002).")]
public string GetEmployeeInfo(
    [Description("The employee ID (e.g., EMP001)")] string employeeId)
```

**Then**, complete the Program.cs TODOs:

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Create agent with tools | • Instantiate: `new CompanyTools()` <br> • Use `chatClient.CreateAIAgent(instructions: "...", tools: [...])` <br> • Create tools: `AIFunctionFactory.Create(instance.MethodName, "tool_name")` |
| **Step 2** | Run the agent with a prompt | • Method: `await agent.RunAsync(string prompt)` |
| **Step 3** | Display the response | • Use `response.ToString()` |
| **Step 4** | Display token usage | • Use `ColoredConsole.WriteBurgundyLine("Token Usage: ")` <br> • Access: `response.Usage?.InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` |

---

#### Scenario 2: Function Tools Calling - Using Reflection

In this scenario, you automatically discover all public instance methods from a class and register them as tools.

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Create tools list using reflection | |
| **Step 1.0** | Create an instance of CompanyTools | • `new CompanyTools()` |
| **Step 1.1** | Get all public instance methods using reflection | • `typeof(CompanyTools).GetMethods(BindingFlags.Public \| BindingFlags.Instance \| BindingFlags.DeclaredOnly)` |
| **Step 1.2** | Create a list of AITool from the methods | • Use LINQ with `AIFunctionFactory.Create(method, instance)` |
| **Step 1.3** | Display the tools | • Use `string.Join(", ", tools.Select(t => t.Name))` |
| **Step 2** | Create agent with tools | • `chatClient.CreateAIAgent(instructions: "...", tools: tools)` |
| **Step 3** | Run the agent with a prompt | • Method: `await agent.RunAsync(string prompt)` |
| **Step 4** | Display the response | • Use `response.ToString()` |
| **Step 5** | Display token usage | • Use `ColoredConsole.WriteBurgundyLine("Token Usage: ")` <br> • Same pattern as Scenario 1 |

---

#### Scenario 3: Function Tools Calling - Static Tools with Dependency Injection

In this scenario, you use static methods as tools and inject dependencies via `IServiceProvider`.

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Configure services using ServiceCollection | • Create: `new ServiceCollection()` <br> • Register: `services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>()` |
| **Step 2** | Build the ServiceProvider | • Method: `services.BuildServiceProvider()` |
| *(no step)* | Create agent with static tools and serviceProvider | • Create tools from static methods <br> • Pass `services: serviceProvider` to CreateAIAgent |
| **Step 3** | Run the agent | • Method: `await agent.RunAsync(string prompt)` |
| **Step 4** | Display the response | • Use `response.ToString()` |
| **Step 5** | Display token usage | • Use `ColoredConsole.WriteBurgundyLine("Token Usage: ")` <br> • Same pattern as previous scenarios |

---

### Step 3: Run and Test

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

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AzureOpenAIClient` | Client to connect to Azure OpenAI service |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `ChatClient` | Client for chat completions with a specific deployment |
| `ChatClientAgent` | Microsoft Agents wrapper around ChatClient |
| `Function Tools` | Functions that the agent can call to perform actions |
| `AIFunctionFactory.Create` | Creates an AIFunction from a method (instance or static) |
| `AITool` | Base type for tools that can be registered with an agent |
| `[Description]` attribute | Describes the function/parameter to the LLM |
| `ServiceCollection` | Microsoft DI container for registering services |
| `IServiceProvider` | Resolves dependencies at runtime |
| `BindingFlags` | Flags used with reflection to filter methods |

## Optional: Using the Console Spinner

The `CommonUtilities` library provides a `ConsoleSpinner` that shows a loading animation while the agent is processing. This is **optional** but improves the user experience.

**Usage with extension method:**
```csharp
// Simply chain .WithSpinner() to any async Task
AgentRunResponse response = await agent.RunAsync("your prompt")
    .WithSpinner("Running agent");
```

**Usage with using statement:**
```csharp
using var spinner = new ConsoleSpinner("Processing request");
AgentRunResponse response = await agent.RunAsync("your prompt");
// Spinner automatically stops when disposed
```

The spinner displays an animated indicator with elapsed time: `⠋ Running agent... [00:03]`

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `AgentRunResponse` |
| `Microsoft.Extensions.AI` | Provides `AIFunctionFactory`, `AITool` |
| `Microsoft.Extensions.DependencyInjection` | Provides `ServiceCollection`, `IServiceProvider` |
| `System.Reflection` | Provides `MethodInfo`, `BindingFlags` |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

```
Azure OpenAI Settings: 
Azure OpenAI Endpoint: https://your-resource.openai.azure.com/
Azure OpenAI Chat Deployment Name: your-deployment-name

────────────────────────────────────────────────────

=== Scenario 1: Function tools calling - basic ===
Employee Found:
- ID: EMP001
- Name: Mohammed BEN SAID
- Department: Engineering
...

Token Usage: 
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx

────────────────────────────────────────────────────

=== Scenario 2: Function tools calling - using reflection ===
Tools that will be available to the agent: GetEmployeeInfo, GetMeetingRooms, BookMeetingRoom
...

Token Usage: 
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx

────────────────────────────────────────────────────

=== Scenario 3: Function tools calling - static tools with DI ===
Notification sent successfully!
...

Token Usage: 
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx
```

**Color Legend:**
- Scenario titles (`=== ... ===`) appear in **cyan**
- `Token Usage:` appears in **Burgundy (dark red)**
- Token values appear in **gray**

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithFunctionTools.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Tools/NotificationTools.cs` - Notification tools (static methods with DI)
- `Repositories/INotificationRepository.cs` - Repository interface
- `Repositories/InMemoryNotificationRepository.cs` - In-memory implementation

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Tools/CompanyTools.cs` - Add `[Description]` attributes to `GetEmployeeInfo` method (Scenario 1)
- `Program.cs` - Complete the TODOs

## Useful Links

- [Using function tools with an agent](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/function-tools?pivots=programming-language-csharp)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
