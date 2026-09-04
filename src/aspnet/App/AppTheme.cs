using MudBlazor;

namespace Finort.App;

/// <summary>Tema Finort: navy e gold, tipografia Inter.</summary>
public static class AppTheme
{
    private static readonly string[] Fonte = { "Inter", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "sans-serif" };

    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1A2F4C",
            Secondary = "#C9A94E",
            Info = "#1A2F4C",
            Success = "#248A3D",
            Error = "#D70015",
            Background = "#FBF9F8",
            Surface = "#FFFFFF",
            AppbarBackground = "#1A2F4C",
            AppbarText = "#C9A94E",
            DrawerBackground = "#1A2F4C",
            DrawerText = "#FBF9F8",
            TextPrimary = "#1B1C1C",
            TextSecondary = "#44474E",
            Divider = "#E2E8F0"
        },
        Typography = new Typography
        {
            H5 = new H5Typography
            {
                FontFamily = Fonte,
                FontSize = "1.3125rem",   // 21px
                FontWeight = "600",
                LineHeight = "1.19",
                LetterSpacing = "-0.2px"
            },
            H6 = new H6Typography
            {
                FontFamily = Fonte,
                FontSize = "1.0625rem",   // 17px
                FontWeight = "600",
                LineHeight = "1.29",
                LetterSpacing = "-0.2px"
            },
            Body1 = new Body1Typography
            {
                FontFamily = Fonte,
                FontSize = "1.0625rem",   // 17px
                FontWeight = "400",
                LineHeight = "1.47",
                LetterSpacing = "-0.2px"
            },
            Body2 = new Body2Typography
            {
                FontFamily = Fonte,
                FontSize = "0.875rem",    // 14px
                FontWeight = "400",
                LineHeight = "1.43"
            },
            Button = new ButtonTypography
            {
                FontFamily = Fonte,
                FontSize = "0.875rem",    // 14px
                FontWeight = "600"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            AppbarHeight = "48px",
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px",
            DrawerWidthRight = "260px"
        }
    };
}
