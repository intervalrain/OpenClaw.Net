using System.Net.Http.Headers;

namespace OpenClaw.Api.IntegrationTests.Common;

public class AppHttpClient(HttpClient _httpClient)
{
    public HttpClient HttpClient => _httpClient;

    public void SetBearerToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
