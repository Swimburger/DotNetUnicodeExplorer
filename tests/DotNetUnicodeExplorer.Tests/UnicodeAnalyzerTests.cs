using DotNetUnicodeExplorer.Core;
using Xunit;

namespace DotNetUnicodeExplorer.Tests;

public sealed class UnicodeAnalyzerTests
{
    [Fact]
    public void EmojiUsesOneRuneAndTwoUtf16CodeUnits()
    {
        var result = UnicodeAnalyzer.Analyze("🥰");

        Assert.Equal(2, result.Value.Length);
        Assert.Single(result.TextElements);
        Assert.Single(result.Runes);
        Assert.Equal(2, result.CodeUnits.Count);
        Assert.Equal("U+1F970", result.Runes[0].CodePoint);
        Assert.Equal("F0 9F A5 B0", result.Runes[0].Utf8Hex);
    }

    [Fact]
    public void FamilyEmojiIsOneTextElementAndSeveralRunes()
    {
        var result = UnicodeAnalyzer.Analyze("👨‍👩‍👧‍👦");

        Assert.Single(result.TextElements);
        Assert.Equal(7, result.Runes.Count);
        Assert.Equal(11, result.Value.Length);
    }

    [Fact]
    public void EncodedBytesMapBackToTheirTextElement()
    {
        var result = UnicodeAnalyzer.Analyze("A🥰");
        var utf8 = Assert.Single(result.Encodings, item => item.Name == "UTF-8");

        Assert.Equal(5, utf8.Bytes.Count);
        Assert.Equal(0, utf8.Bytes[0].Utf16Start);
        Assert.All(utf8.Bytes.Skip(1), item =>
        {
            Assert.Equal(1, item.Utf16Start);
            Assert.Equal(2, item.Utf16Length);
        });
    }

    [Theory]
    [InlineData(@"abc\u202Etxt", "Bidirectional control")]
    [InlineData(@"user\u200Bname", "Invisible character")]
    [InlineData(@"before\uD800after", "Invalid UTF-16")]
    public void FlagsInvisibleAndInvalidInput(string escaped, string warningKind)
    {
        var parsed = UnicodeAnalyzer.ParseEscaped(escaped);
        var result = UnicodeAnalyzer.Analyze(parsed.Value);

        Assert.Contains(result.Warnings, item => item.Kind == warningKind);
    }

    [Fact]
    public void MixedLatinAndCyrillicIsOnlyAReviewHint()
    {
        var result = UnicodeAnalyzer.Analyze("paypal раypal");

        var warning = Assert.Single(result.Warnings, item => item.Kind == "Potentially confusable text");
        Assert.Contains("not proof", warning.Message);
    }

    [Fact]
    public void NormalizationShowsEquivalentAccentRepresentations()
    {
        var result = UnicodeAnalyzer.Analyze("e\u0301");
        var nfc = Assert.Single(result.Normalizations, item => item.ShortName == "NFC");

        Assert.Equal("é", nfc.Value);
        Assert.True(nfc.Changed);
        Assert.Equal(1, nfc.Utf16Length);
    }

    [Theory]
    [InlineData(@"hello\nworld", "hello\nworld")]
    [InlineData(@"\u00E9", "é")]
    [InlineData(@"\U0001F970", "🥰")]
    [InlineData(@"\x41", "A")]
    public void ParsesSupportedCSharpEscapes(string escaped, string expected)
    {
        var result = UnicodeAnalyzer.ParseEscaped(escaped);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(@"\u12")]
    [InlineData(@"\U00110000")]
    [InlineData(@"\q")]
    public void RejectsInvalidEscapes(string escaped)
    {
        var result = UnicodeAnalyzer.ParseEscaped(escaped);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.NotEmpty(result.Error);
    }
}
