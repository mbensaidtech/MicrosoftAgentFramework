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
AIAgent returnExchangeAgent = chatClient.CreateAIAgent(name: "Return/Exchange Agent", instructions: "You are a returns/exchange assistant. Detect if the customer needs a return or exchange, and summarize it.");
AIAgent refundAgent = chatClient.CreateAIAgent(name: "Refund Agent", instructions: "You are a refund assistant. Detect if the customer is eligible for a refund, and summarize the details. don't answer to the user, only return the eligibity.");
AIAgent followUpAgent = chatClient.CreateAIAgent(name: "Follow-up Agent", instructions: "You are a customer communication assistant. Create a polite reply summarizing actions for the customer.");

// Step 2: Build concurrent workflow with agents (all agents process the same input in parallel)
var workflow = AgentWorkflowBuilder.BuildConcurrent(workflowName: "ecommerce-after-sales-concurrent-workflow", agents: [returnExchangeAgent, refundAgent, followUpAgent]);

// Step 3: Prepare input messages
var messages = new List<ChatMessage>
{
    new(ChatRole.User, "I received my order, but the charger is missing and I also want to return the old one.")
};

// Step 4: Execute the workflow
StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

// Step 5: Collect results from workflow events
List<ChatMessage> result = new();
await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
{
   if (evt is WorkflowOutputEvent outputEvt)
    {
        result = (List<ChatMessage>)outputEvt.Data!;
        break;
    }
}

// Step 6: Display final results
ColoredConsole.WriteInfoLine("Results:");
foreach (var message in result.Where(x => x.Role != ChatRole.User))
{
   ColoredConsole.WritePrimaryLogLine($"{message.Role} - {message.AuthorName??"Unknown"}: ");

   ColoredConsole.WriteSecondaryLogLine(message.Text);
}

#endregion

