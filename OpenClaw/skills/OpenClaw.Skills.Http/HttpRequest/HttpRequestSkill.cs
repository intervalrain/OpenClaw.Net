using System.ComponentModel;
using System.Net.Security;
using System.Text;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.Http.HttpRequest;

public class HttpRequestSkill(
    TimeSpan? timeout = null,
    int maxResponseLength = 200000) : AgentSkillBase<HttpRequestArgs>
{
    public static HttpRequestSkill Default => new(
        timeout: TimeSpan.FromSeconds(30),
        maxResponseLength: 200000);

    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(30);
    private readonly int _maxResponseLength = maxResponseLength;

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
            {
                return sslPolicyErrors == SslPolicyErrors.None ||
                       sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors;
            }
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    public override string Name => "http_request";
    public override string Description => "Send an HTTP request (GET or POST) to a URL and return the response";

    public override async Task<SkillResult> ExecuteAsync(HttpRequestArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Url))
        {
            return SkillResult.Failure("URL is required.");
        }

        if (!Uri.TryCreate(args.Url, UriKind.Absolute, out var uri))
        {
            return SkillResult.Failure($"Invalid URL: {args.Url}");
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

            return SkillResult.Success($"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n\n{content}");
        }
        catch (TaskCanceledException)
        {
            return SkillResult.Failure("Request timed out.");
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Request failed: {ex.Message}");
        }
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