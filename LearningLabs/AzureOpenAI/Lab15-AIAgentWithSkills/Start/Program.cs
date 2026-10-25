using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentWithSkills;
using static AIAgentWithSkills.SkillsConsole;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create the OpenAIClientOptions that point to the Azure OpenAI v1 endpoint
// Hint: AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) turns "https://<resource>.openai.azure.com/" into ".../openai/v1/"
// OpenAIClientOptions clientOptions = ...

// TODO 2: Create the OpenAIClient
// - If settings.APIKey is set: authenticate with an ApiKeyCredential
// - Otherwise: authenticate with Microsoft Entra ID using a BearerTokenPolicy built from
//   DefaultAzureCredential and the scope "https://ai.azure.com/.default"
// Note: the AuthenticationPolicy constructor is flagged experimental (OPENAI001), keep the pragma around it.
#pragma warning disable OPENAI001
// OpenAIClient client = ...
#pragma warning restore OPENAI001

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

// Step 4: The folder of the file-based skills, copied next to the program (one sub-folder per skill, each with a SKILL.md)
string skillsPath = Path.Combine(AppContext.BaseDirectory, "skills");
ColoredConsole.WriteSecondaryLogLine($"Skills folder: {Path.GetFileName(skillsPath)}/ ({Directory.GetDirectories(skillsPath).Length} skills)");

#endregion

#region Scenario 1: Skills from SKILL.md files, loaded on demand

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Skills from SKILL.md files, loaded on demand ===");

    // TODO 4: Create the skills provider for the folder of file-based skills
    // Hint: AgentSkillsProvider skillsProvider = new(skillsPath);

    // TODO 5: Create the agent "EmployeeAssistant" with the provider, trace the skill tools, and auto-approve the read-only ones,
    // then display the skills advertised
    // Hint: chatClient.AsAIAgent(new ChatClientAgentOptions { Name = ..., ChatOptions = new() { Instructions = ... }, AIContextProviders = [skillsProvider] })
    //       .AsBuilder().Use(TraceSkillCall).UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] }).Build();
    //       await WriteFileSkillsAsync(skillsPath, agent);
    // AIAgent agent = ...

    // TODO 6: Ask a question that matches a skill (see the README), and display the answer and the number of skill tools run
    // Hint: int callsBefore = SkillCallCount; ... ColoredConsole.WriteSecondaryLogLine($"Skill tools run for this question: {SkillCallCount - callsBefore}");

    // TODO 7: Ask a question that no skill covers ("What is the capital of Italy?"), same display
}

#endregion

#region Scenario 2: A skill defined in code, with a dynamic resource and a script

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: A skill defined in code, with a dynamic resource and a script ===");

    // TODO 8: Define the skill "shipping-quote" in code: name, description, instructions (see the README),
    // the static resource "rates", the dynamic resource "validity" and the script "quote" (ShippingRates.cs)
    // Hint: new AgentInlineSkill(name: ..., description: ..., instructions: ...).AddResource("rates", ShippingRates.RatesTable)
    //       .AddResource("validity", ShippingRates.Validity).AddScript("quote", ShippingRates.Quote);
    // AgentInlineSkill shippingSkill = ...

    // TODO 9: Create the agent "ShippingAssistant" with a provider for this skill, trace the skill tools, and auto-approve all the skill tools
    // Hint: AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule]
    // AIAgent agent = ...

    // TODO 10: Ask for a quote (see the README) and display the answer
}

#endregion

#region Scenario 3: Approve the skill tools

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Approve the skill tools ===");

    // TODO 11: Create the provider of the same skill as scenario 2, and the ChatClientAgentOptions of the agent
    // ChatClientAgentOptions agentOptions = ...

    // TODO 12: Without auto-approval (UseToolApproval() with no rule), run a question and display the pending approval request
    // Hint: for each ToolApprovalRequestContent of the response:
    //       ColoredConsole.WriteWarningLine($"Without auto-approval, approval requested for {FormatCall((FunctionCallContent)request.ToolCall)}");

    // TODO 13: Create the agent that auto-approves the read-only skill tools only (AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule)
    // AIAgent agent = ...

    // TODO 14: For a 2.5 kg parcel and a 120 kg parcel: run the question in a new session, then answer each pending request with
    // the decision of ScriptApprovalPolicy.Decide(call) until no request is left, and display the answer (see the README for the loop)
}

#endregion
