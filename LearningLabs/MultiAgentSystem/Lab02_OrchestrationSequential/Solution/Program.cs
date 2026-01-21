using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Microsoft.Agents.AI.Workflows;
using CommonUtilities;
using System.ClientModel;

using OrchestrationSequential;

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Chat Deployment Name: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient (API key or DefaultAzureCredential)
AzureOpenAIClient client = !string.IsNullOrEmpty(settings.APIKey)
    ? new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey))
    : new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

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
AIAgent summaryAgent = chatClient.CreateAIAgent(name: "SummaryAgent", instructions: "You are a helpful assistant that can summarize a meeting transcript. Don't include any other text in your response. You must use the same language as the meeting transcript.");
AIAgent actionsExtractorAgent = chatClient.CreateAIAgent(name: "ActionsExtractorAgent", instructions: "You are a helpful assistant that can extract action items from a meeting transcript. Don't include any other text in your response. You must use the same language as the meeting transcript.");

// Step 3: Build sequential workflow with agents
var workflow = AgentWorkflowBuilder.BuildSequential(workflowName: "meeting-transcript-workflow", agents: [summaryAgent, actionsExtractorAgent]);

// Step 4: Prepare input messages
var messages = new List<ChatMessage>
{
    new(ChatRole.User, meetingTranscript)
};

// Step 5: Execute the workflow
StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

// Step 6: Collect results from workflow events
List<ChatMessage> result = new();
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
   if (evt is WorkflowOutputEvent outputEvt)
    {
        result = (List<ChatMessage>)outputEvt.Data!;
        break;
    }
}

// Step 7: Display final results
ColoredConsole.WriteInfoLine("Results:");
foreach (var message in result.Where(x => x.Role != ChatRole.User))
{
   ColoredConsole.WritePrimaryLogLine($"{message.Role} - {message.AuthorName??"Unknown"}: ");

   ColoredConsole.WriteSecondaryLogLine(message.Text);
}

#endregion
