# Lab 08 - Data Format Comparison

## Objective

In this lab, you will compare different data formats (**JSON vs Toon**), demonstrating how format choice impacts token usage and parsing.

Toon is a compact text-based format (CSV-like) that can significantly reduce output tokens compared to JSON, while still maintaining data structure.

## What You Will Learn

- How to create AI agents with function tools
- How to use structured output with `RunAsync<T>` to get typed JSON results
- How to configure agent instructions for specific output formats
- How to use ToonNetSerializer for compact data serialization
- How to compare token usage between different response formats
- When to choose JSON vs Toon format for your use case

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab08-DataFormatComparison/
├── README.md
├── Start/                      <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── Models/
│   │   └── Hotel.cs
│   ├── Tools/
│   │   └── HotelTools.cs       <-- Complete GetAllHotelsUsingToonFormat
│   ├── Data/
│   │   └── hotels.json         <-- 50 sample hotels
│   └── ...
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

### Step 2: Complete the HotelTools.cs

Open `Start/Tools/HotelTools.cs` and complete the TODO:

| TODO | Description |
|------|-------------|
| TODO | Encode hotels list to Toon format using `ToonNet.Encode()` |

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

| TODO | Description |
|------|-------------|
| TODO 1 | Create `AzureOpenAIClient` with managed identity authentication |
| TODO 2 | Get a `ChatClient` for the deployment |
| TODO 3 | Create a hotel agent with JSON format tools |
| TODO 4 | Run the agent with structured output `RunAsync<List<Hotel>>` |
| TODO 5 | Display the hotel results |
| TODO 6 | Display token usage for JSON format |
| TODO 7 | Create a hotel agent with Toon format instructions |
| TODO 8 | Run the agent (returns text, not typed object) |
| TODO 9 | Display the raw Toon response |
| TODO 10 | Display token usage and compare with JSON |

### Step 4: Run and Test

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
| `CreateAIAgent` | Creates an AI agent with instructions and tools |
| `AIFunctionFactory.Create` | Wraps a method as a tool for the agent |
| `RunAsync<T>` | Run agent with structured/typed JSON output |
| `RunAsync` | Run agent with raw text output |
| `ToonNet.Encode()` | Serialize objects to compact Toon format |
| `ToonNet.Decode<T>()` | Deserialize Toon format to typed objects |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent` and `AgentRunResponse` |
| `Microsoft.Extensions.AI` | Provides `AIFunctionFactory` for creating tools |
| `ToonNetSerializer` | Toon format serialization (`ToonNet.Encode/Decode`) |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Using JSON format ===
Hotel: Backpacker Hostel, Price: 25
Hotel: Budget Stay Express, Price: 35
...
----------------------------------------
Token Usage: 
  Input tokens: 2500
  Output tokens: 1200
  Total tokens: 3700
----------------------------------------
=== Scenario 2: Using Toon format ===
Response: 
Name,PricePerNight,Currency
Backpacker Hostel,25,USD
Budget Stay Express,35,USD
...
----------------------------------------
Token Usage: 
  Input tokens: 2500
  Output tokens: 400    <-- Much fewer output tokens!
  Total tokens: 2900
```

## Provided Files

The following files are provided and should not be modified:

- `DataFormatComparison.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Models/Hotel.cs` - Hotel model class
- `Data/hotels.json` - 50 sample hotels with prices, ratings, etc.

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete the TODOs
- `Tools/HotelTools.cs` - Complete the Toon encoding method

## Useful Links

- [ToonNet GitHub](https://github.com/Nicola898989/ToonNet) - .NET implementation of TOON (Token-Oriented Object Notation)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
