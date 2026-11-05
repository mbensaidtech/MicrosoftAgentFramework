namespace OrchestrationSequential;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names and the instructions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.
// The input of every workflow is the meeting transcript of Data/meeting-transcript.txt.

/// <summary>
/// The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.
/// In a sequential workflow, the name of an agent is also the author of its messages and the start of the id of its executor.
/// </summary>
public static class Agents
{
    /// <summary>Scenario 1: the first step of the pipeline, which summarizes the transcript.</summary>
    public static class MeetingSummarizer
    {
        public const string Name = "MeetingSummarizer";

        public const string Instructions =
            "You summarize meeting transcripts. Answer with five bullet points at most, in the language of the transcript. " +
            "Answer only with the bullet points.";
    }

    /// <summary>
    /// Scenarios 1, 2 and 3: extracts the action items. It needs the transcript itself: the owners and the due dates are there.
    /// </summary>
    public static class ActionItemsExtractor
    {
        public const string Name = "ActionItemsExtractor";

        public const string Instructions =
            "You extract the action items of a meeting transcript. Answer only with a numbered list, one line per action item: " +
            "Owner - Task - Due date (as said in the meeting, or 'no date'). Use the language of the transcript.";
    }

    /// <summary>Scenarios 2 and 3: writes the follow-up email from the action items of the previous message.</summary>
    public static class FollowUpWriter
    {
        public const string Name = "FollowUpWriter";

        public const string Instructions =
            "You write the follow-up email of a meeting for its attendees, from the action items of the previous message. " +
            "Start with a line 'Subject: ...', then write a short email that lists every action item with its owner and its due date. " +
            "Answer only with the email.";
    }

    /// <summary>Scenario 2: the last step, which translates the email of the previous message.</summary>
    public static class Translator
    {
        public const string Name = "Translator";

        public const string Instructions =
            "You translate the email of the previous message into French. Keep its format. Answer only with the translation of that email.";
    }

    /// <summary>
    /// Scenario 3: sends the email of the previous message with its tool <c>send_email</c>, which requires an approval.
    /// The instructions name the tool: keep this name in <c>AIFunctionFactory.Create</c>.
    /// </summary>
    public static class MinutesSender
    {
        public const string Name = "MinutesSender";

        public const string Instructions =
            "You send the follow-up email of the previous message to the attendees of the meeting. " +
            "Call send_email exactly once: the recipients are the first names of the attendees of the meeting, separated by commas, " +
            "and the subject and the body are those of the email. Then say in one sentence whether the email was sent.";
    }
}
