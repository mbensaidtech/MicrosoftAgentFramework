using System.Text;

namespace LabDashboard.Reporting;

/// <summary>
/// The trainer's command (Business Rule 23): <c>dotnet run --project Dashboard/LabDashboard -- invitation</c> asks for the admin API key and the
/// workshop key without echoing them, then prints the invitation code to e-mail to the candidates. It starts no dashboard, calls no network and
/// writes nothing to disk. The keys are never accepted as arguments (they would stay in the shell history) and never printed back.
/// The code goes to the standard output alone; prompts and notes go to the standard error.
/// </summary>
internal static class InvitationCommand
{
    public const string Name = "invitation";

    /// <summary>Runs the command. <paramref name="readSecret"/> shows its prompt and reads one value without echoing it (null: end of input).</summary>
    public static int Run(IReadOnlyList<string> arguments, Func<string, string?> readSecret, TextWriter output, TextWriter messages)
    {
        if (arguments.Count > 0)
        {
            messages.WriteLine($"Usage: dotnet run --project Dashboard/LabDashboard -- {Name}");
            messages.WriteLine("The keys are asked for, never passed as arguments: arguments stay in the shell history.");
            return 2;
        }

        messages.WriteLine("Invitation code for the Lab Bench: it carries the admin API key and the workshop key (Business Rule 21).");
        string? apiKey = readSecret("Admin API key (not echoed): ");
        if (Refuse("admin API key", apiKey, messages))
        {
            return 1;
        }

        string? workshopKey = readSecret("Workshop key (not echoed): ");
        if (Refuse("workshop key", workshopKey, messages))
        {
            return 1;
        }

        string code;
        try
        {
            code = InvitationCode.Create(apiKey!, workshopKey!);
        }
        catch (ArgumentException)
        {
            // Both keys are valid at this point: only their length is left (the message names no value anyway).
            messages.WriteLine($"The keys are too long: an invitation code has {InvitationCode.MaxLength} characters at most. No invitation code was created.");
            return 1;
        }

        output.WriteLine(code);
        messages.WriteLine("E-mail this code to the candidates: they paste it in \"Join the workshop\" (or Settings → Workshop).");
        messages.WriteLine("It is as sensitive as the two keys: never post it in a public channel.");
        return 0;
    }

    /// <summary>Reads a line without echoing it on a terminal; reads the redirected input as is (a script piping the keys).</summary>
    public static string? ReadHidden(string prompt)
    {
        Console.Error.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.In.ReadLine();
        }

        StringBuilder value = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }
            }
            else if (key.KeyChar != '\0')
            {
                value.Append(key.KeyChar);
            }
        }
    }

    private static bool Refuse(string name, string? value, TextWriter messages)
    {
        switch (InvitationCode.ValidateKey(value))
        {
            case InvitationCode.RequiredError:
                messages.WriteLine($"The {name} is empty: no invitation code was created.");
                return true;
            case not null:
                messages.WriteLine($"The {name} contains whitespace or characters other than visible ASCII: no invitation code was created.");
                return true;
            default:
                return false;
        }
    }
}
