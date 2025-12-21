namespace CommonUtilities;

/// <summary>
/// Provides static methods for Console to write messages with different colors based on message type.
/// Similar to Console.Write/WriteLine but with automatic color coding.
/// </summary>
public static class ColoredConsole
{
    /// <summary>
    /// Writes an error message followed by a line terminator in red color.
    /// </summary>
    public static void WriteErrorLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes a warning message followed by a line terminator in yellow color.
    /// </summary>
    public static void WriteWarningLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes a success message followed by a line terminator in green color.
    /// </summary>
    public static void WriteSuccessLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes an information message followed by a line terminator in cyan color.
    /// </summary>
    public static void WriteInfoLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes a user message followed by a line terminator in green color.
    /// </summary>
    public static void WriteUserLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes an assistant message followed by a line terminator in cyan color.
    /// </summary>
    public static void WriteAssistantLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes a system message followed by a line terminator in yellow color.
    /// </summary>
    public static void WriteSystemLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes primary/important log information in blue color followed by a line terminator.
    /// </summary>
    public static void WritePrimaryLogLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes secondary/less important log information in dark gray color followed by a line terminator.
    /// </summary>
    public static void WriteSecondaryLogLine(string? value)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(value);
        Console.ResetColor();
    }

    /// <summary>
    /// Writes an empty line.
    /// </summary>
    public static void WriteEmptyLine()
    {
        Console.WriteLine();
    }

    /// <summary>
    /// Writes a visual divider line using dashes in dark gray color, with empty lines before and after.
    /// </summary>
    public static void WriteDividerLine()
    {
        Console.WriteLine();
        var divider = new string('-', Math.Max(1, Console.WindowWidth - 1));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(divider);
        Console.ResetColor();
        Console.WriteLine();
    }
}
