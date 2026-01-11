using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using OpenAI;
using CommonUtilities;
using AgenticRAG;
using AgenticRAG.Services;
using AgenticRAG.Tools;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Chat Deployment: {settings.ChatDeploymentName}");
Console.WriteLine($"Embedding Deployment: {settings.EmbeddingDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get embedding generator
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = client
    .GetEmbeddingClient(settings.EmbeddingDeploymentName)
    .AsIEmbeddingGenerator();

// Step 4: Get MongoDB database
var mongoDatabase = ConfigurationHelper.GetMongoDatabase();
Console.WriteLine($"MongoDB Database: {mongoDatabase.DatabaseNamespace.DatabaseName}");

// Step 5: Create FAQ Vector Store Service
var faqService = new FaqVectorStoreService(mongoDatabase, embeddingGenerator);

// Step 6: Create Search Tools
var searchTools = new SearchTools(faqService);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Initialize Vector Store with FAQ Data

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Initialize Vector Store with FAQ Data ===");

    // TODO: Call faqService.InitializeAsync() to initialize the vector store
    throw new NotImplementedException("Implement Scenario 1");
}

#endregion

#region Scenario 2: Search Vector Store for FAQ Data Without Using Agent

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Search Vector Store for FAQ Data ===");

    // TODO: Implement Scenario 2
    // Step 1: Call searchTools.SearchFaqAsync with a question and topK
    // Step 2: Loop through results and display each one
    throw new NotImplementedException("Implement Scenario 2");
}

#endregion

#region Scenario 3: Search Vector Store for FAQ Data Using Agent

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Search Vector Store for FAQ Data Using Agent ===");

    // TODO: Implement Scenario 3
    // Step 1: Create a new AI agent with instructions and the search tool
    // Step 2: Run the agent with a question
    // Step 3: Display the agent's response
    // Step 4: Display token usage
    throw new NotImplementedException("Implement Scenario 3");
}

#endregion
