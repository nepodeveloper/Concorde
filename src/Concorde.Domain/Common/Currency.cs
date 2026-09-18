namespace Concorde.Domain.Common;

/// <summary>
/// Represents an ISO 4217 currency code (e.g., ZAR, USD, EUR).
/// Always stored uppercase. Comparison is case-insensitive.
/// </summary>
public sealed class Currency : IEquatable<Currency>
{
    // Common ISO 4217 codes - not exhaustive, but covers common scenarios
    private static readonly HashSet<string> ValidCurrencyCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN",
        "BAM", "BBD", "BDT", "BGN", "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTC", "BTN", "BWP", "BYN", "BZD",
        "CAD", "CDF", "CHE", "CHF", "CHW", "CLF", "CLP", "CNY", "COP", "COU", "CRC", "CUC", "CUP", "CVE", "CZK",
        "DJF", "DKK", "DOP", "DZD",
        "EGP", "ERN", "ETB", "EUR",
        "FJD", "FKP",
        "GBP", "GEL", "GGP", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD",
        "HKD", "HNL", "HRK", "HTG", "HUF",
        "IDR", "ILS", "IMP", "INR", "IQD", "IRR", "ISK",
        "JEP", "JMD", "JOD", "JPY",
        "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD", "KZT",
        "LAK", "LBP", "LKR", "LRD", "LSL", "LYD",
        "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MXV", "MYR", "MZN",
        "NAD", "NGN", "NIO", "NOK", "NPR", "NZD",
        "OMR",
        "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG",
        "QAR",
        "RON", "RSD", "RUB", "RWF",
        "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SLL", "SOS", "SRD", "SSP", "STN", "SYP", "SZL",
        "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD", "TWD", "TZS",
        "UAH", "UGX", "USD", "USN", "UYI", "UYU", "UYW", "UZS",
        "VES", "VND", "VUV",
        "WST",
        "XAF", "XAG", "XAU", "XBA", "XBB", "XBC", "XBD", "XCD", "XDR", "XOF", "XPD", "XPF", "XPT", "XSU", "XTS", "XUA", "XXX",
        "YER",
        "ZAR", "ZMW", "ZWL"
    };

    public string Code { get; }

    /// <summary>
    /// Returns true when the code is a recognized ISO 4217 alpha-3 currency code.
    /// </summary>
    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ValidCurrencyCodes.Contains(code.Trim());

    public Currency(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Currency code is required.", nameof(code));

        var upperCode = code.ToUpperInvariant().Trim();

        if (upperCode.Length != 3)
            throw new ArgumentException("Currency code must be exactly 3 characters.", nameof(code));

        if (!upperCode.All(char.IsLetter))
            throw new ArgumentException("Currency code must contain only letters.", nameof(code));

        if (!ValidCurrencyCodes.Contains(upperCode))
            throw new ArgumentException($"'{upperCode}' is not a recognized ISO 4217 currency code.", nameof(code));

        Code = upperCode;
    }

    public override bool Equals(object? obj) => Equals(obj as Currency);

    public bool Equals(Currency? other) =>
        other is not null && Code.Equals(other.Code, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => Code.ToUpperInvariant().GetHashCode();

    public override string ToString() => Code;

    // Implicit conversion for convenience
    public static implicit operator string(Currency currency) => currency.Code;
}
