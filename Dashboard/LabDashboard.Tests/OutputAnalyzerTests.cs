using LabDashboard.Catalog;
using LabDashboard.Execution;

namespace LabDashboard.Tests;

public class OutputAnalyzerTests
{
    private static readonly string[] Lab01Output =
    [
        "Endpoint: https://example.openai.azure.com/",
        "=== Scenario 4: Get consumed tokens from the agent response ===",
        "- White",
        "Token Usage: ",
        "  Input tokens: 56",
        "  Output tokens: 29",
        "  Reasoning tokens (included in output): 0",
        "  Total tokens: 85",
        "=== Scenario 5: Streaming ===",
        "Once upon a time...",
        "Token Usage (streaming): ",
        "  Input tokens: 38",
        "  Output tokens: 120",
        "  Total tokens: 158",
    ];

    [Fact]
    public void Parses_every_token_usage_block_printed_by_the_exercise()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab01Output));

        Assert.Equal(
            [new TokenUsageReport("Scenario 4 · Token Usage", 56, 29, 0, 85), new TokenUsageReport("Scenario 5 · Token Usage (streaming)", 38, 120, null, 158)],
            usage.Reports);
        Assert.Equal((94L, 149L, 0L, 243L), (usage.Input!.Value, usage.Output!.Value, usage.Reasoning!.Value, usage.Total!.Value));
    }

    // Standard output of the Lab02 solution, as decoded by the dashboard (spinner lines removed).
    private static readonly string[] Lab02SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Manually defined structured output ===",
        "Successfully parsed structured response:",
        "Name: Le Bernardin",
        "Chef: Éric Ripert",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 166",
        "  Output tokens: 72",
        "  Total tokens: 238",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Automatically generated structured output with RunAsync<T> ===",
        "Structured response:",
        "Name: Le Bernardin",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 237",
        "  Output tokens: 125",
        "  Total tokens: 362",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Structured output configured on the agent ===",
        "JSON response:",
        """{"name":"Le Bernardin","chefName":"Éric Ripert","cuisine":"French","michelinStars":3}""",
        "Deserialized response:",
        "Name: Le Bernardin",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 239",
        "  Output tokens: 101",
        "  Total tokens: 340",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 4: Structured output with AgentRunOptions and streaming ===",
        """{"name":"Le Bernardin","chefName":"Éric Ripert","cuisine":"French","michelinStars":3}""",
        "Deserialized response:",
        "Name: Le Bernardin",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 239",
        "  Output tokens: 95",
        "  Total tokens: 334",
    ];

    // Standard output of the Lab02 exercise as delivered (TODOs not done yet).
    private static readonly string[] Lab02StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "=== Scenario 1: Manually defined structured output ===",
        "",
        "-------------------------------------------------------------------------------",
        "=== Scenario 2: Automatically generated structured output with RunAsync<T> ===",
        "",
        "-------------------------------------------------------------------------------",
        "=== Scenario 3: Structured output configured on the agent ===",
        "",
        "-------------------------------------------------------------------------------",
        "=== Scenario 4: Structured output with AgentRunOptions and streaming ===",
    ];

    [Fact]
    public void Labels_identical_token_usage_headings_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab02SolutionOutput));

        Assert.Equal(
            ["Scenario 1 · Token Usage", "Scenario 2 · Token Usage", "Scenario 3 · Token Usage", "Scenario 4 · Token Usage"],
            usage.Reports.Select(r => r.Label));
        Assert.Equal((881L, 393L, 1274L), (usage.Input!.Value, usage.Output!.Value, usage.Total!.Value));
    }

    [Fact]
    public void Every_lab02_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab02 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab02")!;

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab02.Expectations, Lab02SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab02.Expectations, Lab02StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void A_lab02_scenario_without_usage_does_not_borrow_the_usage_of_the_next_one()
    {
        LabDefinition lab02 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab02")!;
        string[] output = [.. Lab02SolutionOutput.Where((_, index) => index is < 12 or > 15)]; // scenario 1 usage removed

        ExpectationResult scenario1Usage = OutputAnalyzer.EvaluateExpectations(lab02.Expectations, output).Single(r => r.Id == "scenario1-usage");
        Assert.False(scenario1Usage.Passed);
    }

    // Standard output of the Lab03 solution, as decoded by the dashboard (spinner lines removed).
    private static readonly string[] Lab03SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Function tools calling - basic ===",
        "Tools available to the agent:",
        "- get_employee_info: Retrieves detailed information about an employee using their employee ID (e.g., EMP001, EMP002).",
        "- get_meeting_rooms: Lists all available meeting rooms with their capacity and features.",
        "- book_meeting_room: Books a meeting room for a specific date, time, and subject.",
        "",
        "Here is the information for the employee with ID EMP001:",
        "- **Name:** Mohammed BEN SAID",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 507",
        "  Output tokens: 75",
        "  Total tokens: 582",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Function tools calling - a plugin that declares its tools ===",
        "Tools declared by the plugin: get_meeting_rooms, book_meeting_room",
        "[Tool] get_meeting_rooms returned: Available Meeting Rooms:",
        "[Tool] book_meeting_room returned: Cannot book: 2025-12-16 is in the past (today is 2026-10-25).",
        "[Tool] book_meeting_room returned: Booking confirmed!",
        "1. **Innovation Lab (ROOM-A)**",
        "- The booking for **ROOM-A** on **2025-12-16** could not be confirmed as the date is in the past.",
        "- The booking for **ROOM-B** on **2026-10-26** has been successfully confirmed for the employee with ID **EMP002**.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 1186",
        "  Output tokens: 159",
        "  Total tokens: 1345",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Function tools calling - static tools with DI ===",
        "",
        "The notification has been sent to Mohammed with the message: **\"Meeting at 3pm tomorrow\"**.",
        "- **[97576bb9] 14:17:57:** Meeting at 3pm tomorrow",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 396",
        "  Output tokens: 109",
        "  Total tokens: 505",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 4: Function calling middleware ===",
        "[Middleware] Calling get_employee_info(employeeId: EMP003)",
        "[Middleware] get_employee_info returned: Employee Found:",
        "[Middleware] Calling book_meeting_room(roomId: ROOM-B, employeeId: EMP003, date: 2026-10-27, startTime: 14:00, endTime: 15:00, subject: Design Review)",
        "[Middleware] book_meeting_room returned: Booking confirmed!",
        "- **Name:** Charlie Brown",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 1276",
        "  Output tokens: 219",
        "  Total tokens: 1495",
    ];

    // Standard output of the Lab03 exercise as delivered (TODOs not done yet).
    private static readonly string[] Lab03StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Function tools calling - basic ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Function tools calling - a plugin that declares its tools ===",
        "Tools declared by the plugin: ",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Function tools calling - static tools with DI ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 4: Function calling middleware ===",
    ];

    [Fact]
    public void Every_lab03_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab03 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab03")!;

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab03.Expectations, Lab03SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab03.Expectations, Lab03StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void A_lab03_answer_without_tool_data_fails_its_checks()
    {
        LabDefinition lab03 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab03")!;
        // The agent answered without calling the tools: no employee data, no tool result, no notification id, no middleware trace.
        string[] output =
        [
            "=== Scenario 1: Function tools calling - basic ===",
            "Tools available to the agent:",
            "- get_employee_info: ",
            "I don't have access to employee records.",
            "=== Scenario 2: Function tools calling - a plugin that declares its tools ===",
            "Tools declared by the plugin: get_meeting_rooms, book_meeting_room, cancel_all_bookings",
            "The room ROOM-A has been booked for 2025-12-16.",
            "=== Scenario 3: Function tools calling - static tools with DI ===",
            "It appears that there was an error while trying to send the notification 'Meeting at 3pm tomorrow'.",
            "=== Scenario 4: Function calling middleware ===",
            "Innovation Lab has been booked.",
        ];

        Assert.Empty(OutputAnalyzer.EvaluateExpectations(lab03.Expectations, output).Where(r => r.Passed && !r.Id.EndsWith("-usage", StringComparison.Ordinal)));
    }

    [Fact]
    public void Labels_the_four_lab03_token_usage_blocks_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab03SolutionOutput));

        Assert.Equal(
            ["Scenario 1 · Token Usage", "Scenario 2 · Token Usage", "Scenario 3 · Token Usage", "Scenario 4 · Token Usage"],
            usage.Reports.Select(r => r.Label));
        Assert.Equal(3927L, usage.Total!.Value); // 582 + 1345 + 505 + 1495
    }

    // Standard output of the Lab04 solution (real run), as decoded by the dashboard (spinner lines removed).
    private static readonly string[] Lab04SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "MCP Server: https://huggingface.co/mcp (anonymous)",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Discover the tools of the MCP server ===",
        "Connected to: huggingface.co/mcp 0.4.23",
        "MCP tools available (4): hf_whoami, hub_repo_search, hub_repo_details, hf_fs",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Agent with MCP tools and structured output ===",
        "Found 4 models:",
        "  Name: jinaai/jina-embeddings-v5-text-nano-classification",
        "  Task: text-classification",
        "  Library: llama.cpp",
        "  Link: https://hf.co/jinaai/jina-embeddings-v5-text-nano-classification",
        "",
        "  Name: jinaai/jina-embeddings-v5-text-nano",
        "  Task: feature-extraction",
        "  Library: transformers",
        "  Link: https://hf.co/jinaai/jina-embeddings-v5-text-nano",
        "",
        "MCP tools called by the agent:",
        "- hub_repo_search(query: text embedding, repo_types: [\"model\"], limit: 4)",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 4435",
        "  Output tokens: 240",
        "  Total tokens: 4675",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Give the agent only the MCP tools it needs ===",
        "Tools given to the agent: hub_repo_search",
        "Found 4 models: jinaai/jina-embeddings-v5-text-nano-classification, trapoom555/MiniCPM-2B-Text-Embedding-cft, jinaai/jina-embeddings-v5-text-nano, jinaai/jina-embeddings-v5-text-small",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 1927",
        "  Output tokens: 240",
        "  Total tokens: 2167",
    ];

    // Standard output of the Lab04 exercise as delivered (TODOs not done yet).
    private static readonly string[] Lab04StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "MCP Server: https://huggingface.co/mcp (anonymous)",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Discover the tools of the MCP server ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Agent with MCP tools and structured output ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Give the agent only the MCP tools it needs ===",
    ];

    [Fact]
    public void Every_lab04_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab04 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab04")!;

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab04.Expectations, Lab04SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab04.Expectations, Lab04StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void A_lab04_answer_without_mcp_tools_fails_its_checks()
    {
        LabDefinition lab04 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab04")!;
        // The agent answered from its own knowledge: no server information, no tool called, no Hub link, every tool kept.
        string[] output =
        [
            "=== Scenario 1: Discover the tools of the MCP server ===",
            "MCP tools available (0): ",
            "=== Scenario 2: Agent with MCP tools and structured output ===",
            "Found 1 models:",
            "  Name: all-MiniLM-L6-v2",
            "  Task: sentence-similarity",
            "  Library: sentence-transformers",
            "  Link: https://www.sbert.net",
            "MCP tools called by the agent:",
            "=== Scenario 3: Give the agent only the MCP tools it needs ===",
            "Tools given to the agent: hf_whoami, hub_repo_search, hub_repo_details, hf_fs",
            "Found 0 models: ",
        ];

        Assert.Empty(OutputAnalyzer.EvaluateExpectations(lab04.Expectations, output).Where(r => r.Passed && !r.Id.EndsWith("-usage", StringComparison.Ordinal)));
    }

    [Fact]
    public void Labels_the_two_lab04_token_usage_blocks_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab04SolutionOutput));

        Assert.Equal(["Scenario 2 · Token Usage", "Scenario 3 · Token Usage"], usage.Reports.Select(r => r.Label));
        Assert.Equal(6842L, usage.Total!.Value); // 4675 + 2167
    }

    // Standard output of the Lab05 solution (real run), as decoded by the dashboard (spinner lines removed).
    private static readonly string[] Lab05SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Session with the default in-memory chat history ===",
        "User: Hello, my name is Ada. What is the capital of France?",
        "",
        "",
        "Agent: The capital of France is Paris.",
        "Messages in the session: 2 (user, assistant)",
        "Serialized session:",
        "{",
        "  \"stateBag\": {",
        "    \"InMemoryChatHistoryProvider\": {",
        "      \"messages\": [",
        "        {",
        "          \"role\": \"user\",",
        "          \"contents\": [",
        "            {",
        "              \"$type\": \"text\",",
        "              \"text\": \"Hello, my name is Ada. What is the capital of France?\"",
        "            }",
        "          ]",
        "        },",
        "        {",
        "          \"authorName\": \"GlobalAgent\",",
        "          \"createdAt\": \"2026-09-27T14:26:39\\u002B00:00\",",
        "          \"role\": \"assistant\",",
        "          \"contents\": [",
        "            {",
        "              \"$type\": \"text\",",
        "              \"text\": \"The capital of France is Paris.\"",
        "            }",
        "          ],",
        "          \"messageId\": \"chatcmpl-ESk7rNKQoFQEyrH0BazHrRuKKsNVZ\"",
        "        }",
        "      ]",
        "    }",
        "  }",
        "}",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Restoring the session from the saved JSON...",
        "User: What is the population of that city? And what is my name?",
        "",
        "",
        "Agent: As of 2023, the population of Paris is approximately 2.1 million. Your name is Ada.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (follow-up):",
        "  Input tokens: 79",
        "  Output tokens: 23",
        "  Total tokens: 102",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Bound the chat history with a chat reducer ===",
        "",
        "",
        "User: Hello, my name is Ada. What is the capital of France?",
        "Agent: The capital of France is Paris.",
        "Messages kept in the session: 2 (input tokens of this run: 47)",
        "",
        "",
        "User: And what is the capital of Italy?",
        "Agent: The capital of Italy is Rome.",
        "Messages kept in the session: 4 (input tokens of this run: 73)",
        "",
        "",
        "User: What is the population of that city? And what is my name?",
        "Agent: As of 2023, Rome's population is approximately 2.8 million. I don't know your name.",
        "Messages kept in the session: 4 (input tokens of this run: 73)",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (follow-up):",
        "  Input tokens: 73",
        "  Output tokens: 23",
        "  Total tokens: 96",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===",
        "User: Hello, my name is Ada. What is the capital of France?",
        "",
        "",
        "Agent: The capital of France is Paris.",
        "Serialized session:",
        "{",
        "  \"stateBag\": {",
        "    \"MongoChatHistoryProvider\": {",
        "      \"sessionDbKey\": \"8ec5b27595174bebb3b3b5cdb2a7e04e\"",
        "    }",
        "  }",
        "}",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Simulating a restart: new agent, new MongoDB connection, session restored from the saved JSON...",
        "Restored session - chat history stored in MongoDB under the key: 8ec5b27595174bebb3b3b5cdb2a7e04e",
        "User: What is the population of that city? And what is my name?",
        "",
        "",
        "Agent: The population of Paris is approximately 2.1 million. Your name is Ada.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (follow-up):",
        "  Input tokens: 79",
        "  Output tokens: 17",
        "  Total tokens: 96",
        "",
    ];

    // Standard output of the Lab05 exercise as delivered (TODOs not done yet).
    private static readonly string[] Lab05StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Session with the default in-memory chat history ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Bound the chat history with a chat reducer ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===",
    ];

    [Fact]
    public void Every_lab05_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab05 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab05")!;

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab05.Expectations, Lab05SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab05.Expectations, Lab05StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void A_lab05_conversation_that_lost_its_history_fails_its_checks()
    {
        LabDefinition lab05 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab05")!;
        // The follow-up questions were asked in new sessions, the history of scenario 2 was not bounded (no reducer: the name is still known),
        // and the restarted agent of scenario 3 used another key.
        string[] output =
        [
            "=== Scenario 1: Session with the default in-memory chat history ===",
            "Agent: The capital of France is Paris.",
            "Messages in the session: 0 ()",
            "Serialized session:",
            "{",
            "  \"stateBag\": {}",
            "}",
            "Restoring the session from the saved JSON...",
            "Agent: I don't know which city you mean, and I don't know your name.",
            "=== Scenario 2: Bound the chat history with a chat reducer ===",
            "User: Hello, my name is Ada. What is the capital of France?",
            "Agent: The capital of France is Paris.",
            "Messages kept in the session: 2 (input tokens of this run: 47)",
            "User: And what is the capital of Italy?",
            "Agent: The capital of Italy is Rome.",
            "Messages kept in the session: 4 (input tokens of this run: 73)",
            "User: What is the population of that city? And what is my name?",
            "Agent: Rome has about 2.8 million inhabitants, and your name is Ada.",
            "Messages kept in the session: 6 (input tokens of this run: 98)",
            "=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===",
            "Serialized session:",
            "{",
            "  \"stateBag\": {",
            "    \"MongoChatHistoryProvider\": {",
            "      \"sessionDbKey\": \"f3b863cd58334e6aa5edfcc72d376ff9\"",
            "    }",
            "  }",
            "}",
            "Simulating a restart: new agent, new MongoDB connection, session restored from the saved JSON...",
            "Restored session - chat history stored in MongoDB under the key: 0123456789abcdef0123456789abcdef",
            "Agent: Which city do you mean? What is your name?",
        ];

        Assert.DoesNotContain(OutputAnalyzer.EvaluateExpectations(lab05.Expectations, output), r => r.Passed);
    }

    [Fact]
    public void Labels_the_three_lab05_token_usage_blocks_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab05SolutionOutput));

        Assert.Equal(
            ["Scenario 1 · Token Usage (follow-up)", "Scenario 2 · Token Usage (follow-up)", "Scenario 3 · Token Usage (follow-up)"],
            usage.Reports.Select(r => r.Label));
        Assert.Equal(294L, usage.Total!.Value); // 102 + 96 + 96
    }

    [Fact]
    public void Returns_null_when_no_token_usage_is_printed()
    {
        Assert.Null(OutputAnalyzer.ParseTokenUsage(["=== Scenario 1: Basic Agent ===", "", "-----"]));
    }

    [Fact]
    public void Evaluates_expectations_on_the_whole_output()
    {
        LabExpectation[] expectations =
        [
            new("s4", "Scenario 4 answered", @"^=== Scenario 4:[^\n]*===\n\S"),
            new("s1", "Scenario 1 answered", @"^=== Scenario 1:[^\n]*===\n\S"),
            new("usage", "Usage displayed", @"(?is)=== Scenario 5:.*?input tokens:?\s*\d+"),
        ];

        IReadOnlyList<ExpectationResult> results = OutputAnalyzer.EvaluateExpectations(expectations, Lab01Output);

        Assert.Equal([true, false, true], results.Select(r => r.Passed));
    }

    [Fact]
    public void An_unanswered_scenario_fails_its_check()
    {
        // The Start project as delivered prints the header followed by an empty line.
        IReadOnlyList<ExpectationResult> results = OutputAnalyzer.EvaluateExpectations(
            [new LabExpectation("s1", "Scenario 1 answered", @"^=== Scenario 1:[^\n]*===\n\S")],
            ["=== Scenario 1: Basic Agent ===", "", "-----"]);

        Assert.False(Assert.Single(results).Passed);
    }

    [Fact]
    public void Extracts_distinct_build_diagnostics_without_the_project_suffix()
    {
        string[] build =
        [
            "/repo/CommonUtilities/CommonUtilities.csproj : warning NU1903: Package 'Snappier' 1.0.0 has a known high severity vulnerability [/repo/Lab/Start.csproj]",
            "/repo/CommonUtilities/CommonUtilities.csproj : warning NU1903: Package 'Snappier' 1.0.0 has a known high severity vulnerability",
            "/repo/Lab/Program.cs(12,5): error CS1002: ; expected [/repo/Lab/Start.csproj]",
        ];

        Assert.Equal(["NU1903: Package 'Snappier' 1.0.0 has a known high severity vulnerability"], OutputAnalyzer.BuildDiagnostics(build, "warning"));
        Assert.Equal(["CS1002: ; expected"], OutputAnalyzer.BuildDiagnostics(build, "error"));
    }

    // Standard output of the Lab06 client solution (real run from the dashboard, companion server on port 5071).
    private static readonly string[] Lab06ClientSolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Discover a remote agent from its agent card ===",
        "Agent card: AuthAgent (version 1.0.0)",
        "  Description: An authentication agent specialized in generating and validating API keys. Only handles authentication-related tasks.",
        "  Skill: GenerateAPIKey - Generates a new random API key that starts with 'Meknes'. The key includes a cryptographic signature for validation.",
        "  Skill: ValidateAPIKey - Validates an API key by checking if it starts with 'Meknes' and verifying its cryptographic signature.",
        "  Interface: JSONRPC (A2A 1.0) at http://localhost:5071/a2a/authAgent",
        "  Interface: HTTP+JSON (A2A 1.0) at http://localhost:5071/a2a/authAgent",
        "Generated API key: MeknesF_4sPEHMGX1nrxDzl9TZTNqb5nIbohNdRSWpAq9jqVY.TR8qmEPNvgwNU4MCIqs1IFMGveXeYGITgWXXulZlOM8",
        "Validation of the generated key: The API key is valid.",
        "Validation of a tampered key: The API key you provided is not valid. It either does not start with \"Meknes\" or its cryptographic signature could not be verified. Please check the key and try again.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Connect to a remote agent by URL (direct configuration) ===",
        "Remote agent: CustomerToneAgent at http://localhost:5071/a2a/customerToneAgent",
        "Customer message: I have been waiting for my order for two weeks and nobody answers my emails!",
        "Tone: Frustrated. The customer expresses dissatisfaction with the wait time for their order and the lack of communication, indicating their growing impatience.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: A remote agent as a function tool of a local agent ===",
        "Tool called: AuthAgent",
        "Tool called: AuthAgent",
        "Assistant: Your generated API key is:",
        "",
        "```",
        "MeknesvWeX7Q6ddCCBJdgP4vM5sTX2GFHQtHOrtMozrbbruvs.SHWK8IlFNiwtYFGTfkiAhs6Qk3aL4-s-lsbx-sE1uvE",
        "```",
        "",
        "The result of the validation check is: **The API key is valid.**",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 612",
        "  Output tokens: 181",
        "  Total tokens: 793",
    ];

    // Standard output of the Lab06 client exercise as delivered (TODOs not done yet), with the companion server running.
    private static readonly string[] Lab06ClientStartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Discover a remote agent from its agent card ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Connect to a remote agent by URL (direct configuration) ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: A remote agent as a function tool of a local agent ===",
    ];

    // What the checks of the Lab06 server read: the output of the server solution, then of the companion client (real run).
    private static readonly string[] Lab06ServerSolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: A2A server with two agents ===",
        "APIKeySettings:SecretKey is not set: API keys are signed with a random secret, valid until the server stops.",
        "AuthAgent:         http://localhost:5071/a2a/authAgent",
        "  Agent card:      http://localhost:5071/a2a/authAgent/.well-known/agent-card.json",
        "CustomerToneAgent: http://localhost:5071/a2a/customerToneAgent",
        "  Agent card:      http://localhost:5071/a2a/customerToneAgent/.well-known/agent-card.json",
        "Press Ctrl+C to stop the server.",
        "info: Microsoft.Hosting.Lifetime[14]",
        "      Now listening on: http://localhost:5071",
        "info: Microsoft.Hosting.Lifetime[0]",
        "      Application started. Press Ctrl+C to shut down.",
        "info: Microsoft.Hosting.Lifetime[0]",
        "      Hosting environment: Production",
        "info: Microsoft.Hosting.Lifetime[0]",
        "      Content root path: /repo/LearningLabs/AzureOpenAI/Lab06_A2AServer/Solution",
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Discover a remote agent from its agent card ===",
        "Agent card: AuthAgent (version 1.0.0)",
        "  Description: An authentication agent specialized in generating and validating API keys. Only handles authentication-related tasks.",
        "  Skill: GenerateAPIKey - Generates a new random API key that starts with 'Meknes'. The key includes a cryptographic signature for validation.",
        "  Skill: ValidateAPIKey - Validates an API key by checking if it starts with 'Meknes' and verifying its cryptographic signature.",
        "  Interface: JSONRPC (A2A 1.0) at http://localhost:5071/a2a/authAgent",
        "  Interface: HTTP+JSON (A2A 1.0) at http://localhost:5071/a2a/authAgent",
        "Generated API key: MekneshRpAhFKNbxHIdqCIU-4wvacQBaNBpOJbMlktaZ0oOkM.bjF3nhsoYdWh27lI-JBgDBs7YBcavOCfDC22Dkl_18U",
        "Validation of the generated key: The API key is valid.",
        "Validation of a tampered key: The API key is not valid. It does not start with \"Meknes\" as required.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Connect to a remote agent by URL (direct configuration) ===",
        "Remote agent: CustomerToneAgent at http://localhost:5071/a2a/customerToneAgent",
        "Customer message: I have been waiting for my order for two weeks and nobody answers my emails!",
        "Tone: Frustrated. The customer expresses dissatisfaction due to the delay in receiving their order and the lack of response to their emails, indicating a feeling of irritation and urgency.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: A remote agent as a function tool of a local agent ===",
        "Tool called: AuthAgent",
        "Tool called: AuthAgent",
        "Assistant: Here is your generated API key:",
        "",
        "```",
        "Mekneska0nOpeJodhBwz2_dTcVAtd0GzSQpC--3BIymJGKRG8.V5giRUpLuc0_mjYdk8m0Nm7Va6U4w6Qg_kwPlkKAKgc",
        "```",
        "",
        "The API key is valid.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 675",
        "  Output tokens: 172",
        "  Total tokens: 847",
    ];

    // Standard output of the Lab06 server exercise as delivered: it exits before listening, so the companion client never runs.
    private static readonly string[] Lab06ServerStartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: A2A server with two agents ===",
        "AuthAgent:         http://localhost:5071/a2a/authAgent",
        "  Agent card:      http://localhost:5071/a2a/authAgent/.well-known/agent-card.json",
        "CustomerToneAgent: http://localhost:5071/a2a/customerToneAgent",
        "  Agent card:      http://localhost:5071/a2a/customerToneAgent/.well-known/agent-card.json",
        "Press Ctrl+C to stop the server.",
    ];

    [Theory]
    [InlineData("azureopenai-lab06-client")]
    [InlineData("azureopenai-lab06-server")]
    public void Every_lab06_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise(string labId)
    {
        LabDefinition lab = LabCatalogTests.LoadRealCatalog().Find(labId)!;
        (string[] solution, string[] start) = labId.EndsWith("client", StringComparison.Ordinal)
            ? (Lab06ClientSolutionOutput, Lab06ClientStartOutput)
            : (Lab06ServerSolutionOutput, Lab06ServerStartOutput);

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab.Expectations, solution), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(["config"], OutputAnalyzer.EvaluateExpectations(lab.Expectations, start).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab06_checks_fail_when_the_answers_do_not_come_from_the_tools_of_the_remote_agent()
    {
        LabDefinition lab = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab06-client")!;
        string[] output = [.. Lab06ClientSolutionOutput.Select(line => line
            .Replace("Validation of the generated key: The API key is valid.", "Validation of the generated key: The API key is not valid.")
            .Replace("Validation of a tampered key: The API key you provided is not valid.", "Validation of a tampered key: The API key is valid.")
            .Replace("  Interface: HTTP+JSON (A2A 1.0)", "  Interface: HTTP+JSON (A2A 0.3)"))
            .Where(line => !line.StartsWith("Tool called:", StringComparison.Ordinal) && !line.StartsWith("Meknes", StringComparison.Ordinal))];

        Assert.Equal(
            ["scenario1-card", "scenario1-valid", "scenario1-tampered", "scenario3-tool", "scenario3-key"],
            OutputAnalyzer.EvaluateExpectations(lab.Expectations, output).Where(r => !r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Labels_the_lab06_token_usage_with_scenario_3()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab06ServerSolutionOutput));
        Assert.Equal("Scenario 3 · Token Usage", Assert.Single(usage.Reports).Label);
        Assert.Equal(675, usage.Input);
    }

    // Standard output of the Lab07 solution, as decoded by the dashboard (spinner lines removed).
    private static readonly string[] Lab07SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Chat deployment: gpt-4o-mini",
        "Embedding deployment: text-embedding-3-small",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Fill the vector store with the FAQ ===",
        "Loaded 20 FAQ entries from Data/sav-faq.json",
        "Vector store ready: 20 FAQ entries indexed in the collection 'sav-faq'",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Semantic search - without an agent ===",
        "Question: Is there a phone number I can call for help?",
        "1. faq-010 (score 0.5412): How do I contact customer support?",
        "   You can reach our customer support via email at support@example.com, by phone at +33 1 23 45 67 89 (Mon-Fri 9am-6pm), or through live chat on our website. Average response time is under 2 hours during business hours.",
        "2. faq-004 (score 0.3980): Can I change or cancel my order after placing it?",
        "   You can modify or cancel your order within 1 hour of placing it by contacting our customer service. After this window, or once the order has been shipped, changes or cancellations are no longer possible. You may return the item after delivery instead.",
        "3. faq-005 (score 0.3671): What should I do if I receive a damaged product?",
        "   If you receive a damaged product, please contact our customer service within 48 hours of delivery with photos of the damage. We will arrange a free return pickup and send you a replacement or process a full refund, including shipping costs.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===",
        "Question: I received a broken item yesterday. What should I do?",
        "Tool called: search_faq(question: received a broken item, top: 3)",
        "Agent: If you received a damaged product, contact our customer service within 48 hours of delivery with photos of the damage. We will arrange a free return pickup and send you a replacement or process a full refund, including shipping costs (faq-005).",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 612",
        "  Output tokens: 71",
        "  Total tokens: 683",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call ===",
        "Question: I want to send back a jacket I bought last week. How does it work?",
        "[TextSearchProvider] Search input: I want to send back a jacket I bought last week. How does it work?",
        "[TextSearchProvider] Results: faq-001, faq-008",
        "Agent: Go to 'My Orders' in your account, select the order and click 'Request Return'. You have 30 days from the delivery date, and the jacket must be unused and in its original packaging (faq-001).",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (first question):",
        "  Input tokens: 331",
        "  Output tokens: 52",
        "  Total tokens: 383",
        "Question: And how long until I get my money back?",
        "[TextSearchProvider] Search input: I want to send back a jacket I bought last week. How does it work? | And how long until I get my money back?",
        "[TextSearchProvider] Results: faq-002, faq-001",
        "Agent: Refunds are processed within 5-7 business days after we receive and inspect the returned item, to your original payment method (faq-002).",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (follow-up):",
        "  Input tokens: 402",
        "  Output tokens: 38",
        "  Total tokens: 440",
    ];

    // Standard output of the Lab07 exercise as delivered (TODOs not done yet).
    private static readonly string[] Lab07StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Chat deployment: gpt-4o-mini",
        "Embedding deployment: text-embedding-3-small",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Fill the vector store with the FAQ ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Semantic search - without an agent ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call ===",
    ];

    [Fact]
    public void Every_lab07_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab07 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab07")!;

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab07.Expectations, Lab07SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab07.Expectations, Lab07StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab07_checks_fail_when_the_answers_do_not_come_from_the_faq()
    {
        LabDefinition lab07 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab07")!;
        // Only 3 entries indexed, the search ranks another entry first, the agent answered without the tool and without the FAQ facts,
        // and the provider searched the follow-up question alone.
        string[] output =
        [
            "=== Scenario 1: Fill the vector store with the FAQ ===",
            "Loaded 3 FAQ entries from Data/sav-faq.json",
            "Vector store ready: 3 FAQ entries indexed in the collection 'sav-faq'",
            "=== Scenario 2: Semantic search - without an agent ===",
            "1. faq-004 (score 0.4001): Can I change or cancel my order after placing it?",
            "2. faq-010 (score 0.3900): How do I contact customer support?",
            "=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===",
            "Tool called: (none)",
            "Agent: I'm sorry to hear that. Please contact the seller as soon as possible.",
            "=== Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call ===",
            "[TextSearchProvider] Search input: I want to send back a jacket I bought last week. How does it work?",
            "[TextSearchProvider] Results: faq-008, faq-015",
            "Agent: Returns are usually accepted within a few weeks.",
            "[TextSearchProvider] Search input: And how long until I get my money back?",
            "[TextSearchProvider] Results: faq-002, faq-018",
            "Agent: Refunds usually take a couple of weeks.",
        ];

        Assert.DoesNotContain(OutputAnalyzer.EvaluateExpectations(lab07.Expectations, output), r => r.Passed && !r.Id.EndsWith("-usage", StringComparison.Ordinal));
    }

    [Fact]
    public void Labels_the_three_lab07_token_usage_blocks_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab07SolutionOutput));

        Assert.Equal(
            ["Scenario 3 · Token Usage", "Scenario 4 · Token Usage (first question)", "Scenario 4 · Token Usage (follow-up)"],
            usage.Reports.Select(r => r.Label));
        Assert.Equal(1506L, usage.Total!.Value); // 683 + 383 + 440
    }

    // Standard output of the Lab08 solution, as decoded by the dashboard (spinner lines removed).
    private static readonly string[] Lab08SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "Loaded 50 hotels from Data/hotels.json",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: JSON - the tool returns objects, the agent answers with structured output ===",
        "Question: Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.",
        "Tool called: get_all_hotels()",
        "Tool result sent to the model: 10348 characters",
        "Hotels returned by the agent: 43",
        "  Backpacker Hostel (Bangkok) - 25 USD/night - stars: 1 - rating: 3.8",
        "  Budget Stay Express (Chicago) - 35 USD/night - stars: 2 - rating: 3.5",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (JSON):",
        "  Input tokens: 3180",
        "  Output tokens: 1902",
        "  Total tokens: 5082",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: CSV - the tool returns text, the agent answers in CSV ===",
        "Question: Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.",
        "Tool called: get_all_hotels()",
        "Tool result sent to the model: 2765 characters",
        "Agent answer (CSV):",
        "Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating",
        "Backpacker Hostel,Bangkok,1,25,USD,40,false,true,3.8",
        "Budget Stay Express,Chicago,2,35,USD,60,false,true,3.5",
        "Parsed back 43 hotels from the CSV answer",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (CSV):",
        "  Input tokens: 1421",
        "  Output tokens: 698",
        "  Total tokens: 2119",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Comparison: JSON vs CSV ===",
        "                            JSON       CSV   CSV vs JSON",
        "Tool result (chars)        10348      2765          -73%",
        "Input tokens                3180      1421          -55%",
        "Output tokens               1902       698          -63%",
        "Total tokens                5082      2119          -58%",
    ];

    // Standard output of the Lab08 exercise as delivered (TODOs not done yet).
    private static readonly string[] Lab08StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "Loaded 50 hotels from Data/hotels.json",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: JSON - the tool returns objects, the agent answers with structured output ===",
        "Question: Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: CSV - the tool returns text, the agent answers in CSV ===",
        "Question: Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.",
    ];

    [Fact]
    public void Every_lab08_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab08 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab08")!;

        Assert.All(OutputAnalyzer.EvaluateExpectations(lab08.Expectations, Lab08SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab08.Expectations, Lab08StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab08_checks_fail_when_the_formats_or_the_savings_are_not_there()
    {
        LabDefinition lab08 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab08")!;
        // No tool call in scenario 1 and a small (text) tool result, hotels not ordered by price, a CSV answer wrapped in code fences
        // with a different header that could not be parsed, and a comparison where CSV costs more than JSON.
        string[] output =
        [
            "=== Scenario 1: JSON - the tool returns objects, the agent answers with structured output ===",
            "Tool called: (none)",
            "Tool result sent to the model: 512 characters",
            "Hotels returned by the agent: 43",
            "  Grand Plaza Hotel (Paris) - 75 USD/night - stars: 4 - rating: 4.5",
            "  Backpacker Hostel (Bangkok) - 25 USD/night - stars: 1 - rating: 3.8",
            "=== Scenario 2: CSV - the tool returns text, the agent answers in CSV ===",
            "Tool called: get_all_hotels(format: csv)",
            "Agent answer (CSV):",
            "```csv",
            "Name,PricePerNight,Currency",
            "Backpacker Hostel,25,USD",
            "```",
            "The answer is not the expected CSV: The first line must be the header 'Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating', got: 'Name,PricePerNight,Currency'.",
            "=== Comparison: JSON vs CSV ===",
            "Tool result (chars)         2765     10348         +274%",
            "Input tokens                1421      3180         +124%",
            "Output tokens                698      1902         +172%",
            "Total tokens                2119      5082         +140%",
        ];

        Assert.DoesNotContain(OutputAnalyzer.EvaluateExpectations(lab08.Expectations, output), r => r.Passed && !r.Id.EndsWith("-usage", StringComparison.Ordinal));
    }

    [Fact]
    public void Labels_the_two_lab08_token_usage_blocks_with_their_scenario_and_ignores_the_comparison_rows()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab08SolutionOutput));

        // The "Input tokens ... -55%" rows of the comparison table have no colon: they are not token usage blocks.
        Assert.Equal(["Scenario 1 · Token Usage (JSON)", "Scenario 2 · Token Usage (CSV)"], usage.Reports.Select(r => r.Label));
        Assert.Equal(7201L, usage.Total!.Value); // 5082 + 2119
    }

    // Standard output of the Lab09 solution, as decoded by the dashboard (spinner lines removed), with "Y" typed at the prompt.
    private static readonly string[] Lab09SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "Agent instructions: You are an HR assistant. Use the tools to look up employees and to delete employee data when asked. Only report a deletion as done when the delete_employee_data tool confirmed it; if a deletion was rejected, say so and give the reason.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Human approval at the console ===",
        "User: Delete all the data of the employee with the ID EMP001.",
        "Agent run paused: 1 approval request(s) pending",
        "APPROVAL REQUIRED",
        "The agent would like to invoke the following sensitive function:",
        "  Function: delete_employee_data",
        "  Arguments: employeeId=EMP001",
        "Please reply Y to approve, or anything else to reject:",
        "Function call approved by user.",
        "Tool results:",
        "Tool result (delete_employee_data): Sensitive operation executed: All data for employee 'EMP001' has been permanently deleted. This action cannot be undone.",
        "Agent answer:",
        "All data for the employee with ID EMP001 has been permanently deleted. This action cannot be undone.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (2 runs):",
        "  Input tokens: 317",
        "  Output tokens: 38",
        "  Total tokens: 355",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Several approval requests decided by a policy ===",
        "User: The employees EMP001 and EMP002 asked us to erase their data. Look up both employees, then delete the data of each of them.",
        "Agent run paused: 2 approval request(s) pending",
        "Approval requested for delete_employee_data(employeeId=EMP001)",
        "Policy decision: approved - EMP001 (Alice Martin) left the company on 2025-12-31.",
        "Approval requested for delete_employee_data(employeeId=EMP002)",
        "Policy decision: rejected - EMP002 (Bob Lee) is still employed: only the data of former employees can be deleted.",
        "Tool results:",
        "Tool result (get_employee_info): Employee EMP001: Alice Martin, Engineering, left the company on 2025-12-31.",
        "Tool result (get_employee_info): Employee EMP002: Bob Lee, Finance, currently employed.",
        "Tool result (delete_employee_data): Tool call invocation rejected. EMP002 (Bob Lee) is still employed: only the data of former employees can be deleted.",
        "Tool result (delete_employee_data): Sensitive operation executed: All data for employee 'EMP001' has been permanently deleted. This action cannot be undone.",
        "Agent answer:",
        "I found the following information:",
        "- **EMP001**: Alice Martin, Engineering, left the company on 2025-12-31. (Data has been successfully deleted.)",
        "- **EMP002**: Bob Lee, Finance, is currently employed. (Deletion was rejected because only former employees can have their data deleted.)",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (2 runs):",
        "  Input tokens: 879",
        "  Output tokens: 175",
        "  Total tokens: 1054",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Guardrails with an agent run middleware ===",
        "User: What is the salary of the employee EMP002?",
        "[Guardrail] Request blocked: forbidden topic 'salary'. The agent was not called.",
        "Agent: I can't help with salaries or passwords. Please contact the HR department directly.",
        "Token usage: none, the model was not called",
        "User: My phone number is +33 6 12 34 56 78 and my e-mail is ada.lovelace@example.com. Call me back with the status of the employee EMP001.",
        "[Guardrail] Request: 2 personal data item(s) masked before the agent sees them",
        "[Guardrail] Answer: 0 personal data item(s) masked",
        "Agent: Employee EMP001, Alice Martin, was part of the Engineering department and left the company on December 31, 2025.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (1 runs):",
        "  Input tokens: 432",
        "  Output tokens: 44",
        "  Total tokens: 476",
    ];

    // Standard output of the Lab09 exercise as delivered (TODOs not done yet): no prompt, no approval, no tool.
    private static readonly string[] Lab09StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "Agent instructions: You are an HR assistant. Use the tools to look up employees and to delete employee data when asked. Only report a deletion as done when the delete_employee_data tool confirmed it; if a deletion was rejected, say so and give the reason.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Human approval at the console ===",
        "User: Delete all the data of the employee with the ID EMP001.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Several approval requests decided by a policy ===",
        "User: The employees EMP001 and EMP002 asked us to erase their data. Look up both employees, then delete the data of each of them.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Guardrails with an agent run middleware ===",
    ];

    [Fact]
    public void Every_lab09_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab09 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab09")!;

        Assert.True(lab09.Interactive);
        Assert.All(OutputAnalyzer.EvaluateExpectations(lab09.Expectations, Lab09SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab09.Expectations, Lab09StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab09_checks_fail_when_the_approvals_are_not_honored()
    {
        LabDefinition lab09 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab09")!;
        // Scenario 1: the user approved but the tool never ran (the approval was sent without the session).
        // Scenario 2: the harmless tool was surfaced as an approval request, the policy decisions are inverted,
        // and the deletion of EMP002 was executed although it was rejected.
        // Scenario 3: no guardrail around the agent.
        string[] output =
        [
            "=== Scenario 1: Human approval at the console ===",
            "Agent run paused: 1 approval request(s) pending",
            "APPROVAL REQUIRED",
            "  Function: delete_employee_data",
            "  Arguments: employeeId=EMP002",
            "Function call approved by user.",
            "Tool results: none (no tool was executed)",
            "Agent answer:",
            "I could not delete the data.",
            "=== Scenario 2: Several approval requests decided by a policy ===",
            "Agent run paused: 4 approval request(s) pending",
            "Approval requested for get_employee_info(employeeId=EMP001)",
            "Policy decision: rejected - The employee ID is missing from the call.",
            "Approval requested for delete_employee_data(employeeId=EMP001)",
            "Policy decision: rejected - EMP001 (Alice Martin) is still employed: only the data of former employees can be deleted.",
            "Approval requested for delete_employee_data(employeeId=EMP002)",
            "Policy decision: approved - EMP002 (Bob Lee) left the company on 2025-12-31.",
            "Tool results:",
            "Tool result (get_employee_info): Employee EMP001: Alice Martin, Engineering, left the company on 2025-12-31.",
            "Tool result (get_employee_info): Employee EMP002: Bob Lee, Finance, currently employed.",
            "Tool result (delete_employee_data): Tool call invocation rejected. EMP002 (Bob Lee) is still employed: only the data of former employees can be deleted.",
            "Tool result (delete_employee_data): Sensitive operation executed: All data for employee 'EMP002' has been permanently deleted. This action cannot be undone.",
            // Scenario 3: the guardrail was not applied: the model answered the forbidden question and saw the personal data.
            "=== Scenario 3: Guardrails with an agent run middleware ===",
            "User: What is the salary of the employee EMP002?",
            "Agent: I don't have access to salary information.",
            "Token usage: 312",
            "Agent: I will call you back at ada.lovelace@example.com about EMP001.",
        ];

        Assert.DoesNotContain(OutputAnalyzer.EvaluateExpectations(lab09.Expectations, output), r => r.Passed && !r.Id.EndsWith("-usage", StringComparison.Ordinal));
    }

    [Fact]
    public void Labels_the_three_lab09_token_usage_blocks_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab09SolutionOutput));

        Assert.Equal(["Scenario 1 · Token Usage (2 runs)", "Scenario 2 · Token Usage (2 runs)", "Scenario 3 · Token Usage (1 runs)"], usage.Reports.Select(r => r.Label));
        Assert.Equal(1885L, usage.Total!.Value); // 355 + 1054 + 476
    }

    // Standard output of the Lab10 solution, as decoded by the dashboard (spinner lines removed). The [server] lines are the
    // stderr of the stdio MCP server (a child process), relayed by the lab on its own stdout.
    private static readonly string[] Lab10SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP) ===",
        "MCP server listening on http://localhost:5100/mcp/companyassistant",
        "Query: Get the information of the employee with the ID EMP003.",
        "MCP tools available (1):",
        "- CompanyAssistant: Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.",
        "  Input schema: {\"type\":\"object\",\"properties\":{\"query\":{\"description\":\"Input query to invoke the agent.\",\"type\":\"string\"}},\"required\":[\"query\"]}",
        "",
        "Tool result:",
        "Here is the information for the employee with ID EMP003:",
        "",
        "- **Name:** Charlie Brown",
        "- **Department:** Engineering",
        "- **Position:** Tech Lead",
        "- **Email:** charlie.brown@company.com",
        "- **Vacation Days Remaining:** 12",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Expose the agent as a local MCP tool (stdio) ===",
        "Query: Which meeting rooms can host at least 10 people?",
        "[server] Endpoint: https://example.openai.azure.com/",
        "[server] Deployment: gpt-4o-mini",
        "[server] CompanyAssistant MCP server ready on stdio.",
        "MCP tools available (1):",
        "- CompanyAssistant: Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.",
        "  Input schema: {\"type\":\"object\",\"properties\":{\"query\":{\"description\":\"Input query to invoke the agent.\",\"type\":\"string\"}},\"required\":[\"query\"]}",
        "",
        "Tool result:",
        "The meeting rooms that can host at least 10 people are:",
        "",
        "1. **Innovation Lab (ROOM-A)**",
        "   - Capacity: 10 people",
        "   - Features: Projector, Whiteboard, Video Conference",
        "",
        "2. **Executive Suite (ROOM-C)**",
        "   - Capacity: 20 people",
        "   - Features: Projector, Video Conference, Catering Service",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Another agent uses the CompanyAssistant through MCP ===",
        "User: Book the meeting room ROOM-B for the employee EMP002 on 2026-10-26 from 10:00 to 11:00, subject: Campaign review. Tell me the name of the room and the name of the employee.",
        "[server] Endpoint: https://example.openai.azure.com/",
        "[server] Deployment: gpt-4o-mini",
        "[server] CompanyAssistant MCP server ready on stdio.",
        "",
        "Tool called: CompanyAssistant(query: Book the meeting room ROOM-B for the employee EMP002 on 2026-10-26 from 10:00 to 11:00, subject: Campaign review.)",
        "Tool result: The meeting room ROOM-B has been successfully booked for the employee EMP002. Here are the details:",
        "",
        "- **Room:** Brainstorm Corner (ROOM-B)",
        "- **Date:** 2026-10-26",
        "- **Time:** 10:00 - 11:00",
        "- **Subject:** Campaign review",
        "- **Booked by:** Bob Johnson",
        "",
        "If you need anything else, let me know!",
        "Tool called: CompanyAssistant(query: Get the name of the employee with ID EMP002.)",
        "Tool result: The name of the employee with ID EMP002 is Bob Johnson.",
        "Tool called: CompanyAssistant(query: Get the name of the room with ID ROOM-B.)",
        "Tool result: The name of the room with ID ROOM-B is \"Brainstorm Corner.\"",
        "Agent answer:",
        "The meeting room ROOM-B has been successfully booked for the employee Bob Johnson on 2026-10-26 from 10:00 to 11:00 for the subject \"Campaign review.\"",
        "",
        "- **Room Name:** Brainstorm Corner",
        "- **Employee Name:** Bob Johnson",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 568",
        "  Output tokens: 169",
        "  Total tokens: 737",
    ];

    // Standard output of the Lab10 exercise as delivered (TODOs not done yet): no server, no MCP client, no agent run.
    private static readonly string[] Lab10StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP) ===",
        "Query: Get the information of the employee with the ID EMP003.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Expose the agent as a local MCP tool (stdio) ===",
        "Query: Which meeting rooms can host at least 10 people?",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 3: Another agent uses the CompanyAssistant through MCP ===",
        "User: Book the meeting room ROOM-B for the employee EMP002 on 2026-10-26 from 10:00 to 11:00, subject: Campaign review. Tell me the name of the room and the name of the employee.",
    ];

    [Fact]
    public void Every_lab10_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab10 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab10")!;

        Assert.False(lab10.Interactive);
        Assert.Null(lab10.Companion);
        Assert.All(OutputAnalyzer.EvaluateExpectations(lab10.Expectations, Lab10SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab10.Expectations, Lab10StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab10_checks_fail_when_the_answers_do_not_come_from_the_exposed_agent()
    {
        LabDefinition lab10 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab10")!;
        // Scenario 1: the HTTP server is up and lists the tool, but the agent failed inside the server.
        // Scenario 2: the stdio server never started (no [server] line, no tool) and the answer does not come from the tools.
        // Scenario 3: the OfficeAssistant answered by itself, with invented names.
        string[] output =
        [
            "=== Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP) ===",
            "MCP server listening on http://localhost:5100/mcp/companyassistant",
            "Query: Get the information of the employee with the ID EMP003.",
            "MCP tools available (1):",
            "- CompanyAssistant: Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.",
            "  Input schema: {\"type\":\"object\",\"properties\":{\"query\":{\"description\":\"Input query to invoke the agent.\",\"type\":\"string\"}},\"required\":[\"query\"]}",
            "The tool reported an error:",
            "An error occurred invoking 'CompanyAssistant'.",
            "=== Scenario 2: Expose the agent as a local MCP tool (stdio) ===",
            "Query: Which meeting rooms can host at least 10 people?",
            "Tool result:",
            "I do not have access to the list of meeting rooms.",
            "=== Scenario 3: Another agent uses the CompanyAssistant through MCP ===",
            "User: Book the meeting room ROOM-B for the employee EMP002 on 2026-10-26 from 10:00 to 11:00, subject: Campaign review. Tell me the name of the room and the name of the employee.",
            "Tool calls: none",
            "Agent answer:",
            "ROOM-B (Room Alpha) is booked for John Doe.",
        ];

        Assert.Equal(
            ["scenario1-server", "scenario1-tool"],
            OutputAnalyzer.EvaluateExpectations(lab10.Expectations, output).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Labels_the_lab10_token_usage_with_scenario_3()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab10SolutionOutput));

        Assert.Equal(["Scenario 3 · Token Usage"], usage.Reports.Select(r => r.Label));
        Assert.Equal(737L, usage.Total!.Value);
    }

    // Standard output of the Lab11 solution (real run, shortened: content filter results, latency details, ids and most
    // response headers removed). The handler prints every HTTP request and response, with the Authorization header masked.
    private static readonly string[] Lab11SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: See the HTTP traffic of an agent run ===",
        "User: Hello, what is the capital of France?",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== HTTP REQUEST #1 ===",
        "[URL] POST https://example.openai.azure.com/openai/v1/chat/completions",
        "[Headers]",
        "   Accept: application/json",
        "   User-Agent: OpenAI/2.13.0 (.NET 10.0.13; macOS 27.0.0),MEAI/10.10.0 agent-framework-dotnet/1.22.0 (feat=v1.40000000002001)",
        "   X-Stainless-Lang: csharp",
        "   Authorization: Bearer [MASKED]",
        "   X-agent-name: AIAgentWithCustomHttpTransport",
        "   Content-Type: application/json",
        "",
        "[Body]",
        "   {",
        "     \"messages\": [",
        "       {",
        "         \"role\": \"user\",",
        "         \"content\": \"Hello, what is the capital of France?\"",
        "       }",
        "     ],",
        "     \"model\": \"gpt-4o-mini\"",
        "   }",
        "",
        "=== HTTP RESPONSE #1 ===",
        "[Status] 200 OK (OK) in 1485 ms",
        "[Headers]",
        "   x-ms-served-model: gpt-4o-mini-2024-07-18",
        "   x-ratelimit-remaining-tokens: 99984",
        "   apim-request-id: 1524c5f7-a7d4-412c-9184-6407e26f837b",
        "   x-ms-region: France Central",
        "   Content-Length: 1543",
        "   Content-Type: application/json",
        "",
        "[Body]",
        "   {",
        "     \"choices\": [",
        "       {",
        "         \"finish_reason\": \"stop\",",
        "         \"message\": {",
        "           \"content\": \"The capital of France is Paris.\",",
        "           \"role\": \"assistant\"",
        "         }",
        "       }",
        "     ],",
        "     \"model\": \"gpt-4o-mini-2024-07-18\",",
        "     \"usage\": {",
        "       \"completion_tokens\": 7,",
        "       \"prompt_tokens\": 16,",
        "       \"total_tokens\": 23",
        "     }",
        "   }",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Agent answer:",
        "The capital of France is Paris.",
        "HTTP requests sent by this run: 1",
        "",
        "Token Usage:",
        "  Input tokens: 16",
        "  Output tokens: 7",
        "  Total tokens: 23",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: One agent run, several HTTP requests (function calling) ===",
        "User: What is the weather like in Amsterdam?",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== HTTP REQUEST #2 ===",
        "[URL] POST https://example.openai.azure.com/openai/v1/chat/completions",
        "[Headers]",
        "   Accept: application/json",
        "   User-Agent: OpenAI/2.13.0 (.NET 10.0.13; macOS 27.0.0),MEAI/10.10.0 agent-framework-dotnet/1.22.0 (feat=v1.40000000002001)",
        "   X-Stainless-Lang: csharp",
        "   Authorization: Bearer [MASKED]",
        "   X-agent-name: AIAgentWithCustomHttpTransport",
        "   Content-Type: application/json",
        "",
        "[Body]",
        "   {",
        "     \"messages\": [",
        "       {",
        "         \"role\": \"system\",",
        "         \"content\": \"You are a helpful assistant. Use the tools to answer.\"",
        "       },",
        "       {",
        "         \"role\": \"user\",",
        "         \"content\": \"What is the weather like in Amsterdam?\"",
        "       }",
        "     ],",
        "     \"model\": \"gpt-4o-mini\",",
        "     \"tools\": [",
        "       {",
        "         \"type\": \"function\",",
        "         \"function\": {",
        "           \"description\": \"Get the weather for a given location.\",",
        "           \"name\": \"get_weather\",",
        "         }",
        "       }",
        "     ],",
        "     \"tool_choice\": \"auto\"",
        "   }",
        "",
        "=== HTTP RESPONSE #2 ===",
        "[Status] 200 OK (OK) in 656 ms",
        "[Headers]",
        "   x-ms-served-model: gpt-4o-mini-2024-07-18",
        "   x-ratelimit-remaining-tokens: 99926",
        "   apim-request-id: 5b8848d0-4460-476c-abd7-d56357965f99",
        "   x-ms-region: France Central",
        "   Content-Length: 1671",
        "   Content-Type: application/json",
        "",
        "[Body]",
        "   {",
        "     \"choices\": [",
        "       {",
        "         \"finish_reason\": \"tool_calls\",",
        "         \"message\": {",
        "           \"content\": null,",
        "           \"role\": \"assistant\",",
        "           \"tool_calls\": [",
        "             {",
        "               \"function\": {",
        "                 \"arguments\": \"{\\\"location\\\":\\\"Amsterdam\\\"}\",",
        "                 \"name\": \"get_weather\"",
        "               },",
        "               \"type\": \"function\"",
        "             }",
        "           ]",
        "         }",
        "       }",
        "     ],",
        "     \"model\": \"gpt-4o-mini-2024-07-18\",",
        "     \"usage\": {",
        "       \"completion_tokens\": 14,",
        "       \"prompt_tokens\": 74,",
        "       \"total_tokens\": 88",
        "     }",
        "   }",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== HTTP REQUEST #3 ===",
        "[URL] POST https://example.openai.azure.com/openai/v1/chat/completions",
        "[Headers]",
        "   Accept: application/json",
        "   User-Agent: OpenAI/2.13.0 (.NET 10.0.13; macOS 27.0.0),MEAI/10.10.0 agent-framework-dotnet/1.22.0 (feat=v1.40000000002001)",
        "   X-Stainless-Lang: csharp",
        "   Authorization: Bearer [MASKED]",
        "   X-agent-name: AIAgentWithCustomHttpTransport",
        "   Content-Type: application/json",
        "",
        "[Body]",
        "   {",
        "     \"messages\": [",
        "       {",
        "         \"role\": \"system\",",
        "         \"content\": \"You are a helpful assistant. Use the tools to answer.\"",
        "       },",
        "       {",
        "         \"role\": \"user\",",
        "         \"content\": \"What is the weather like in Amsterdam?\"",
        "       },",
        "       {",
        "         \"role\": \"assistant\",",
        "         \"tool_calls\": [",
        "           {",
        "             \"type\": \"function\",",
        "             \"function\": {",
        "               \"name\": \"get_weather\",",
        "               \"arguments\": \"{\\n  \\\"location\\\": \\\"Amsterdam\\\"\\n}\"",
        "             }",
        "           }",
        "         ]",
        "       },",
        "       {",
        "         \"role\": \"tool\",",
        "         \"tool_call_id\": \"call_zQNpb2BQFXHiLMJDlfN5HINS\",",
        "         \"content\": \"\\\"The weather in Amsterdam is cloudy with a high of 15 degrees Celsius.\\\"\"",
        "       }",
        "     ],",
        "     \"model\": \"gpt-4o-mini\",",
        "     \"tool_choice\": \"auto\"",
        "   }",
        "",
        "=== HTTP RESPONSE #3 ===",
        "[Status] 200 OK (OK) in 772 ms",
        "[Headers]",
        "   x-ms-served-model: gpt-4o-mini-2024-07-18",
        "   x-ratelimit-remaining-tokens: 99889",
        "   apim-request-id: b1561fe4-f16c-4b9a-ab1e-fe356cac8249",
        "   x-ms-region: France Central",
        "   Content-Length: 1597",
        "   Content-Type: application/json",
        "",
        "[Body]",
        "   {",
        "     \"choices\": [",
        "       {",
        "         \"finish_reason\": \"stop\",",
        "         \"message\": {",
        "           \"content\": \"The weather in Amsterdam is currently cloudy with a high of 15 degrees Celsius.\",",
        "           \"role\": \"assistant\"",
        "         }",
        "       }",
        "     ],",
        "     \"model\": \"gpt-4o-mini-2024-07-18\",",
        "     \"usage\": {",
        "       \"completion_tokens\": 17,",
        "       \"prompt_tokens\": 111,",
        "       \"total_tokens\": 128",
        "     }",
        "   }",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Agent answer:",
        "The weather in Amsterdam is currently cloudy with a high of 15 degrees Celsius.",
        "HTTP requests sent by this run: 2",
        "",
        "Token Usage:",
        "  Input tokens: 185",
        "  Output tokens: 31",
        "  Total tokens: 216",
    ];

    // Standard output of the Lab11 exercise as delivered (TODOs not done yet): no client, no agent run, no HTTP log.
    private static readonly string[] Lab11StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: See the HTTP traffic of an agent run ===",
        "User: Hello, what is the capital of France?",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: One agent run, several HTTP requests (function calling) ===",
        "User: What is the weather like in Amsterdam?",
    ];

    [Fact]
    public void Every_lab11_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab11 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab11")!;

        Assert.False(lab11.Interactive);
        Assert.Null(lab11.Companion);
        Assert.All(OutputAnalyzer.EvaluateExpectations(lab11.Expectations, Lab11SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab11.Expectations, Lab11StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab11_checks_fail_when_the_handler_only_forwards_the_requests()
    {
        LabDefinition lab11 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab11")!;
        // Setup and scenarios done, handler not done: the agents answer through the transport and the requests are
        // counted, but nothing of the HTTP traffic is logged.
        string[] output =
        [
            "Endpoint: https://example.openai.azure.com/",
            "=== Scenario 1: See the HTTP traffic of an agent run ===",
            "User: Hello, what is the capital of France?",
            "Agent answer:",
            "The capital of France is Paris.",
            "HTTP requests sent by this run: 1",
            "Token Usage:",
            "  Input tokens: 16",
            "  Output tokens: 7",
            "  Total tokens: 23",
            "=== Scenario 2: One agent run, several HTTP requests (function calling) ===",
            "User: What is the weather like in Amsterdam?",
            "Agent answer:",
            "The weather in Amsterdam is cloudy with a high of 15 degrees Celsius.",
            "HTTP requests sent by this run: 2",
            "Token Usage:",
            "  Input tokens: 185",
            "  Output tokens: 31",
            "  Total tokens: 216",
        ];

        Assert.Equal(
            ["config", "scenario1-answer", "scenario1-usage", "scenario2-answer", "scenario2-usage"],
            OutputAnalyzer.EvaluateExpectations(lab11.Expectations, output).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab11_checks_fail_when_a_credential_is_logged_or_the_tool_is_not_called()
    {
        LabDefinition lab11 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab11")!;
        // Scenario 1: the beginning of the key is shown (masked after 20 characters) and an api-key header is logged in clear.
        // Scenario 2: the model answered without calling the tool: one request, no tool_calls, no tool result sent back.
        string[] output =
        [
            "Endpoint: https://example.openai.azure.com/",
            "=== Scenario 1: See the HTTP traffic of an agent run ===",
            "=== HTTP REQUEST #1 ===",
            "[URL] POST https://example.openai.azure.com/openai/v1/chat/completions",
            "[Headers]",
            "   Authorization: Bearer 0123456789abc... [MASKED]",
            "   api-key: 0123456789abcdef0123456789abcdef",
            "   X-agent-name: AIAgentWithCustomHttpTransport",
            "",
            "=== HTTP RESPONSE #1 ===",
            "[Status] 200 OK (OK) in 812 ms",
            "Agent answer:",
            "The capital of France is Paris.",
            "HTTP requests sent by this run: 1",
            "Token Usage:",
            "  Input tokens: 16",
            "=== Scenario 2: One agent run, several HTTP requests (function calling) ===",
            "=== HTTP REQUEST #2 ===",
            "[URL] POST https://example.openai.azure.com/openai/v1/chat/completions",
            "[Headers]",
            "   Authorization: Bearer [MASKED]",
            "[Body]",
            "   {",
            "     \"tools\": [",
            "       {",
            "         \"type\": \"function\",",
            "         \"function\": {",
            "           \"name\": \"get_weather\"",
            "         }",
            "       }",
            "     ]",
            "   }",
            "=== HTTP RESPONSE #2 ===",
            "[Status] 200 OK (OK) in 640 ms",
            "[Body]",
            "   {",
            "     \"choices\": [ { \"finish_reason\": \"stop\", \"message\": { \"content\": \"I cannot check the weather in real time.\" } } ]",
            "   }",
            "Agent answer:",
            "I cannot check the weather in real time.",
            "HTTP requests sent by this run: 1",
            "Token Usage:",
            "  Input tokens: 60",
        ];

        Assert.Equal(
            ["config", "scenario1-request", "scenario1-header", "scenario1-response", "scenario1-answer", "scenario1-usage", "scenario2-tools", "scenario2-usage"],
            OutputAnalyzer.EvaluateExpectations(lab11.Expectations, output).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Labels_the_lab11_token_usage_by_scenario_and_ignores_the_usage_printed_in_the_http_bodies()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab11SolutionOutput));

        // The "usage" objects of the response bodies ("prompt_tokens": 74) and the rate limit headers are not usage reports
        Assert.Equal(["Scenario 1 · Token Usage", "Scenario 2 · Token Usage"], usage.Reports.Select(r => r.Label));
        Assert.Equal(201L, usage.Input!.Value); // 16 + 185
        Assert.Equal(239L, usage.Total!.Value); // 23 + 216
    }

    // Standard output of the Lab12 solution (real run), with "I work remotely on Fridays." then an empty line typed in the chat
    // of scenario 2. The typed lines are not on stdout: the "You: " prompt is followed by the next output of the program.
    private static readonly string[] Lab12SolutionOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Agent with a long-term memory - the memories of the user are stored in MongoDB",
        "User id: ada",
        "MemoryAssistant instructions: You are a friendly personal assistant. Use what you remember about the user to personalize your answers. When a conversation starts without a message from the user, introduce yourself and welcome the user, by name if you know it.",
        "Memory extractor instructions: You extract facts about the user from the user message: name, job, city, interests, preferences, constraints. Write each fact as a short sentence in the third person, for example: \"The user works as a nurse.\". Do not extract a fact that is already known, nor questions or requests. Return an empty list when there is no new fact.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Remember the user from one session to another ===",
        "Memories of user 'ada' cleared (6 deleted)",
        "First session",
        "User: Hi! I'm Ada, a software developer from Lyon. I love hiking in the Alps and I'm vegetarian.",
        "[Memory] No memory of user 'ada' yet: nothing added to the request",
        "[Memory] 5 memories extracted and stored for user 'ada' (extraction run: 265 tokens):",
        "  + The user's name is Ada.",
        "  + The user works as a software developer.",
        "  + The user is from Lyon.",
        "  + The user loves hiking in the Alps.",
        "  + The user is vegetarian.",
        "Agent: Hi Ada! It's great to meet you. Welcome! As a software developer and a vegetarian who enjoys hiking in the Alps, you must have some interesting stories to tell. How can I assist you today?",
        "Memories stored in MongoDB for user 'ada': 5",
        "  - The user's name is Ada.",
        "  - The user works as a software developer.",
        "  - The user is from Lyon.",
        "  - The user loves hiking in the Alps.",
        "  - The user is vegetarian.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "New session (empty chat history)",
        "User: What is my name? Suggest a dinner idea and a weekend activity for me.",
        "[Memory] 5 memories of user 'ada' added to the request",
        "[Memory] No new memory for user 'ada' (extraction run: 256 tokens)",
        "Agent: Hello, Ada! It's great to hear from you. For dinner, how about a delicious vegetarian stir-fry with tofu, colorful veggies, and rice? You can toss in some teriyaki sauce for extra flavor!",
        "",
        "As for a weekend activity, since you love hiking in the Alps, how about planning a day hike on one of the beautiful trails near Lyon? You could explore a new route and enjoy the stunning scenery. What do you think?",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage (new session, with memories):",
        "  Input tokens: 119",
        "  Output tokens: 90",
        "  Total tokens: 209",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Chat with the agent - it greets you with what it remembers ===",
        "[Memory] 5 memories of user 'ada' added to the request",
        "[Memory] No user message in this run: nothing to extract",
        "Agent: Hello, Ada! I'm your friendly personal assistant, and I'm here to help you with anything you need. How can I assist you today?",
        "Type a message, or an empty line / 'exit' to end the chat.",
        "You: [Memory] 5 memories of user 'ada' added to the request",
        "[Memory] 1 memory extracted and stored for user 'ada' (extraction run: 254 tokens):",
        "  + The user works remotely on Fridays.",
        "Agent: That's great to hear, Ada! Working remotely on Fridays can be a nice way to wrap up the week. Do you have any special plans for your remote workday, or do you like to keep it flexible?",
        "You: ",
        "Chat ended. Run scenario 2 again: the agent still remembers what you told it (the memories are in MongoDB).",
    ];

    // Standard output of the Lab12 exercise as delivered (TODOs not done yet): no MongoDB, no agent, no provider.
    private static readonly string[] Lab12StartOutput =
    [
        "Endpoint: https://example.openai.azure.com/",
        "Deployment: gpt-4o-mini",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Agent with a long-term memory - the memories of the user are stored in MongoDB",
        "User id: ada",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 1: Remember the user from one session to another ===",
        "First session",
        "User: Hi! I'm Ada, a software developer from Lyon. I love hiking in the Alps and I'm vegetarian.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "New session (empty chat history)",
        "User: What is my name? Suggest a dinner idea and a weekend activity for me.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "=== Scenario 2: Chat with the agent - it greets you with what it remembers ===",
        "Type a message, or an empty line / 'exit' to end the chat.",
        "Chat ended. Run scenario 2 again: the agent still remembers what you told it (the memories are in MongoDB).",
    ];

    [Fact]
    public void Every_lab12_check_passes_on_the_solution_output_and_only_config_on_the_delivered_exercise()
    {
        LabDefinition lab12 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab12")!;

        Assert.True(lab12.Interactive);
        Assert.Null(lab12.Companion);
        Assert.All(OutputAnalyzer.EvaluateExpectations(lab12.Expectations, Lab12SolutionOutput), r => Assert.True(r.Passed, r.Id));
        Assert.Equal(
            ["config"],
            OutputAnalyzer.EvaluateExpectations(lab12.Expectations, Lab12StartOutput).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab12_checks_fail_when_the_context_provider_is_not_written()
    {
        LabDefinition lab12 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab12")!;
        // Agent and scenarios done, provider skeleton only: no [Memory] line, nothing stored, the new session knows nothing.
        string[] output =
        [
            "Endpoint: https://example.openai.azure.com/",
            "=== Scenario 1: Remember the user from one session to another ===",
            "Memories of user 'ada' cleared (0 deleted)",
            "First session",
            "User: Hi! I'm Ada, a software developer from Lyon. I love hiking in the Alps and I'm vegetarian.",
            "Agent: Hi Ada! It's great to meet you! How can I assist you today?",
            "Memories stored in MongoDB for user 'ada': 0",
            "New session (empty chat history)",
            "User: What is my name? Suggest a dinner idea and a weekend activity for me.",
            "Agent: I don't know your name yet! For dinner, how about a homemade stir-fry? As for a weekend activity, consider a hike.",
            "Token Usage (new session, with memories):",
            "  Input tokens: 71",
            "  Output tokens: 85",
            "  Total tokens: 156",
            "=== Scenario 2: Chat with the agent - it greets you with what it remembers ===",
            "Agent: Hello! I'm your friendly personal assistant. What can I assist you with today?",
            "Type a message, or an empty line / 'exit' to end the chat.",
        ];

        Assert.Equal(
            ["config", "scenario1-usage"],
            OutputAnalyzer.EvaluateExpectations(lab12.Expectations, output).Where(r => r.Passed).Select(r => r.Id));
    }

    [Fact]
    public void Lab12_checks_fail_when_the_memories_are_stored_but_never_given_to_the_model()
    {
        LabDefinition lab12 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab12")!;
        // StoreAIContextAsync done, ProvideAIContextAsync not done: the memories reach MongoDB, but neither the new session
        // nor the greeting can use them (and the answer of the new session is guessed from the question only).
        string[] output =
        [
            "Endpoint: https://example.openai.azure.com/",
            "=== Scenario 1: Remember the user from one session to another ===",
            "Memories of user 'ada' cleared (5 deleted)",
            "First session",
            "User: Hi! I'm Ada, a software developer from Lyon. I love hiking in the Alps and I'm vegetarian.",
            "[Memory] 5 memories extracted and stored for user 'ada' (extraction run: 265 tokens):",
            "  + The user's name is Ada.",
            "  + The user works as a software developer.",
            "  + The user is from Lyon.",
            "  + The user loves hiking in the Alps.",
            "  + The user is vegetarian.",
            "Agent: Hi Ada! It's great to meet you!",
            "Memories stored in MongoDB for user 'ada': 5",
            "  - The user's name is Ada.",
            "  - The user works as a software developer.",
            "  - The user is from Lyon.",
            "  - The user loves hiking in the Alps.",
            "  - The user is vegetarian.",
            "New session (empty chat history)",
            "User: What is my name? Suggest a dinner idea and a weekend activity for me.",
            "[Memory] No new memory for user 'ada' (extraction run: 222 tokens)",
            "Agent: I don't know your name yet! For dinner, how about a vegetarian stir-fry?",
            "Token Usage (new session, with memories):",
            "  Input tokens: 71",
            "=== Scenario 2: Chat with the agent - it greets you with what it remembers ===",
            "[Memory] No user message in this run: nothing to extract",
            "Agent: Hello! I'm your friendly personal assistant. How can I help you today?",
            "Type a message, or an empty line / 'exit' to end the chat.",
        ];

        Assert.Equal(
            ["config", "scenario1-extracted", "scenario1-stored", "scenario1-usage"],
            OutputAnalyzer.EvaluateExpectations(lab12.Expectations, output).Where(r => r.Passed).Select(r => r.Id));
    }

    [Theory]
    // Real answer of a dashboard run: "vegetable stir-fry with tofu" and "hiking in the Alps", without the word "vegetarian"
    [InlineData("Agent: Hello, Ada! For dinner, how about trying a delicious vegetable stir-fry with tofu?|As for the weekend activity, since you love hiking in the Alps, how about planning a hike?", true)]
    [InlineData("Agent: Hello, Ada! How about a vegetarian lasagna, and a walk in Lyon?", true)]
    // The name alone, with ideas that do not come from the memories: not enough
    [InlineData("Agent: Hello, Ada! How about a stir-fry with your favorite vegetables, and a visit to a museum?", false)]
    // The preferences without the name
    [InlineData("Agent: Hello! How about a vegetarian stir-fry, and a hike in the Alps?", false)]
    public void Lab12_recall_check_needs_the_name_and_a_preference_that_only_the_memories_give(string answer, bool expected)
    {
        LabDefinition lab12 = LabCatalogTests.LoadRealCatalog().Find("azureopenai-lab12")!;
        string[] output =
        [
            "=== Scenario 1: Remember the user from one session to another ===",
            "New session (empty chat history)",
            "User: What is my name? Suggest a dinner idea and a weekend activity for me.",
            "[Memory] 5 memories of user 'ada' added to the request",
            "[Memory] No new memory for user 'ada' (extraction run: 256 tokens)",
            .. answer.Split('|'),
            "",
            "Token Usage (new session, with memories):",
            "  Input tokens: 119",
        ];

        Assert.Equal(expected, OutputAnalyzer.EvaluateExpectations(lab12.Expectations, output).Single(r => r.Id == "scenario1-recall").Passed);
    }

    [Fact]
    public void Labels_the_lab12_token_usage_with_its_scenario_and_ignores_the_extraction_cost_of_the_provider()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab12SolutionOutput));

        // The "(extraction run: 265 tokens)" of the [Memory] lines is not a usage report
        Assert.Equal(["Scenario 1 · Token Usage (new session, with memories)"], usage.Reports.Select(r => r.Label));
        Assert.Equal(119L, usage.Input!.Value);
        Assert.Equal(209L, usage.Total!.Value);
    }
}
