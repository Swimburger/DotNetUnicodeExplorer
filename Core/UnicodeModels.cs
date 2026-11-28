using System.Globalization;

namespace DotNetUnicodeExplorer.Core;

public sealed record TextElementInfo(
    int Index,
    int Utf16Start,
    int Utf16Length,
    string Value,
    string Escaped,
    int RuneCount);

public sealed record RuneInfo(
    int Index,
    int Utf16Start,
    int Utf16Length,
    string Value,
    string CodePoint,
    UnicodeCategory Category,
    string Utf8Hex,
    string Escaped);

public sealed record CodeUnitInfo(
    int Index,
    char Value,
    string Display,
    string Hex,
    string Kind,
    string Escaped);

public sealed record EncodedByteInfo(
    int Index,
    byte Value,
    string Hex,
    int Utf16Start,
    int Utf16Length);

public sealed record EncodingInfo(
    string Name,
    int ByteCount,
    string Hex,
    IReadOnlyList<EncodedByteInfo> Bytes);

public sealed record UnicodeWarning(
    string Kind,
    string Message,
    int Utf16Start,
    int Utf16Length);

public sealed record NormalizationInfo(
    string ShortName,
    string Name,
    string Value,
    string Escaped,
    int Utf16Length,
    int RuneCount,
    int Utf8ByteCount,
    bool Changed,
    bool Supported = true,
    string? Note = null);

public sealed record UnicodeAnalysis(
    string Value,
    string Escaped,
    bool IsWellFormedUtf16,
    IReadOnlyList<TextElementInfo> TextElements,
    IReadOnlyList<RuneInfo> Runes,
    IReadOnlyList<CodeUnitInfo> CodeUnits,
    IReadOnlyList<EncodingInfo> Encodings,
    IReadOnlyList<NormalizationInfo> Normalizations,
    IReadOnlyList<UnicodeWarning> Warnings);

public sealed record ParseResult(bool Success, string Value, string? Error)
{
    public static ParseResult Ok(string value) => new(true, value, null);
    public static ParseResult Fail(string error) => new(false, string.Empty, error);
}
