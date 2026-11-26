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
