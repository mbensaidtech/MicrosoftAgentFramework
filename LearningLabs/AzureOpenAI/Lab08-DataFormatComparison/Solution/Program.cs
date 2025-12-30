using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ToonNetSerializer;

using CommonUtilities;
using DataFormatComparison;
using DataFormatComparison.Tools;
using DataFormatComparison.Models;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [2];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WriteSecondaryLogLine($"Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: JSON Format Response

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Using JSON format ===");

    //Step 1: Create a hotel agent with structured output and tools
    ChatClientAgent hotelAgent = chatClient.CreateAIAgent(
        instructions: @"You are a hotel expert assistant. When asked about a hotel",
        name: "HotelAgent",
        tools:[AIFunctionFactory.Create(HotelTools.GetAllHotelsUsingJsonFormat, "get_all_hotels")]);

    //Step 2: Run the agent with a question
    AgentRunResponse<List<Hotel>> hotelResponse = await hotelAgent.RunAsync<List<Hotel>>("Get hotels with price less than 100 and order them by price");

    //Step 3: Display the response
    foreach (var hotel in hotelResponse.Result)
    {
        Console.WriteLine($"Hotel: {hotel.Name}, Price: {hotel.PricePerNight}");
    }

    // Step 4: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {hotelResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {hotelResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {hotelResponse.Usage?.TotalTokenCount}");

}

#endregion


#region Scenario 2: Text Format Response

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Using Toon format ===");

    //Step 1: Create a hotel agent with tools
    ChatClientAgent hotelAgent = chatClient.CreateAIAgent(
    instructions: @"You are a hotel data assistant.
    You MUST respond ONLY with the exact Toon format.
    NO explanations, NO markdown, NO code fences, NO additional text.
    The format must be exactly like this example:
    Name,PricePerNight,Currency
    Hotel1,100,USD
    Hotel2,200,USD
    ",
    name: "HotelAgent",tools:[AIFunctionFactory.Create(HotelTools.GetAllHotelsUsingToonFormat, "get_all_hotels")]);

    //Step 2: Run the agent with a question
    AgentRunResponse hotelResponse = await hotelAgent.RunAsync("Get hotels with price less than 100 and order them by price.");

    //Step 3: Display the response
    ColoredConsole.WritePrimaryLogLine("Response: ");
    ColoredConsole.WriteSecondaryLogLine(hotelResponse.ToString());

    // Step 4: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {hotelResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {hotelResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {hotelResponse.Usage?.TotalTokenCount}");

}

#endregion

