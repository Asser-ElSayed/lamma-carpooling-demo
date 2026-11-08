using System.Diagnostics;

namespace Lamma.Api.Common.ErrorHandling;

public sealed record ApiError(int Status, string Code, string Detail);

public sealed record ApiResult<T>(T? Value, ApiError? Error = null, bool Replay = false)
{
    public static ApiResult<T> Fail(int status, string code, string detail) => new(default, new(status, code, detail));
}

public static class ApiResults
{
    public static IResult Problem(HttpContext context, int status, string code, string detail) =>
        Results.Problem(statusCode: status, title: code, detail: detail,
            type: $"https://www.rfc-editor.org/rfc/rfc9110#section-15.{(status < 500 ? 5 : 6)}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
            });

    public static IResult ToHttp<T>(this ApiResult<T> result, HttpContext context, int successStatus = 200) =>
        result.Error is { } error
            ? Problem(context, error.Status, error.Code, error.Detail)
            : Results.Json(result.Value, statusCode: result.Replay ? 200 : successStatus);
}
