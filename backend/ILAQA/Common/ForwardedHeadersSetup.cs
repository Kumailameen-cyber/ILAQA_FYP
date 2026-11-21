using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace ILAQA.Common;

// Settings for app.UseForwardedHeaders(). Behind Azure's ingress every
// request arrives from the proxy, so the per-IP rate limits would otherwise
// share one bucket for the whole world.
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Trust ONE hop only: the address the ingress itself appended. Anything
        // a client put earlier in X-Forwarded-For is ignored, so a client
        // cannot fake its address to dodge a rate limit.
        options.ForwardLimit = 1;

        // The ingress address is not fixed, so no allow-list of proxies.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
}
