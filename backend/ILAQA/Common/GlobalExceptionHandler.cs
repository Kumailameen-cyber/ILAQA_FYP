using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace ILAQA.Common;

// One place that turns exceptions into HTTP responses (registered in Program.cs).
// - ApiException: the expected errors, written exactly as the service chose.
// - Anything else: logged, and answered with a generic 500.
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ApiException apiError)
        {
            logger.LogError(exception, "Unhandled error for {Method} {Path}.",
                httpContext.Request.Method, httpContext.Request.Path);

            return false; // Let the default handler write a 500 problem response.
        }

        // ObjectResult uses the same formatters as a controller would:
        // records become JSON, strings become text/plain.
        var result = new ObjectResult(apiError.Body) { StatusCode = apiError.StatusCode };
        var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());

        await result.ExecuteResultAsync(actionContext);
        return true;
    }
}
