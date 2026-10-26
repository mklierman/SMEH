using System.Text;

namespace SMEH.Helpers;

public sealed class IndentedConsoleWriter : TextWriter
{
    private readonly TextWriter _inner;
    private readonly string _indent;
    // Output is built up here and passed to the inner writer in one call; the console flushes on every
    // write, so writing per character made redraws (e.g. progress bars) slow and visibly flicker the cursor
    private readonly StringBuilder _pending = new();
    private bool _atLineStart = true;
    private bool _inAnsiEscape;
    private bool _inAnsiControlSequence;

    public IndentedConsoleWriter(TextWriter inner, int spaces)
    {
        _inner = inner;
        _indent = new string(' ', spaces);
    }

    public override Encoding Encoding => _inner.Encoding;

    public override void Write(char value)
    {
        Append(value);
        WritePending();
    }

    public override void Write(string? value)
    {
        if (value == null)
        {
            return;
        }

        foreach (var character in value)
        {
            Append(character);
        }

        WritePending();
    }

    public override void Write(char[] buffer, int index, int count)
    {
        for (var i = index; i < index + count; i++)
        {
            Append(buffer[i]);
        }

        WritePending();
    }

    public override void WriteLine(string? value)
    {
        Write((value ?? "") + NewLine);
    }

    private void Append(char value)
    {
        if (_inAnsiEscape)
        {
            _pending.Append(value);
            _inAnsiEscape = false;
            _inAnsiControlSequence = value == '[';
            return;
        }

        if (_inAnsiControlSequence)
        {
            _pending.Append(value);
            if (value is >= '@' and <= '~')
            {
                _inAnsiControlSequence = false;
            }

            return;
        }

        if (value == '\u001b')
        {
            _pending.Append(value);
            _inAnsiEscape = true;
            return;
        }

        if (_atLineStart && !char.IsControl(value))
        {
            _pending.Append(_indent);
            _atLineStart = false;
        }

        _pending.Append(value);

        if (value is '\r' or '\n')
        {
            _atLineStart = true;
        }
        else if (!char.IsControl(value))
        {
            _atLineStart = false;
        }
    }

    private void WritePending()
    {
        if (_pending.Length == 0)
        {
            return;
        }

        _inner.Write(_pending.ToString());
        _pending.Clear();
    }

    public override void Flush()
    {
        _inner.Flush();
    }
}
