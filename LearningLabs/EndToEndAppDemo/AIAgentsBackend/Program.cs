using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Extensions;
using A2A.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Register all application services (Azure OpenAI, MongoDB, Agents, VectorStore)
builder.Services.AddApplicationServices(builder.Configuration);

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

// IMPORTANT: Add unified agent context middleware BEFORE MapA2A and routing
// This extracts contextId from ALL agent requests (A2A and Frontend) and stores in HttpContext.Items
app.UseAgentContext();

// Create agent instances with their cards
var agentFactory = app.Services.GetRequiredService<IAgentFactory>();

var (translationAgent, translationAgentCard) = agentFactory.GetTranslationAgent();
var (customerSupportAgent, customerSupportAgentCard) = agentFactory.GetCustomerSupportAgent();
var (historyAgent, historyAgentCard) = agentFactory.GetHistoryAgent();

// Expose agents via A2A protocol
app.MapA2A(translationAgent, 
    path: "/a2a/translationAgent", 
    agentCard: translationAgentCard,
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/translationAgent"));

app.MapA2A(customerSupportAgent, 
    path: "/a2a/customerSupportAgent", 
    agentCard: customerSupportAgentCard,
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/customerSupportAgent"));

app.MapA2A(historyAgent, 
    path: "/a2a/historyAgent", 
    agentCard: historyAgentCard,
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/historyAgent"));

Console.WriteLine("Exposed Agents:");
Console.WriteLine("  - Translation Agent:       /a2a/translationAgent");
Console.WriteLine("  - Customer Support Agent:  /a2a/customerSupportAgent (with policy tools)");
Console.WriteLine("  - History Agent:           /a2a/historyAgent (with MongoDB conversation memory)");
Console.WriteLine("========================================");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();
