using System.Net;
using ILAQA.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ILAQA.Tests;

// Behind Azure's ingress every request reaches the API from the proxy's
// address. The per-IP rate limits (login, public endpoints) need the real
// client address, and only the hop the ingress itself appends may be trusted,
// so a client cannot send its own X-Forwarded-For to dodge a limit.
// Uses the framework's ForwardedHeadersMiddleware with the same options as
// Program.cs.
public class ForwardedHeadersTests
{
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.7");

    private static async Task<HttpContext> Send(Action<HttpRequest> setUp)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options);

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = Proxy;
        setUp(context.Request);

        await middleware.Invoke(context);
        return context;
    }

    [Fact]
    public async Task ClientAddress_IsTheLastHop_SoASpoofedFirstEntryIsIgnored()
    {
        var context = await Send(r => r.Headers["X-Forwarded-For"] = "1.2.3.4, 5.6.7.8");

        Assert.Equal(IPAddress.Parse("5.6.7.8"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ASingleForwardedAddress_IsUsed()
    {
        var context = await Send(r => r.Headers["X-Forwarded-For"] = "203.0.113.9");

        Assert.Equal(IPAddress.Parse("203.0.113.9"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task WithoutTheHeader_TheConnectionAddressIsKept()
    {
        var context = await Send(_ => { });

        Assert.Equal(Proxy, context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task ForwardedProtoHttps_MakesTheRequestHttps()
    {
        var context = await Send(r =>
        {
            r.Scheme = "http";
            r.Headers["X-Forwarded-Proto"] = "https";
        });

        Assert.True(context.Request.IsHttps);
    }

    [Fact]
    public async Task ForwardedHost_IsNotTrusted()
    {
        var context = await Send(r =>
        {
            r.Host = new HostString("api.example.com");
            r.Headers["X-Forwarded-Host"] = "evil.example.net";
        });

        Assert.Equal("api.example.com", context.Request.Host.Value);
    }
}
