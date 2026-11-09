using System.Text;
using ILAQA.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ILAQA.Tests;

// The app reads these error bodies, so their format must not change.
public class GlobalExceptionHandlerTests
{
    private static async Task<(bool Handled, HttpContext Context, string Body)> Handle(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddControllers().Services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
            .TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        return (handled, context, await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync());
    }

    [Fact]
    public async Task MessageError_ShouldBeCamelCaseJsonWithoutEmptyFields()
    {
        var (handled, context, body) = await Handle(ApiException.Conflict("This email is already registered."));

        Assert.True(handled);
        Assert.Equal(409, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        Assert.Equal("{\"message\":\"This email is already registered.\"}", body);
    }

    [Fact]
    public async Task ErrorWithExtraFields_ShouldIncludeThem()
    {
        var error = new ApiException(403,
            new ErrorResponse("Please verify your email before logging in.") { RequiresEmailVerification = true, Email = "a@b.com" });

        var (_, context, body) = await Handle(error);

        Assert.Equal(403, context.Response.StatusCode);
        Assert.Contains("\"requiresEmailVerification\":true", body);
        Assert.Contains("\"email\":\"a@b.com\"", body);
    }

    [Fact]
    public async Task RunsTextError_ShouldBePlainTextTheAppCanMatch()
    {
        var (_, context, body) = await Handle(
            ApiException.Text(409, "GPS points cannot be uploaded to this run."));

        Assert.Equal(409, context.Response.StatusCode);
        Assert.StartsWith("text/plain", context.Response.ContentType);
        Assert.Contains("cannot be uploaded to this run", body); // RUN_CLOSED_TEXT in mobile/services/runSync.ts
    }

    [Fact]
    public async Task UnexpectedError_ShouldBeLeftToTheDefault500Handler()
    {
        var (handled, _, _) = await Handle(new InvalidOperationException("boom"));

        Assert.False(handled);
    }
}
