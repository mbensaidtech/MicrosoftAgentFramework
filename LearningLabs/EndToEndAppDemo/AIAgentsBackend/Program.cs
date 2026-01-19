using AIAgentsBackend.Agents.Extensions;
using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Agents.Middleware;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Extensions;
using A2A.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register AI agents (settings, definitions, and factory)
builder.Services.AddAIAgents(builder.Configuration);

// Register Vector Store services (embedding generator, policy services, and initializer)
builder.Services.AddVectorStoreServices(builder.Configuration);

var app = builder.Build();

// Display Azure OpenAI settings
var azureSettings = app.Services.GetRequiredService<AzureOpenAISettings>();
Console.WriteLine("========================================");
Console.WriteLine("Azure OpenAI Configuration:");
Console.WriteLine($"  Endpoint:                  {azureSettings.Endpoint}");
Console.WriteLine($"  DefaultChatDeploymentName: {azureSettings.DefaultChatDeploymentName}");
Console.WriteLine($"  APIKey:                    {MaskApiKey(azureSettings.APIKey)}");
Console.WriteLine("========================================");

static string MaskApiKey(string apiKey)
{
    if (string.IsNullOrEmpty(apiKey)) return "(not set)";
    if (apiKey.Length <= 8) return "****";
    return apiKey[..4] + "****" + apiKey[^4..];
}

// IMPORTANT: Add A2A context middleware BEFORE MapA2A
// This extracts contextId from request body and stores in HttpContext.Items
app.UseA2AContext();

// Create agent instances with their cards
var agentFactory = app.Services.GetRequiredService<IAgentFactory>();

var (translationAgent, translationAgentCard) = agentFactory.GetTranslationAgent();
var (historyAgent, historyAgentCard) = agentFactory.GetHistoryAgent();

// Expose agents via A2A protocol
app.MapA2A(translationAgent, 
    path: "/a2a/translationAgent", 
    agentCard: translationAgentCard,
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/translationAgent"));

app.MapA2A(historyAgent, 
    path: "/a2a/historyAgent", 
    agentCard: historyAgentCard,
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/historyAgent"));

Console.WriteLine("Exposed Agents:");
Console.WriteLine("  - Translation Agent: /a2a/translationAgent");
Console.WriteLine("  - History Agent:     /a2a/historyAgent (with MongoDB conversation memory)");
Console.WriteLine("========================================");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();
