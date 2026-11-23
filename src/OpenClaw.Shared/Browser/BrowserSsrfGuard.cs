using System;
using System.Net;
using System.Net.Sockets;

namespace OpenClaw.Shared.Browser;

/// <summary>
/// Server-Side Request Forgery (SSRF) and egress protection guard for browser automation.
/// Prevents agent browser access to local loopback, cloud metadata endpoints, and private intranet networks.
/// </summary>
public static class BrowserSsrfGuard
{
    private static readonly string[] CloudMetadataHosts =
    [
        "169.254.169.254",
        "metadata.google.internal",
        "100.100.100.200"
    ];

    public static (bool IsAllowed, string? Reason) CheckUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return (false, "URL cannot be empty.");

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
            return (false, "Invalid URL format.");

        // Scheme check
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return (false, $"Disallowed scheme '{uri.Scheme}'. Only HTTP and HTTPS are permitted.");

        var host = uri.Host;

        // Named loopback checks
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Access to localhost or local domains is prohibited.");
        }

        // Known metadata service hostnames
        foreach (var meta in CloudMetadataHosts)
        {
            if (host.Equals(meta, StringComparison.OrdinalIgnoreCase))
                return (false, "Access to cloud instance metadata services is prohibited.");
        }

        // IP address range checks
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsPrivateOrLoopback(ip))
                return (false, $"Access to private, loopback, or link-local IP '{ip}' is prohibited.");
        }

        return (true, null);
    }

    public static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.Equals(IPAddress.IPv6Loopback) || ip.Equals(IPAddress.IPv6Any);
        }

        var bytes = ip.GetAddressBytes();

        // 10.0.0.0/8
        if (bytes[0] == 10) return true;

        // 172.16.0.0/12 (172.16.0.0 - 172.31.255.255)
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168) return true;

        // 169.254.0.0/16 (Link-local / APIPA / Cloud metadata)
        if (bytes[0] == 169 && bytes[1] == 254) return true;

        // 127.0.0.0/8 (Loopback block)
        if (bytes[0] == 127) return true;

        // 0.0.0.0/8
        if (bytes[0] == 0) return true;

        return false;
    }
}
