using System.Diagnostics;
using System.Text;

namespace LabDashboard.Execution;

public enum OutputStream
{
    Stdout,
    Stderr
}

/// <summary>Receives the output of a child process as it is produced. Calls may come from two threads.</summary>
public interface IProcessOutputObserver
{
    /// <summary>Text to append to the current line of <paramref name="stream"/> (possibly partial).</summary>
    void OnText(OutputStream stream, string text, bool endOfLine);

    /// <summary>A line overwritten by the program (progress indicator).</summary>
    void OnTransient(OutputStream stream, string text);

    /// <summary>A completed line, for analysis.</summary>
    void OnLine(OutputStream stream, string line);
}

/// <summary>
/// Starts a process without a shell (arguments are passed as a list, never parsed from a string),
/// streams stdout/stderr to an observer and kills the whole process tree on cancellation.
/// </summary>
/// <summary>Standard input of an interactive run and the input hook to load into the program.</summary>
public sealed record InteractiveInput(RunInput Input, string HookPath)
{
    public const string ActivationVariable = "LAB_DASHBOARD_RUN";
    public const string StartupHooksVariable = "DOTNET_STARTUP_HOOKS";

    /// <summary>The hook assembly is copied next to the dashboard (project reference to LabInputHook).</summary>
    public static string DefaultHookPath => Path.Combine(AppContext.BaseDirectory, "LabInputHook.dll");
}

public static class ProcessRunner
{
    public static async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IProcessOutputObserver observer,
        CancellationToken cancellationToken,
        InteractiveInput? interactive = null)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        if (interactive is not null)
        {
            // Loads the input hook into the lab program (see LabInputHook): each Console.ReadLine is announced on stderr.
            startInfo.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            startInfo.Environment[InteractiveInput.ActivationVariable] = "1";
            string? hooks = startInfo.Environment.TryGetValue(InteractiveInput.StartupHooksVariable, out string? existing) ? existing : null;
            startInfo.Environment[InteractiveInput.StartupHooksVariable] = string.IsNullOrEmpty(hooks)
                ? interactive.HookPath
                : $"{interactive.HookPath}{Path.PathSeparator}{hooks}";
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        if (interactive is null)
        {
            // Not interactive: close stdin so a stray Console.ReadLine() returns instead of hanging.
            process.StandardInput.Close();
        }
        else
        {
            process.StandardInput.AutoFlush = true;
            interactive.Input.Attach(process.StandardInput);
        }

        Task stdout = PumpAsync(process.StandardOutput, OutputStream.Stdout, observer, null);
        Task stderr = PumpAsync(process.StandardError, OutputStream.Stderr, observer, interactive?.Input);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        finally
        {
            interactive?.Input.Detach();
        }

        await Task.WhenAll(stdout, stderr);
        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, OutputStream stream, IProcessOutputObserver observer, RunInput? input)
    {
        ConsoleStreamDecoder decoder = new();
        InputSignalParser? signals = input is null ? null : new InputSignalParser();
        StringBuilder currentLine = new();
        char[] buffer = new char[4096];

        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0)
        {
            ReadOnlySpan<char> chunk = buffer.AsSpan(0, read);
            Dispatch(decoder.Decode(signals is null ? chunk : signals.Feed(chunk, input!.OnReadSignal)));
        }

        if (signals is not null)
        {
            Dispatch(decoder.Decode(signals.Complete()));
        }

        Dispatch(decoder.Complete());

        void Dispatch(IReadOnlyList<ConsoleFragment> fragments)
        {
            foreach (ConsoleFragment fragment in fragments)
            {
                if (fragment.Kind == ConsoleFragmentKind.Transient)
                {
                    observer.OnTransient(stream, fragment.Text);
                    continue;
                }

                observer.OnText(stream, fragment.Text, fragment.EndOfLine);
                currentLine.Append(fragment.Text);
                if (fragment.EndOfLine)
                {
                    observer.OnLine(stream, currentLine.ToString());
                    currentLine.Clear();
                }
            }
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already exited.
        }
    }
}
