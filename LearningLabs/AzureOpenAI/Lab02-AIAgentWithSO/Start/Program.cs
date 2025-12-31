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
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// Use the endpoint from settings and DefaultAzureCredential for authentication
// AzureOpenAIClient client = ...

// TODO 2: Get a ChatClient for the specific deployment
// Use the deployment name from settings
// ChatClient chatClient = ...

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Manual Structured Output - Restaurant Information

// TODO 3: Create an agent with structured output instructions for Restaurant
// Use CreateAIAgent with instructions that specify the JSON format
// The instructions should tell the agent to respond with valid JSON matching the Restaurant model
// ChatClientAgent restaurantAgent = ...

// TODO 4: Run the agent with a restaurant question
// Ask about a restaurant like "Le Bernardin" in New York, request JSON response
// AgentRunResponse restaurantResponse = ...

// TODO 5: Display the scenario title
ColoredConsole.WriteInfoLine("=== Scenario 1: Manually defined structured output - Restaurant Information ===");

// TODO 6: Parse the JSON response using JsonSerializer
// Use JsonSerializerOptions with PropertyNameCaseInsensitive and JsonStringEnumConverter
// Restaurant restaurant = ...

// TODO 7: Display the parsed restaurant information
// Use ColoredConsole.WritePrimaryLogLine and ColoredConsole.WriteSecondaryLogLine
// Display: Name, ChefName, Cuisine, MichelinStars, AveragePricePerPerson, City, Country, YearEstablished

// TODO 8: Display token usage
// Use restaurantResponse.Usage properties: InputTokenCount, OutputTokenCount, TotalTokenCount

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 2: Automatically generated structured output (Recommended) - Restaurant Information

// TODO 9: Create an agent for automatic structured output
// Use CreateAIAgent with simple instructions (no JSON format specification needed)
// ChatClientAgent restaurantAgentWithStructuredOutput = ...

// TODO 10: Display the scenario title
ColoredConsole.WriteInfoLine("=== Scenario 2: Automatically generated structured output - Restaurant Information ===");

// TODO 11: Run the agent with RunAsync<Restaurant> for automatic structured output
// The generic method automatically generates JSON schema from the Restaurant type
// AgentRunResponse<Restaurant> structuredRestaurantResponse = ...

// TODO 12: Display the structured response
// Access properties directly via structuredRestaurantResponse.Result (no manual JSON parsing needed!)
// Display: Name, ChefName, Cuisine, MichelinStars, AveragePricePerPerson, City, Country, YearEstablished

// TODO 13: Display token usage
// Use structuredRestaurantResponse.Usage properties: InputTokenCount, OutputTokenCount, TotalTokenCount

#endregion
