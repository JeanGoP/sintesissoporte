using System.ComponentModel.DataAnnotations;
using System.Text;
namespace Sidecil.Tickets.Application;

public record ChatSiteRequest(
    [property: Required, StringLength(80, MinimumLength = 2)] string Name,
    [property: Required, StringLength(300)] string Origin);
public record ChatSiteEnabledRequest(bool Enabled);
public record ChatStartRequest(Guid SiteId);
public record ChatDraftRequest(
    [property: Required(AllowEmptyStrings = true), StringLength(120)] string Name,
    [property: Required(AllowEmptyStrings = true), StringLength(200)] string Email,
    [property: Required(AllowEmptyStrings = true), StringLength(120)] string Module,
    [property: Required(AllowEmptyStrings = true), StringLength(180)] string Subject,
    [property: Required(AllowEmptyStrings = true), StringLength(8000)] string Body,
    [property: Required, StringLength(60)] string Category, Guid? ModuleId = null, string? CompanyName = null);

public static class ChatRules
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxFiles = 3;
    public static readonly string[] Categories = ["General", "Soporte técnico", "Facturación", "Accesos", "Servicios"];
    public static string? NormalizeOrigin(string value, bool development)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            (uri.Scheme != "https" && !(development && uri.Scheme == "http" && uri.IsLoopback))) return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }
    public static string? FileType(string name, byte[] content)
    {
        if (content.Length == 0 || content.Length > MaxFileBytes) return null;
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (ext == ".png" && content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (ext is ".jpg" or ".jpeg" && content.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (ext == ".pdf" && content.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (ext == ".txt") {
            try {
                var text = new UTF8Encoding(false, true).GetString(content);
                if (!text.Any(x => char.IsControl(x) && x is not ('\r' or '\n' or '\t'))) return "text/plain";
            } catch (DecoderFallbackException) { }
        }
        return null;
    }
    public static string SafeName(string name) => Path.GetFileName(name.Replace('\\', '/'))
        .Replace('\r', '_').Replace('\n', '_');
}
