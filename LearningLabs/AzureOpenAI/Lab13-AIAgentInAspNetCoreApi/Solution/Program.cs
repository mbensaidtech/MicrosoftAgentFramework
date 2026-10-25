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
// Set to: [1], [2], [3], [4] or [1, 2, 3, 4] to run specific scenarios (3 and 4 continue the conversation of scenario 2).
// Run with "dotnet run -- --serve" to keep the API running and call it yourself (curl, REST client).
HashSet<int> scenariosToRun = [1, 2, 3, 4];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

// Step 4: Load the address of the API (WebApi:BaseUrl)
WebApiSettings webApiSettings = ConfigurationHelper.GetWebApiSettings();

#endregion

#region The API: an agent behind an authenticated endpoint that streams its answers

// Step 1: Create the ASP.NET Core application builder
var builder = WebApplication.CreateBuilder(args);

// Provided: the development token issuer (Auth/DevTokenIssuer.cs), used by the API (step 4) and by the scenarios
DevTokenIssuer tokenIssuer = new();

// Step 2: Register the agent in dependency injection, as a singleton: an agent keeps no conversation (that is the role of
// the session), so all the requests can share it. Its tool resolves services (IHttpContextAccessor) from "services:"
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AIAgent>(serviceProvider => chatClient.AsAIAgent(
    instructions: "You are the assistant of the company intranet. Answer in one or two sentences. " +
                  "Use get_my_profile for any question about the user's team or office.",
    name: "IntranetAssistant",
    tools: [AIFunctionFactory.Create(EmployeeDirectory.GetMyProfile, "get_my_profile")],
    services: serviceProvider));

// Step 3: Register the store of the conversations (the serialized sessions, Storage/ConversationStore.cs)
builder.Services.AddSingleton<ConversationStore>();

// Step 4: Authentication and authorization: the API accepts bearer tokens (JWT) signed by the development issuer
// (in place of an identity provider such as Microsoft Entra ID)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep the claim names of the token: the user id is "sub"
        options.TokenValidationParameters = tokenIssuer.ValidationParameters;
    });
builder.Services.AddAuthorization();

// Step 5: Errors are returned as problem details (application/problem+json), never with the exception message
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Step 6: Map the endpoint. RequireAuthorization: without a valid token, the request is refused (401) before the agent runs
app.MapPost("/api/conversations/{conversationId:guid}/messages",
    IResult (Guid conversationId, ChatRequest request, ClaimsPrincipal user, AIAgent agent, ConversationStore store, CancellationToken cancellationToken) =>
    {
        // The message comes from the caller: check it before it reaches the model (and your token budget)
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["The message must contain 1 to 2000 characters."] });
        }

        // The conversation belongs to the authenticated user: the storage key combines the user id of the token and the
        // conversation id. A conversation id is not a proof of ownership: another user who sends it gets another conversation
        string userId = user.FindFirstValue("sub") ?? throw new InvalidOperationException("The token has no 'sub' claim.");
        string storageKey = $"{userId}/{conversationId}";

        // Stream the answer as Server-Sent Events (text/event-stream): one event per piece of text (AgentEndpoint, at the end of this file)
        return TypedResults.ServerSentEvents(AgentEndpoint.StreamAnswerAsync(agent, store, storageKey, request.Message, cancellationToken));
    })
    .RequireAuthorization();

// Provided: start the API on the configured address
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

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Alice - the conversation continues in another request ===");

    // Step 1: A new HTTP request, the same conversation id: the API loads the session saved by the previous request
    await SendMessageAsync(httpClient, aliceToken, aliceConversationId, "Which meeting room do I prefer?");
}

#endregion

#region Scenario 4: Bob - the same conversation id, another user

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
        // Step 1: Load the session of this conversation (a JSON document saved by the previous request), or create a new one
        AgentSession session = store.TryGet(storageKey, out JsonElement serializedSession)
            ? await agent.DeserializeSessionAsync(serializedSession, cancellationToken: cancellationToken)
            : await agent.CreateSessionAsync(cancellationToken);

        // Step 2: Run the agent in streaming mode and send each piece of text as soon as it arrives
        await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(message, session, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }

        // Step 3: Save the session: the next request of this conversation continues from here
        store.Save(storageKey, await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken));
    }
}

#endregion
