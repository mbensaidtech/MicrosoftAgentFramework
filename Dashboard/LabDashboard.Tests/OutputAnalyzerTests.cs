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
        "=== Scenario 2: Function tools calling - using reflection ===",
        "Tools that will be available to the agent: GetEmployeeInfo, GetMeetingRooms, BookMeetingRoom",
        "",
        "1. **Innovation Lab (ROOM-A)**",
        "The **Innovation Lab (ROOM-A)** has been successfully booked for the employee with ID **EMP001**.",
        "",
        "-------------------------------------------------------------------------------",
        "",
        "Token Usage:",
        "  Input tokens: 1110",
        "  Output tokens: 235",
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
        "[Middleware] Calling book_meeting_room(roomId: ROOM-B, employeeId: EMP003, date: 2026-10-15, startTime: 14:00, endTime: 15:00, subject: Design Review)",
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
        "=== Scenario 2: Function tools calling - using reflection ===",
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
        // The agent answered without calling the tools: no employee data, no booking, no notification id, no middleware trace.
        string[] output =
        [
            "=== Scenario 1: Function tools calling - basic ===",
            "Tools available to the agent:",
            "- get_employee_info: ",
            "I don't have access to employee records.",
            "=== Scenario 2: Function tools calling - using reflection ===",
            "Tools that will be available to the agent: GetEmployeeInfo, GetMeetingRooms, BookMeetingRoom, SearchEmployees",
            "I cannot list the rooms.",
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
        "=== Scenario 2: Custom ChatHistoryProvider - chat history in a vector store ===",
        "User: Hello, my name is Ada. What is the capital of France?",
        "",
        "",
        "Agent: The capital of France is Paris.",
        "Chat history stored in the vector store under the key: 58cc7732b4d04a34a96c81bb0915bcac",
        "Serialized session:",
        "{",
        "  \"stateBag\": {",
        "    \"VectorChatHistoryProvider\": {",
        "      \"sessionDbKey\": \"58cc7732b4d04a34a96c81bb0915bcac\"",
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
        "=== Scenario 2: Custom ChatHistoryProvider - chat history in a vector store ===",
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
        // The follow-up questions were asked in new sessions, the vector store session also serialized its messages,
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
            "=== Scenario 2: Custom ChatHistoryProvider - chat history in a vector store ===",
            "Chat history stored in the vector store under the key: 846e2685bdcf4ff4921c7627b50aefb6",
            "Serialized session:",
            "{",
            "  \"stateBag\": {",
            "    \"VectorChatHistoryProvider\": {",
            "      \"sessionDbKey\": \"846e2685bdcf4ff4921c7627b50aefb6\",",
            "      \"messages\": []",
            "    }",
            "  }",
            "}",
            "Restoring the session from the saved JSON...",
            "Agent: Paris has about 2.1 million inhabitants, but I don't know your name.",
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
        Assert.Equal(300L, usage.Total!.Value); // 102 + 102 + 96
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
        ];

        Assert.DoesNotContain(OutputAnalyzer.EvaluateExpectations(lab09.Expectations, output), r => r.Passed && !r.Id.EndsWith("-usage", StringComparison.Ordinal));
    }

    [Fact]
    public void Labels_the_two_lab09_token_usage_blocks_with_their_scenario()
    {
        TokenUsageSummary usage = Assert.IsType<TokenUsageSummary>(OutputAnalyzer.ParseTokenUsage(Lab09SolutionOutput));

        Assert.Equal(["Scenario 1 · Token Usage (2 runs)", "Scenario 2 · Token Usage (2 runs)"], usage.Reports.Select(r => r.Label));
        Assert.Equal(1409L, usage.Total!.Value); // 355 + 1054
    }
}
