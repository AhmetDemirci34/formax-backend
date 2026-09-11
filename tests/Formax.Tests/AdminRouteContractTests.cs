using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// ADMIN ROTA SÖZLEŞMESİ — bir metodun rota özniteliği komşu metoda kaymasın.
///
/// ÖLÇÜLDÜ (11.09.2026): istek dökümü eklenirken <c>[HttpGet("usage")]</c> yeni metodun
/// üstünde kaldı; <c>/admin/api-football/usage</c> istek dökümüne yönlendi ve eski
/// kullanım raporu denetleyici köküne düştü. Test kaynak üzerinden, öznitelik ile
/// hemen altındaki metodun eşleşmesini sabitler.
/// </summary>
public class AdminRouteContractTests
{
    private static string Source()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(dir!, "Formax.API", "Controllers", "Admin", "AdminApiFootballUsageController.cs"));
    }

    [Theory]
    [InlineData("usage", "Usage")]
    [InlineData("requests", "Requests")]
    public void RotaOzniteligi_DogruMetodunHemenUstunde(string route, string method)
    {
        var pattern = $@"\[HttpGet\(""{route}""\)\]\s*\r?\n\s*public [^\(]*\b{method}\(";
        Assert.Matches(new Regex(pattern), Source());
    }
}
