using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Sidecil.Tickets.Api;
using Xunit;

namespace Sidecil.Tickets.Domain.Tests;

public class FrontendHostingTests
{
    [Theory]
    [InlineData("https://portal.example.com/path")]
    [InlineData("https://evil@portal.example.com")]
    [InlineData("http://portal.example.com")]
    [InlineData("https://portal.example.com?return=evil")]
    [InlineData("*")]
    public void RejectsUnsafeFrontendOrigins(string origin)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Frontend:PublicBaseUrl"] = origin }).Build();
        Assert.Throws<InvalidOperationException>(() => FrontendHosting.Origin(config, false));
    }

    [Fact]
    public async Task CorsAllowsOnlyConfiguredFrontendAndDoesNotBypassCsrf()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sidecil.Tickets.Api")));
            builder.UseSetting("Frontend:PublicBaseUrl", "https://portal.example.com");
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        foreach (var origin in new[] { "https://portal.example.com", "https://evil.example.com" }) {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");
            using var response = await client.SendAsync(request);
            if (origin.Contains("evil")) Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
            else {
                Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
                Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
            }
        }
        using var csrfMissing = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        csrfMissing.Headers.Add("Origin", "https://portal.example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(csrfMissing)).StatusCode);
        Assert.Equal("https://portal.example.com/", (await client.GetAsync("/")).Headers.Location!.AbsoluteUri);
    }
}
