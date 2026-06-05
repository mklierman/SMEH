using System.Text;

namespace SMEH.Helpers;

public sealed class IndentedConsoleWriter : TextWriter
{
    private readonly TextWriter _inner;
    private readonly string _indent;
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
        if (_inAnsiEscape)
        {
            _inner.Write(value);
            _inAnsiEscape = false;
            _inAnsiControlSequence = value == '[';
            return;
        }

        if (_inAnsiControlSequence)
        {
            _inner.Write(value);
            if (value is >= '@' and <= '~')
                _inAnsiControlSequence = false;
            return;
        }

        if (value == '\u001b')
        {
            _inner.Write(value);
            _inAnsiEscape = true;
            return;
        }

        if (_atLineStart && !char.IsControl(value))
        {
            _inner.Write(_indent);
            _atLineStart = false;
        }

        _inner.Write(value);

        if (value is '\r' or '\n')
            _atLineStart = true;
        else if (!char.IsControl(value))
            _atLineStart = false;
    }

    public override void Write(string? value)
    {
        if (value == null)
            return;

        foreach (var character in value)
            Write(character);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        for (var i = index; i < index + count; i++)
            Write(buffer[i]);
    }

    public override void Flush()
    {
        _inner.Flush();
    }
}
