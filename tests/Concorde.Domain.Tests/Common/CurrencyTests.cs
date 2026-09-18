namespace Concorde.Domain.Tests.Common;

using Concorde.Domain.Common;
using Xunit;

public class CurrencyTests
{
    [Theory]
    [InlineData("USD")]
    [InlineData("ZAR")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void Constructor_WithValidCurrencyCode_Succeeds(string code)
    {
        var currency = new Currency(code);
        Assert.NotNull(currency);
        Assert.Equal(code.ToUpperInvariant(), currency.Code);
    }

    [Fact]
    public void Constructor_WithNullCode_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency(null!));
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithEmptyCode_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency(""));
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceOnlyCode_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency("   "));
        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void Constructor_WithTwoCharacterCode_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency("US"));
        Assert.Contains("exactly 3 characters", ex.Message);
    }

    [Fact]
    public void Constructor_WithFourCharacterCode_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency("USDA"));
        Assert.Contains("exactly 3 characters", ex.Message);
    }

    [Fact]
    public void Constructor_WithNumericCharacters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency("US1"));
        Assert.Contains("only letters", ex.Message);
    }

    [Fact]
    public void Constructor_WithSpecialCharacters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency("US$"));
        Assert.Contains("only letters", ex.Message);
    }

    [Fact]
    public void Constructor_WithInvalidCurrencyCode_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Currency("ABC"));
        Assert.Contains("not a recognized ISO 4217", ex.Message);
    }

    [Fact]
    public void Constructor_WithLowercaseCode_ConvertsToUppercase()
    {
        var currency = new Currency("usd");
        Assert.Equal("USD", currency.Code);
    }

    [Fact]
    public void Constructor_WithMixedCaseCode_ConvertsToUppercase()
    {
        var currency = new Currency("UsD");
        Assert.Equal("USD", currency.Code);
    }

    [Fact]
    public void Constructor_WithWhitespaceAroundCode_IsTrimmed()
    {
        var currency = new Currency("  USD  ");
        Assert.Equal("USD", currency.Code);
    }

    [Fact]
    public void Equals_WithSameCode_ReturnsTrue()
    {
        var currency1 = new Currency("USD");
        var currency2 = new Currency("USD");
        
        Assert.Equal(currency1, currency2);
    }

    [Fact]
    public void Equals_WithDifferentCode_ReturnsFalse()
    {
        var currency1 = new Currency("USD");
        var currency2 = new Currency("EUR");
        
        Assert.NotEqual(currency1, currency2);
    }

    [Fact]
    public void Equals_WithDifferentCaseButSameCode_ReturnsTrue()
    {
        var currency1 = new Currency("USD");
        var currency2 = new Currency("usd");
        
        Assert.Equal(currency1, currency2);
    }

    [Fact]
    public void ImplicitConversion_ToCurrencyCode_Succeeds()
    {
        var currency = new Currency("USD");
        string code = currency;
        
        Assert.Equal("USD", code);
    }

    [Fact]
    public void ToString_ReturnsCurrencyCode()
    {
        var currency = new Currency("ZAR");
        Assert.Equal("ZAR", currency.ToString());
    }
}
