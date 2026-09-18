namespace Concorde.Application.Validation;

/// <summary>
/// A single field-level validation failure (US-08 envelope detail entry).
/// </summary>
public sealed record ValidationError(string? Field, string Code, string Message);

/// <summary>
/// Detail codes used in the US-08 error envelope.
/// </summary>
public static class ValidationErrorCodes
{
    public const string Required = "REQUIRED";
    public const string TooLong = "TOO_LONG";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string InvalidQuantity = "INVALID_QUANTITY";
    public const string InvalidPrice = "INVALID_PRICE";
    public const string InvalidCurrency = "INVALID_CURRENCY";
    public const string InvalidStatus = "INVALID_STATUS";
}

/// <summary>
/// Thrown when a request fails validation. Carries all failures (FR-03.6).
/// Maps to 400 VALIDATION_FAILED.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
