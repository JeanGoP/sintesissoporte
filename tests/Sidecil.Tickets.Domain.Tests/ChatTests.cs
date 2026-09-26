using System.Text;
using Sidecil.Tickets.Application;
using Xunit;
namespace Sidecil.Tickets.Domain.Tests;
public sealed class ChatTests
{
    [Theory]
    [InlineData("https://erp.sidecil.com", "https://erp.sidecil.com")]
    [InlineData("https://ERP.Sidecil.com/", "https://erp.sidecil.com")]
    [InlineData("https://erp.sidecil.com:8443", "https://erp.sidecil.com:8443")]
    public void AcceptsExactHttpsOrigins(string input, string expected) => Assert.Equal(expected, ChatRules.NormalizeOrigin(input, false));
    [Theory]
    [InlineData("http://erp.sidecil.com")]
    [InlineData("https://erp.sidecil.com/path")]
    [InlineData("https://user:pass@erp.sidecil.com")]
    [InlineData("https://erp.sidecil.com?x=1")]
    [InlineData("https://erp.sidecil.com#hash")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://erp.sidecil.com; frame-ancestors *")]
    public void RejectsUnsafeOrigins(string input) => Assert.Null(ChatRules.NormalizeOrigin(input, false));
    [Fact] public void LocalHttpOnlyInDevelopment() { Assert.Null(ChatRules.NormalizeOrigin("http://localhost:5091", false)); Assert.Equal("http://localhost:5091", ChatRules.NormalizeOrigin("http://localhost:5091", true)); }
    [Fact] public void ChecksContentInsteadOfTrustingFileExtension() {
        Assert.Null(ChatRules.FileType("screen.png", Encoding.UTF8.GetBytes("<script>alert(1)</script>")));
        Assert.Null(ChatRules.FileType("image.svg", Encoding.UTF8.GetBytes("<svg />")));
        Assert.Equal("application/pdf", ChatRules.FileType("evidence.pdf", Encoding.UTF8.GetBytes("%PDF-1.7\n")));
    }
    [Fact] public void RejectsEmptyOversizedAndBinaryText() {
        Assert.Null(ChatRules.FileType("notes.txt", []));
        Assert.Null(ChatRules.FileType("notes.txt", new byte[ChatRules.MaxFileBytes + 1]));
        Assert.Null(ChatRules.FileType("notes.txt", [0, 1, 2]));
        Assert.Null(ChatRules.FileType("notes.txt", [0xff, 0xff]));
        Assert.Equal("text/plain", ChatRules.FileType("notes.txt", Encoding.UTF8.GetBytes("Descripción\nMódulo: Facturación")));
    }
    [Fact] public void StripsPathsAndHeaderNewlines() => Assert.Equal("evidence__.txt", ChatRules.SafeName("C:\\private\\evidence\r\n.txt"));
}
