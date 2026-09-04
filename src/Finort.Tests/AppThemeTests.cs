using Finort.App;
using Xunit;

namespace Finort.Tests;

public class AppThemeTests
{
    [Fact]
    public void Palette_usa_cores_finort_navy_gold()
    {
        var p = AppTheme.Instance.PaletteLight;

        Assert.Equal("#1A2F4C", p.Primary);
        Assert.Equal("#C9A94E", p.Secondary);
        Assert.Equal("#FBF9F8", p.Background);
        Assert.Equal("#FFFFFF", p.Surface);
        Assert.Equal("#1A2F4C", p.AppbarBackground);
        Assert.Equal("#C9A94E", p.AppbarText);
        Assert.Equal("#1A2F4C", p.DrawerBackground);
        Assert.Equal("#FBF9F8", p.DrawerText);
        Assert.Equal("#1B1C1C", p.TextPrimary);
        Assert.Equal("#44474E", p.TextSecondary);
    }
}
