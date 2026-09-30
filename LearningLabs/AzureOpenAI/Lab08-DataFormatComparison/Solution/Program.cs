using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using DataFormatComparison;
using DataFormatComparison.Models;
using DataFormatComparison.Tools;
using static DataFormatComparison.FormatConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2] or [1, 2] to run specific scenarios. The comparison at the end needs both.
HashSet<int> scenariosToRun = [1, 2];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration, client and hotel data

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

// Step 4: Load the hotel catalog once; both scenarios expose it to the agent through HotelTools, in a different format
List<Hotel> hotels = await HotelData.LoadAsync();
HotelTools hotelTools = new(hotels);
Console.WriteLine($"Loaded {hotels.Count} hotels from {HotelData.FileName}");

// The same question is asked in both scenarios; the responses are kept for the comparison at the end
string question = "Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.";
AgentResponse<List<Hotel>>? jsonResponse = null;
AgentResponse? csvResponse = null;

#endregion

#region Scenario 1: JSON - the tool returns objects, the agent answers with structured output

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: JSON - the tool returns objects, the agent answers with structured output ===");
    ColoredConsole.WriteSecondaryLogLine($"Question: {question}");

    // Step 1: The tool returns the hotels as objects: the framework serializes them as JSON for the model
    AITool getAllHotelsAsJson = AIFunctionFactory.Create(hotelTools.GetAllHotelsAsJson, "get_all_hotels");
    AIAgent jsonAgent = chatClient.AsAIAgent(
        instructions: "You are a hotel data assistant. Call get_all_hotels to get the hotels, then answer with every hotel that matches the request, in the requested order.",
        name: "HotelJsonAgent",
        tools: [getAllHotelsAsJson]);

    // Step 2: RunAsync<T> (Lab 02): the answer is JSON that follows the schema generated from List<Hotel>, deserialized for us
    jsonResponse = await jsonAgent.RunAsync<List<Hotel>>(question).WithSpinner("Running agent");

    // Step 3: Display the tool calls (with the size of the tool result the model received) and the typed result
    WriteToolCalls(jsonResponse);
    WriteHotels(jsonResponse.Result, "Hotels returned by the agent");

    // Step 4: Display token usage (total of the model calls of the run: the one that asks for the tool, then the one that answers)
    WriteTokenUsage(jsonResponse, "Token Usage (JSON):");
}

#endregion

#region Scenario 2: CSV - the tool returns text, the agent answers in CSV

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: CSV - the tool returns text, the agent answers in CSV ===");
    ColoredConsole.WriteSecondaryLogLine($"Question: {question}");

    // Step 1: The tool returns the hotels as CSV text (sent to the model as is), and the instructions ask for CSV back
    AITool getAllHotelsAsCsv = AIFunctionFactory.Create(hotelTools.GetAllHotelsAsCsv, "get_all_hotels");
    AIAgent csvAgent = chatClient.AsAIAgent(
        instructions: $"""
            You are a hotel data assistant. Call get_all_hotels to get the hotels as CSV, then answer with every hotel that matches the request, in the requested order.
            Answer ONLY in CSV, with exactly this header line and then one line per hotel, with the same columns and values as the tool result:
            {HotelCsv.Header}
            No explanation, no markdown, no code fences.
            """,
        name: "HotelCsvAgent",
        tools: [getAllHotelsAsCsv]);

    // Step 2: The non-generic RunAsync: the answer is text
    csvResponse = await csvAgent.RunAsync(question).WithSpinner("Running agent");

    // Step 3: Display the tool calls and the raw answer
    WriteToolCalls(csvResponse);
    ColoredConsole.WritePrimaryLogLine("Agent answer (CSV):");
    ColoredConsole.WriteSecondaryLogLine(csvResponse.Text);

    // Step 4: Parse the CSV back into hotels. No schema guarantees the format (unlike the structured output of scenario 1):
    // the instructions are a request, not a guarantee, so the parsing can fail
    try
    {
        List<Hotel> csvHotels = HotelCsv.Deserialize(csvResponse.Text);
        ColoredConsole.WritePrimaryLogLine($"Parsed back {csvHotels.Count} hotels from the CSV answer");
    }
    catch (FormatException ex)
    {
        ColoredConsole.WriteErrorLine($"The answer is not the expected CSV: {ex.Message}");
    }

    // Step 5: Display token usage
    WriteTokenUsage(csvResponse, "Token Usage (CSV):");
}

#endregion

#region Comparison: JSON vs CSV

// Both scenarios asked the same question on the same data: compare what the model received (tool result) and consumed (tokens)
if (jsonResponse is not null && csvResponse is not null)
{
    WriteComparison(jsonResponse, csvResponse);
}

#endregion
