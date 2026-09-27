using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed partial class PortalOidcTests
{
    [Theory]
    [InlineData("https://soporte.example.invalid", true)]
    [InlineData("https://otro.example.invalid", false)]
    public async Task ChatAttachmentPreflightAllowsFileNameOnlyForConfiguredFrontend(string origin, bool allowed)
    {
        using var factory = new PortalOidcFactory("https://soporte.example.invalid");
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Hosting:PathBase", "/API_SoporteSidecil"));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/API_SoporteSidecil/api/v1/chat/sessions/00000000-0000-0000-0000-000000000001/attachments");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-sidecil-widget,x-file-name");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        if (allowed) {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
            Assert.Contains("x-file-name", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant());
        } else Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
