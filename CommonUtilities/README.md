# CommonUtilities

A shared utility library providing common functionality for all Learning Labs in the Microsoft Agents Framework series.

## Overview

This project contains reusable utility classes (console output, spinner, Azure OpenAI endpoint helper, MongoDB health check) shared by all labs. It is referenced as a project dependency by each lab.

## Features

### ColoredConsole

A static class that provides color-coded console output methods for better readability and visual distinction between different types of messages.

## Available Methods

| Method | Color | Purpose |
|--------|-------|---------|
| `WriteErrorLine(string)` | 🔴 Red | Error messages |
| `WriteWarningLine(string)` | 🟡 Yellow | Warning messages |
| `WriteSuccessLine(string)` | 🟢 Green | Success messages |
| `WriteInfoLine(string)` | 🔵 Cyan | Information/section headers |
| `WriteUserLine(string)` | 🟢 Green | User input/messages |
| `WriteAssistantLine(string)` | 🔵 Cyan | AI assistant responses |
| `WriteSystemLine(string)` | 🟡 Yellow | System messages |
| `WritePrimaryLogLine(string)` | 🔵 Blue | Primary log information |
| `WriteSecondaryLogLine(string)` | ⚫ Dark Gray | Secondary/detailed log information |
| `WriteEmptyLine()` | - | Empty line |
| `WriteDividerLine()` | ⚫ Dark Gray | Visual separator line |

## Usage

```csharp
using CommonUtilities;

// Display section header
ColoredConsole.WriteInfoLine("=== Scenario 1: Basic Agent ===");

// Display results
ColoredConsole.WritePrimaryLogLine("Token Usage:");
ColoredConsole.WriteSecondaryLogLine($"  Input tokens: 150");
ColoredConsole.WriteSecondaryLogLine($"  Output tokens: 85");

// Visual separation
ColoredConsole.WriteDividerLine();

// Error handling
ColoredConsole.WriteErrorLine("An error occurred!");

// Success message
ColoredConsole.WriteSuccessLine("Operation completed successfully!");
```

### AzureOpenAIEndpoint

Builds the Azure OpenAI **v1** endpoint expected by the official `OpenAI` SDK from the resource endpoint shown in the Azure portal.

```csharp
using CommonUtilities;
using OpenAI;

// https://my-resource.openai.azure.com/  ->  https://my-resource.openai.azure.com/openai/v1/
OpenAIClientOptions options = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };
```

Endpoints that already end with `/openai/v1` are returned unchanged.

### ConsoleSpinner / WithSpinner

Shows a loading animation while an async operation runs: `await agent.RunAsync("...").WithSpinner("Running agent");`

## Project Reference

Each lab references this project in its `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\..\CommonUtilities\CommonUtilities.csproj" />
</ItemGroup>
```

## Target Framework

- .NET 10.0
