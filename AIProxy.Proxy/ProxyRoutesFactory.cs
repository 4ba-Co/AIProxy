using AIProxy.Common;
using Yarp.ReverseProxy.Configuration;

namespace AIProxy.Proxy;

public sealed class ProxyRoutesFactory(ReverseMode mode, string cfToken)
{
    private IReadOnlyList<IReadOnlyDictionary<string, string>> CommonTransforms =>
    [
        new Dictionary<string, string> { { "RequestHeadersCopy", "true" } },
        new Dictionary<string, string> { { "X-Forwarded", "Off" } },
        new Dictionary<string, string> { { "PathPattern", "/{provider}/{**else}" } },
        new Dictionary<string, string>
        {
            { "RequestHeader", "cf-aig-authorization" },
            { "Set", $"Bearer {cfToken}" }
        }
    ];

    private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _commonTransformsWithoutToken =
    [
        new Dictionary<string, string> { { "RequestHeadersCopy", "true" } },
        new Dictionary<string, string> { { "X-Forwarded", "Off" } },
        new Dictionary<string, string> { { "PathPattern", "/{provider}/{**else}" } }
    ];

    public RouteConfig[] Produce()
    {
        return mode switch
        {
            ReverseMode.AmericaGateway =>
            [
                BuildRouteConfig("ai-proxy-endpoint-na", "ai-proxy-na-cf", CommonTransforms, false)
            ],
            ReverseMode.SingaporeGateway =>
            [
                BuildRouteConfig("ai-proxy-endpoint-sg", "ai-proxy-sg-cf", CommonTransforms, false)
            ],
            ReverseMode.GermanyGateway =>
            [
                BuildRouteConfig("ai-proxy-endpoint-de", "ai-proxy-de-cf", CommonTransforms, false)
            ],
            ReverseMode.HongKong2Singapore =>
            [
                BuildRouteConfig("ai-proxy-endpoint-hk", "ai-proxy-sg", _commonTransformsWithoutToken, false)
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null) // never
        };
    }


    private static RouteConfig BuildRouteConfig(string routeId, string clusterId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> transforms, bool enableHeaderMatch,
        string? authorizationPolicy = null)
    {
        return new RouteConfig
        {
            RouteId = routeId,
            ClusterId = clusterId,
            AuthorizationPolicy = authorizationPolicy,
            Match = new RouteMatch
            {
                Path = "/{provider}/{**else}",
                Headers = enableHeaderMatch
                    ?
                    [
                        new RouteHeader
                        {
                            IsCaseSensitive = false,
                            Name = "x-ai-proxy-token",
                            Mode = HeaderMatchMode.Exists
                        }
                    ]
                    : null
            },
            Transforms = transforms
        };
    }
}