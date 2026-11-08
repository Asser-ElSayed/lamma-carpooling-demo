using Microsoft.AspNetCore.Diagnostics;

namespace Lamma.Api.Common.ErrorHandling;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var badRequest = exception is BadHttpRequestException;
        var code = badRequest ? "VALIDATION_FAILED" : "UNEXPECTED_ERROR";
        logger.LogWarning("Request failed: {Code}, {ExceptionType}, trace {TraceId}",
            code, exception.GetType().Name, context.TraceIdentifier);
        await ApiResults.Problem(context, badRequest ? 400 : 500, code,
            badRequest ? "The request does not match the documented contract." : "An unexpected error occurred.").ExecuteAsync(context);
        return true;
    }
}
