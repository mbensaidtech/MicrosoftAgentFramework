# CommonUtilities

A shared utility library providing common functionality for all Learning Labs in the Microsoft Agents Framework series.

## Overview

This project contains reusable utility classes that simplify console output formatting across all labs. It is referenced as a project dependency by each lab.

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

## Project Reference

Each lab references this project in its `.csproj` file:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\..\CommonUtilities\CommonUtilities.csproj" />
</ItemGroup>
```

## Target Framework

- .NET 10.0
