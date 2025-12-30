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
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [2,3];
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
    ColoredConsole.WriteInfoLine("=== Scenario 1: Initialize Vector Store with FAQ Data ===");
    await faqService.InitializeAsync();
}

#endregion

#region Scenario 2: Search Vector Store for FAQ Data Without Using Agent

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteInfoLine("=== Scenario 2: Search Vector Store for FAQ Data ===");
    var results = await searchTools.SearchFaqAsync("How can I contact support?", topK: 3)
        .WithSpinner("Searching FAQ");
    foreach (var result in results)
    {
        Console.WriteLine(result);
        ColoredConsole.WriteDividerLine();
    }
}

#endregion

#region Scenario 3: Search Vector Store for FAQ Data Using Agent

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteInfoLine("=== Scenario 3: Search Vector Store for FAQ Data Using Agent ===");

    //Step 1: Create a new agent
    AIAgent savAgent = client
    .GetChatClient(settings.ChatDeploymentName)
    .CreateAIAgent(instructions: "You are a helpful assistant that can answer questions about the FAQ.",
    name: "FAQAgent",
    tools: [AIFunctionFactory.Create(searchTools.SearchFaqAsync, "search_faq")]);

    //Step 2: Run the agent
    AgentRunResponse response = await savAgent.RunAsync("How can I contact support?")
        .WithSpinner("Agent is thinking");
    Console.WriteLine(response);

    //Step 3: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    ColoredConsole.WriteDividerLine();
}
#endregion