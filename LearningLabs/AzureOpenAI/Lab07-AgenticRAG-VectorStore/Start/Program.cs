using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.AI;

using CommonUtilities;
using AgenticRAG;

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
Console.WriteLine($"Deployment: {settings.DeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.DeploymentName);

// Step 4: Get MongoDB database (optional, for vector store scenarios)
var mongoDatabase = ConfigurationHelper.GetMongoDatabase();
Console.WriteLine($"MongoDB Database: {mongoDatabase.DatabaseNamespace.DatabaseName}");

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Agentic RAG with Vector Store

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteInfoLine("=== Scenario 1: Agentic RAG with Vector Store ===");

    // TODO: Implement your Agentic RAG scenario here
}

#endregion
