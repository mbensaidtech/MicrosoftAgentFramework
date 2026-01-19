using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

using CommonUtilities;
using AIAgentWithSO;
using AIAgentWithSO.Models;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1,2,3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Manual Structured Output - Restaurant Information

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    // Step 1: Create an agent with structured output instructions for Restaurant
    ChatClientAgent restaurantAgent = chatClient.CreateAIAgent(
        instructions: @"You are a culinary expert assistant. When asked about a restaurant, always respond with valid JSON in this exact format:
            {
                ""Name"": ""restaurant name"",
                ""ChefName"": ""head chef name"",
                ""Cuisine"": ""French|Italian|Japanese|Chinese|Mexican|Indian|Spanish|American|Mediterranean|Thai|Korean|Vietnamese|Other"",
                ""MichelinStars"": number (0-3),
                ""AveragePricePerPerson"": number (in euros),
                ""City"": ""city name"",
                ""Country"": ""country name"",
                ""YearEstablished"": number
            }
            Only respond with the JSON, no other text.",
        name: "RestaurantInfoAgent");

    // Step 2: Run the agent with a restaurant question (with spinner to show loading)
    ColoredConsole.WriteInfoLine("=== Scenario 1: Manually defined structured output - Restaurant Information ===");
    AgentRunResponse restaurantResponse = await restaurantAgent.RunAsync(
        "Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON.").WithSpinner("Running agent");

    // Step 3: Parse and display the structured response
    try
    {
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        
        var restaurant = JsonSerializer.Deserialize<Restaurant>(restaurantResponse.ToString(), options);
        
        if (restaurant != null)
        {
            ColoredConsole.WritePrimaryLogLine("Successfully parsed structured response:");
            ColoredConsole.WriteSecondaryLogLine($"Name: {restaurant.Name}");
            ColoredConsole.WriteSecondaryLogLine($"Chef: {restaurant.ChefName}");
            ColoredConsole.WriteSecondaryLogLine($"Cuisine: {restaurant.Cuisine}");
            ColoredConsole.WriteSecondaryLogLine($"Michelin Stars: {restaurant.MichelinStars}");
            ColoredConsole.WriteSecondaryLogLine($"Average Price: €{restaurant.AveragePricePerPerson}");
            ColoredConsole.WriteSecondaryLogLine($"Location: {restaurant.City}, {restaurant.Country}");
            ColoredConsole.WriteSecondaryLogLine($"Established: {restaurant.YearEstablished}");
        }
    }
    catch (JsonException ex)
    {
        ColoredConsole.WriteSecondaryLogLine($"Failed to parse JSON: {ex.Message}");
    }

    // Step 4: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {restaurantResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {restaurantResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {restaurantResponse.Usage?.TotalTokenCount}");
}

#endregion

#region Scenario 2: automatically generated structured output using ChatClientAgent and RunAsync<T> (Recommended) - Restaurant Information

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an agent with structured output instructions for Restaurant
    ChatClientAgent restaurantAgentWithStructuredOutput = chatClient.CreateAIAgent(
        instructions: "You are a helpful assistant that can answer questions about restaurants.",
        name: "RestaurantInfoAgent");

    // Step 2: Run the agent with a restaurant question (with spinner to show loading)
    ColoredConsole.WriteInfoLine("=== Scenario 2: Automatically generated structured output - Restaurant Information ===");
    AgentRunResponse<Restaurant> structuredRestaurantResponse = await restaurantAgentWithStructuredOutput.RunAsync<Restaurant>(
        "Tell me about the restaurant 'Le Bernardin' in New York.").WithSpinner("Running agent");

    // Step 3: Display the structured response
    ColoredConsole.WritePrimaryLogLine("Structured response: ");
    ColoredConsole.WriteSecondaryLogLine($"Name: {structuredRestaurantResponse.Result.Name}");
    ColoredConsole.WriteSecondaryLogLine($"Chef: {structuredRestaurantResponse.Result.ChefName}");
    ColoredConsole.WriteSecondaryLogLine($"Cuisine: {structuredRestaurantResponse.Result.Cuisine}");
    ColoredConsole.WriteSecondaryLogLine($"Michelin Stars: {structuredRestaurantResponse.Result.MichelinStars}");
    ColoredConsole.WriteSecondaryLogLine($"Average Price: €{structuredRestaurantResponse.Result.AveragePricePerPerson}");
    ColoredConsole.WriteSecondaryLogLine($"Location: {structuredRestaurantResponse.Result.City}, {structuredRestaurantResponse.Result.Country}");
    ColoredConsole.WriteSecondaryLogLine($"Established: {structuredRestaurantResponse.Result.YearEstablished}");

    // Step 4: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {structuredRestaurantResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {structuredRestaurantResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {structuredRestaurantResponse.Usage?.TotalTokenCount}");
}

#endregion

#region Scenario 3: Automatically generated structured output using AIAgent and ChatOptions - Restaurant Information

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // Step1: Create a schema for the structured output
    JsonElement schema = AIExtensions.AIJsonUtilities.CreateJsonSchema(typeof(Restaurant));

    // Step2: Create a ChatOptions object with the schema
    AIExtensions.ChatOptions chatOptions = new()
    {
        Instructions = "You are a helpful assistant that can answer questions about restaurants.",
        ResponseFormat = AIExtensions.ChatResponseFormat.ForJsonSchema(
            schema: schema,
            schemaName: "RestaurantInfo",
            schemaDescription: "Information about a restaurant including its name, chef, cuisine, Michelin stars, average price per person, city, country, and year established")
    };

    // Step 3: Create an agent with structured output instructions for Restaurant
    AIAgent restaurantAgentWithStructuredOutput = chatClient.CreateAIAgent( new ChatClientAgentOptions()
    {
        Name = "RestaurantInfoAgent",
        ChatOptions = chatOptions
    });

    // Step 4: Run the agent with a restaurant question (with spinner to show loading)
    var response = await restaurantAgentWithStructuredOutput.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York.").WithSpinner("Running agent");
    
    // JsonSerializerOptions.Web doesn't include JsonStringEnumConverter, so we need custom options
    var jsonOptions = new JsonSerializerOptions(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    var restaurantInfo = response.Deserialize<Restaurant>(jsonOptions);

    // Step 5: Display the structured response
    ColoredConsole.WritePrimaryLogLine("Structured response: ");
    ColoredConsole.WriteSecondaryLogLine($"Name: {restaurantInfo.Name}");
    ColoredConsole.WriteSecondaryLogLine($"Chef: {restaurantInfo.ChefName}");
    ColoredConsole.WriteSecondaryLogLine($"Cuisine: {restaurantInfo.Cuisine}");
    ColoredConsole.WriteSecondaryLogLine($"Michelin Stars: {restaurantInfo.MichelinStars}");
    ColoredConsole.WriteSecondaryLogLine($"Average Price: €{restaurantInfo.AveragePricePerPerson}");
    ColoredConsole.WriteSecondaryLogLine($"Location: {restaurantInfo.City}, {restaurantInfo.Country}");
    ColoredConsole.WriteSecondaryLogLine($"Established: {restaurantInfo.YearEstablished}");

    // Step 6: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
}

#endregion