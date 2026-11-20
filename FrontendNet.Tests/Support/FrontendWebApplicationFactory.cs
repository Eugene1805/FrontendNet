using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace FrontendNet.Tests.Support;

public sealed partial class FrontendWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeBackendState Backend { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ApiBaseUrl", "http://fake-api.local");
        builder.UseSetting("Security:RequireHttps", "false");
        builder.UseSetting("Pagination:PageSize", "5");
        builder.UseSetting("Cloudflare:TurnstileSiteKey", "");
        builder.UseSetting("Cloudflare:TurnstileSecretKey", "");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Backend);
            services.AddSingleton<IHttpMessageHandlerBuilderFilter>(
                _ => new FakePrimaryHandlerFilter(Backend));
        });
    }

    public HttpClient CreateBrowserClient(bool allowAutoRedirect = false) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });

    public async Task<string> ExtractAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        var match = AntiforgeryInputRegex().Match(html);
        if (!match.Success)
            throw new InvalidOperationException($"Antiforgery token not found in GET {path} (HTTP {(int)response.StatusCode}).");
        return match.Groups[1].Value;
    }

    public async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        HttpClient client,
        string formGetPath,
        string postPath,
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        var token = await ExtractAntiforgeryTokenAsync(client, formGetPath);
        var formData = new List<KeyValuePair<string, string>>(fields)
        {
            new("__RequestVerificationToken", token)
        };
        return await client.PostAsync(postPath, new FormUrlEncodedContent(formData));
    }

    public async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await PostFormWithAntiforgeryAsync(
            client,
            "/Auth",
            "/Auth",
            [
                new("Email", email),
                new("Password", password)
            ]);

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            throw new InvalidOperationException($"Login failed for {email}: HTTP {(int)response.StatusCode}. Body snippet: {html[..Math.Min(500, html.Length)]}");
        }
    }

    public static string DecodeHtml(string html) => WebUtility.HtmlDecode(html);

    private sealed class FakePrimaryHandlerFilter(FakeBackendState backend) : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            builder.PrimaryHandler = new FakeBackendHandler(backend);
        };
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryInputRegex();
}
