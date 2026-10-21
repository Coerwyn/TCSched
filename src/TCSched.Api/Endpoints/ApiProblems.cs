namespace TCSched.Api.Endpoints;

/// <summary>
/// ProblemDetails responses with a machine-readable <c>code</c> the UI maps to messages.
/// </summary>
public static class ApiProblems
{
    public static IResult Validation(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, extensions: Code("VALIDATION"));

    public static IResult NotFound(string what) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"{what} not found.", extensions: Code("NOT_FOUND"));

    public static IResult BadRequest(string code, string title, string? detail = null) =>
        Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: title, detail: detail, extensions: Code(code));

    public static IResult Conflict(string code, string title, string? detail = null) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: title, detail: detail, extensions: Code(code));

    private static Dictionary<string, object?> Code(string code) => new() { ["code"] = code };
}
