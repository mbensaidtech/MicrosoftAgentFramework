# Lab 11 - AI Agent With Custom HTTP Transport

## Objective

In this lab, you will create an AI Agent that uses a **Custom HTTP Transport** to intercept and log all HTTP requests and responses to Azure OpenAI.

You will learn how to inject a custom `HttpClientHandler` into the Azure OpenAI client to monitor, modify, or log HTTP traffic.

## What You Will Learn

- How to create a custom `HttpClientHandler` to intercept HTTP traffic
- How to inject custom HTTP headers into requests
- How to configure `AzureOpenAIClient` with a custom HTTP transport using `HttpClientPipelineTransport`
- How to log and format HTTP requests and responses

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab11-AIAgentWithCustomHttpTransport/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── CustomHttpClientHandler.cs  <-- Implement the HTTP handler here
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithCustomHttpTransport.csproj
└── Solution/                       <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 2: Implement the CustomHttpClientHandler

Open `Start/CustomHttpClientHandler.cs` and implement the `SendAsync` method.

---

#### CustomHttpClientHandler Implementation

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Add custom headers to the request | • Use `request.Headers.Add("X-agent-name", "AIAgentWithCustomHttpTransport")` |
| **Step 2** | Write the HTTP request to the console | • Use `ColoredConsole.WriteDividerLine()` to start <br> • Use `ColoredConsole.WriteInfoLine("=== HTTP REQUEST ===")` for the title <br> • Display method and URI: `request.Method` and `request.RequestUri` <br> • Loop through `request.Headers` to display headers <br> • Mask Authorization header for security <br> • Use `request.Content.ReadAsStringAsync()` to get the body <br> • Use `WriteFormattedJson()` helper for JSON formatting |
| **Step 3** | Send the HTTP request | • Call `var response = await base.SendAsync(request, cancellationToken);` |
| **Step 4** | Write the HTTP response to the console | • Use `ColoredConsole.WriteSuccessLine("=== HTTP RESPONSE ===")` for the title <br> • Display status: `response.StatusCode` with `GetStatusLabel()` helper <br> • Loop through `response.Headers` to display headers <br> • Use `response.Content.ReadAsStringAsync()` to get the body <br> • Use `WriteFormattedJson()` helper for JSON formatting <br> • Return the response |

**Example for Step 2 - Displaying request headers with masked Authorization:**
```csharp
ColoredConsole.WriteWarningLine("[Headers]");
foreach (var header in request.Headers)
{
    var value = string.Join(", ", header.Value);
    if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
    {
        value = value.Length > 20 ? $"{value[..20]}... [MASKED]" : "[MASKED]";
    }
    ColoredConsole.WriteSecondaryLogLine($"   {header.Key}: {value}");
}
```

**Example for Step 4 - Displaying response status:**
```csharp
ColoredConsole.WritePrimaryLogLine($"[Status] {(int)response.StatusCode} {response.StatusCode} ({GetStatusLabel(response.StatusCode)})");
```

---

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the setup region.

---

#### Setup: Configuration and Client Initialization

| Step | Description | Hints |
|------|-------------|-------|
| **Step 2** | Create HttpClient with custom handler | • `var httpClient = new HttpClient(new CustomHttpClientHandler());` |
| **Step 3** | Create `AzureOpenAIClient` with custom transport | • Use constructor: `new AzureOpenAIClient(Uri, TokenCredential, AzureOpenAIClientOptions)` <br> • Create options: `new AzureOpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient) }` <br> • Optionally set `NetworkTimeout = TimeSpan.FromSeconds(30)` |
| **Step 4** | Get `ChatClient` for the deployment | • Method: `client.GetChatClient(settings.ChatDeploymentName)` |

**Complete example for Steps 2-4:**
```csharp
// Step 2
var httpClient = new HttpClient(new CustomHttpClientHandler());

// Step 3
AzureOpenAIClient client = new AzureOpenAIClient(
    new Uri(settings.Endpoint), 
    new DefaultAzureCredential(),  
    new AzureOpenAIClientOptions 
    { 
        Transport = new HttpClientPipelineTransport(httpClient), 
        NetworkTimeout = TimeSpan.FromSeconds(30) 
    });

// Step 4
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);
```

---

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
| `HttpClientHandler` | Base class for custom HTTP handling - override `SendAsync` to intercept requests |
| `HttpClientPipelineTransport` | Adapter to use `HttpClient` as the transport for Azure SDK clients |
| `AzureOpenAIClientOptions` | Configuration options for `AzureOpenAIClient` including custom transport |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `ColoredConsole` | Utility class for formatted colored console output |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` and `AzureOpenAIClientOptions` |
| `System.ClientModel.Primitives` | Provides `HttpClientPipelineTransport` |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `AgentRunResponse` |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |
| `System.Text.Json` | JSON serialization for pretty-printing |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name

────────────────────────────────────────────────────

=== Running Scenario 1: Basic Agent ===

────────────────────────────────────────────────────

=== HTTP REQUEST ===

[URL] POST https://your-resource.openai.azure.com/openai/deployments/...

[Headers]
   Accept: application/json
   User-Agent: azsdk-net-AI.OpenAI/...
   x-ms-client-request-id: ...
   Authorization: Bearer eyJ0eXAiOiJKV1... [MASKED]

[Body]
   {
     "messages": [
       {
         "role": "user",
         "content": "Hello, what is the capital of France?"
       }
     ],
     "model": "your-deployment-name"
   }

=== HTTP RESPONSE ===

[Status] 200 OK (OK)

[Headers]
   apim-request-id: ...
   x-ms-region: France Central
   ...

[Body]
   {
     "choices": [
       {
         "message": {
           "content": "The capital of France is Paris.",
           ...
         }
       }
     ],
     ...
   }

────────────────────────────────────────────────────

The capital of France is Paris.
```

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithCustomHttpTransport.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `CustomHttpClientHandler.cs` - Implement the `SendAsync` method
- `Program.cs` - Complete the setup TODOs and uncomment scenario code

## Use Cases for Custom HTTP Transport

- **Logging & Debugging**: Monitor all requests/responses to Azure OpenAI
- **Custom Headers**: Add tracking headers, correlation IDs, or custom metadata
- **Request/Response Modification**: Transform requests before sending or responses after receiving
- **Metrics Collection**: Measure latency, track usage patterns
- **Security Auditing**: Log API calls for compliance

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
