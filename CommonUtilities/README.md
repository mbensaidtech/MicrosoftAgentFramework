# CommonUtilities

A shared utility library providing common functionality for all Learning Labs in the Microsoft Agents Framework series.

## Overview

This project contains reusable utility classes (console output, spinner, Azure OpenAI endpoint helper) shared by all labs. It is referenced as a project dependency by each lab.
It has no package dependency: a lab that needs a database driver (for example `MongoDB.Driver` in Lab05 and Lab12) references it itself.

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

### ScenarioSelection / LabScenarios

Chooses the scenarios a lab runs from its command line. Every migrated lab starts with:

```csharp
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);
```

| Command | Runs |
|---------|------|
| `dotnet run` | The implemented scenarios: the code of their `#region Scenario N: …` block, and of the setup before the first scenario, is no longer the delivered code (comments and layout do not count) |
| `dotnet run -- --scenarios 2` / `--scenarios 1,3` | Those scenarios (and the ones they require: `// Requires scenario N` in their block), implemented or not |
| `dotnet run -- --scenarios all` | Every scenario |
| `dotnet run -- --list-scenarios` | Nothing: lists the scenarios and their state |

`ShouldRun` prints the selection once (`>>> Scenarios to run: …`) and a line when a scenario starts (`>>> Running scenario N: Title`).
The rules themselves are in `LabScenarios.cs`, a file without dependency that the Lab Bench dashboard compiles too: the dashboard reads the
same scenarios, applies the same default and runs the lab with the same option (`-- --scenarios 1,3`).
The delivered code is known from `exercise-fingerprints.json` in the folder of the lab: fingerprints of the code of the exercise as delivered
(regenerated with `dotnet run -- exercise-fingerprints` in `Dashboard/LabDashboard`).

## Project Reference

Each lab references this project in its `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\..\CommonUtilities\CommonUtilities.csproj" />
</ItemGroup>
```

## Target Framework

- .NET 10.0
