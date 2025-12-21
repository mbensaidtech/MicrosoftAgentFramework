using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using CommonUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIAgentWithSO;
using AIAgentWithSO.Models;

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.DeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.DeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Manual Structured Output - Restaurant Information

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

// Step 2: Run the agent with a restaurant question
ColoredConsole.WriteInfoLine("=== Scenario 1: Manually defined structured output - Restaurant Information ===");
AgentRunResponse restaurantResponse = await restaurantAgent.RunAsync(
    "Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON.");

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

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 2: automatically generated structured output (Recommended) - Restaurant Information

// Step 1: Create an agent with structured output instructions for Restaurant
ChatClientAgent restaurantAgentWithStructuredOutput = chatClient.CreateAIAgent(
    instructions: @"You are a culinary expert assistant. When asked about a restaurant.",
    name: "RestaurantInfoAgent");

// Step 2: Run the agent with a restaurant question
ColoredConsole.WriteInfoLine("=== Scenario 2: Automatically generated structured output - Restaurant Information ===");
AgentRunResponse<Restaurant> structuredRestaurantResponse = await restaurantAgentWithStructuredOutput.RunAsync<Restaurant>(
    "Tell me about the restaurant 'Le Bernardin' in New York.");

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

#endregion