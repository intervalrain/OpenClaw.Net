using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.Http.HttpRequest;

public class HttpRequestSkill(
    TimeSpan? timeout = null,
    int maxResponseLength = 200000) : AgentToolBase<HttpRequestArgs>
{
    public static HttpRequestSkill Default => new(
        timeout: TimeSpan.FromSeconds(30),
        maxResponseLength: 200000);

    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(30);
    private readonly int _maxResponseLength = maxResponseLength;

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Blocked internal IP ranges for SSRF protection
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedRanges =
    [
        (IPAddress.Parse("127.0.0.0"), 8),       // Loopback
        (IPAddress.Parse("10.0.0.0"), 8),         // Private Class A
        (IPAddress.Parse("172.16.0.0"), 12),      // Private Class B
        (IPAddress.Parse("192.168.0.0"), 16),     // Private Class C
        (IPAddress.Parse("169.254.0.0"), 16),     // Link-local / AWS metadata
        (IPAddress.Parse("0.0.0.0"), 8),          // Unspecified
        (IPAddress.IPv6Loopback, 128),            // ::1
    ];

    public override string Name => "http_request";
    public override string Description => "Send an HTTP request (GET or POST) to a URL and return the response";

    public override async Task<ToolResult> ExecuteAsync(HttpRequestArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Url))
        {
            return ToolResult.Failure("URL is required.");
        }

        if (!Uri.TryCreate(args.Url, UriKind.Absolute, out var uri))
        {
            return ToolResult.Failure($"Invalid URL: {args.Url}");
        }

        // SSRF protection: only allow http and https schemes
        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return ToolResult.Failure($"URL scheme '{uri.Scheme}' is not allowed. Only http and https are permitted.");
        }

        // SSRF protection: block internal/private IP ranges
        var ssrfCheck = await IsBlockedHostAsync(uri.Host, ct);
        if (ssrfCheck)
        {
            return ToolResult.Failure("Request to internal/private network addresses is not allowed.");
        }

        var method = (args?.Method?.ToUpperInvariant()) switch
        {
            "POST" => HttpMethod.Post,
            "PUT" or "UPDATE" => HttpMethod.Put,
            "DELETE" or "REMOVE" => HttpMethod.Delete,
            _ => HttpMethod.Get
        };

        try
        {
            using var request = new HttpRequestMessage(method, uri);

            if (!string.IsNullOrEmpty(args?.Body))
            {
                request.Content = new StringContent(args.Body, Encoding.UTF8, "application/json");
            }

            using var response = await SharedHttpClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (content.Length > _maxResponseLength)
            {
                content = content[.._maxResponseLength] + $"\n... (truncated, total {content.Length} chars)";
            }

            return ToolResult.Success($"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n\n{content}");
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Failure("Request timed out.");
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Request failed: {ex.Message}");
        }
    }

    private static async Task<bool> IsBlockedHostAsync(string host, CancellationToken ct)
    {
        // Block well-known internal hostnames
        var lowerHost = host.ToLowerInvariant();
        if (lowerHost is "localhost" or "metadata.google.internal" or "metadata"
            || lowerHost.EndsWith(".internal")
            || lowerHost.EndsWith(".local"))
        {
            return true;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            foreach (var addr in addresses)
            {
                if (IsPrivateOrReserved(addr))
                    return true;
            }
        }
        catch
        {
            // DNS resolution failed — block to be safe
            return true;
        }

        return false;
    }

    private static bool IsPrivateOrReserved(IPAddress address)
    {
        // Normalize IPv4-mapped IPv6 to IPv4
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        foreach (var (network, prefixLen) in BlockedRanges)
        {
            if (network.AddressFamily != address.AddressFamily)
                continue;

            var networkBytes = network.GetAddressBytes();
            var addressBytes = address.GetAddressBytes();

            var fullBytes = prefixLen / 8;
            var remainingBits = prefixLen % 8;

            var match = true;
            for (var i = 0; i < fullBytes && i < networkBytes.Length; i++)
            {
                if (networkBytes[i] != addressBytes[i])
                {
                    match = false;
                    break;
                }
            }

            if (match && remainingBits > 0 && fullBytes < networkBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - remainingBits));
                if ((networkBytes[fullBytes] & mask) != (addressBytes[fullBytes] & mask))
                    match = false;
            }

            if (match) return true;
        }

        return false;
    }
}

public record HttpRequestArgs(
    [property: Description("The URL to send the request to")]
    string? Url,

    [property: Description("HTTP method: GET, POST, PUT, DELETE, Defaults to GET.")]
    string? Method,

    [property: Description("Request body for POST/PUT requests (JSON format)")]
    string? Body
);
