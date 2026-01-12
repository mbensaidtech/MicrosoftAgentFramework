using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Microsoft.Agents.AI.Workflows;
using CommonUtilities;

using OrchestrationConcurrent;

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Chat Deployment Name: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Concurrent Workflow

ColoredConsole.WriteDividerLine();
ColoredConsole.WriteInfoLine("=== Concurrent Workflow ===");

// Step 1: Create specialized agents (these will run concurrently)
// Hint: CreateAIAgent() with name and instructions
// - Return/Exchange Agent: detects if the customer needs a return or exchange
// - Refund Agent: checks if the customer is eligible for a refund
// - Follow-up Agent: creates a polite reply summarizing actions

// Step 2: Build concurrent workflow with agents
// Hint: AgentWorkflowBuilder.BuildConcurrent()

// Step 3: Prepare input messages
// Hint: Create a List<ChatMessage> with the customer message:
// "I received my order, but the charger is missing and I also want to return the old one."

// Step 4: Execute the workflow
// Hint: InProcessExecution.StreamAsync(), TrySendMessageAsync()

// Step 5: Collect results from workflow events
// Hint: WatchStreamAsync(), WorkflowOutputEvent

// Step 6: Display final results
// Hint: Loop through messages and display Role, AuthorName, Text

#endregion
