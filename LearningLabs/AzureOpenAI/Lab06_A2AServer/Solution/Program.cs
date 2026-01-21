using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using A2AServer;
using Microsoft.Extensions.AI;
using A2A;
using Microsoft.AspNetCore.Builder;
using A2A.AspNetCore;
using System.ClientModel;

using A2AServer.Tools;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient (API key or DefaultAzureCredential)
AzureOpenAIClient client = !string.IsNullOrEmpty(settings.APIKey)
    ? new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey))
    : new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Generate and validate API keys

if (ShouldRunScenario(1))
{
    var builder = WebApplication.CreateBuilder(args);
    var app = builder.Build();


    var apiKeySettings = ConfigurationHelper.GetAPIKeySettings();
    var apiKeyTools = new APIKeyTools(apiKeySettings);
    IList<AITool> tools = [AIFunctionFactory.Create(apiKeyTools.GenerateAPIKey, "generate_api_key"), AIFunctionFactory.Create(apiKeyTools.ValidateAPIKey, "validate_api_key")];
    
    ChatClientAgent apiKeyAgent = chatClient.CreateAIAgent(
        name: "APIKeyAgent", 
        instructions: "You are a helpful API key assistant. You are able to generate and validate API keys.",
        tools: tools);

      ChatClientAgent customerToneAgent = chatClient.CreateAIAgent(
        name: "CustomerToneAgent", 
        instructions: "You are a helpful customer tone assistant. You are able to detect the tone of a customer's message.");    


     //Get agent card
    AgentCard authAgentCard = AgentCards.CreateAuthAgentCard();
    AgentCard customerToneAgentCard = AgentCards.CreateCustomerToneAgentCard();

    app.MapA2A(apiKeyAgent, path: "/a2a/authAgent", agentCard: authAgentCard, taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/authAgent"));

    app.MapA2A(customerToneAgent, path: "/a2a/customerToneAgent", agentCard: customerToneAgentCard, taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/customerToneAgent"));

    await app.RunAsync();
    Console.WriteLine("Server is running on http://localhost:5000");
    Console.WriteLine("Press Ctrl+C to stop the server");
    return;
}

#endregion
