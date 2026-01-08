using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Microsoft.Agents.AI.Workflows;
using CommonUtilities;

using OrchestrationSequential;

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

#region Sequential Workflow

ColoredConsole.WriteDividerLine();
ColoredConsole.WriteInfoLine("=== Sequential Workflow ===");

// Step 1: Load meeting transcript
string meetingTranscript = File.ReadAllText("Data/meeting-transcript.txt");
ColoredConsole.WriteSecondaryLogLine($"Loaded transcript: {meetingTranscript.Length} characters");

// Step 2: Create specialized agents
// Hint: CreateAIAgent() with name and instructions
// - SummaryAgent: summarizes the meeting transcript
// - ActionsExtractorAgent: extracts action items

// Step 3: Build sequential workflow with agents
// Hint: AgentWorkflowBuilder.BuildSequential()

// Step 4: Prepare input messages
// Hint: Create a List<ChatMessage> with the meeting transcript

// Step 5: Execute the workflow
// Hint: InProcessExecution.StreamAsync(), TrySendMessageAsync()

// Step 6: Collect results from workflow events
// Hint: WatchStreamAsync(), WorkflowOutputEvent

// Step 7: Display final results
// Hint: Loop through messages and display Role, AuthorName, Text

#endregion
