namespace Sidecil.Tickets.Api;

public static class FrontendHosting
{
    public static string Origin(IConfiguration configuration, bool development)
    {
        var value = configuration["Frontend:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(development && uri.Scheme == "http" && uri.IsLoopback)) ||
            uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("Frontend__PublicBaseUrl debe ser un origen HTTPS sin rutas; HTTP local solo en desarrollo.");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    public static string ReturnUrl(HttpContext http, string path)
    {
        var config = http.RequestServices.GetRequiredService<IConfiguration>();
        var env = http.RequestServices.GetRequiredService<IWebHostEnvironment>();
        return Origin(config, env.IsDevelopment()) + path;
    }
}
