# Lab 06 - AI Agent with ToolKit

## Objective

In this lab, you will learn how to use the **AgentFrameworkToolkit** to simplify the creation of function tools for AI Agents using the Microsoft Agents Framework with Azure OpenAI.

The ToolKit provides a declarative approach to define tools using attributes (`[AITool]` and `[Description]`), making it easier to expose methods as callable functions for the AI agent.

## What You Will Learn

- How to use the `[AITool]` attribute to expose methods as AI tools
- How to use the `[Description]` attribute to describe parameters
- How to create an `AIToolsFactory` to automatically discover tools
- How to register tools with an AI Agent using `GetTools()`
- How to run the agent and display responses

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab06-AIAgentWithToolKit/
├── README.md
├── Start/             <-- Your working folder
│   ├── Program.cs     <-- Complete the TODOs here
│   ├── Tools/
│   │   └── CompanyTools.cs  <-- Add [AITool] attributes here
│   └── ...
└── Solution/          <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "DeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 2: Add AITool attributes to CompanyTools.cs

Open `Start/Tools/CompanyTools.cs` and add the `[AITool]` attribute to each **public** method:

| Method | AITool Name | Description |
|--------|-------------|-------------|
| `GetEmployeeInfo` | `get_employee_info` | Retrieves detailed information about an employee using their employee ID |
| `GetMeetingRooms` | `get_meeting_rooms` | Lists all available meeting rooms with their capacity and features |
| `BookMeetingRoom` | `book_meeting_room` | Books a meeting room for a specific date, time, and subject |

Also add `[Description]` attributes to method parameters to help the AI understand what values to pass.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the 7 TODOs in Scenario 1:

| TODO | Description |
|------|-------------|
| TODO 1 | Create an empty list of AITool |
| TODO 2 | Create the AIToolsFactory instance |
| TODO 3 | Get the tools from the factory using the CompanyTools type |
| TODO 4 | Create the agent with instructions and tools |
| TODO 5 | Run the agent with a prompt |
| TODO 6 | Display the response |
| TODO 7 | Display token usage |

### Step 4: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `[AITool]` | Attribute to mark a method as an AI-callable tool with name and description |
| `[Description]` | Attribute to describe method parameters for the AI |
| `AIToolsFactory` | Factory class to discover and create tools from annotated classes |
| `GetTools(Type)` | Method to extract all tools from a class type |
| `AITool` | Microsoft Agents abstraction for tools |
| `ChatClientAgent` | AI Agent that can use tools to answer questions |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Function tools calling - basic ===
Employee Found:
- ID: EMP001
- Name: Mohammed BEN SAID
- Department: Engineering
- Position: Senior Developer
- Email: mohammed.bensaid@company.com
- Vacation Days Remaining: 25
----------------------------------------
Token Usage: 
  Input tokens: 150
  Output tokens: 85
  Total tokens: 235
```

## Provided Files

The following files are provided and should be completed:

- `Tools/CompanyTools.cs` - Add `[AITool]` and `[Description]` attributes to public methods

The following files are provided and should not be modified:

- `AIAgentWithToolKit.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following file should be modified with your values:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment name

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.
