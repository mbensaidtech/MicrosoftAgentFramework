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

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Create an A2A Server with Two Agents (AuthAgent and CustomerToneAgent)

if (ShouldRunScenario(1))
{
    // =====================================================
    // TODO 1: Create a WebApplication builder and build the app
    // Hint: Use WebApplication.CreateBuilder(args) and builder.Build()
    // =====================================================
    

    // =====================================================
    // TODO 2: Get API Key settings and create APIKeyTools
    // Hint: Use ConfigurationHelper.GetAPIKeySettings() and new APIKeyTools(apiKeySettings)
    // =====================================================
    

    // =====================================================
    // TODO 3: Create a list of AI tools for the Auth Agent
    // Hint: Use AIFunctionFactory.Create() to wrap:
    //   - apiKeyTools.GenerateAPIKey with name "generate_api_key"
    //   - apiKeyTools.ValidateAPIKey with name "validate_api_key"
    // Example: IList<AITool> tools = [AIFunctionFactory.Create(...), AIFunctionFactory.Create(...)];
    // =====================================================
    

    // =====================================================
    // TODO 4: Create the AuthAgent (APIKeyAgent)
    // Hint: Use chatClient.CreateAIAgent() with:
    //   - name: "APIKeyAgent"
    //   - instructions: "You are a helpful API key assistant. You are able to generate and validate API keys."
    //   - tools: the tools list created above
    // =====================================================
    

    // =====================================================
    // TODO 5: Create the CustomerToneAgent
    // Hint: Use chatClient.CreateAIAgent() with:
    //   - name: "CustomerToneAgent"
    //   - instructions: "You are a helpful customer tone assistant. You are able to detect the tone of a customer's message."
    //   - No tools needed for this agent
    // =====================================================
    

    // =====================================================
    // TODO 6: Get the AgentCards for both agents
    // Hint: Use AgentCards.CreateAuthAgentCard() and AgentCards.CreateCustomerToneAgentCard()
    // =====================================================
    

    // =====================================================
    // TODO 7: Map the AuthAgent to an A2A endpoint
    // Hint: Use app.MapA2A() with:
    //   - The apiKeyAgent
    //   - path: "/a2a/authAgent"
    //   - agentCard: authAgentCard
    //   - callback: taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/authAgent")
    // =====================================================
    

    // =====================================================
    // TODO 8: Map the CustomerToneAgent to an A2A endpoint
    // Hint: Use app.MapA2A() with:
    //   - The customerToneAgent
    //   - path: "/a2a/customerToneAgent"  
    //   - agentCard: customerToneAgentCard
    //   - callback: taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/customerToneAgent")
    // =====================================================
    

    // =====================================================
    // TODO 9: Run the application
    // Hint: Use await app.RunAsync()
    // =====================================================
    
    
    Console.WriteLine("Server is running on http://localhost:5000");
    Console.WriteLine("Press Ctrl+C to stop the server");
    return;
}

#endregion
