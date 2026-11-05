namespace UseAgentAsTool;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>
/// The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>, and the description of the agent
/// that becomes the description of its tool (scenario 3).
/// </summary>
public static class Agents
{
    /// <summary>
    /// Setup, used by scenarios 1 and 2: the after-sales expert, which answers with the rules of the store.
    /// </summary>
    public static class AfterSalesExpert
    {
        public const string Name = "AfterSalesExpert";

        /// <summary>
        /// A format string: <c>{0}</c> is the after-sales rules (the content of <c>MockedData/sav-rules.yml</c>), given with
        /// <c>string.Format(CultureInfo.InvariantCulture, Agents.AfterSalesExpert.Instructions, savRules)</c>.
        /// </summary>
        public const string Instructions = """
            You are the after-sales expert of an online store. Answer in a few sentences, using only the rules below.
            If the rules do not cover the question, say so.

            {0}
            """;
    }

    /// <summary>
    /// Scenario 1: the reviewer, which scores an answer of the after-sales service with the same rules
    /// (structured output, <c>QualityReview</c>).
    /// </summary>
    public static class QualityReviewer
    {
        public const string Name = "QualityReviewer";

        /// <summary>
        /// A format string: <c>{0}</c> is the after-sales rules (the content of <c>MockedData/sav-rules.yml</c>), given with
        /// <c>string.Format(CultureInfo.InvariantCulture, Agents.QualityReviewer.Instructions, savRules)</c>.
        /// </summary>
        public const string Instructions = """
            You review the answers of the after-sales service of an online store before they are sent to a customer.
            Score each answer with the rules below:
            - 9 to 10: correct according to the rules and answers the question
            - 7 to 8: correct, but incomplete or unclear
            - 4 to 6: partly wrong, or misses an important condition
            - 1 to 3: contradicts the rules, or promises something the store does not offer

            {0}
            """;
    }

    /// <summary>Scenario 2: the order specialist, with its own tool <c>get_order</c>.</summary>
    public static class OrderSpecialist
    {
        public const string Name = "OrderSpecialist";

        public const string Instructions =
            "You are the order specialist of an online store. Look up the orders with get_order and answer only with the facts of the order data.";
    }

    /// <summary>Scenario 2: the front desk, which talks with the customer and has the two specialists as tools.</summary>
    public static class FrontDesk
    {
        public const string Name = "FrontDesk";

        public const string Instructions =
            "You are the front desk of the customer service of an online store. You know neither the orders nor the after-sales rules: " +
            "ask the specialists with your tools, then answer the customer in a few sentences.";
    }

    /// <summary>
    /// Scenario 3: the ticket specialist, which keeps the support ticket of a case up to date. It is turned into a tool without
    /// options: its name and its description become the name and the description of the tool.
    /// </summary>
    public static class TicketSpecialist
    {
        public const string Name = "TicketSpecialist";

        public const string Instructions = """
            You keep the support ticket of ONE customer case up to date. Each message gives new facts about the case.
            Answer only with the complete ticket, with every fact you know, in this format:
            Order: <order number, or unknown>
            Problem: <problem, or unknown>
            Customer request: <request, or unknown>
            """;

        public const string Description =
            "Keeps the support ticket of the current customer case up to date. Give it each new fact about the case.";
    }
}

/// <summary>
/// Scenario 2: the descriptions of the specialists turned into tools, given in <c>AIFunctionFactoryOptions.Description</c>.
/// With the name of the tool, they are all the model of the front desk knows about each specialist.
/// </summary>
public static class ToolDescriptions
{
    /// <summary>Scenario 2: the description of the tool <c>ask_order_specialist</c> (the order specialist).</summary>
    public const string AskOrderSpecialist =
        "Asks the order specialist about an order: product, status, purchase and delivery dates. Include the order number in the query.";

    /// <summary>Scenario 2: the description of the tool <c>ask_after_sales_expert</c> (the after-sales expert).</summary>
    public const string AskAfterSalesExpert =
        "Asks the after-sales expert about the rules of the store: returns, refunds, exchanges, warranty and delivery problems. " +
        "The expert does not see the conversation: give it all the facts it needs.";
}

/// <summary>The messages of the customer, sent to the agents.</summary>
public static class CustomerMessages
{
    /// <summary>Scenario 1: the question answered by the after-sales expert, then given to the reviewer with the answer.</summary>
    public const string HeadphonesReturn =
        "I bought headphones 10 days ago. They are unused and still in their original packaging. Can I return them and get a refund?";

    /// <summary>Scenario 2: the message sent to the front desk, which consults the specialists.</summary>
    public const string BrokenLaptopScreen =
        "Hello, my order ORD-1042 arrived yesterday and the laptop screen is broken. What can I do?";
}

/// <summary>Scenario 1: the messages that the code sends to the reviewer.</summary>
public static class ReviewRequests
{
    /// <summary>
    /// Scenario 1: the message that gives an answer to review. A format string: <c>{0}</c> is the question of the customer
    /// and <c>{1}</c> the answer to review, given with
    /// <c>string.Format(CultureInfo.InvariantCulture, ReviewRequests.QuestionAndAnswer, CustomerMessages.HeadphonesReturn, answer.Text)</c>.
    /// </summary>
    public const string QuestionAndAnswer = "Customer question: {0}\nAnswer to review: {1}";

    /// <summary>
    /// Scenario 1: an answer written by hand, wrong on purpose (it contradicts the rules): the decision of the code must hold it back.
    /// </summary>
    public const string WrongAnswer =
        "Of course! You can return any product within 90 days, even used, and we refund you in cash within 24 hours.";
}

/// <summary>Scenario 3: the facts of the customer case, given to the ticket specialist one per call (the <c>query</c> of the tool).</summary>
public static class CaseFacts
{
    /// <summary>Scenario 3: the first call, the only one that gives the order number (ORD-1042).</summary>
    public const string BrokenScreen = "Order ORD-1042: the laptop screen arrived broken.";

    /// <summary>Scenario 3: the second call.</summary>
    public const string ReplacementRequested = "The customer wants a replacement.";
}
