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

// Step 2: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

// Step 4: The folder of the file-based skills, copied next to the program (one sub-folder per skill, each with a SKILL.md)
string skillsPath = Path.Combine(AppContext.BaseDirectory, "skills");
ColoredConsole.WriteSecondaryLogLine($"Skills folder: {Path.GetFileName(skillsPath)}/ ({Directory.GetDirectories(skillsPath).Length} skills)");

#endregion

#region Scenario 1: Skills from SKILL.md files, loaded on demand

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Skills from SKILL.md files, loaded on demand ===");

    // Step 1: Create the skills provider: it discovers the skills of the folder, advertises their names and descriptions
    // to the model, and gives the agent the tools load_skill and read_skill_resource
    AgentSkillsProvider skillsProvider = new(skillsPath);

    // Step 2: Create the agent with the provider as a context provider. The skill tools require approval by default:
    // the read-only tools are auto-approved here (approvals are the topic of scenario 3).
    // Use(TraceSkillCall) is the function calling middleware of Lab 03: it writes every skill tool that runs
    AIAgent agent = chatClient
        .AsAIAgent(new ChatClientAgentOptions
        {
            Name = "EmployeeAssistant",
            ChatOptions = new() { Instructions = "You are the assistant of Contoso employees. Use the available skills when they match the question." },
            AIContextProviders = [skillsProvider]
        })
        .AsBuilder()
        .Use(TraceSkillCall)
        .UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] })
        .Build();
    await WriteFileSkillsAsync(skillsPath, agent);

    // Step 3: A question that matches a skill: the agent loads it, reads its resource, then answers with the limit of the resource
    const string ExpenseQuestion = "What is the maximum I can spend on a hotel night in Paris for a business trip?";
    ColoredConsole.WriteUserLine($"User: {ExpenseQuestion}");
    int callsBefore = SkillCallCount;
    AgentResponse expenseResponse = await agent.RunAsync(ExpenseQuestion, await agent.CreateSessionAsync());
    ColoredConsole.WriteAssistantLine($"Agent: {expenseResponse.Text}");
    ColoredConsole.WriteSecondaryLogLine($"Skill tools run for this question: {SkillCallCount - callsBefore}");

    // Step 4: A question that no skill covers: no skill is loaded, its instructions never reach the model
    const string OtherQuestion = "What is the capital of Italy?";
    ColoredConsole.WriteUserLine($"User: {OtherQuestion}");
    callsBefore = SkillCallCount;
    AgentResponse otherResponse = await agent.RunAsync(OtherQuestion, await agent.CreateSessionAsync());
    ColoredConsole.WriteAssistantLine($"Agent: {otherResponse.Text}");
    ColoredConsole.WriteSecondaryLogLine($"Skill tools run for this question: {SkillCallCount - callsBefore}");
}

#endregion

#region Scenario 2: A skill defined in code, with a dynamic resource and a script

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: A skill defined in code, with a dynamic resource and a script ===");

    // Step 1: Define the skill in code: name, description, instructions, then its resources and its script.
    // The script is a C# method run in the program: its parameters become the JSON schema the model fills
    AgentInlineSkill shippingSkill = new AgentInlineSkill(
            name: "shipping-quote",
            description: "Quotes the cost of shipping a parcel. Use when asked how much it costs to send a parcel, or for a shipping quote.",
            instructions: """
                Use this skill to quote the shipping cost of a parcel.
                1. Read the rates resource to find the zone of the destination.
                2. Read the validity resource to know until when the quote is valid.
                3. Run the quote script with the weight in kg and the zone, and give the quote id and the price it returns.
                """)
        .AddResource("rates", ShippingRates.RatesTable)
        .AddResource("validity", ShippingRates.Validity)
        .AddScript("quote", ShippingRates.Quote);
    AgentSkillsProvider skillsProvider = new(shippingSkill);

    // Step 2: Create the agent. This skill is our own code, reviewed and trusted: all its tools are auto-approved,
    // the script included (scenario 3 shows how to keep the script under control)
    AIAgent agent = chatClient
        .AsAIAgent(new ChatClientAgentOptions
        {
            Name = "ShippingAssistant",
            ChatOptions = new() { Instructions = "You are the shipping assistant of an online store. Use the available skills." },
            AIContextProviders = [skillsProvider]
        })
        .AsBuilder()
        .Use(TraceSkillCall)
        .UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule] })
        .Build();

    // Step 3: Ask for a quote: the agent loads the skill, reads both resources and runs the script
    const string QuoteQuestion = "How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id and until when it is valid.";
    ColoredConsole.WriteUserLine($"User: {QuoteQuestion}");
    AgentResponse response = await agent.RunAsync(QuoteQuestion, await agent.CreateSessionAsync());
    ColoredConsole.WriteAssistantLine($"Agent: {response.Text}");
}

#endregion

#region Scenario 3: Approve the skill tools

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Approve the skill tools ===");

    // Step 1: The same code-defined skill as scenario 2
    AgentSkillsProvider skillsProvider = new(new AgentInlineSkill(
            name: "shipping-quote",
            description: "Quotes the cost of shipping a parcel. Use when asked how much it costs to send a parcel, or for a shipping quote.",
            instructions: """
                Use this skill to quote the shipping cost of a parcel.
                1. Read the rates resource to find the zone of the destination.
                2. Read the validity resource to know until when the quote is valid.
                3. Run the quote script with the weight in kg and the zone, and give the quote id and the price it returns.
                """)
        .AddResource("rates", ShippingRates.RatesTable)
        .AddResource("validity", ShippingRates.Validity)
        .AddScript("quote", ShippingRates.Quote));
    ChatClientAgentOptions agentOptions = new()
    {
        Name = "ShippingAssistant",
        ChatOptions = new() { Instructions = "You are the shipping assistant of an online store. Use the available skills." },
        AIContextProviders = [skillsProvider]
    };

    // Step 2: No auto-approval: the first skill tool call (load_skill) stops the run with an approval request
    AIAgent strictAgent = chatClient.AsAIAgent(agentOptions).AsBuilder().Use(TraceSkillCall).UseToolApproval().Build();
    AgentResponse pending = await strictAgent.RunAsync("How much does it cost to ship a 2.5 kg parcel to Germany?", await strictAgent.CreateSessionAsync())
        ;
    foreach (ToolApprovalRequestContent request in pending.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>())
    {
        ColoredConsole.WriteWarningLine($"Without auto-approval, approval requested for {FormatCall((FunctionCallContent)request.ToolCall)}");
    }

    // Step 3: Auto-approve the read-only tools (load_skill, read_skill_resource): only run_skill_script still asks
    AIAgent agent = chatClient
        .AsAIAgent(agentOptions)
        .AsBuilder()
        .Use(TraceSkillCall)
        .UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] })
        .Build();

    // Step 4: Two parcels: the policy of the program (ScriptApprovalPolicy) approves the script for 2.5 kg and rejects it for 120 kg
    foreach (string question in new[] { "How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id.", "How much does it cost to ship a 120 kg parcel to Germany? Give me the quote id." })
    {
        ColoredConsole.WriteUserLine($"User: {question}");
        AgentSession session = await agent.CreateSessionAsync();
        AgentResponse response = await agent.RunAsync(question, session);

        // Step 5: Approval loop (as in Lab 09): each pending request gets the decision of the policy, then the run continues
        List<ToolApprovalRequestContent> requests = [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
        while (requests.Count > 0)
        {
            List<ChatMessage> decisions = requests.ConvertAll(request =>
            {
                FunctionCallContent call = (FunctionCallContent)request.ToolCall;
                ApprovalDecision decision = ScriptApprovalPolicy.Decide(call);
                ColoredConsole.WriteWarningLine($"Approval requested for {FormatCall(call)}");
                if (decision.Approved)
                {
                    ColoredConsole.WriteSuccessLine($"Policy decision: approved - {decision.Reason}");
                }
                else
                {
                    ColoredConsole.WriteErrorLine($"Policy decision: rejected - {decision.Reason}");
                }

                return new ChatMessage(ChatRole.User, [request.CreateResponse(decision.Approved, decision.Reason)]);
            });

            response = await agent.RunAsync(decisions, session);
            requests = [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
        }

        // Step 6: Display the answer (the skill tools that ran were written by TraceSkillCall during the runs)
        ColoredConsole.WriteAssistantLine($"Agent: {response.Text}");
    }
}

#endregion
