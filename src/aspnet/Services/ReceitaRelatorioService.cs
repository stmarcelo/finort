using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Finort.Services;

public sealed record ReceitaLinha(Guid Id, DateOnly Data, string? CartaoNome, string? ContaNome,
    int? ParcelaAtual, int? TotalParcelas, string CategoriaRotulo, decimal Valor, bool Confirmado);
public sealed record ReceitaOrigemSubtotal(string Rotulo, decimal Confirmado, decimal NaoConfirmado);
public sealed record ReceitaPessoaSubtotal(Guid PessoaId, string PessoaNome, string? Cor, decimal Confirmado, decimal NaoConfirmado);
public sealed record ReceitaRelatorio(DateOnly Inicio, DateOnly Fim, Guid? PessoaId, string PessoaNome,
    decimal TotalConfirmado, decimal TotalNaoConfirmado,
    IReadOnlyList<ReceitaLinha> Linhas,
    IReadOnlyList<ReceitaOrigemSubtotal> SubtotaisPorOrigem,
    IReadOnlyList<ReceitaPessoaSubtotal> SubtotaisPorPessoa);

public class ReceitaRelatorioService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public ReceitaRelatorioService(AppDbContext db, IWebHostEnvironment env) => (_db, _env) = (db, env);

    public async Task<ReceitaRelatorio> GerarAsync(DateOnly inicio, DateOnly fim, Guid? pessoaId)
    {
        var query = _db.Lancamentos
            .Include(l => l.Conta).Include(l => l.CartaoCredito).Include(l => l.Pessoa)
            .Include(l => l.Categoria).Include(l => l.Subcategoria)
            .Where(l => l.Tipo == LancamentoTipo.Receita && l.Data >= inicio && l.Data <= fim);
        if (pessoaId.HasValue) query = query.Where(l => l.PessoaId == pessoaId.Value);
        var lista = await query.OrderBy(l => l.Data).ToListAsync();

        string pessoaNome = "";
        if (pessoaId.HasValue)
            pessoaNome = (await _db.Pessoas.FindAsync(pessoaId.Value))?.Nome ?? "Pessoa não encontrada";

        var confirmado = lista.Where(l => l.Confirmado).Sum(l => Math.Abs(l.Valor));
        var naoConfirmado = lista.Where(l => !l.Confirmado).Sum(l => Math.Abs(l.Valor));

        // Cartão que originou cada receita de reembolso: despesas de cartão
        // apontam para a receita via ReembolsoId (1:1 por parcela; primeira vence).
        var receitaIds = lista.Select(l => l.Id).ToList();
        var cartaoReembolso = (await _db.Lancamentos
            .Include(l => l.CartaoCredito)
            .Where(l => l.Tipo == LancamentoTipo.Despesa && l.CartaoCreditoId != null
                && l.ReembolsoId.HasValue && receitaIds.Contains(l.ReembolsoId.Value))
            .Select(l => new { ReembolsoId = l.ReembolsoId!.Value, Banco = l.CartaoCredito!.Banco })
            .ToListAsync())
            .GroupBy(x => x.ReembolsoId)
            .ToDictionary(g => g.Key, g => g.First().Banco);

        string? CartaoEfetivo(Lancamento l)
            => l.CartaoCredito?.Banco
               ?? (cartaoReembolso.TryGetValue(l.Id, out var banco) ? banco : null);

        var linhas = pessoaId.HasValue
            ? lista.Select(l => new ReceitaLinha(l.Id, l.Data, CartaoEfetivo(l), l.Conta?.Nome,
                l.ParcelaAtual, l.TotalParcelas,
                l.Subcategoria is null ? l.Categoria.Nome : $"{l.Categoria.Nome} > {l.Subcategoria.Nome}",
                Math.Abs(l.Valor), l.Confirmado)).ToList()
            : new List<ReceitaLinha>();

        var origens = pessoaId.HasValue
            ? lista.GroupBy(l => (Cartao: CartaoEfetivo(l) ?? "", Conta: l.Conta?.Nome ?? ""))
                .Select(g => new ReceitaOrigemSubtotal(RotuloOrigem(g.Key.Cartao, g.Key.Conta),
                    g.Where(x => x.Confirmado).Sum(x => Math.Abs(x.Valor)),
                    g.Where(x => !x.Confirmado).Sum(x => Math.Abs(x.Valor)))).ToList()
            : new List<ReceitaOrigemSubtotal>();

        var porPessoa = pessoaId.HasValue
            ? new List<ReceitaPessoaSubtotal>()
            : lista.GroupBy(l => l.PessoaId)
                .Select(g => {
                    var p = g.First().Pessoa;
                    return new ReceitaPessoaSubtotal(g.Key ?? Guid.Empty, p?.Nome ?? "Sem pessoa",
                        p?.CorDeExibicao, g.Where(x => x.Confirmado).Sum(x => Math.Abs(x.Valor)),
                        g.Where(x => !x.Confirmado).Sum(x => Math.Abs(x.Valor)));
                }).OrderBy(x => x.PessoaNome).ToList();

        return new ReceitaRelatorio(inicio, fim, pessoaId, pessoaNome, confirmado, naoConfirmado,
            linhas, origens, porPessoa);
    }

    private static string RotuloOrigem(string cartao, string conta)
    {
        if (!string.IsNullOrEmpty(cartao) && !string.IsNullOrEmpty(conta)) return $"Cartão {cartao} / Conta {conta}";
        if (!string.IsNullOrEmpty(cartao)) return $"Cartão {cartao}";
        if (!string.IsNullOrEmpty(conta)) return $"Conta {conta}";
        return "Sem origem";
    }

    public async Task<byte[]> GerarPdfBytesAsync(DateOnly inicio, DateOnly fim, Guid? pessoaId)
    {
        var r = await GerarAsync(inicio, fim, pessoaId);
        var agora = DateTime.Now;
        var tituloPessoa = pessoaId.HasValue ? r.PessoaNome : "Todas as pessoas";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(595, 842);
                page.Header().Column(col =>
                {
                    col.Item().Element(c => RelatorioPdfHeader.Cabecalho(c, "Receitas por pessoa", tituloPessoa,
                        new[]
                        {
                            new RelatorioCabecalhoLinha(
                                $"Período: {r.Inicio:dd/MM/yyyy}–{r.Fim:dd/MM/yyyy}", 10, "#666666")
                        },
                        RelatorioPdfHeader.LogoPath(_env)));
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
                        void Card(string titulo, decimal valor, string cor)
                        {
                            row.RelativeItem().PaddingRight(8).Element(c => c
                                .Border(1).CornerRadius(6).BorderColor("#e0e0e0").Padding(8).Column(c =>
                                {
                                    c.Item().Text(titulo).FontSize(8).FontColor("#666666");
                                    c.Item().Text($"R$ {valor:N2}").FontSize(12).SemiBold().FontColor(cor);
                                }));
                        }
                        Card("Total sem confirmar", r.TotalNaoConfirmado, "#F5A623");
                        Card("Total confirmado", r.TotalConfirmado, "#248A3D");
                    });
                    if (r.Linhas.Count > 0)
                    {
                        col.Item().Text("Lançamentos").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(55); c.RelativeColumn(); c.RelativeColumn();
                                c.ConstantColumn(30); c.ConstantColumn(75); c.ConstantColumn(60);
                            });
                            t.Header(h =>
                            {
                                static QuestPDF.Infrastructure.IContainer Head(QuestPDF.Infrastructure.IContainer c)
                                    => c.Background("#f5f5f7").Padding(4);
                                h.Cell().Element(Head).Text("Data").FontSize(9);
                                h.Cell().Element(Head).Text("Cartão").FontSize(9);
                                h.Cell().Element(Head).Text("Conta").FontSize(9);
                                h.Cell().Element(Head).Text("P").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Valor").FontSize(9);
                                h.Cell().Element(Head).PaddingLeft(4).Text("Status").FontSize(9);
                            });
                            var linha = 0;
                            foreach (var l in r.Linhas)
                            {
                                var cor = l.Confirmado ? "#444444" : "#248A3D";
                                var i = linha++;
                                Func<QuestPDF.Infrastructure.IContainer, QuestPDF.Infrastructure.IContainer> Zebrar =
                                    c => i % 2 == 1 ? c.Background("#F2F2F7").PaddingVertical(2) : c.PaddingVertical(2);
                                t.Cell().Element(Zebrar).Text(l.Data.ToString("dd/MM/yyyy")).FontSize(9);
                                t.Cell().Element(Zebrar).Text(l.CartaoNome ?? "—").FontSize(9);
                                t.Cell().Element(Zebrar).Text(l.ContaNome ?? "—").FontSize(9);
                                t.Cell().Element(Zebrar).Text(l.ParcelaAtual.HasValue && l.TotalParcelas.HasValue ? $"{l.ParcelaAtual}/{l.TotalParcelas}" : "—").FontSize(9);
                                t.Cell().Element(Zebrar).AlignRight().Text($"R$ {l.Valor:N2}").FontSize(9).FontColor(cor);
                                t.Cell().Element(Zebrar).PaddingLeft(4).Text(l.Confirmado ? "Confirmado" : "Pendente").FontSize(9).FontColor(cor);
                            }
                        });
                        col.Item().Text("Subtotais por origem").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); });
                            t.Header(h =>
                            {
                                static QuestPDF.Infrastructure.IContainer Head(QuestPDF.Infrastructure.IContainer c)
                                    => c.Background("#f5f5f7").Padding(4);
                                h.Cell().Element(Head).Text("Origem").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Confirmado").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Não confirmado").FontSize(9);
                            });
                            foreach (var s in r.SubtotaisPorOrigem)
                            {
                                t.Cell().Text(s.Rotulo).FontSize(9);
                                t.Cell().AlignRight().Text($"R$ {s.Confirmado:N2}").FontSize(9);
                                t.Cell().AlignRight().Text($"R$ {s.NaoConfirmado:N2}").FontSize(9);
                            }
                        });
                    }
                    else if (r.SubtotaisPorPessoa.Count > 0)
                    {
                        col.Item().Text("Totais por pessoa").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); });
                            var linha = 0;
                            foreach (var p in r.SubtotaisPorPessoa)
                            {
                                var i = linha++;
                                Func<QuestPDF.Infrastructure.IContainer, QuestPDF.Infrastructure.IContainer> Zebrar =
                                    c => i % 2 == 1 ? c.Background("#F2F2F7").PaddingVertical(4) : c.PaddingVertical(4);
                                t.Cell().Element(Zebrar).Text(p.PessoaNome).FontSize(9);
                                t.Cell().Element(Zebrar).AlignRight().Text($"R$ {p.Confirmado:N2}").FontSize(9);
                                t.Cell().Element(Zebrar).AlignRight().Text($"R$ {p.NaoConfirmado:N2}").FontSize(9);
                            }
                        });
                    }
                    else
                    {
                        col.Item().Text("Sem receitas no período.").FontSize(10).FontColor("#666666");
                    }
                });
            });
        }).GeneratePdf();
    }
}
