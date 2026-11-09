using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeMinifier.Minification
{
    /// <summary>
    /// Single-pass, edge-case-aware CSS minifier.
    ///
    /// Safety rules baked in:
    ///  - No "//" line-comment stripping (CSS has no line comments; unquoted
    ///    URLs such as  --api: https://x.com  contain "//").
    ///  - A space before ':' in a selector is a descendant combinator and is
    ///    preserved (a :hover is not the same as a:hover).
    ///  - Value optimizations (0px → 0, #AABBCC → #abc, 0.5 → .5) never touch
    ///    selector text, strings, url(...), math functions, or custom
    ///    property (--name: ...) values.
    ///  - Zero-unit stripping is limited to length units; time (0s), angle
    ///    (0deg), fraction (0fr) and percent values keep their units because
    ///    stripping them produces invalid CSS.
    /// </summary>
    public sealed class CssMinifier : IMinifier
    {
        public string Name => "CSS";

        private static readonly string[] MathFunctions =
        {
            "calc(", "min(", "max(", "clamp(", "env(", "var("
        };

        private static readonly HashSet<string> SpacedAtRules = new HashSet<string>
        {
            "@charset", "@import", "@namespace", "@media", "@supports",
            "@keyframes", "@font-face", "@page", "@layer", "@container"
        };

        // Length units only — never s, ms, deg, rad, turn, fr or %.
        private static readonly Regex ZeroUnitRegex = new Regex(
            @"\b0+(px|em|rem|vw|vh|vmin|vmax|cm|mm|in|pt|pc|ex|ch)\b",
            RegexOptions.Compiled);

        private static readonly Regex LeadingZeroDecimalRegex = new Regex(
            @"(?<![0-9])\b0+(\.[0-9]+)(?!(px|em|rem|%|vw|vh|vmin|vmax|cm|mm|in|pt|pc|ex|ch|fr|deg|rad|turn|ms|s)[a-zA-Z])",
            RegexOptions.Compiled);

        private static readonly Regex ShortHexColorRegex = new Regex(
            @"#([0-9a-fA-F])\1([0-9a-fA-F])\2([0-9a-fA-F])\3\b",
            RegexOptions.Compiled);

        private static readonly Regex AnyHexColorRegex = new Regex(
            @"#[0-9a-fA-F]{3,8}\b",
            RegexOptions.Compiled);

        public string Minify(string css)
        {
            if (string.IsNullOrEmpty(css))
                return string.Empty;

            if (css.Length > 0 && css[0] == '\uFEFF')
                css = css.Substring(1);

            css = NormalizeLineEndings(css);

            var output = new StringBuilder(css.Length);
            int index = 0;
            int length = css.Length;
            bool lastColonWasSelector = false;

            while (index < length)
            {
                char current = css[index];

                // Preserve /*! ... */ license comments
                if (current == '/' && Peek(css, index + 1) == '*' && Peek(css, index + 2) == '!')
                {
                    int end = css.IndexOf("*/", index + 3, StringComparison.Ordinal);
                    if (end < 0)
                        end = length - 2;

                    output.Append(css, index, end + 2 - index);
                    output.Append('\n');
                    index = end + 2;
                    continue;
                }

                // Remove /* ... */ comments (a comment is a token separator,
                // so a space may be needed where it was removed)
                if (current == '/' && Peek(css, index + 1) == '*')
                {
                    int end = css.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    index = end < 0 ? length : end + 2;

                    if (output.Length > 0 && !IsStructural(output[output.Length - 1]))
                    {
                        int lookAhead = index;
                        while (lookAhead < length && IsWhitespace(css[lookAhead]))
                            lookAhead++;

                        if (lookAhead < length && !IsStructural(css[lookAhead]))
                            output.Append(' ');
                    }

                    continue;
                }

                // Preserve strings
                if (current == '\'' || current == '"')
                {
                    index = CopyString(css, index, length, output);
                    continue;
                }

                // Preserve url(...)
                if (StartsWithCI(css, index, "url("))
                {
                    index = CopyUrl(css, index, length, output);
                    continue;
                }

                // Minify calc()/min()/max()/clamp()/env()/var() interiors
                if (IsCalcLike(css, index))
                {
                    index = MinifyMathFunction(css, index, length, output);
                    continue;
                }

                // At-rules
                if (current == '@')
                {
                    index = HandleAtRule(css, index, length, output);
                    continue;
                }

                // Collapse whitespace
                if (IsWhitespace(current))
                {
                    int wsEnd = index + 1;
                    while (wsEnd < length && IsWhitespace(css[wsEnd]))
                        wsEnd++;

                    char prev = output.Length > 0 ? output[output.Length - 1] : '\0';
                    char next = wsEnd < length ? css[wsEnd] : '\0';

                    bool nextStructural = IsStructural(next);

                    // A space before ':' in a selector is a descendant
                    // combinator (a :hover ≠ a:hover) — keep it.
                    if (next == ':' && IsSelectorContext(css, wsEnd + 1))
                        nextStructural = false;

                    bool prevStructural = IsStructural(prev);

                    // Keep "(min-width : 600px)" symmetric.
                    if (prev == ':' && lastColonWasSelector)
                        prevStructural = false;

                    if (prev != '\0' && next != '\0' && !prevStructural && prev != '\n' && !nextStructural)
                        output.Append(' ');

                    index = wsEnd;
                    continue;
                }

                // A selector colon is a plain character, not a structural one.
                if (current == ':' && IsSelectorContext(css, index + 1))
                {
                    output.Append(':');
                    lastColonWasSelector = true;
                    index++;
                    continue;
                }

                if (current == ':')
                    lastColonWasSelector = false;

                // Structural characters absorb surrounding spaces
                if (IsStructural(current))
                {
                    if (output.Length > 0 && output[output.Length - 1] == ' ')
                        output.Remove(output.Length - 1, 1);

                    output.Append(current);
                    index++;

                    while (index < length && css[index] == ' ')
                        index++;

                    continue;
                }

                // !important normalization
                if (current == '!' && StartsWithCI(css, index, "!important"))
                {
                    if (output.Length > 0 && output[output.Length - 1] == ' ')
                        output.Remove(output.Length - 1, 1);

                    output.Append("!important");
                    index += 10;
                    continue;
                }

                output.Append(current);
                index++;
            }

            return PostOptimize(output.ToString()).Trim();
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// True when, scanning forward from <paramref name="index"/>, a '{'
        /// appears before any ';' or '}' — i.e. the current position belongs
        /// to a selector, not to a declaration.
        /// </summary>
        private static bool IsSelectorContext(string css, int index)
        {
            int length = css.Length;

            while (index < length)
            {
                char c = css[index];

                if (c == '\'' || c == '"')
                {
                    index = SkipString(css, index, length);
                    continue;
                }

                if (c == '/' && index + 1 < length && css[index + 1] == '*')
                {
                    int end = css.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    index = end < 0 ? length : end + 2;
                    continue;
                }

                if (StartsWithCI(css, index, "url("))
                {
                    index = SkipUrl(css, index, length);
                    continue;
                }

                if (c == '{')
                    return true;

                if (c == ';' || c == '}')
                    return false;

                index++;
            }

            return false;
        }

        private static int CopyString(string css, int index, int length, StringBuilder output)
        {
            char quote = css[index];
            output.Append(quote);
            index++;

            while (index < length)
            {
                char c = css[index];

                if (c == '\\' && index + 1 < length)
                {
                    output.Append(c);
                    output.Append(css[index + 1]);
                    index += 2;
                    continue;
                }

                output.Append(c);
                index++;

                if (c == quote)
                    break;
            }

            return index;
        }

        private static int CopyUrl(string css, int index, int length, StringBuilder output)
        {
            output.Append(css, index, 4); // "url("
            index += 4;

            while (index < length && IsWhitespace(css[index]))
                index++;

            if (index < length && (css[index] == '\'' || css[index] == '"'))
            {
                index = CopyString(css, index, length, output);

                while (index < length && IsWhitespace(css[index]))
                    index++;

                if (index < length && css[index] == ')')
                {
                    output.Append(')');
                    index++;
                }
            }
            else
            {
                while (index < length && css[index] != ')')
                {
                    output.Append(css[index]);
                    index++;
                }

                if (index < length)
                {
                    output.Append(')');
                    index++;
                }
            }

            return index;
        }

        private static bool IsCalcLike(string css, int index)
        {
            foreach (string function in MathFunctions)
            {
                if (StartsWithCI(css, index, function))
                    return true;
            }

            return false;
        }

        private static int MinifyMathFunction(string css, int start, int length, StringBuilder output)
        {
            int parenPos = css.IndexOf('(', start);
            if (parenPos < 0)
            {
                output.Append(css[start]);
                return start + 1;
            }

            string functionName = css.Substring(start, parenPos - start + 1);
            output.Append(functionName.ToLowerInvariant());

            int index = parenPos + 1;
            int depth = 1;
            var inner = new StringBuilder();

            while (index < length && depth > 0)
            {
                char c = css[index];

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        index++;
                        break;
                    }
                }

                if (c == '\'' || c == '"')
                {
                    index = CopyString(css, index, length, inner);
                    continue;
                }

                inner.Append(c);
                index++;
            }

            output.Append(MinifyMathInner(inner.ToString()));
            output.Append(')');
            return index;
        }

        private static string MinifyMathInner(string inner)
        {
            var output = new StringBuilder(inner.Length);
            int index = 0;
            int length = inner.Length;

            while (index < length)
            {
                char c = inner[index];

                if (IsCalcLike(inner, index))
                {
                    var nested = new StringBuilder();
                    int after = MinifyMathFunction(inner, index, length, nested);
                    output.Append(nested);
                    index = after;
                    continue;
                }

                if (IsWhitespace(c))
                {
                    int wsEnd = index + 1;
                    while (wsEnd < length && IsWhitespace(inner[wsEnd]))
                        wsEnd++;

                    char prev = output.Length > 0 ? output[output.Length - 1] : '\0';
                    char next = wsEnd < length ? inner[wsEnd] : '\0';

                    bool prevBoundary = prev == '(' || prev == '\0';
                    bool nextBoundary = next == ')' || next == '\0';

                    if (!prevBoundary && !nextBoundary)
                    {
                        if (next == '+' || next == '-' || prev == '+' || prev == '-')
                        {
                            // calc() requires spaces around + and -
                            output.Append(' ');
                        }
                        else if (next == '*' || next == '/' || prev == '*' || prev == '/')
                        {
                            // no space needed around * and /
                        }
                        else
                        {
                            output.Append(' ');
                        }
                    }

                    index = wsEnd;
                    continue;
                }

                output.Append(c);
                index++;
            }

            return output.ToString().Trim();
        }

        private static int HandleAtRule(string css, int index, int length, StringBuilder output)
        {
            int start = index;
            index++;

            while (index < length && (char.IsLetterOrDigit(css[index]) || css[index] == '-'))
                index++;

            string keyword = css.Substring(start, index - start).ToLowerInvariant();
            output.Append(keyword);

            if (SpacedAtRules.Contains(keyword))
            {
                while (index < length && IsWhitespace(css[index]))
                    index++;

                if (index < length && css[index] != '{' && css[index] != ';')
                    output.Append(' ');
            }

            return index;
        }

        private static string PostOptimize(string css)
        {
            css = RemoveTrailingSemicolons(css);
            css = ApplyValueTransform(css, ZeroUnitRegex, _ => "0");
            css = ApplyValueTransform(css, LeadingZeroDecimalRegex, m => m.Groups[1].Value);
            css = ApplyValueTransform(css, ShortHexColorRegex,
                m => "#" + m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value);
            css = ApplyValueTransform(css, AnyHexColorRegex, m => m.Value.ToLowerInvariant());
            return css;
        }

        /// <summary>
        /// Runs a value-level optimization, skipping protected ranges
        /// (strings, url(), math functions, custom properties) and selector
        /// text (so ID selectors like #Abc are never rewritten).
        /// </summary>
        private static string ApplyValueTransform(string css, Regex pattern, MatchEvaluator evaluator)
        {
            List<(int Start, int End)> ranges = FindProtectedRanges(css);

            return pattern.Replace(css, match =>
            {
                if (IsProtectedPosition(ranges, match.Index))
                    return match.Value;

                if (IsSelectorContext(css, match.Index + match.Length))
                    return match.Value;

                return evaluator(match);
            });
        }

        private static List<(int Start, int End)> FindProtectedRanges(string css)
        {
            var ranges = new List<(int Start, int End)>();
            int index = 0;
            int length = css.Length;
            char prevSignificant = '\0';

            while (index < length)
            {
                char c = css[index];

                if (c == '\'' || c == '"')
                {
                    int end = SkipString(css, index, length);
                    ranges.Add((index, end));
                    index = end;
                    prevSignificant = c;
                    continue;
                }

                if (StartsWithCI(css, index, "url("))
                {
                    int end = SkipUrl(css, index, length);
                    ranges.Add((index, end));
                    index = end;
                    prevSignificant = ')';
                    continue;
                }

                if (IsCalcLike(css, index))
                {
                    int end = SkipMathFunction(css, index, length);
                    ranges.Add((index, end));
                    index = end;
                    prevSignificant = ')';
                    continue;
                }

                // Custom property declarations (--name: value) keep their
                // value verbatim: the value is an arbitrary token stream and
                // e.g. stripping "0px" to "0" breaks calc(100% - var(--name)).
                if (c == '-' && index + 1 < length && css[index + 1] == '-' &&
                    (prevSignificant == '{' || prevSignificant == ';' || prevSignificant == '\0'))
                {
                    int end = index;
                    int depth = 0;

                    while (end < length)
                    {
                        char ch = css[end];

                        if (ch == '\'' || ch == '"')
                        {
                            end = SkipString(css, end, length);
                            continue;
                        }

                        if (ch == '(')
                        {
                            depth++;
                        }
                        else if (ch == ')')
                        {
                            if (depth > 0)
                                depth--;
                        }
                        else if ((ch == ';' || ch == '}') && depth == 0)
                        {
                            break;
                        }

                        end++;
                    }

                    ranges.Add((index, end));
                    index = end;
                    continue;
                }

                if (!IsWhitespace(c))
                    prevSignificant = c;

                index++;
            }

            return ranges;
        }

        private static int SkipMathFunction(string css, int index, int length)
        {
            int paren = css.IndexOf('(', index);
            if (paren < 0)
                return index + 1;

            index = paren + 1;
            int depth = 1;

            while (index < length && depth > 0)
            {
                char c = css[index];

                if (c == '\'' || c == '"')
                {
                    index = SkipString(css, index, length);
                    continue;
                }

                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;

                index++;
            }

            return index;
        }

        private static bool IsProtectedPosition(List<(int Start, int End)> ranges, int position)
        {
            foreach (var range in ranges)
            {
                if (position >= range.Start && position < range.End)
                    return true;

                if (range.Start > position)
                    break;
            }

            return false;
        }

        /// <summary>Removes ";" before "}" without touching string contents.</summary>
        private static string RemoveTrailingSemicolons(string css)
        {
            List<(int Start, int End)> ranges = FindProtectedRanges(css);
            var output = new StringBuilder(css.Length);
            int i = 0;

            while (i < css.Length)
            {
                if (css[i] == ';' && !IsProtectedPosition(ranges, i))
                {
                    int j = i;
                    while (j < css.Length && css[j] == ';')
                        j++;

                    if (j < css.Length && css[j] == '}')
                    {
                        i = j;
                        continue;
                    }
                }

                output.Append(css[i]);
                i++;
            }

            return output.ToString();
        }

        private static int SkipString(string css, int index, int length)
        {
            char quote = css[index];
            index++;

            while (index < length)
            {
                if (css[index] == '\\')
                {
                    index = Math.Min(index + 2, length); // clamp: never overshoot on a trailing backslash
                    continue;
                }

                if (css[index] == quote)
                {
                    index++;
                    break;
                }

                index++;
            }

            return index;
        }

        private static int SkipUrl(string css, int index, int length)
        {
            index += 4; // url(

            while (index < length && IsWhitespace(css[index]))
                index++;

            if (index < length && (css[index] == '\'' || css[index] == '"'))
            {
                index = SkipString(css, index, length);
            }
            else
            {
                while (index < length && css[index] != ')')
                    index++;
            }

            if (index < length)
                index++;

            return index;
        }

        private static char Peek(string value, int index)
        {
            return index < value.Length ? value[index] : '\0';
        }

        private static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f';
        }

        private static bool IsStructural(char c)
        {
            return c == '{' || c == '}' || c == ';' || c == ':' || c == ',' || c == '>' || c == '~';
        }

        private static bool StartsWithCI(string value, int index, string prefix)
        {
            if (index + prefix.Length > value.Length)
                return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (char.ToLowerInvariant(value[index + i]) != char.ToLowerInvariant(prefix[i]))
                    return false;
            }

            return true;
        }
    }
}
