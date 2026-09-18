namespace Concorde.Api.Errors;

/// <summary>US-08 (FR-08.1) error envelope returned for every non-2xx response.</summary>
public sealed record ErrorResponse(
    string Code,
    string Message,
    IReadOnlyList<ErrorDetail> Details,
    string TraceId);

/// <summary>Field-level failure detail within an error envelope.</summary>
public sealed record ErrorDetail(string? Field, string Code, string Message);

/// <summary>Top-level machine-readable error codes (FR-08.2).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string InternalError = "INTERNAL_ERROR";
}
