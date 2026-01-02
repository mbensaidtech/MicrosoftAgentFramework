using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using AIAgentWithFunctionTools.Tools;
using AIAgentWithFunctionTools.Repositories;
using System.Reflection;
using AIAgentWithFunctionTools;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Chat Deployment Name: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
// TODO: AzureOpenAIClient client = ...

// Step 3: Get a ChatClient for the specific deployment
// TODO: ChatClient chatClient = ...

#endregion


#region Scenario 1: Function tools calling - basic.

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");

    // Step 0: Add [Description] attributes to GetEmployeeInfo in Tools/CompanyTools.cs
    // TODO: Open Tools/CompanyTools.cs and add Description attributes to the GetEmployeeInfo method
    //       - Add [Description("...")] attribute on the method (above public string GetEmployeeInfo)
    //       - Add [Description("...")] attribute on the employeeId parameter
    //       See README.md for the exact descriptions to use

    // Step 1: Create agent with tools
    // TODO: var companyTools = ...
    // TODO: ChatClientAgent basicFunctionToolsAgent = ...

    // Step 2: Run the agent with a prompt
    // TODO: AgentRunResponse basicFunctionToolsResponse = ...

    // Step 3: Display the response
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)

    // Step 4: Display token usage
    ColoredConsole.WriteSectionSeparator();
    // TODO: ColoredConsole.WriteBurgundyLine("Token Usage: ");
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)
    
}

#endregion

#region Scenario 2: Function tools calling - using reflection.

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Function tools calling - using reflection ===");

    // Step 1: Create tools list using reflection
    // Step 1.0: Create an instance of CompanyTools
    // TODO: var companyTools = ...
    // Step 1.1: Get all public instance methods from CompanyTools using reflection
    // TODO: MethodInfo[] methods = ...
    // Step 1.2: Create a list of AITool from the methods using AIFunctionFactory.Create
    // TODO: List<AITool> tools = ...
    // Step 1.3: Display the tools that we will inject to the agent
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)

    // Step 2: Create agent with tools
    // TODO: ChatClientAgent reflectionFunctionToolsAgent = ...

    // Step 3: Run the agent with a prompt
    // TODO: AgentRunResponse reflectionFunctionToolsResponse = ...

    // Step 4: Display the response
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)

    // Step 5: Display token usage
    ColoredConsole.WriteSectionSeparator();
    // TODO: ColoredConsole.WriteBurgundyLine("Token Usage: ");
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)
}

#endregion

#region Scenario 3: Function tools calling - static tools with dependency injection using ServiceCollection.

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Function tools calling - static tools with DI ===");

    // Step 1: Configure services using ServiceCollection
    // TODO: var services = ...
    // TODO: services.AddSingleton<...>(...);
    
    // Step 2: Build the ServiceProvider
    // TODO: var serviceProvider = ...

    // TODO: ChatClientAgent notificationAgent = ...

    // Step 3: Run the agent - send a notification and then retrieve it
    // TODO: AgentRunResponse notificationResponse = ...

    // Step 4: Display the response
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)

    // Step 5: Display token usage
    ColoredConsole.WriteSectionSeparator();
    // TODO: ColoredConsole.WriteBurgundyLine("Token Usage: ");
    // TODO: ColoredConsole.WriteSecondaryLogLine(...)
}

#endregion
