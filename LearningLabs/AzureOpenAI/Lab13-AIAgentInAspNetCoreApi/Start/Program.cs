using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentWebApi;
using AIAgentWebApi.Auth;
using AIAgentWebApi.Storage;
using AIAgentWebApi.Tools;
using static AIAgentWebApi.ApiConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --scenarios all       -> every scenario, implemented or not
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
// Run with "dotnet run -- --serve" to keep the API running and call it yourself (curl, REST client).
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create the OpenAIClientOptions that point to the Azure OpenAI v1 endpoint
// Hint: AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) turns "https://<resource>.openai.azure.com/" into ".../openai/v1/"
// OpenAIClientOptions clientOptions = ...

// TODO 2: Create the OpenAIClient
// - If settings.APIKey is set: authenticate with an ApiKeyCredential
// - Otherwise: authenticate with Microsoft Entra ID using a BearerTokenPolicy built from
//   DefaultAzureCredential and the scope "https://ai.azure.com/.default"
// Note: the AuthenticationPolicy constructor is flagged experimental (OPENAI001), keep the pragma around it.
#pragma warning disable OPENAI001
// OpenAIClient client = ...
#pragma warning restore OPENAI001

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

// Step 4: Load the address of the API (WebApi:BaseUrl)
WebApiSettings webApiSettings = ConfigurationHelper.GetWebApiSettings();

#endregion

#region The API: an agent behind an authenticated endpoint that streams its answers

// Step 1: Create the ASP.NET Core application builder
var builder = WebApplication.CreateBuilder(args);

// Provided: the development token issuer (Auth/DevTokenIssuer.cs), used by the API (TODO 6) and by the scenarios
DevTokenIssuer tokenIssuer = new();

// TODO 4: Register the agent in dependency injection, as a singleton, with the tool get_my_profile
// Hint: builder.Services.AddHttpContextAccessor();
//       builder.Services.AddSingleton<AIAgent>(serviceProvider => chatClient.AsAIAgent(instructions: ..., name: "IntranetAssistant",
//           tools: [AIFunctionFactory.Create(EmployeeDirectory.GetMyProfile, "get_my_profile")], services: serviceProvider));

// TODO 5: Register the store of the conversations as a singleton
// Hint: builder.Services.AddSingleton<ConversationStore>();

// TODO 6: Register the authentication (JWT bearer tokens of tokenIssuer) and the authorization
// Hint: builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => { options.MapInboundClaims = false;
//           options.TokenValidationParameters = tokenIssuer.ValidationParameters; }); builder.Services.AddAuthorization();

// TODO 7: Return the errors as problem details
// Hint: builder.Services.AddProblemDetails();

var app = builder.Build();

// TODO 8: Add the error handling middleware (needs TODO 7)
// Hint: app.UseExceptionHandler(); app.UseStatusCodePages();

// TODO 9: Map the endpoint POST /api/conversations/{conversationId:guid}/messages, protected with RequireAuthorization()
// - check the message (1 to 2000 characters), otherwise TypedResults.ValidationProblem(...)
// - storage key: the user id of the token (user.FindFirstValue("sub"), throw if it is missing) and the conversation id
// - return TypedResults.ServerSentEvents(AgentEndpoint.StreamAnswerAsync(agent, store, storageKey, request.Message, cancellationToken))
// (see the README for the complete lambda)

// Provided: start the API on the configured address (until TODO 9, it has no endpoint: the scenarios get 404)
app.Urls.Add(webApiSettings.BaseUrl);
await app.StartAsync();
ColoredConsole.WritePrimaryLogLine($"API: POST {webApiSettings.BaseUrl}/api/conversations/{{conversationId}}/messages (bearer token required)");

#endregion

// "--serve": keep the API running until Ctrl+C, with a development token for each user of the lab
if (args.Contains("--serve"))
{
    ColoredConsole.WriteSecondaryLogLine($"Development token of alice (valid 30 minutes, until the program stops): {tokenIssuer.CreateToken("alice")}");
    ColoredConsole.WriteSecondaryLogLine($"Development token of bob: {tokenIssuer.CreateToken("bob")}");
    ColoredConsole.WriteSecondaryLogLine("Press Ctrl+C to stop the API.");
    await app.WaitForShutdownAsync();
    return;
}

// The client side of the scenarios: an HttpClient that calls the API, and a token for each user
using HttpClient httpClient = new() { BaseAddress = new Uri($"{webApiSettings.BaseUrl}/") };
string aliceToken = tokenIssuer.CreateToken("alice");
string bobToken = tokenIssuer.CreateToken("bob");
Guid aliceConversationId = Guid.NewGuid();

#region Scenario 1: Requests refused before the agent runs

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Requests refused before the agent runs ===");

    // Step 1: No token: the authorization of the endpoint refuses the request (401 Unauthorized)
    await SendMessageAsync(httpClient, token: null, Guid.NewGuid(), "Hello, which team am I in?");

    // Step 2: A valid token, but an empty message: the endpoint refuses it (400 Bad Request, problem details)
    await SendMessageAsync(httpClient, aliceToken, Guid.NewGuid(), " ");
}

#endregion

#region Scenario 2: Alice - a streamed answer, with a tool that knows who she is

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Alice - a streamed answer, with a tool that knows who she is ===");

    // Step 1: Alice starts a conversation. The tool reads her profile from her token, not from the model
    await SendMessageAsync(httpClient, aliceToken, aliceConversationId,
        "Which team am I in? Please also remember that my favorite meeting room is ROOM-B.");
}

#endregion

#region Scenario 3: Alice - the conversation continues in another request
// Requires scenario 2: Alice continues the conversation she started there.

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Alice - the conversation continues in another request ===");

    // Step 1: A new HTTP request, the same conversation id: the API loads the session saved by the previous request
    await SendMessageAsync(httpClient, aliceToken, aliceConversationId, "Which meeting room do I prefer?");
}

#endregion

#region Scenario 4: Bob - the same conversation id, another user
// Requires scenario 2: Bob sends the conversation id of Alice's conversation.

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Bob - the same conversation id, another user ===");

    // Step 1: Bob sends the conversation id of Alice. The storage key contains his own user id: he gets a new conversation,
    // without Alice's messages, and the tool gives his profile
    await SendMessageAsync(httpClient, bobToken, aliceConversationId, "Which meeting room do I prefer, and which team am I in?");
}

#endregion

await app.StopAsync();

#region The work of the endpoint: load the conversation, stream the answer, save the conversation

/// <summary>
/// What the endpoint does for one message, once the request is authorized and checked.
/// </summary>
internal static class AgentEndpoint
{
    public static async IAsyncEnumerable<string> StreamAnswerAsync(
        AIAgent agent, ConversationStore store, string storageKey, string message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // TODO 10: Load the session of the conversation, stream the answer, then save the session
        // - store.TryGet(storageKey, out JsonElement serializedSession): if found, await agent.DeserializeSessionAsync(serializedSession, ...),
        //   otherwise await agent.CreateSessionAsync(cancellationToken)
        // - await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(message, session, ...)): yield return update.Text (when not empty)
        // - store.Save(storageKey, await agent.SerializeSessionAsync(session, ...))
        // Remove the "yield break;" line below.
        yield break;
    }
}

#endregion
