using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentWithFunctionTools;

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.DeploymentName}");

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// Use the endpoint from settings and DefaultAzureCredential for authentication
// AzureOpenAIClient client = ...

// TODO 2: Get a ChatClient for the specific deployment
// Use the deployment name from settings
// ChatClient chatClient = ...

#endregion

ColoredConsole.WriteDividerLine();
