using System;
using System.Collections.Generic;
using System.Text;

namespace CodeMinifier.Minification
{
    /// <summary>
    /// Edge-case-aware JavaScript minifier.
    ///
    /// Safety rules baked in:
    ///  - Automatic semicolon insertion (ASI): a line break is kept in the
    ///    output whenever removing it could merge two statements. Keeping a
    ///    newline that was in the source never changes program meaning, so
    ///    semicolon-less code stays valid and "return\n42" keeps returning
    ///    undefined.
    ///  - A division sign followed by a regex literal gets a separating space
    ///    so the output never accidentally forms a "//" line comment.
    ///  - Parentheses of if/while/for/with are tracked so a '/' right after
    ///    the closing ')' is correctly read as a regex literal (its content,
    ///    including spaces, is preserved verbatim).
    /// </summary>
    public sealed class JsMinifier : IMinifier
    {
        public string Name => "JavaScript";

        private static readonly HashSet<string> RegexPrecedingKeywords = new HashSet<string>
        {
            "return", "typeof", "instanceof", "in", "of", "new", "delete",
            "throw", "void", "case", "yield", "await", "else", "keyof",
            "from", "as", "async", "export", "import"
        };

        private static readonly HashSet<string> ControlKeywords = new HashSet<string>
        {
            "if", "while", "for", "with"
        };

        private static readonly string[] MultiCharOperators =
        {
            ">>>=",
            "**=", "&&=", "||=", "??=", "<<=", ">>=", ">>>", "===", "!==", "...",
            "**", "&&", "||", "??", "?.", "=>", "++", "--", "+=", "-=", "*=", "/=",
            "%=", "&=", "|=", "^=", "<=", ">=", "==", "!=", "<<", ">>"
        };

        private string _source = string.Empty;
        private int _position;
        private int _length;

        private string _lastToken = string.Empty;
        private char _lastEmitted = '\0';
        private bool _pendingSpace;
        private bool _pendingNewline;
        private bool _lastParenWasControl;
        private readonly Stack<bool> _parenStack = new Stack<bool>();
        private readonly StringBuilder _output = new StringBuilder();

        public string Minify(string js)
        {
            if (string.IsNullOrEmpty(js))
                return string.Empty;

            if (js[0] == '\uFEFF')
                js = js.Substring(1);

            js = NormalizeLineEndings(js);

            _source = js;
            _position = 0;
            _length = js.Length;
            _lastToken = string.Empty;
            _lastEmitted = '\0';
            _pendingSpace = false;
            _pendingNewline = false;
            _lastParenWasControl = false;
            _parenStack.Clear();
            _output.Clear();

            while (_position < _length)
                ProcessNext();

            return _output.ToString().Trim();
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private void ProcessNext()
        {
            char c = _source[_position];

            // Block comment
            if (c == '/' && Peek(1) == '*')
            {
                bool isLicense = Peek(2) == '!';
                _position += isLicense ? 3 : 2; // skip the '!' too, so it is not duplicated below
                int start = _position;

                while (_position < _length - 1 && !(_source[_position] == '*' && _source[_position + 1] == '/'))
                    _position++;

                string body = _source.Substring(start, _position - start);

                if (_position < _length - 1)
                    _position += 2;
                else
                    _position = _length;

                if (isLicense)
                {
                    Emit("/*!" + body + "*/");
                    Emit('\n');
                }
                else
                {
                    _pendingSpace = true;

                    // A block comment containing a newline acts as a line
                    // terminator for ASI purposes.
                    if (body.IndexOf('\n') >= 0)
                        _pendingNewline = true;
                }

                return;
            }

            // Line comment
            if (c == '/' && Peek(1) == '/')
            {
                _position += 2;
                while (_position < _length && _source[_position] != '\n')
                    _position++;

                _pendingSpace = true;
                return;
            }

            // Template literal
            if (c == '`')
            {
                ReadTemplateLiteral();
                return;
            }

            // String
            if (c == '"' || c == '\'')
            {
                ReadString(c);
                return;
            }

            // Regex literal
            if (c == '/' && IsRegexStart())
            {
                ReadRegex();
                return;
            }

            // Whitespace
            if (IsWhitespace(c))
            {
                while (_position < _length && IsWhitespace(_source[_position]))
                {
                    if (_source[_position] == '\n')
                        _pendingNewline = true;

                    _position++;
                }

                _pendingSpace = true;
                return;
            }

            // Operators
            string op = ReadOperator();
            if (op != null)
            {
                FlushPendingSpace(op[0]);
                Emit(op);
                _lastToken = op;
                return;
            }

            // Identifier or number
            if (IsIdentifierStart(c) || char.IsDigit(c) || (c == '.' && _position + 1 < _length && char.IsDigit(_source[_position + 1])))
            {
                string token = ReadIdentifierOrNumber();

                // A number like ".5" must behave like a digit for spacing and
                // ASI decisions (otherwise "return .5" becomes "return.5").
                char flushChar = token[0] == '.' ? '0' : token[0];
                FlushPendingSpace(flushChar);
                Emit(token);
                _lastToken = token;
                return;
            }

            // Track whether '(' belongs to if/while/for/with so that a '/'
            // right after the matching ')' is correctly read as a regex.
            if (c == '(')
                _parenStack.Push(ControlKeywords.Contains(_lastToken));
            else if (c == ')')
                _lastParenWasControl = _parenStack.Count > 0 && _parenStack.Pop();

            // Single char fallback
            FlushPendingSpace(c);
            Emit(c);
            _lastToken = c.ToString();
            _position++;
        }

        private void FlushPendingSpace(char next)
        {
            if (!_pendingSpace && !_pendingNewline)
                return;

            bool hadNewline = _pendingNewline;
            _pendingSpace = false;
            _pendingNewline = false;

            // Keep the original line break whenever removing it could merge
            // two statements (automatic semicolon insertion). Keeping a
            // newline that was in the source never changes program meaning.
            if (hadNewline && IsAsiHazard(_lastEmitted, next))
            {
                _output.Append('\n');
                _lastEmitted = '\n';
                return;
            }

            if (NeedsSpace(_lastEmitted, next))
                _output.Append(' ');
        }

        private static bool IsAsiHazard(char last, char next)
        {
            bool lastRisky = IsIdentifierPart(last) || last == ')' || last == ']' || last == '}' ||
                             last == '"' || last == '\'' || last == '`' || last == '+' || last == '-';

            if (!lastRisky)
                return false;

            return IsIdentifierPart(next) || next == '(' || next == '[' || next == '{' ||
                   next == '+' || next == '-' || next == '/' || next == '!' || next == '~' ||
                   next == '"' || next == '\'' || next == '`';
        }

        private void Emit(char c)
        {
            _output.Append(c);
            _lastEmitted = c;
        }

        private void Emit(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            _output.Append(value);
            _lastEmitted = value[value.Length - 1];
        }

        private void ReadString(char quote)
        {
            FlushPendingSpace(quote);

            var buffer = new StringBuilder();
            buffer.Append(quote);
            _position++;

            while (_position < _length)
            {
                char c = _source[_position];

                if (c == '\\' && _position + 1 < _length)
                {
                    if (_source[_position + 1] == '\n')
                    {
                        _position += 2;
                        continue;
                    }

                    buffer.Append(c);
                    buffer.Append(_source[_position + 1]);
                    _position += 2;
                    continue;
                }

                buffer.Append(c);
                _position++;

                if (c == quote || c == '\n')
                    break;
            }

            string result = buffer.ToString();
            Emit(result);
            _lastToken = result;
        }

        private void ReadTemplateLiteral()
        {
            FlushPendingSpace('`');

            var buffer = new StringBuilder();
            buffer.Append('`');
            _position++;

            while (_position < _length)
            {
                char c = _source[_position];

                if (c == '\\' && _position + 1 < _length)
                {
                    buffer.Append(c);
                    buffer.Append(_source[_position + 1]);
                    _position += 2;
                    continue;
                }

                if (c == '$' && _position + 1 < _length && _source[_position + 1] == '{')
                {
                    buffer.Append("${");
                    _position += 2;

                    Emit(buffer.ToString());
                    _lastToken = string.Empty;
                    buffer.Clear();

                    int depth = 1;
                    while (_position < _length && depth > 0)
                    {
                        if (_source[_position] == '{')
                        {
                            depth++;
                        }
                        else if (_source[_position] == '}')
                        {
                            depth--;
                            if (depth == 0)
                                break;
                        }

                        ProcessNext();
                    }

                    // Don't let whitespace inside ${ ... } leak past the
                    // closing brace.
                    _pendingSpace = false;
                    _pendingNewline = false;

                    if (_position < _length)
                    {
                        Emit('}');
                        _position++;
                    }

                    continue;
                }

                buffer.Append(c);
                _position++;

                if (c == '`')
                {
                    string tail = buffer.ToString();
                    Emit(tail);
                    _lastToken = tail;
                    return;
                }
            }

            string remaining = buffer.ToString();
            if (remaining.Length > 0)
            {
                Emit(remaining);
                _lastToken = remaining;
            }
        }

        private void ReadRegex()
        {
            FlushPendingSpace('/');

            var buffer = new StringBuilder();
            buffer.Append('/');
            _position++;
            bool inClass = false;

            while (_position < _length)
            {
                char c = _source[_position];

                if (c == '\\' && _position + 1 < _length)
                {
                    buffer.Append(c);
                    buffer.Append(_source[_position + 1]);
                    _position += 2;
                    continue;
                }

                if (c == '[')
                    inClass = true;
                else if (c == ']')
                    inClass = false;
                else if (c == '/' && !inClass)
                {
                    buffer.Append(c);
                    _position++;
                    break;
                }
                else if (c == '\n')
                {
                    break;
                }

                buffer.Append(c);
                _position++;
            }

            while (_position < _length && "gimsuyv".IndexOf(_source[_position]) >= 0)
            {
                buffer.Append(_source[_position]);
                _position++;
            }

            string regex = buffer.ToString();
            Emit(regex);
            _lastToken = regex;
        }

        private string ReadIdentifierOrNumber()
        {
            if (char.IsDigit(_source[_position]) ||
                (_source[_position] == '.' && _position + 1 < _length && char.IsDigit(_source[_position + 1])))
            {
                return ReadNumber();
            }

            int start = _position;
            while (_position < _length && IsIdentifierPart(_source[_position]))
                _position++;

            return _source.Substring(start, _position - start);
        }

        private string ReadNumber()
        {
            int start = _position;

            if (_source[_position] == '0' && _position + 1 < _length)
            {
                char next = char.ToLowerInvariant(_source[_position + 1]);
                if (next == 'x' || next == 'b' || next == 'o')
                {
                    _position += 2;
                    while (_position < _length && (IsHexDigit(_source[_position]) || _source[_position] == '_'))
                        _position++;

                    if (_position < _length && _source[_position] == 'n')
                        _position++;

                    return _source.Substring(start, _position - start);
                }
            }

            while (_position < _length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
                _position++;

            if (_position < _length && _source[_position] == '.')
            {
                _position++;
                while (_position < _length && (char.IsDigit(_source[_position]) || _source[_position] == '_'))
                    _position++;
            }

            if (_position < _length && char.ToLowerInvariant(_source[_position]) == 'e')
            {
                _position++;
                if (_position < _length && (_source[_position] == '+' || _source[_position] == '-'))
                    _position++;

                while (_position < _length && char.IsDigit(_source[_position]))
                    _position++;
            }

            if (_position < _length && _source[_position] == 'n')
                _position++;

            return _source.Substring(start, _position - start);
        }

        private string ReadOperator()
        {
            foreach (string op in MultiCharOperators)
            {
                if (_position + op.Length > _length)
                    continue;

                bool matched = true;
                for (int i = 0; i < op.Length; i++)
                {
                    if (_source[_position + i] != op[i])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    _position += op.Length;
                    return op;
                }
            }

            return null;
        }

        private bool IsRegexStart()
        {
            char last = _lastEmitted;

            if (last == '\0')
                return true;

            if (last == ')')
                return _lastParenWasControl;

            if (last == ']')
                return RegexPrecedingKeywords.Contains(_lastToken);

            if (IsIdentifierPart(last) && !char.IsDigit(last))
                return RegexPrecedingKeywords.Contains(_lastToken);

            if (char.IsDigit(last))
                return false;

            if (last == '"' || last == '\'' || last == '`')
                return false;

            return true;
        }

        private static bool NeedsSpace(char last, char next)
        {
            if (last == '\0')
                return false;

            bool lastIdentifierLike = IsIdentifierPart(last) || last == '$';
            bool nextIdentifierLike = IsIdentifierPart(next) || next == '$';

            if (lastIdentifierLike && nextIdentifierLike)
                return true;

            if (last == '+' && next == '+')
                return true;

            if (last == '-' && next == '-')
                return true;

            // "a / /re/" must never fuse into a "//" line comment.
            if (last == '/' && next == '/')
                return true;

            if (last == '!' && next == '=')
                return false;

            if (last == '/' && next == '=')
                return false;

            if (lastIdentifierLike && next == '/')
                return true;

            return false;
        }

        private char Peek(int offset)
        {
            return _position + offset < _length ? _source[_position + offset] : '\0';
        }

        private static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\f' || c == '\v';
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_' || c == '$' || c > '\u007F';
        }

        private static bool IsIdentifierPart(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c > '\u007F';
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'a' && c <= 'f') ||
                   (c >= 'A' && c <= 'F');
        }
    }
}
