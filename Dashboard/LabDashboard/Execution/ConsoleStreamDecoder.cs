using System.Text;

namespace LabDashboard.Execution;

public enum ConsoleFragmentKind
{
    /// <summary>Text to append to the current line; <see cref="ConsoleFragment.EndOfLine"/> closes the line.</summary>
    Text,

    /// <summary>A line that the program overwrote with a bare carriage return (e.g. a console spinner frame).</summary>
    Transient
}

public sealed record ConsoleFragment(ConsoleFragmentKind Kind, string Text, bool EndOfLine = false);

/// <summary>
/// Turns raw console output into display fragments with terminal semantics:
/// <list type="bullet">
/// <item><c>\n</c> or <c>\r\n</c> ends a line;</item>
/// <item>text followed by a bare <c>\r</c> is overwritten by the program, so it is reported as <see cref="ConsoleFragmentKind.Transient"/>;</item>
/// <item>text written without a newline (streamed answers) is flushed as soon as it is read, so it can be shown live.</item>
/// </list>
/// </summary>
public sealed class ConsoleStreamDecoder
{
    private readonly StringBuilder _line = new();
    private int _flushed;
    private bool _pendingCarriageReturn;
    private bool _lineStartedAfterCarriageReturn;

    public IReadOnlyList<ConsoleFragment> Decode(ReadOnlySpan<char> chunk)
    {
        List<ConsoleFragment> output = [];
        foreach (char c in chunk)
        {
            if (_pendingCarriageReturn)
            {
                _pendingCarriageReturn = false;
                if (c == '\n')
                {
                    EndLine(output);
                    continue;
                }

                Overwrite(output);
            }

            switch (c)
            {
                case '\r':
                    _pendingCarriageReturn = true;
                    break;
                case '\n':
                    EndLine(output);
                    break;
                default:
                    _line.Append(c);
                    break;
            }
        }

        FlushPartialLine(output);
        return output;
    }

    /// <summary>Flushes whatever is left when the stream ends.</summary>
    public IReadOnlyList<ConsoleFragment> Complete()
    {
        List<ConsoleFragment> output = [];
        if (_pendingCarriageReturn)
        {
            _pendingCarriageReturn = false;
            Overwrite(output);
        }
        else if (_line.Length > 0 || _flushed > 0)
        {
            EndLine(output);
        }

        return output;
    }

    private void EndLine(List<ConsoleFragment> output)
    {
        output.Add(new ConsoleFragment(ConsoleFragmentKind.Text, _line.ToString(_flushed, _line.Length - _flushed), EndOfLine: true));
        ResetLine();
    }

    private void Overwrite(List<ConsoleFragment> output)
    {
        if (_flushed > 0)
        {
            // Part of this line was already shown as regular text: keep it and close it.
            EndLine(output);
        }
        else
        {
            string text = _line.ToString().Trim();
            if (text.Length > 0)
            {
                output.Add(new ConsoleFragment(ConsoleFragmentKind.Transient, text));
            }

            ResetLine();
        }

        _lineStartedAfterCarriageReturn = true;
    }

    private void FlushPartialLine(List<ConsoleFragment> output)
    {
        // A line that follows a bare \r is probably a transient frame: wait until we know how it ends.
        if (_pendingCarriageReturn || _lineStartedAfterCarriageReturn || _line.Length == _flushed)
        {
            return;
        }

        output.Add(new ConsoleFragment(ConsoleFragmentKind.Text, _line.ToString(_flushed, _line.Length - _flushed)));
        _flushed = _line.Length;
    }

    private void ResetLine()
    {
        _line.Clear();
        _flushed = 0;
        _lineStartedAfterCarriageReturn = false;
    }
}
