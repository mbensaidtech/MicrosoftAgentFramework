using System.Reflection;
using System.Text;

/// <summary>
/// Startup hook (the runtime requires this exact name, outside any namespace, with a static Initialize method).
/// Replaces <see cref="Console.In"/> with a reader that announces each <c>ReadLine</c> to the dashboard with an invisible
/// signal on stderr — <c>ESC ] lab;input;&lt;n&gt; BEL</c> — then reads standard input as UTF-8 on every platform.
/// </summary>
internal static class StartupHook
{
    public const string ActivationVariable = "LAB_DASHBOARD_RUN";

    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable(ActivationVariable) != "1")
        {
            return;
        }

        // `dotnet run` inherits the variables too: only act inside the lab program, not in the dotnet CLI host.
        if (Assembly.GetEntryAssembly()?.GetName().Name is null or "dotnet")
        {
            return;
        }

        // SetIn wraps the reader in a synchronized one.
        Console.SetIn(new SignalingReader(new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false))));
    }

    private sealed class SignalingReader(TextReader inner) : TextReader
    {
        private int _reads;

        public override string? ReadLine()
        {
            // The prompt the program just wrote must reach the dashboard before the signal.
            Console.Out.Flush();
            Console.Error.Write($"\u001b]lab;input;{++_reads}\u0007");
            Console.Error.Flush();
            return inner.ReadLine();
        }

        public override int Peek() => inner.Peek();

        public override int Read() => inner.Read();

        public override int Read(char[] buffer, int index, int count) => inner.Read(buffer, index, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
