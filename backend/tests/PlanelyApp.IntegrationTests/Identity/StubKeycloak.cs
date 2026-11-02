using System.Net;
using System.Text;

namespace PlanelyApp.IntegrationTests.Identity;

internal sealed record RecordedRequest(HttpMethod Method, string Path, string Query, string? Token, string Body);

/// <summary>A Keycloak that answers what each test says and remembers what it was asked.</summary>
internal sealed class StubKeycloak : HttpMessageHandler
{
    private readonly Dictionary<string, List<Func<HttpResponseMessage>>> _routes = [];
    private readonly Dictionary<string, int> _calls = [];

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>The answers for a route, in order; the last one repeats.</summary>
    public StubKeycloak On(HttpMethod method, string path, params Func<HttpResponseMessage>[] responses)
    {
        _routes[$"{method} {path}"] = [.. responses];

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var uri = request.RequestUri!;

        Requests.Add(new RecordedRequest(
            request.Method, uri.AbsolutePath, uri.Query, request.Headers.Authorization?.Parameter, body));

        var key = $"{request.Method} {uri.AbsolutePath}";

        if (!_routes.TryGetValue(key, out var responses))
        {
            throw new InvalidOperationException($"The test set up no answer for {key}.");
        }

        var call = _calls.GetValueOrDefault(key);
        _calls[key] = call + 1;

        return responses[Math.Min(call, responses.Count - 1)]();
    }
}

internal static class Reply
{
    public static Func<HttpResponseMessage> Status(HttpStatusCode status, string body = "") =>
        () => new HttpResponseMessage(status) { Content = new StringContent(body) };

    public static Func<HttpResponseMessage> Json(HttpStatusCode status, string json) =>
        () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static Func<HttpResponseMessage> Created(string location) => () =>
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri(location);

        return response;
    };

    public static Func<HttpResponseMessage> Token(string value, int expiresIn = 300) =>
        Json(HttpStatusCode.OK, $$"""{"access_token":"{{value}}","expires_in":{{expiresIn}}}""");
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}