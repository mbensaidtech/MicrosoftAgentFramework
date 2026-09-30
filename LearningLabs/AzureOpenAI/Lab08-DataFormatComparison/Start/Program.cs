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

    // TODO 4: Turn hotelTools.GetAllHotelsAsJson into a tool named "get_all_hotels" and create the agent with it
    // Hint: AITool getAllHotelsAsJson = AIFunctionFactory.Create(hotelTools.GetAllHotelsAsJson, "get_all_hotels");
    //       chatClient.AsAIAgent(instructions: "...", name: "HotelJsonAgent", tools: [getAllHotelsAsJson]) (see the README for the instructions)
    // AIAgent jsonAgent = ...

    // TODO 5: Run the agent with structured output: the answer follows the JSON schema of List<Hotel>
    // Hint: jsonResponse = await jsonAgent.RunAsync<List<Hotel>>(question).WithSpinner("Running agent");

    // TODO 6: Display the tool calls and the typed result
    // Hint: WriteToolCalls(jsonResponse); WriteHotels(jsonResponse.Result, "Hotels returned by the agent");

    // TODO 7: Display token usage
    // Hint: WriteTokenUsage(jsonResponse, "Token Usage (JSON):")
}

#endregion

#region Scenario 2: CSV - the tool returns text, the agent answers in CSV

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: CSV - the tool returns text, the agent answers in CSV ===");
    ColoredConsole.WriteSecondaryLogLine($"Question: {question}");

    // TODO 8: Turn hotelTools.GetAllHotelsAsCsv into a tool named "get_all_hotels" and create the agent with it
    // Hint: AITool getAllHotelsAsCsv = AIFunctionFactory.Create(hotelTools.GetAllHotelsAsCsv, "get_all_hotels");
    //       chatClient.AsAIAgent(instructions: $"""...""", name: "HotelCsvAgent", tools: [getAllHotelsAsCsv])
    //       The instructions (see the README) ask for a CSV answer with the header line HotelCsv.Header
    // AIAgent csvAgent = ...

    // TODO 9: Run the agent with the non-generic RunAsync: the answer is text
    // Hint: csvResponse = await csvAgent.RunAsync(question).WithSpinner("Running agent");

    // TODO 10: Display the tool calls and the raw answer
    // Hint: WriteToolCalls(csvResponse);
    //       ColoredConsole.WritePrimaryLogLine("Agent answer (CSV):"); ColoredConsole.WriteSecondaryLogLine(csvResponse.Text);

    // TODO 11: Parse the CSV back into hotels with HotelCsv.Deserialize(csvResponse.Text) in a try/catch (FormatException),
    // then display "Parsed back {count} hotels from the CSV answer" (or the error message)

    // TODO 12: Display token usage
    // Hint: WriteTokenUsage(csvResponse, "Token Usage (CSV):")
}

#endregion

#region Comparison: JSON vs CSV

// Both scenarios asked the same question on the same data: compare what the model received (tool result) and consumed (tokens)
if (jsonResponse is not null && csvResponse is not null)
{
    // TODO 13: Display the comparison
    // Hint: WriteComparison(jsonResponse, csvResponse)
}

#endregion
