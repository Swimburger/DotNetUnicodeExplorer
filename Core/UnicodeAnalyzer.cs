using System.Buffers;
using System.Globalization;
using System.Text;

namespace DotNetUnicodeExplorer.Core;

public static class UnicodeAnalyzer
{
    public static UnicodeAnalysis Analyze(string value)
    {
        var textElements = AnalyzeTextElements(value);
        var runes = AnalyzeRunes(value);
        return new UnicodeAnalysis(
            value,
            Escape(value),
            IsWellFormedUtf16(value),
            textElements,
            runes,
            AnalyzeCodeUnits(value),
            AnalyzeEncodings(value, textElements),
            AnalyzeNormalizations(value),
            AnalyzeWarnings(value, runes));
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

    private static IReadOnlyList<EncodingInfo> AnalyzeEncodings(string value, IReadOnlyList<TextElementInfo> textElements)
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
            var mappedBytes = new List<EncodedByteInfo>(bytes.Length);
            foreach (var element in textElements)
            {
                var elementBytes = item.Encoding.GetBytes(element.Value);
                foreach (var value in elementBytes)
                {
                    mappedBytes.Add(new EncodedByteInfo(
                        mappedBytes.Count,
                        value,
                        value.ToString("X2", CultureInfo.InvariantCulture),
                        element.Utf16Start,
                        element.Utf16Length));
                }
            }
            return new EncodingInfo(item.Name, bytes.Length, Hex(bytes), mappedBytes);
        }).ToArray();
    }

    private static IReadOnlyList<UnicodeWarning> AnalyzeWarnings(string value, IReadOnlyList<RuneInfo> runes)
    {
        var result = new List<UnicodeWarning>();
        foreach (var rune in runes)
        {
            if (rune.CodePoint.StartsWith("Invalid", StringComparison.Ordinal))
            {
                result.Add(new("Invalid UTF-16", "Unpaired surrogate: .NET replacement encoders may substitute this value.", rune.Utf16Start, rune.Utf16Length));
                continue;
            }

            var scalar = rune.Value.EnumerateRunes().First().Value;
            if (IsBidiControl(scalar))
            {
                result.Add(new("Bidirectional control", $"{InvisibleName(scalar)} changes visual text order. Review it carefully in identifiers, paths, and source code.", rune.Utf16Start, rune.Utf16Length));
            }
            else if (rune.Category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                result.Add(new("Invisible character", $"{InvisibleName(scalar)} may not be visible when rendered.", rune.Utf16Start, rune.Utf16Length));
            }

            if (IsNoncharacter(scalar))
            {
                result.Add(new("Unicode noncharacter", $"U+{scalar:X4} is permanently reserved as a noncharacter and should not be used for open interchange.", rune.Utf16Start, rune.Utf16Length));
            }
        }

        var scripts = runes
            .Select(item => item.CodePoint.StartsWith("Invalid", StringComparison.Ordinal) ? null : ScriptOf(item.Value.EnumerateRunes().First().Value))
            .Where(item => item is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (scripts.Length > 1 && scripts.Contains("Latin") && (scripts.Contains("Cyrillic") || scripts.Contains("Greek")))
        {
            result.Add(new("Potentially confusable text",
                $"This value mixes {string.Join(" and ", scripts)} letters. Some glyphs can look alike; this is a review hint, not proof of spoofing.",
                0, value.Length));
        }

        return result;
    }

    public static string InvisibleName(int scalar) => scalar switch
    {
        0x0000 => "NULL (U+0000)",
        0x0009 => "CHARACTER TABULATION (U+0009)",
        0x000A => "LINE FEED (U+000A)",
        0x000D => "CARRIAGE RETURN (U+000D)",
        0x00AD => "SOFT HYPHEN (U+00AD)",
        0x061C => "ARABIC LETTER MARK (U+061C)",
        0x200B => "ZERO WIDTH SPACE (U+200B)",
        0x200C => "ZERO WIDTH NON-JOINER (U+200C)",
        0x200D => "ZERO WIDTH JOINER (U+200D)",
        0x200E => "LEFT-TO-RIGHT MARK (U+200E)",
        0x200F => "RIGHT-TO-LEFT MARK (U+200F)",
        0x202A => "LEFT-TO-RIGHT EMBEDDING (U+202A)",
        0x202B => "RIGHT-TO-LEFT EMBEDDING (U+202B)",
        0x202C => "POP DIRECTIONAL FORMATTING (U+202C)",
        0x202D => "LEFT-TO-RIGHT OVERRIDE (U+202D)",
        0x202E => "RIGHT-TO-LEFT OVERRIDE (U+202E)",
        0x2060 => "WORD JOINER (U+2060)",
        0x2066 => "LEFT-TO-RIGHT ISOLATE (U+2066)",
        0x2067 => "RIGHT-TO-LEFT ISOLATE (U+2067)",
        0x2068 => "FIRST STRONG ISOLATE (U+2068)",
        0x2069 => "POP DIRECTIONAL ISOLATE (U+2069)",
        0xFEFF => "ZERO WIDTH NO-BREAK SPACE / BOM (U+FEFF)",
        _ => $"{(scalar <= 0x1F || scalar == 0x7F ? "CONTROL" : "FORMAT")} U+{scalar:X4}"
    };

    private static bool IsBidiControl(int scalar) => scalar is 0x061C or 0x200E or 0x200F or >= 0x202A and <= 0x202E or >= 0x2066 and <= 0x2069;
    private static bool IsNoncharacter(int scalar) => scalar is >= 0xFDD0 and <= 0xFDEF || (scalar & 0xFFFE) == 0xFFFE;

    private static string? ScriptOf(int scalar) => scalar switch
    {
        >= 0x0041 and <= 0x024F => "Latin",
        >= 0x0370 and <= 0x03FF => "Greek",
        >= 0x0400 and <= 0x052F => "Cyrillic",
        _ => null
    };

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
