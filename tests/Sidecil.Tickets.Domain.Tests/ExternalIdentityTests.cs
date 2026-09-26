using Sidecil.Tickets.Application;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed class ExternalIdentityTests
{
    private const string Tenant = "9188040d-6c67-4c5b-b112-36a304b66dad";
    [Fact] public void PersonalMicrosoftTenantHasAnExactIssuer() => Assert.True(ExternalIdentityRules.ValidMicrosoftIssuer($"https://login.microsoftonline.com/{Tenant}/v2.0", Tenant));
    [Fact] public void BusinessMicrosoftTenantHasAnExactIssuer() {
        var id = Guid.NewGuid().ToString();
        Assert.True(ExternalIdentityRules.ValidMicrosoftIssuer($"https://login.microsoftonline.com/{id}/v2.0", id));
    }
    [Theory]
    [InlineData("https://attacker.example/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0")]
    [InlineData("https://login.microsoftonline.com.attacker.example/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0")]
    [InlineData("http://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0")]
    [InlineData("https://login.microsoftonline.com/common/v2.0")]
    [InlineData("https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/v2.0")]
    public void RejectsSpoofedOrMismatchedIssuer(string issuer) => Assert.False(ExternalIdentityRules.ValidMicrosoftIssuer(issuer, Tenant));
    [Theory]
    [InlineData(null)]
    [InlineData("common")]
    [InlineData("../common")]
    [InlineData("9188040d6c674c5bb11236a304b66dad")]
    public void RequiresCanonicalTenant(string? tenant) => Assert.False(ExternalIdentityRules.ValidMicrosoftIssuer($"https://login.microsoftonline.com/{tenant}/v2.0", tenant));
    [Theory]
    [InlineData("Google",true)]
    [InlineData("Microsoft",true)]
    [InlineData("Fake",false)]
    [InlineData("https://attacker.example",false)]
    public void ProviderNamesAreAllowlisted(string provider, bool supported) => Assert.Equal(supported,ExternalIdentityRules.Supported(provider));
}
