using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System.Globalization;

namespace Finort.Services;

public sealed record PizzaFatia(string Rotulo, decimal Valor, double Percentual);
public sealed record RelatorioLinha(DateOnly Data, string Tipo, string CategoriaSubcategoria, decimal Valor);
public sealed record ProjetoRelatorio(
    Projeto Projeto, string ProjetoNome, string PessoaNome, DateOnly DataContratacao, decimal ValorContratado,
    bool Concluido, DateOnly? DataConclusao,
    decimal TotalReceitas, decimal TotalDespesas, decimal Resultado, decimal PendenteReceber,
    IReadOnlyList<RelatorioLinha> Linhas, IReadOnlyList<PizzaFatia> DespesasPorCategoria);

public class ProjetoRelatorioService
{
    private readonly AppDbContext _db;

    public ProjetoRelatorioService(AppDbContext db) => _db = db;

    public async Task<ProjetoRelatorio?> GerarAsync(Guid projetoId)
    {
        var projeto = await _db.Projetos.Include(p => p.Pessoa).FirstOrDefaultAsync(p => p.Id == projetoId);
        if (projeto is null) return null;

        var lancamentos = await _db.Lancamentos
            .Include(l => l.Categoria)
            .Include(l => l.Subcategoria)
            .Where(l => l.ProjetoId == projetoId)
            .OrderBy(l => l.Data)
            .ToListAsync();

        var receitas = lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita).Sum(l => l.Valor);
        var despesas = lancamentos.Where(l => l.Tipo == LancamentoTipo.Despesa).Sum(l => Math.Abs(l.Valor));

        var linhas = lancamentos.Select(l => new RelatorioLinha(
            l.Data,
            l.Tipo == LancamentoTipo.Receita ? "Receita" : "Despesa",
            l.Subcategoria is null ? l.Categoria.Nome : $"{l.Categoria.Nome} > {l.Subcategoria.Nome}",
            Math.Abs(l.Valor))).ToList();

        var agrupado = lancamentos.Where(l => l.Tipo == LancamentoTipo.Despesa)
            .GroupBy(l => l.Subcategoria is null
                ? l.Categoria.Nome
                : $"{l.Categoria.Nome} > {l.Subcategoria.Nome}")
            .Select(g => new PizzaFatia(g.Key, g.Sum(x => Math.Abs(x.Valor)), 0))
            .OrderByDescending(f => f.Valor)
            .ToList();

        var fatias = despesas == 0
            ? agrupado
            : agrupado.Select(f => f with { Percentual = Math.Round((double)(f.Valor * 100m / despesas), 1) })
                .ToList();

        return new ProjetoRelatorio(projeto, projeto.Descricao, projeto.Pessoa?.Nome ?? "", projeto.DataContratacao,
            projeto.ValorContratado, projeto.Concluido, projeto.DataConclusao,
            receitas, despesas, receitas - despesas,
            Math.Max(0, projeto.ValorContratado - receitas), linhas, fatias);
    }

    public async Task<byte[]?> GerarPdfBytesAsync(Guid projetoId)
    {
        var relatorio = await GerarAsync(projetoId);
        if (relatorio is null) return null;
        var agora = DateTime.Now;
        var detalhes = $"Contratação: {relatorio.DataContratacao:dd/MM/yyyy} — Valor contratado: R$ {relatorio.ValorContratado:N2}"
            + (relatorio.Concluido ? $" — Conclusão: {relatorio.DataConclusao:dd/MM/yyyy}" : "");
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(595, 842);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Relatório de projeto").FontSize(18).SemiBold().FontColor("#1D1D1F");
                            c.Item().PaddingTop(4).Text($"{relatorio.ProjetoNome}").FontSize(14).SemiBold().FontColor("#333333");
                            c.Item().PaddingTop(2).Text($"{relatorio.PessoaNome}").FontSize(12).FontColor("#666666");
                            c.Item().PaddingTop(2).Text(detalhes).FontSize(10).FontColor("#666666");
                        });
                    });
                    col.Item().Height(10);
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Gerado em {agora:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#999999");
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.Span("Página ").FontSize(8).FontColor("#999999");
                        text.CurrentPageNumber().FontSize(8).FontColor("#999999");
                    });
                    row.RelativeItem().AlignRight().Text("Finort - Finanças Norteadas").FontSize(8).FontColor("#999999");
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Row(row =>
                    {
                        void Card(string titulo, string valor, string cor)
                        {
                            row.RelativeItem().PaddingRight(8).Element(c => c
                                .Border(1).CornerRadius(6).BorderColor("#e0e0e0").Padding(8)
                                .Column(c =>
                                {
                                    c.Item().Text(titulo).FontSize(8).FontColor("#666666");
                                    c.Item().Text($"R$ {valor}").FontSize(12).SemiBold().FontColor(cor);
                                }));
                        }
                        Card("Receitas", relatorio.TotalReceitas.ToString("N2"), "#248A3D");
                        Card("Despesas", relatorio.TotalDespesas.ToString("N2"), "#D70015");
                        Card("Resultado", relatorio.Resultado.ToString("N2"),
                            relatorio.Resultado >= 0 ? "#0066CC" : "#D70015");
                        Card("Pendente a receber", relatorio.PendenteReceber.ToString("N2"), "#F5A623");
                    });

                    col.Item().Text("Lançamentos").FontSize(12).SemiBold().FontColor("#1D1D1F");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(55);
                            c.ConstantColumn(50);
                            c.RelativeColumn();
                            c.ConstantColumn(70);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(Head).Text("Data");
                            h.Cell().Element(Head).Text("Tipo");
                            h.Cell().Element(Head).Text("Categoria");
                            h.Cell().Element(Head).AlignRight().Text("Valor");
                        });
                        foreach (var l in relatorio.Linhas)
                        {
                            var cor = l.Tipo == "Receita" ? "#248A3D" : "#D70015";
                            t.Cell().Element(c => c.PaddingVertical(3)).Text(l.Data.ToString("dd/MM/yyyy")).FontSize(9);
                            t.Cell().Element(c => c.PaddingVertical(3)).Text(l.Tipo).FontSize(9).FontColor(cor);
                            t.Cell().Element(c => c.PaddingVertical(3)).Text(l.CategoriaSubcategoria).FontSize(9);
                            t.Cell().Element(c => c.PaddingVertical(3)).AlignRight().Text($"R$ {l.Valor:N2}").FontSize(9).FontColor(cor);
                        }
                    });

                    if (relatorio.DespesasPorCategoria.Count > 0)
                    {
                        col.Item().PaddingTop(8).Text("Despesas por categoria").FontSize(12).SemiBold().FontColor("#1D1D1F");
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(140).Height(140).PaddingRight(12).Svg(size =>
                            {
                                using var stream = new MemoryStream();
                                using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, size.Width, size.Height), stream))
                                    DesenharPizza(canvas, size, relatorio.DespesasPorCategoria);
                                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
                            });
                            r.RelativeItem().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(70); c.ConstantColumn(40); });
                                foreach (var f in relatorio.DespesasPorCategoria)
                                {
                                    t.Cell().Text(f.Rotulo).FontSize(9);
                                    t.Cell().AlignRight().Text($"R$ {f.Valor:N2}").FontSize(9);
                                    t.Cell().AlignRight().Text($"{f.Percentual:0.0}%").FontSize(9).FontColor("#666666");
                                }
                            });
                        });
                    }
                });
            });
        }).GeneratePdf();

        static IContainer Head(IContainer c) => c.Background("#f5f5f7").Padding(4);
    }

    private static void DesenharPizza(SKCanvas canvas, Size size, IReadOnlyList<PizzaFatia> fatias)
    {
        var total = fatias.Sum(f => f.Valor);
        var lado = Math.Min(size.Width, size.Height);
        var rect = new SKRect(0, 0, lado, lado);
        var inicio = -90f;
        var cores = new[] { "#0066cc", "#34c759", "#ff9500", "#af52de", "#ff3b30", "#5ac8fa", "#ffcc00", "#8e8e93" };
        var i = 0;
        foreach (var fatia in fatias)
        {
            var sweep = total == 0 ? 360f : (float)((double)(fatia.Valor / total) * 360.0);
            using var paint = new SKPaint { Color = SKColor.Parse(cores[i % cores.Length]), IsAntialias = true };
            using var builder = new SKPathBuilder();
            builder.MoveTo(rect.MidX, rect.MidY);
            builder.ArcTo(rect, inicio, sweep, false);
            builder.Close();
            using var path = builder.Snapshot();
            canvas.DrawPath(path, paint);
            inicio += sweep;
            i++;
        }
    }
}
