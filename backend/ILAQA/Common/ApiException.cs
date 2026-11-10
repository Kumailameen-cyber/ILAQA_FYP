using System.Text.Json.Serialization;

namespace ILAQA.Common;

// Thrown by the feature services when a request cannot be completed.
// GlobalExceptionHandler turns it into the HTTP response, so controllers
// and services never build error responses or write try-catch blocks.
public sealed class ApiException(int statusCode, object? body = null)
    : Exception(body?.ToString())
{
    public int StatusCode { get; } = statusCode;

    // Either an ErrorResponse (sent as JSON) or a plain string (sent as text).
    public object? Body { get; } = body;

    public static ApiException BadRequest(string message) => new(StatusCodes.Status400BadRequest, new ErrorResponse(message));
    public static ApiException Unauthorized(string message) => new(StatusCodes.Status401Unauthorized, new ErrorResponse(message));
    public static ApiException Conflict(string message) => new(StatusCodes.Status409Conflict, new ErrorResponse(message));
    public static ApiException TooManyRequests(string message) => new(StatusCodes.Status429TooManyRequests, new ErrorResponse(message));

    // The runs API answers with plain text; the app matches some of these
    // texts (mobile/services/runSync.ts), so their wording must not change.
    public static ApiException Text(int statusCode, string text) => new(statusCode, text);
}

// The JSON body of every auth and profile error. Optional fields are left
// out of the JSON when they are null.
public sealed record ErrorResponse(string Message)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? Errors { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequiresEmailVerification { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RegistrationExpired { get; init; }
}

public sealed record MessageResponse(string Message);
