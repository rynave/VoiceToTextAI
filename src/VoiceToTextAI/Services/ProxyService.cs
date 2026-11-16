using System.Net;
using VoiceToTextAI.Models;

namespace VoiceToTextAI.Services;

/// <summary>
/// Uses the Windows system proxy (including auto-detect/WPAD and PAC scripts, which .NET resolves
/// through WinHTTP). If no proxy is configured, connections are direct.
/// </summary>
public static class ProxyService
{
    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        UseProxy = true,
        Proxy = HttpClient.DefaultProxy,
        // Lets corporate proxies that use Windows (NTLM/Kerberos) authentication work.
        DefaultProxyCredentials = CredentialCache.DefaultCredentials,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>Human-readable description of how requests to Google will be routed. May block briefly (PAC).</summary>
    public static string Describe()
    {
        try
        {
            var target = new Uri(AppSettings.ApiBase);
            var proxy = HttpClient.DefaultProxy;
            if (proxy.IsBypassed(target)) return "Connection: direct (no proxy)";
            var p = proxy.GetProxy(target);
            return p == null || p == target
                ? "Connection: direct (no proxy)"
                : $"Connection: via system proxy {p.Host}:{p.Port}";
        }
        catch
        {
            return "Connection: system settings";
        }
    }
}
