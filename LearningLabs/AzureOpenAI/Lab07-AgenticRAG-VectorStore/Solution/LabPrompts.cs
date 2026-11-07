namespace AgenticRAG;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.
// The questions are about entries of Data/sav-faq.json: the README Expected Output gives the entries they find.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>
    /// Scenarios 3 and 5: the agent that searches the FAQ itself, with the search_faq function tool
    /// (on Chat Completions in scenario 3, on the Responses API in scenario 5).
    /// </summary>
    public static class Faq
    {
        public const string Name = "FaqAgent";

        public const string Instructions =
            "You are the customer support assistant of an online shop. " +
            "Answer only from the FAQ: call search_faq to find the relevant entries, answer from them and quote the id of the entries you used. " +
            "If the FAQ does not cover the question, say so.";
    }

    /// <summary>Scenario 4: the agent that receives the FAQ entries from the TextSearchProvider, before every model call.</summary>
    public static class FaqRag
    {
        public const string Name = "FaqRagAgent";

        public const string Instructions =
            "You are the customer support assistant of an online shop. " +
            "Answer only from the provided context and quote the id of the FAQ entries you used. Keep the answers short.";
    }

    /// <summary>
    /// Scenario 6: the agent that searches the FAQ with the file search tool hosted by the service (one file per FAQ entry,
    /// named after the id of the entry).
    /// </summary>
    public static class HostedFaq
    {
        public const string Name = "HostedFaqAgent";

        public const string Instructions =
            "You are the customer support assistant of an online shop. " +
            "Answer only from the FAQ: search the FAQ files, answer from them and quote the id of the entries you used (the name of their file, without the extension). " +
            "If the FAQ does not cover the question, say so.";
    }
}

/// <summary>The questions to search in the FAQ or to ask the agents.</summary>
public static class Questions
{
    /// <summary>
    /// Scenario 2: the question of the semantic search (no agent). It shares no word with faq-010
    /// ("How do I contact customer support?"), which it is about.
    /// </summary>
    public const string PhoneNumberForHelp = "Is there a phone number I can call for help?";

    /// <summary>
    /// Scenarios 3, 5 and 6: answered from faq-005 (contact customer service within 48 hours, with photos).
    /// The same question with your vector store (Chat Completions, then Responses) and with the hosted file search.
    /// </summary>
    public const string BrokenItem = "I received a broken item yesterday. What should I do?";

    /// <summary>Scenario 4: the first question of the session, answered from faq-001 (returns).</summary>
    public const string SendBackJacket = "I want to send back a jacket I bought last week. How does it work?";

    /// <summary>
    /// Scenario 4: the follow-up question, asked in the same session. Vague on its own, it is searched with the first
    /// question and answered from faq-002 (refund delay).
    /// </summary>
    public const string MoneyBackDelay = "And how long until I get my money back?";
}
