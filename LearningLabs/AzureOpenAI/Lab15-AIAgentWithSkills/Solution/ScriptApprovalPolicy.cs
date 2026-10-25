using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AIAgentWithSkills;

/// <summary>The decision taken on an approval request, and its reason (sent to the model when the call is rejected).</summary>
public sealed record ApprovalDecision(bool Approved, string Reason);

/// <summary>
/// The approval policy of scenario 3: the program decides, instead of a person, whether a skill script may run.
/// Only the "quote" script of the "shipping-quote" skill is allowed, for parcels up to 30 kg.
/// </summary>
public static class ScriptApprovalPolicy
{
    public const double MaxWeightKg = 30;

    public static ApprovalDecision Decide(FunctionCallContent call)
    {
        string skill = Argument(call, "skillName") ?? string.Empty;
        string script = Argument(call, "scriptName") ?? string.Empty;
        if (skill != "shipping-quote" || script != "quote")
        {
            return new ApprovalDecision(false, $"The script '{skill}/{script}' is not on the allow list.");
        }

        double? weight = WeightOf(call);
        if (weight is null)
        {
            return new ApprovalDecision(false, "The weight of the parcel is missing from the call.");
        }

        return weight <= MaxWeightKg
            ? new ApprovalDecision(true, $"Parcel of {Format(weight.Value)} kg: within the {Format(MaxWeightKg)} kg limit.")
            : new ApprovalDecision(false, $"Parcel of {Format(weight.Value)} kg: over the {Format(MaxWeightKg)} kg limit, a freight quote is needed from the logistics team.");
    }

    private static string? Argument(FunctionCallContent call, string name) =>
        call.Arguments is not null && call.Arguments.TryGetValue(name, out object? value) ? value?.ToString() : null;

    // The arguments of the script travel as JSON in the "arguments" argument of run_skill_script
    private static double? WeightOf(FunctionCallContent call)
    {
        if (call.Arguments is null || !call.Arguments.TryGetValue("arguments", out object? value) || value is null)
        {
            return null;
        }

        JsonElement json = value is JsonElement element ? element : JsonSerializer.SerializeToElement(value);
        if (json.ValueKind == JsonValueKind.String)
        {
            json = JsonDocument.Parse(json.GetString()!).RootElement;
        }

        return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("weightKg", out JsonElement weight) && weight.TryGetDouble(out double kg)
            ? kg
            : null;
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
