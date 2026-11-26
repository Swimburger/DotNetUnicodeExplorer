using System.Buffers;
using System.Globalization;
using System.Text;

namespace DotNetUnicodeExplorer.Core;

public static class UnicodeAnalyzer
{
    public static UnicodeAnalysis Analyze(string value)
    {
        var runes = AnalyzeRunes(value);
        return new UnicodeAnalysis(
            value,
            Escape(value),
            IsWellFormedUtf16(value),
            AnalyzeTextElements(value),
            runes,
            AnalyzeCodeUnits(value),
            AnalyzeEncodings(value),
            AnalyzeNormalizations(value));
    }

    public static ParseResult ParseEscaped(string input)
    {
        var output = new StringBuilder(input.Length);
        for (var index = 0; index < input.Length; index++)
        {
            var current = input[index];
            if (current != '\\')
            {
                output.Append(current);
                continue;
            }

            if (++index >= input.Length)
                return ParseResult.Fail("The input ends with an incomplete escape. Add a character after the final backslash.");

            current = input[index];
            switch (current)
            {
                case '\\': output.Append('\\'); break;
                case '"': output.Append('"'); break;
                case '\'': output.Append('\''); break;
                case '0': output.Append('\0'); break;
                case 'a': output.Append('\a'); break;
                case 'b': output.Append('\b'); break;
                case 'f': output.Append('\f'); break;
                case 'n': output.Append('\n'); break;
                case 'r': output.Append('\r'); break;
                case 't': output.Append('\t'); break;
                case 'v': output.Append('\v'); break;
                case 'u':
                {
                    var parsed = ParseHex(input, index + 1, 4, "\\u");
                    if (!parsed.Success) return ParseResult.Fail(parsed.Error!);
                    output.Append((char)parsed.Value);
                    index += 4;
                    break;
                }
                case 'U':
                {
                    var parsed = ParseHex(input, index + 1, 8, "\\U");
                    if (!parsed.Success) return ParseResult.Fail(parsed.Error!);
                    if (!Rune.IsValid(parsed.Value))
                        return ParseResult.Fail($"\\U{parsed.Value:X8} isn't a valid Unicode scalar value.");
                    output.Append(new Rune(parsed.Value));
                    index += 8;
                    break;
                }
                case 'x':
                {
                    var digitCount = 0;
                    var scalar = 0;
                    while (digitCount < 4 && index + 1 + digitCount < input.Length &&
                           TryHex(input[index + 1 + digitCount], out var digit))
                    {
                        scalar = (scalar * 16) + digit;
                        digitCount++;
                    }
                    if (digitCount == 0)
                        return ParseResult.Fail("A \\x escape must contain between one and four hexadecimal digits.");
                    output.Append((char)scalar);
                    index += digitCount;
                    break;
                }
                default:
                    return ParseResult.Fail($"\\{current} isn't a supported C# escape. Use \\, \\uXXXX, \\UXXXXXXXX, or a standard character escape.");
            }
        }

        return ParseResult.Ok(output.ToString());
    }

    public static string Escape(string value)
    {
        var output = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            output.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ when char.IsControl(character) || char.IsSurrogate(character) => $"\\u{(int)character:X4}",
                _ => character.ToString()
            });
        }
        return output.ToString();
    }

    private static IReadOnlyList<TextElementInfo> AnalyzeTextElements(string value)
    {
        var starts = StringInfo.ParseCombiningCharacters(value);
        var result = new List<TextElementInfo>(starts.Length);
        for (var index = 0; index < starts.Length; index++)
        {
            var start = starts[index];
            var length = (index + 1 < starts.Length ? starts[index + 1] : value.Length) - start;
            var element = value.Substring(start, length);
            result.Add(new(index, start, length, element, Escape(element), CountRunes(element)));
        }
        return result;
    }

    private static IReadOnlyList<RuneInfo> AnalyzeRunes(string value)
    {
        var result = new List<RuneInfo>();
        var span = value.AsSpan();
        var utf16Start = 0;
        while (!span.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(span, out var rune, out var consumed);
            if (status != OperationStatus.Done)
            {
                var invalid = span[0];
                result.Add(new(result.Count, utf16Start, 1, "�", $"Invalid U+{(int)invalid:X4}", UnicodeCategory.Surrogate, "EF BF BD", Escape(invalid.ToString())));
                span = span[1..];
                utf16Start++;
                continue;
            }

            Span<byte> bytes = new byte[4];
            rune.TryEncodeToUtf8(bytes, out var bytesWritten);
            result.Add(new(
                result.Count,
                utf16Start,
                consumed,
                rune.ToString(),
                $"U+{rune.Value:X4}",
                Rune.GetUnicodeCategory(rune),
                Hex(bytes[..bytesWritten]),
                Escape(rune.ToString())));
            span = span[consumed..];
            utf16Start += consumed;
        }
        return result;
    }

    private static IReadOnlyList<CodeUnitInfo> AnalyzeCodeUnits(string value) => value
        .Select((character, index) => new CodeUnitInfo(
            index,
            character,
            Display(character),
            $"0x{(int)character:X4}",
            char.IsHighSurrogate(character) ? "High surrogate" :
            char.IsLowSurrogate(character) ? "Low surrogate" :
            char.IsControl(character) ? "Control" : "Code unit",
            Escape(character.ToString())))
        .ToArray();

    private static IReadOnlyList<EncodingInfo> AnalyzeEncodings(string value)
    {
        var encodings = new (string Name, Encoding Encoding)[]
        {
            ("UTF-8", new UTF8Encoding(false)),
            ("UTF-16 LE", new UnicodeEncoding(false, false)),
            ("UTF-16 BE", new UnicodeEncoding(true, false)),
            ("UTF-32 LE", new UTF32Encoding(false, false)),
            ("UTF-32 BE", new UTF32Encoding(true, false))
        };
        return encodings.Select(item =>
        {
            var bytes = item.Encoding.GetBytes(value);
            return new EncodingInfo(item.Name, bytes.Length, Hex(bytes));
        }).ToArray();
    }

    private static IReadOnlyList<NormalizationInfo> AnalyzeNormalizations(string value)
    {
        var result = new List<NormalizationInfo>
        {
            AnalyzeNormalization(value, "NFC", "Canonical composition", NormalizationForm.FormC),
            AnalyzeNormalization(value, "NFD", "Canonical decomposition", NormalizationForm.FormD)
        };

        if (OperatingSystem.IsBrowser())
        {
            result.Add(UnsupportedCompatibilityNormalization("NFKC", "Compatibility composition"));
            result.Add(UnsupportedCompatibilityNormalization("NFKD", "Compatibility decomposition"));
            return result;
        }

        // String.Normalize explicitly marks compatibility forms unsupported in browser builds.
        // This guarded path is retained for the reusable core library on supported runtimes.
#pragma warning disable CA1416
        result.Add(AnalyzeNormalization(value, "NFKC", "Compatibility composition", NormalizationForm.FormKC));
        result.Add(AnalyzeNormalization(value, "NFKD", "Compatibility decomposition", NormalizationForm.FormKD));
#pragma warning restore CA1416
        return result;
    }

    private static NormalizationInfo AnalyzeNormalization(string value, string shortName, string name, NormalizationForm form)
    {
        try
        {
            var normalized = value.Normalize(form);
            return new NormalizationInfo(shortName, name, normalized, Escape(normalized), normalized.Length,
                CountRunes(normalized), Encoding.UTF8.GetByteCount(normalized), !string.Equals(value, normalized, StringComparison.Ordinal));
        }
        catch (ArgumentException)
        {
            return new NormalizationInfo(shortName, name, value, "Invalid UTF-16 cannot be normalized", value.Length,
                CountRunes(value), Encoding.UTF8.GetByteCount(value), false, false, "The input contains invalid UTF-16.");
        }
        catch (PlatformNotSupportedException)
        {
            return new NormalizationInfo(shortName, name, string.Empty, "Not available", 0, 0, 0, false, false,
                "This normalization form isn't supported by System.String on the current runtime.");
        }
    }

    private static NormalizationInfo UnsupportedCompatibilityNormalization(string shortName, string name) =>
        new(shortName, name, string.Empty, "Not available", 0, 0, 0, false, false,
            "Compatibility normalization isn't supported by System.String on browser targets.");

    private static bool IsWellFormedUtf16(string value)
    {
        var span = value.AsSpan();
        while (!span.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(span, out _, out var consumed);
            if (status != OperationStatus.Done) return false;
            span = span[consumed..];
        }
        return true;
    }

    private static string Display(char character) => character switch
    {
        ' ' => "␠",
        '\n' => "↵",
        '\r' => "␍",
        '\t' => "⇥",
        '\0' => "␀",
        _ when char.IsControl(character) => "·",
        _ when char.IsSurrogate(character) => "—",
        _ => character.ToString()
    };

    private static string Hex(ReadOnlySpan<byte> bytes) => string.Join(' ', bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));

    private static int CountRunes(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes()) count++;
        return count;
    }

    private static (bool Success, int Value, string? Error) ParseHex(string input, int start, int count, string prefix)
    {
        if (start + count > input.Length)
            return (false, 0, $"A {prefix} escape must contain exactly {count} hexadecimal digits.");
        var value = 0;
        for (var index = 0; index < count; index++)
        {
            if (!TryHex(input[start + index], out var digit))
                return (false, 0, $"'{input[start + index]}' isn't a hexadecimal digit in the {prefix} escape at position {start - 2}.");
            value = (value * 16) + digit;
        }
        return (true, value, null);
    }

    private static bool TryHex(char value, out int digit)
    {
        digit = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1
        };
        return digit >= 0;
    }
}
