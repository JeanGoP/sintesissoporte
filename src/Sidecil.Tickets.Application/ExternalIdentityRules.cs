namespace Sidecil.Tickets.Application;

public static class ExternalIdentityRules
{
    public static bool ValidMicrosoftIssuer(string issuer, string? tenant) =>
        Guid.TryParseExact(tenant, "D", out var id) &&
        string.Equals(issuer, $"https://login.microsoftonline.com/{id:D}/v2.0", StringComparison.OrdinalIgnoreCase);
    public static bool Supported(string provider) => provider is "Microsoft" or "Google";
}
