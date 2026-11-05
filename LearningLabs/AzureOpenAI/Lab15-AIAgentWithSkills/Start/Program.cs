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
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

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

    // TODO 5: Create the agent with the name and the instructions of Agents.EmployeeAssistant (provided in LabPrompts.cs) and the provider,
    // trace the skill tools, and auto-approve the read-only ones, then display the skills advertised
    // Hint: chatClient.AsAIAgent(new ChatClientAgentOptions { Name = Agents.EmployeeAssistant.Name,
    //       ChatOptions = new() { Instructions = Agents.EmployeeAssistant.Instructions }, AIContextProviders = [skillsProvider] })
    //       .AsBuilder().Use(TraceSkillCall).UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] }).Build();
    //       await WriteFileSkillsAsync(skillsPath, agent);
    // AIAgent agent = ...

    // TODO 6: Ask the question Questions.HotelNightInParis, which matches a skill, and display the answer and the number of skill tools run
    // Hint: int callsBefore = SkillCallCount; ... ColoredConsole.WriteSecondaryLogLine($"Skill tools run for this question: {SkillCallCount - callsBefore}");

    // TODO 7: Ask the question Questions.CapitalOfItaly, which no skill covers, same display
}

#endregion

#region Scenario 2: A skill defined in code, with a dynamic resource and a script

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: A skill defined in code, with a dynamic resource and a script ===");

    // TODO 8: Define the skill Skills.ShippingQuote in code: its name, description and instructions (provided in LabPrompts.cs),
    // the static resource "rates", the dynamic resource "validity" and the script "quote" (ShippingRates.cs)
    // Hint: new AgentInlineSkill(name: Skills.ShippingQuote.Name, description: Skills.ShippingQuote.Description,
    //       instructions: Skills.ShippingQuote.Instructions).AddResource("rates", ShippingRates.RatesTable)
    //       .AddResource("validity", ShippingRates.Validity).AddScript("quote", ShippingRates.Quote);
    // AgentInlineSkill shippingSkill = ...

    // TODO 9: Create the agent with the name and the instructions of Agents.ShippingAssistant and a provider for this skill,
    // trace the skill tools, and auto-approve all the skill tools
    // Hint: AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule]
    // AIAgent agent = ...

    // TODO 10: Ask for a quote with the question Questions.QuoteWithValidity and display the answer
}

#endregion

#region Scenario 3: Approve the skill tools

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Approve the skill tools ===");

    // TODO 11: Create the provider of the same skill as scenario 2 (Skills.ShippingQuote), and the ChatClientAgentOptions of the agent
    // (same Agents.ShippingAssistant as scenario 2)
    // ChatClientAgentOptions agentOptions = ...

    // TODO 12: Without auto-approval (UseToolApproval() with no rule), run the question Questions.CostFor2Point5Kg
    // and display the pending approval request
    // Hint: for each ToolApprovalRequestContent of the response:
    //       ColoredConsole.WriteWarningLine($"Without auto-approval, approval requested for {FormatCall((FunctionCallContent)request.ToolCall)}");

    // TODO 13: Create the agent that auto-approves the read-only skill tools only (AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule)
    // AIAgent agent = ...

    // TODO 14: For a 2.5 kg parcel and a 120 kg parcel (Questions.QuoteFor2Point5Kg, Questions.QuoteFor120Kg): run the question in a new session, then answer each pending request with
    // the decision of ScriptApprovalPolicy.Decide(call) until no request is left, and display the answer (see the README for the loop)
}

#endregion
