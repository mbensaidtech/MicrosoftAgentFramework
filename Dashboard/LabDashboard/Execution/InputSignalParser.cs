using System.Text;

namespace LabDashboard.Execution;

/// <summary>
/// Removes the read signals written on stderr by the input hook (<c>ESC ] lab;input;&lt;n&gt; BEL</c>, see LabInputHook)
/// and reports their number. A signal split across two reads is recognized; anything that turns out not to be a signal
/// is given back as ordinary text, unchanged.
/// </summary>
public sealed class InputSignalParser
{
    private const string Prefix = "\u001b]lab;input;";
    private const char Terminator = '\u0007';
    private const int MaxDigits = 9;

    private readonly StringBuilder _held = new();

    /// <summary>Returns the text to display; <paramref name="onSignal"/> receives the number of each complete signal.</summary>
    public string Feed(ReadOnlySpan<char> chunk, Action<int> onSignal)
    {
        StringBuilder text = new(chunk.Length);
        foreach (char c in chunk)
        {
            Accept(c, text, onSignal);
        }

        return text.ToString();
    }

    /// <summary>Gives back an incomplete signal as text when the stream ends.</summary>
    public string Complete()
    {
        string rest = _held.ToString();
        _held.Clear();
        return rest;
    }

    private void Accept(char c, StringBuilder text, Action<int> onSignal)
    {
        if (_held.Length == 0)
        {
            if (c == Prefix[0])
            {
                _held.Append(c);
            }
            else
            {
                text.Append(c);
            }

            return;
        }

        if (_held.Length < Prefix.Length)
        {
            if (c == Prefix[_held.Length])
            {
                _held.Append(c);
                return;
            }
        }
        else if (char.IsAsciiDigit(c) && _held.Length - Prefix.Length < MaxDigits)
        {
            _held.Append(c);
            return;
        }
        else if (c == Terminator && _held.Length > Prefix.Length)
        {
            onSignal(int.Parse(_held.ToString(Prefix.Length, _held.Length - Prefix.Length)));
            _held.Clear();
            return;
        }

        // Not a signal: release what was held and look at this character again (it may start a signal).
        text.Append(_held);
        _held.Clear();
        Accept(c, text, onSignal);
    }
}
