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
}
