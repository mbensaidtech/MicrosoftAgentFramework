using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentWithCustomHttpTransport;
using AIAgentWithCustomHttpTransport.Tools;
using static AIAgentWithCustomHttpTransport.HttpConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2] or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create the logging handler (bottom of this file) and an HttpClient that sends every request through it.
// A DelegatingHandler only intercepts: InnerHandler (HttpClientHandler) is the handler that talks to the network.
HttpLoggingHandler loggingHandler = new() { InnerHandler = new HttpClientHandler() };
using HttpClient httpClient = new(loggingHandler);

// Step 3: Create the client options: the Azure OpenAI v1 endpoint, and the custom HTTP transport.
// HttpClientPipelineTransport (System.ClientModel) makes the OpenAI SDK send its requests with our HttpClient.
// NetworkTimeout is applied to each attempt (the SDK retries some failures, each attempt goes through the handler).
OpenAIClientOptions clientOptions = new()
{
    Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint),
    Transport = new HttpClientPipelineTransport(httpClient),
    NetworkTimeout = TimeSpan.FromSeconds(30)
};

// Step 4: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 5: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: See the HTTP traffic of an agent run

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: See the HTTP traffic of an agent run ===");
    string prompt = "Hello, what is the capital of France?";
    ColoredConsole.WriteUserLine($"User: {prompt}");

    // Step 1: Create a basic AI Agent from the ChatClient (its requests go through the custom transport)
    AIAgent basicAgent = chatClient.AsAIAgent();

    // Step 2: Run the agent, counting the HTTP requests sent by the handler during the run
    // (no spinner in this lab: it would write over the HTTP log printed by the handler)
    int requestsBefore = loggingHandler.RequestCount;
    AgentResponse basicResponse = await basicAgent.RunAsync(prompt);

    // Step 3: Display the answer, the number of HTTP requests of the run and the token usage
    ColoredConsole.WritePrimaryLogLine("Agent answer:");
    ColoredConsole.WriteAssistantLine(basicResponse.Text);
    ColoredConsole.WritePrimaryLogLine($"HTTP requests sent by this run: {loggingHandler.RequestCount - requestsBefore}");
    WriteTokenUsage(basicResponse);
}

#endregion

#region Scenario 2: One agent run, several HTTP requests (function calling)

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: One agent run, several HTTP requests (function calling) ===");
    string prompt = "What is the weather like in Amsterdam?";
    ColoredConsole.WriteUserLine($"User: {prompt}");

    // Step 1: Create an agent with a function tool (its description and JSON schema are sent in the "tools" of each request)
    AIAgent weatherAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant. Use the tools to answer.",
        name: "WeatherAgent",
        tools: [AIFunctionFactory.Create(WeatherTools.GetWeather, "get_weather")]);

    // Step 2: Run the agent, counting the HTTP requests: the model first asks for the tool ("tool_calls"),
    // then the agent sends the tool result ("role": "tool") in a second request and gets the final answer
    int requestsBefore = loggingHandler.RequestCount;
    AgentResponse weatherResponse = await weatherAgent.RunAsync(prompt);

    // Step 3: Display the answer, the number of HTTP requests of the run and the token usage (total of all the requests)
    ColoredConsole.WritePrimaryLogLine("Agent answer:");
    ColoredConsole.WriteAssistantLine(weatherResponse.Text);
    ColoredConsole.WritePrimaryLogLine($"HTTP requests sent by this run: {loggingHandler.RequestCount - requestsBefore}");
    WriteTokenUsage(weatherResponse);
}

#endregion

#region Custom HTTP handler: intercepts every request sent by the OpenAI SDK, and its response

/// <summary>
/// HTTP message handler placed in the HttpClient used by the OpenAI SDK: it sees each HTTP request when it leaves
/// the SDK (after its retry and authentication policies) and each response before the SDK reads it.
/// </summary>
internal sealed class HttpLoggingHandler : DelegatingHandler
{
    private int _requestCount;

    /// <summary>Number of HTTP requests sent through this handler since the start of the program.</summary>
    public int RequestCount => _requestCount;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The same handler can serve several requests at the same time: count them atomically
        int number = Interlocked.Increment(ref _requestCount);

        // Step 1: Add a custom header to the request (correlation, tracking, routing through a gateway...)
        request.Headers.Add("X-agent-name", "AIAgentWithCustomHttpTransport");

        // Step 2: Write the HTTP request: method and URL, headers (credentials masked by the helper), body
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WriteInfoLine($"=== HTTP REQUEST #{number} ===");
        ColoredConsole.WritePrimaryLogLine($"[URL] {request.Method} {request.RequestUri}");
        WriteHeaders(request.Headers, request.Content);
        await WriteBodyAsync(request.Content, cancellationToken);

        // Step 3: Send the request to the next handler (the network) and measure the duration
        long start = Stopwatch.GetTimestamp();
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        TimeSpan duration = Stopwatch.GetElapsedTime(start);

        // Step 4: Write the HTTP response: status and duration, headers, body; then return it to the SDK
        ColoredConsole.WriteEmptyLine();
        ColoredConsole.WriteSuccessLine($"=== HTTP RESPONSE #{number} ===");
        ColoredConsole.WritePrimaryLogLine($"[Status] {(int)response.StatusCode} {response.StatusCode} ({StatusLabel(response.StatusCode)}) in {duration.TotalMilliseconds:F0} ms");
        WriteHeaders(response.Headers, response.Content);
        await WriteBodyAsync(response.Content, cancellationToken);
        ColoredConsole.WriteDividerLine();

        return response;
    }
}

#endregion
