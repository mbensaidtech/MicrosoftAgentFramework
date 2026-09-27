using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using CommonUtilities;

using AIAgentWithSO;
using AIAgentWithSO.Models;
using static AIAgentWithSO.RestaurantConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3], [4] or [1, 2, 3, 4] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3, 4];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

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

// Provided: JSON options used to generate the schema (scenarios 3 and 4) and to deserialize the JSON text ourselves:
// web defaults (camelCase, case-insensitive property names) + enums written as strings (e.g. "French")
JsonSerializerOptions jsonOptions = new(JsonSerializerOptions.Web)
{
    Converters = { new JsonStringEnumConverter() }
};

#endregion

#region Scenario 1: Manual Structured Output - JSON format described in the instructions

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();

    // TODO 4: Create an agent whose instructions describe the expected JSON format (see the README for the instructions)
    // Hint: chatClient.AsAIAgent(instructions: "...", name: "RestaurantInfoAgent")
    // AIAgent restaurantAgent = ...

    // TODO 5: Run the agent with the restaurant question, asking for JSON only
    // Hint: await restaurantAgent.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON.").WithSpinner("Running agent")
    // AgentResponse response = ...

    ColoredConsole.WriteInfoLine("=== Scenario 1: Manually defined structured output ===");

    // TODO 6: Parse the JSON text yourself and display the restaurant
    // Hint: JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions) inside a try/catch (JsonException),
    //       then WriteRestaurant(restaurant)

    // TODO 7: Display token usage
    // Hint: WriteTokenUsage(response)
}

#endregion

#region Scenario 2: Automatic Structured Output with RunAsync<T> (Recommended)

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // TODO 8: Create an agent with simple instructions - no JSON format needed
    // AIAgent restaurantAgent = ...

    // TODO 9: Run the agent with RunAsync<Restaurant>: the framework generates the JSON schema and deserializes the answer
    // AgentResponse<Restaurant> response = ...

    ColoredConsole.WriteInfoLine("=== Scenario 2: Automatically generated structured output with RunAsync<T> ===");

    // TODO 10: Display the strongly-typed result
    // Hint: response.Result is already a Restaurant - WriteRestaurant(response.Result)

    // TODO 11: Display token usage
    // Hint: AgentResponse<T> is an AgentResponse - WriteTokenUsage(response)
}

#endregion

#region Scenario 3: Structured Output configured on the agent - ChatClientAgentOptions and ResponseFormat

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // TODO 12: Create an agent with ChatClientAgentOptions whose ChatOptions define the instructions and the response format
    // Hint: ResponseFormat = AIExtensions.ChatResponseFormat.ForJsonSchema<Restaurant>(jsonOptions, schemaName: "RestaurantInfo")
    // AIAgent restaurantAgent = ...

    // TODO 13: Run the agent with the non-generic RunAsync
    // AgentResponse response = ...

    ColoredConsole.WriteInfoLine("=== Scenario 3: Structured output configured on the agent ===");

    // TODO 14: Display the JSON text (response.Text), then deserialize it and display the restaurant
    // Hint: JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions)

    // TODO 15: Display token usage
}

#endregion

#region Scenario 4: Structured Output per run with AgentRunOptions - Streaming

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();

    // TODO 16: Create an agent with simple instructions - no response format on the agent itself
    // AIAgent restaurantAgent = ...

    // TODO 17: Define the response format for this run only
    // Hint: AgentRunOptions runOptions = new() { ResponseFormat = ... }

    ColoredConsole.WriteInfoLine("=== Scenario 4: Structured output with AgentRunOptions and streaming ===");

    // TODO 18: Stream the answer with RunStreamingAsync("Tell me about the restaurant 'Le Bernardin' in New York.", options: runOptions)
    // Hint: write each update.Text with Console.Write and keep the updates in a List<AgentResponseUpdate>
    //       (no .WithSpinner() here: the spinner would overwrite the streamed text)

    // TODO 19: Combine the updates into one AgentResponse, deserialize its Text, display the restaurant and the token usage
    // Hint: AgentResponse response = updates.ToAgentResponse();
}

#endregion
