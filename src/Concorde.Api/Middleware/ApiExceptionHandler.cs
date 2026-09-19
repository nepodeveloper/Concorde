namespace Concorde.Api.Middleware;

using System.Diagnostics;
using Concorde.Api.Errors;
using Concorde.Application.Common;
using Concorde.Application.Validation;
using Microsoft.AspNetCore.Diagnostics;

/// <summary>
/// Maps application exceptions to the US-08 error envelope and status codes (FR-08.2).
/// Unexpected exceptions become 500 INTERNAL_ERROR without leaking details (FR-08.4).
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, envelope) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                new ErrorResponse(
                    ErrorCodes.ValidationFailed,
                    "One or more validation errors occurred.",
                    validation.Errors.Select(e => new ErrorDetail(e.Field, e.Code, e.Message)).ToList(),
                    traceId)),

            OrderNotFoundException => (
                StatusCodes.Status404NotFound,
                new ErrorResponse(
                    ErrorCodes.OrderNotFound,
                    "The requested order was not found.",
                    [],
                    traceId)),

            InvalidStatusTransitionException transition => (
                StatusCodes.Status409Conflict,
                new ErrorResponse(
                    ErrorCodes.InvalidStatusTransition,
                    transition.Message,
                    [],
                    traceId)),

            OrderNotEditableException notEditable => (
                StatusCodes.Status409Conflict,
                new ErrorResponse(
                    ErrorCodes.OrderNotEditable,
                    notEditable.Message,
                    [],
                    traceId)),

            _ => (0, null!),
        };

        if (statusCode == 0)
        {
            _logger.LogError(exception, "Unhandled exception for {Path} (trace {TraceId})",
                httpContext.Request.Path, traceId);

            statusCode = StatusCodes.Status500InternalServerError;
            envelope = new ErrorResponse(
                ErrorCodes.InternalError,
                "An unexpected error occurred.",
                [],
                traceId);
        }
        else
        {
            _logger.LogWarning("Request rejected with {Code}: {Message} (trace {TraceId})",
                envelope.Code, envelope.Message, traceId);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(envelope, cancellationToken);
        return true;
    }
}
