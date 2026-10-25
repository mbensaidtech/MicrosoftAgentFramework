using System.ComponentModel;

namespace AIAgentWithAIContextProvider;

/// <summary>
/// The structured output of the memory extractor agent: <c>RunAsync&lt;ExtractedMemories&gt;</c> sends the JSON schema of
/// this type to the model (the <see cref="DescriptionAttribute"/> texts included) and deserializes its answer.
/// </summary>
[Description("The new facts about the user found in the user message.")]
public sealed class ExtractedMemories
{
    [Description("Each new fact about the user, as a short sentence in the third person (for example: \"The user's name is Ada.\"). Empty when the message contains no new fact.")]
    public List<string> Memories { get; set; } = [];
}
