namespace CommonUtilities;

/// <summary>
/// A console spinner that shows a loading animation while waiting for async operations.
/// </summary>
public class ConsoleSpinner : IDisposable
{
    private string _message;
    private readonly CancellationTokenSource _cts;
    private readonly Task _spinnerTask;
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private bool _disposed;

    public ConsoleSpinner(string message = "Processing")
    {
        _message = message;
        _cts = new CancellationTokenSource();
        _spinnerTask = SpinAsync(_cts.Token);
    }

    /// <summary>
    /// Updates the spinner message dynamically (useful for showing progress).
    /// </summary>
    public void UpdateMessage(string message)
    {
        _message = message;
    }

    private async Task SpinAsync(CancellationToken cancellationToken)
    {
        int frameIndex = 0;
        var startTime = DateTime.Now;

        Console.CursorVisible = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            var elapsed = DateTime.Now - startTime;
            var elapsedStr = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            
            // Pad with spaces to clear any previous longer message
            var output = $"\r{SpinnerFrames[frameIndex]} {_message}... [{elapsedStr}]";
            Console.Write(output.PadRight(Console.WindowWidth - 1));
            
            frameIndex = (frameIndex + 1) % SpinnerFrames.Length;
            
            try
            {
                await Task.Delay(80, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _spinnerTask.Wait();
        _cts.Dispose();

        // Clear the spinner line
        Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
        Console.CursorVisible = true;
    }
}

/// <summary>
/// Extension methods for running async tasks with a spinner.
/// </summary>
public static class SpinnerExtensions
{
    /// <summary>
    /// Runs an async task while displaying a loading spinner.
    /// </summary>
    public static async Task<T> WithSpinner<T>(this Task<T> task, string message = "Processing")
    {
        using var spinner = new ConsoleSpinner(message);
        return await task;
    }

    /// <summary>
    /// Runs an async task (without return value) while displaying a loading spinner.
    /// </summary>
    public static async Task WithSpinner(this Task task, string message = "Processing")
    {
        using var spinner = new ConsoleSpinner(message);
        await task;
    }
}
