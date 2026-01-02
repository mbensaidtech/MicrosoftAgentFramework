# Lab 09: AI Agent with Function Tools and Human Approval

## Overview

This lab demonstrates how to implement **human-in-the-loop** patterns for AI agents that execute sensitive operations. You'll learn how to require user approval before executing potentially dangerous function calls, such as deleting data.

## Learning Objectives

By completing this lab, you will:

1. Understand how to wrap functions with `ApprovalRequiredAIFunction` to require user consent
2. Learn how to use **threads** to maintain conversation context during the approval flow
3. Handle `UserInputRequestContent` and `FunctionApprovalRequestContent` to process approval requests
4. Build an interactive approval workflow for sensitive operations

## Key Concepts

### ApprovalRequiredAIFunction

The `ApprovalRequiredAIFunction` wrapper allows you to mark certain functions as requiring human approval before execution. When the AI agent decides to call such a function, the execution is paused and a `FunctionApprovalRequestContent` is returned instead.

### Threads for Conversation Context

To implement the approval flow, we use **threads** (`agent.GetNewThread()`) to maintain conversation context. This allows us to:
1. Run the initial prompt
2. Receive the approval request
3. Get user input
4. Continue the conversation with the approval/rejection response

### UserInputRequestContent

The `UserInputRequests` property of `AgentRunResponse` contains any pending user input requests, including function approval requests. You can filter these using `.OfType<FunctionApprovalRequestContent>()`.

## Prerequisites

- .NET 10.0 SDK
- Azure OpenAI resource with a deployed chat model
- Azure credentials configured (using DefaultAzureCredential)

## Project Structure

```
Lab09-AIAgentWithFunctionToolsHumanApproval/
├── README.md
├── Solution/                    # Complete implementation
│   ├── AIAgentWithFunctionToolsHumanApproval.csproj
│   ├── appsettings.json
│   ├── AzureOpenAISettings.cs
│   ├── ConfigurationHelper.cs
│   ├── Program.cs
│   └── Tools/
│       └── SensitiveTools.cs
└── Start/                       # Starter template with TODOs
    ├── AIAgentWithFunctionToolsHumanApproval.csproj
    ├── appsettings.json
    ├── AzureOpenAISettings.cs
    ├── ConfigurationHelper.cs
    ├── Program.cs
    └── Tools/
        └── SensitiveTools.cs
```

## Getting Started

1. Navigate to the `Start` folder
2. Update `appsettings.json` with your Azure OpenAI settings:
   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
       "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
     }
   }
   ```
3. Follow the TODO comments in `Program.cs` to complete the implementation

## Implementation Steps

### Step 1: Create Azure OpenAI Client
Set up the `AzureOpenAIClient` with `DefaultAzureCredential` for authentication.

### Step 2: Get Chat Client
Get a `ChatClient` for your specific deployment.

### Step 3: Wrap Sensitive Functions
Use `ApprovalRequiredAIFunction` to wrap functions that should require approval:
```csharp
List<AITool> tools = [new ApprovalRequiredAIFunction(
    AIFunctionFactory.Create(SensitiveTools.DeleteEmployeeData, "delete_employee_data"))];
```

### Step 4: Create Agent with Thread
Create the agent and get a new thread for conversation context:
```csharp
ChatClientAgent agent = chatClient.CreateAIAgent(instructions: "...", tools: tools);
var thread = agent.GetNewThread();
```

### Step 5: Run and Handle Approval
Run the agent and check for approval requests:
```csharp
AgentRunResponse response = await agent.RunAsync(prompt, thread);
List<UserInputRequestContent> userInputRequests = response.UserInputRequests.ToList();
```

### Step 6: Process Approval Requests
For each `FunctionApprovalRequestContent`, get user consent and create a response:
```csharp
bool approved = Console.ReadLine()?.ToLower() == "y";
var responseMessage = new ChatMessage(ChatRole.User, 
    [functionApprovalRequest.CreateResponse(approved)]);
```

### Step 7: Continue Conversation
Continue the agent with the approval responses:
```csharp
response = await agent.RunAsync(userResponses, thread);
```

## Running the Lab

```bash
cd Start  # or Solution
dotnet run
```

When prompted, type `Y` to approve the sensitive operation or any other key to reject it.

## Expected Output

```
Azure OpenAI Settings: 
Azure OpenAI Endpoint: https://your-resource.openai.azure.com/
Azure OpenAI Chat Deployment Name: your-deployment

────────────────────────────────────────────────────

=== Function Tools Calling with Human Approval ===
This scenario demonstrates how to require human approval before executing sensitive operations.

APPROVAL REQUIRED
The agent would like to invoke the following sensitive function:
  Function Name: delete_employee_data
  Arguments: employeeId=EMP001
Please reply Y to approve, or any other key to reject:
y
Function call approved by user.

Agent Response:
The employee data for EMP001 has been successfully deleted...

Token Usage:
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx
```

**Color Legend:**
- Scenario title (`=== ... ===`) appears in **cyan**
- `APPROVAL REQUIRED` appears in **yellow**
- `Function call approved` appears in **green** (or **red** if rejected)
- `Token Usage:` appears in **Burgundy (dark red)**

## Key Takeaways

1. **Human-in-the-loop** is essential for sensitive AI operations
2. **ApprovalRequiredAIFunction** provides a simple way to implement approval workflows
3. **Threads** enable multi-turn conversations needed for approval flows
4. Always log and audit approval decisions for compliance purposes

## Optional: Using the Console Spinner

The `CommonUtilities` library provides a `ConsoleSpinner` that shows a loading animation while the agent is processing. This is **optional** but improves the user experience.

**Usage with extension method:**
```csharp
// Chain .WithSpinner() to any async Task
AgentRunResponse response = await agent.RunAsync("delete the employee with the ID EMP001", thread)
    .WithSpinner("Running agent");

// After approval, continue with spinner
response = await agent.RunAsync(userResponses, thread)
    .WithSpinner("Executing approved function");
```

The spinner displays an animated indicator with elapsed time: `⠋ Running agent... [00:03]`

## Next Steps

- Explore adding multiple sensitive functions with different approval requirements
- Implement approval logging for audit trails
- Consider implementing role-based approval (admin vs. user)
- Add timeout handling for approval requests

