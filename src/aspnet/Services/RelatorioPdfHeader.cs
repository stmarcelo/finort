using Microsoft.AspNetCore.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Finort.Services;

/// <summary>Linha de subtítulo do cabeçalho (texto, tamanho em pt, cor hex).</summary>
public sealed record RelatorioCabecalhoLinha(string Texto, int Tamanho, string Cor);

/// <summary>Cabeçalho reutilizável dos PDFs de relatórios: logotipo + título + destaque + linhas.</summary>
public static class RelatorioPdfHeader
{
    private const string LogoArquivo = "logo_finort.png";

    public static string? LogoPath(IWebHostEnvironment env)
    {
        var caminho = Path.Combine(env.WebRootPath, "img", LogoArquivo);
        return File.Exists(caminho) ? caminho : null;
    }
    public static void Cabecalho(IContainer container, string titulo, string destaque,
        IReadOnlyList<RelatorioCabecalhoLinha> linhas, string? logoPath)
    {
        container.Row(row =>
        {
            if (logoPath is not null)
                row.ConstantItem(100).AlignMiddle().Image(logoPath, ImageScaling.FitArea);

            row.RelativeItem().PaddingLeft(logoPath is null ? 0 : 12).Column(col =>
            {
                col.Item().Text(titulo).FontSize(18).SemiBold().FontColor("#1D1D1F");
                col.Item().PaddingTop(4).Text(destaque).FontSize(14).SemiBold().FontColor("#333333");
                foreach (var linha in linhas)
                    col.Item().PaddingTop(2).Text(linha.Texto).FontSize(linha.Tamanho).FontColor(linha.Cor);
            });
        });
    }
}
