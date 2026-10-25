using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenTelemetry;
using OpenTelemetry.Trace;
using CommonUtilities;

using AIAgentWithCustomHttpTransport;
using AIAgentWithCustomHttpTransport.Tools;
using static AIAgentWithCustomHttpTransport.HttpConsole;
using static AIAgentWithCustomHttpTransport.TraceConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create the logging handler (HttpLoggingHandler, bottom of this file) and an HttpClient that uses it
// - HttpLoggingHandler is a DelegatingHandler: set its InnerHandler to a new HttpClientHandler (the handler that talks to the network)
// - Keep the handler in a variable: the scenarios read its RequestCount
// HttpLoggingHandler loggingHandler = ...
// using HttpClient httpClient = ...

// TODO 2: Create the OpenAIClientOptions
// - Endpoint: AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) turns "https://<resource>.openai.azure.com/" into ".../openai/v1/"
// - Transport: a new HttpClientPipelineTransport built from httpClient (the OpenAI SDK will send its requests with it)
// - NetworkTimeout: TimeSpan.FromSeconds(30) (applied to each attempt)
// OpenAIClientOptions clientOptions = ...

// TODO 3: Create the OpenAIClient with these options
// - If settings.APIKey is set: authenticate with an ApiKeyCredential
// - Otherwise: authenticate with Microsoft Entra ID using a BearerTokenPolicy built from
//   DefaultAzureCredential and the scope "https://ai.azure.com/.default"
// Note: the AuthenticationPolicy constructor is flagged experimental (OPENAI001), keep the pragma around it.
#pragma warning disable OPENAI001
// OpenAIClient client = ...
#pragma warning restore OPENAI001

// TODO 4: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

#endregion

#region Scenario 1: See the HTTP traffic of an agent run

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: See the HTTP traffic of an agent run ===");
    string prompt = "Hello, what is the capital of France?";
    ColoredConsole.WriteUserLine($"User: {prompt}");

    // TODO 5: Create a basic AI Agent from the ChatClient (no instructions, no name)
    // AIAgent basicAgent = ...

    // TODO 6: Run the agent with the prompt, counting the HTTP requests sent by the handler during the run
    // Hint: read loggingHandler.RequestCount before the run; no spinner here (it would write over the HTTP log)
    // int requestsBefore = ...
    // AgentResponse basicResponse = ...

    // TODO 7: Display the answer, the number of HTTP requests of the run and the token usage
    // Hint: basicResponse.Text, loggingHandler.RequestCount - requestsBefore, WriteTokenUsage(basicResponse) (HttpConsole.cs)
}

#endregion

#region Scenario 2: One agent run, several HTTP requests (function calling)

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: One agent run, several HTTP requests (function calling) ===");
    string prompt = "What is the weather like in Amsterdam?";
    ColoredConsole.WriteUserLine($"User: {prompt}");

    // TODO 8: Create an agent with instructions, a name and the function tool WeatherTools.GetWeather (Tools/WeatherTools.cs)
    // Hint: tools: [AIFunctionFactory.Create(WeatherTools.GetWeather, "get_weather")]
    // AIAgent weatherAgent = ...

    // TODO 9: Run the agent with the prompt, counting the HTTP requests (same as TODO 6)
    // int requestsBefore = ...
    // AgentResponse weatherResponse = ...

    // TODO 10: Display the answer, the number of HTTP requests of the run and the token usage (same as TODO 7)
}

#endregion

#region Scenario 3: Observe an agent with OpenTelemetry

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Observe an agent with OpenTelemetry ===");
    string prompt = "I am going to Amsterdam tomorrow. Do I need an umbrella?";
    ColoredConsole.WriteUserLine($"User: {prompt}");

    // TODO 15: Create the tracer provider: it listens to the activity source "AIAgentWithCustomHttpTransport"
    // and keeps the finished spans in a list
    // Hint: const string TelemetrySourceName = "AIAgentWithCustomHttpTransport"; List<Activity> spans = [];
    //       using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder().AddSource(...).AddInMemoryExporter(spans).Build();

    // TODO 16: Create the weather agent of scenario 2 (with a description), instrumented at the chat client level and at the agent level
    // Hint: chatClient.AsAIAgent(..., clientFactory: innerClient => innerClient.AsBuilder().UseOpenTelemetry(sourceName: TelemetrySourceName).Build())
    //       .AsBuilder().UseOpenTelemetry(sourceName: TelemetrySourceName).Build()
    // AIAgent weatherAgent = ...

    // TODO 17: Create a travel assistant with the weather agent as a tool (weatherAgent.AsAIFunction()), instrumented the same way
    // AIAgent travelAgent = ...

    // TODO 18: Run the travel assistant with the HTTP log turned off, counting the HTTP requests
    // Hint: loggingHandler.WriteTraffic = false; then the run (as TODO 6, a spinner is fine here); then loggingHandler.WriteTraffic = true;
    // AgentResponse travelResponse = ...

    // TODO 19: Display the answer, the spans (WriteSpanTree(spans)), the number of HTTP requests of the run and the token usage
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

    /// <summary>When false, the requests are counted and forwarded without being written (scenario 3).</summary>
    public bool WriteTraffic { get; set; } = true;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The same handler can serve several requests at the same time: count them atomically
        int number = Interlocked.Increment(ref _requestCount);

        // TODO 11: Add the custom header "X-agent-name" with the value "AIAgentWithCustomHttpTransport" to the request

        // Provided: scenario 3 turns the HTTP log off, OpenTelemetry shows the same run as spans
        if (!WriteTraffic)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // TODO 12: Write the HTTP request
        // - a divider and the title $"=== HTTP REQUEST #{number} ==="
        // - the method and the URL: request.Method, request.RequestUri
        // - the headers and the body with the helpers of HttpConsole.cs: WriteHeaders(...), await WriteBodyAsync(...)

        // TODO 13: Send the request to the next handler (the network) and measure the duration
        // Replace the last line of this method: keep the response of base.SendAsync(...) in a variable,
        // and measure the call with Stopwatch.GetTimestamp() / Stopwatch.GetElapsedTime(start)

        // TODO 14: Write the HTTP response, then return it to the SDK
        // - the title $"=== HTTP RESPONSE #{number} ===", the status (code, name, StatusLabel(...)) and the duration
        // - the headers and the body (same helpers), a divider, then: return response;

        // Until TODO 13 is done, the request is only forwarded to the network
        return await base.SendAsync(request, cancellationToken);
    }
}

#endregion
