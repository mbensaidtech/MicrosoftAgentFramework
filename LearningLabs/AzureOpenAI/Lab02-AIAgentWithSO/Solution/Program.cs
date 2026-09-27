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

// Step 4: JSON options used to generate the schema (scenarios 3 and 4) and to deserialize the JSON text ourselves:
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

    // Step 1: Create an agent whose instructions describe the expected JSON format
    AIAgent restaurantAgent = chatClient.AsAIAgent(
        instructions: """
            You are a culinary expert assistant. When asked about a restaurant, always respond with valid JSON in this exact format:
            {
                "Name": "restaurant name",
                "ChefName": "head chef name",
                "Cuisine": "French|Italian|Japanese|Chinese|Mexican|Indian|Spanish|American|Mediterranean|Thai|Korean|Vietnamese|Other",
                "MichelinStars": number (0-3),
                "AveragePricePerPerson": number (in euros),
                "City": "city name",
                "Country": "country name",
                "YearEstablished": number
            }
            Only respond with the JSON, no other text.
            """,
        name: "RestaurantInfoAgent");

    // Step 2: Run the agent with a restaurant question (with spinner to show loading)
    AgentResponse response = await restaurantAgent.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON.").WithSpinner("Running agent");

    // Step 3: Parse the JSON text ourselves - nothing guarantees that the model followed the format
    ColoredConsole.WriteInfoLine("=== Scenario 1: Manually defined structured output ===");
    try
    {
        Restaurant? restaurant = JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions);
        if (restaurant is not null)
        {
            ColoredConsole.WritePrimaryLogLine("Successfully parsed structured response:");
            WriteRestaurant(restaurant);
        }
    }
    catch (JsonException ex)
    {
        ColoredConsole.WriteErrorLine($"Failed to parse JSON: {ex.Message}");
        ColoredConsole.WriteSecondaryLogLine(response.Text);
    }

    // Step 4: Display token usage
    WriteTokenUsage(response);
}

#endregion

#region Scenario 2: Automatic Structured Output with RunAsync<T> (Recommended)

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an agent with simple instructions - no JSON format needed
    AIAgent restaurantAgent = chatClient.AsAIAgent(
        instructions: "You are a culinary expert assistant that answers questions about restaurants.",
        name: "RestaurantInfoAgent");

    // Step 2: Run the agent with RunAsync<T>: the framework generates the JSON schema from the Restaurant type,
    // sends it as the response format of this run, and deserializes the answer
    AgentResponse<Restaurant> response = await restaurantAgent.RunAsync<Restaurant>("Tell me about the restaurant 'Le Bernardin' in New York.").WithSpinner("Running agent");

    // Step 3: Display the strongly-typed result
    ColoredConsole.WriteInfoLine("=== Scenario 2: Automatically generated structured output with RunAsync<T> ===");
    ColoredConsole.WritePrimaryLogLine("Structured response:");
    WriteRestaurant(response.Result);

    // Step 4: Display token usage
    WriteTokenUsage(response);
}

#endregion

#region Scenario 3: Structured Output configured on the agent - ChatClientAgentOptions and ResponseFormat

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an agent whose ChatOptions define the instructions and the response format of every run
    AIAgent restaurantAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = "RestaurantInfoAgent",
        ChatOptions = new AIExtensions.ChatOptions
        {
            Instructions = "You are a culinary expert assistant that answers questions about restaurants.",
            // JSON schema generated from the Restaurant type (its [Description] becomes the schema description)
            ResponseFormat = AIExtensions.ChatResponseFormat.ForJsonSchema<Restaurant>(jsonOptions, schemaName: "RestaurantInfo")
        }
    });

    // Step 2: Run the agent with the non-generic RunAsync (with spinner to show loading)
    AgentResponse response = await restaurantAgent.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York.").WithSpinner("Running agent");

    // Step 3: The answer is JSON text that follows the schema: use it as is (logs, other agents...) or deserialize it
    ColoredConsole.WriteInfoLine("=== Scenario 3: Structured output configured on the agent ===");
    ColoredConsole.WritePrimaryLogLine("JSON response:");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    Restaurant restaurant = JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions)!;
    ColoredConsole.WritePrimaryLogLine("Deserialized response:");
    WriteRestaurant(restaurant);

    // Step 4: Display token usage
    WriteTokenUsage(response);
}

#endregion

#region Scenario 4: Structured Output per run with AgentRunOptions - Streaming

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an agent with simple instructions - no response format on the agent itself
    AIAgent restaurantAgent = chatClient.AsAIAgent(
        instructions: "You are a culinary expert assistant that answers questions about restaurants.",
        name: "RestaurantInfoAgent");

    // Step 2: Define the response format for this run only
    AgentRunOptions runOptions = new()
    {
        ResponseFormat = AIExtensions.ChatResponseFormat.ForJsonSchema<Restaurant>(jsonOptions, schemaName: "RestaurantInfo")
    };

    // Step 3: Stream the answer: the JSON arrives piece by piece
    // (no spinner here: the text itself shows the progress)
    ColoredConsole.WriteInfoLine("=== Scenario 4: Structured output with AgentRunOptions and streaming ===");
    List<AgentResponseUpdate> updates = [];
    await foreach (AgentResponseUpdate update in restaurantAgent.RunStreamingAsync("Tell me about the restaurant 'Le Bernardin' in New York.", options: runOptions))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }

    Console.WriteLine();

    // Step 4: The JSON can only be deserialized once complete: combine the updates into a single AgentResponse
    AgentResponse response = updates.ToAgentResponse();
    Restaurant restaurant = JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions)!;
    ColoredConsole.WritePrimaryLogLine("Deserialized response:");
    WriteRestaurant(restaurant);

    // Step 5: Display token usage
    WriteTokenUsage(response);
}

#endregion
