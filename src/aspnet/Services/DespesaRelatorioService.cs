using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Finort.Services;

public sealed record DespesaLinha(Guid Id, DateOnly Data, string? CartaoNome, string? ContaNome,
    int? ParcelaAtual, int? TotalParcelas, string CategoriaRotulo, decimal Valor, bool Confirmado);
public sealed record DespesaOrigemSubtotal(string Rotulo, decimal Confirmado, decimal NaoConfirmado);
public sealed record DespesaCategoriaSubtotal(string Rotulo, decimal Confirmado, decimal NaoConfirmado);
public sealed record DespesaPessoaSubtotal(Guid PessoaId, string PessoaNome, string? Cor, decimal Confirmado, decimal NaoConfirmado);
public sealed record DespesaRelatorio(DateOnly Inicio, DateOnly Fim, Guid? PessoaId, string PessoaNome,
    Guid? CartaoId, string CartaoNome, Guid? CategoriaId, string CategoriaNome,
    decimal TotalConfirmado, decimal TotalNaoConfirmado,
    IReadOnlyList<DespesaLinha> Linhas,
    IReadOnlyList<DespesaOrigemSubtotal> SubtotaisPorOrigem,
    IReadOnlyList<DespesaCategoriaSubtotal> SubtotaisPorCategoria,
    IReadOnlyList<DespesaPessoaSubtotal> SubtotaisPorPessoa);

public class DespesaRelatorioService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public DespesaRelatorioService(AppDbContext db, IWebHostEnvironment env) => (_db, _env) = (db, env);

    /// <summary>
    /// Período padrão do mês corrente levando em conta os dias de antecipação
    /// gravados (mesma janela do fluxo: mês M vai de M/(D+1) até (M+1)/D;
    /// D=0 ou sem configuração = mês cheio).
    /// </summary>
    public async Task<(DateOnly Inicio, DateOnly Fim)> PeriodoPadraoDoMesAsync()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var da = (await _db.Configuracoes.AsNoTracking().FirstOrDefaultAsync())?.DiasAntecipacao ?? 0;
        da = Math.Clamp(da, 0, 15);
        if (da == 0)
            return (new DateOnly(hoje.Year, hoje.Month, 1), new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1));
        var inicio = new DateOnly(hoje.Year, hoje.Month, Math.Min(da + 1, DateTime.DaysInMonth(hoje.Year, hoje.Month)));
        var prox = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1);
        var fim = new DateOnly(prox.Year, prox.Month, Math.Min(da, DateTime.DaysInMonth(prox.Year, prox.Month)));
        return (inicio, fim);
    }

    /// <summary>
    /// Despesas do período por pessoa, com filtro opcional de cartão.
    /// Critério de data: despesas de cartão entram pelo vencimento da fatura
    /// (como no fluxo); demais despesas, pela data do lançamento.
    /// </summary>
    public async Task<DespesaRelatorio> GerarAsync(DateOnly inicio, DateOnly fim, Guid? pessoaId, Guid? cartaoId, Guid? categoriaId)
    {
        var query = _db.Lancamentos
            .Include(l => l.Conta).Include(l => l.CartaoCredito).Include(l => l.Pessoa)
            .Include(l => l.Categoria).Include(l => l.Subcategoria)
            .Where(l => l.Tipo == LancamentoTipo.Despesa
                && (l.CartaoCreditoId != null
                    ? (l.DataVencimentoCartao ?? l.Data) >= inicio && (l.DataVencimentoCartao ?? l.Data) <= fim
                    : l.Data >= inicio && l.Data <= fim));
        if (pessoaId.HasValue) query = pessoaId.Value == Guid.Empty
            ? query.Where(l => l.PessoaId == null)
            : query.Where(l => l.PessoaId == pessoaId.Value);
        if (cartaoId.HasValue) query = query.Where(l => l.CartaoCreditoId == cartaoId.Value);
        if (categoriaId.HasValue) query = query.Where(l => l.CategoriaId == categoriaId.Value
            || (l.SubcategoriaId.HasValue && l.Subcategoria!.CategoriaId == categoriaId.Value));
        var lista = (await query.ToListAsync())
            .OrderBy(l => l.CartaoCreditoId != null ? (l.DataVencimentoCartao ?? l.Data) : l.Data)
            .ToList();

        string pessoaNome = "";
        if (pessoaId.HasValue)
            pessoaNome = pessoaId.Value == Guid.Empty ? "Sem pessoa"
                : (await _db.Pessoas.FindAsync(pessoaId.Value))?.Nome ?? "Pessoa não encontrada";

        string cartaoNome = "";
        if (cartaoId.HasValue)
        {
            var cartao = await _db.CartoesCredito.FindAsync(cartaoId.Value);
            cartaoNome = cartao is null ? "Cartão não encontrado" : $"{cartao.Banco} (****{cartao.Ultimos4Digitos})";
        }

        string categoriaNome = "";
        if (categoriaId.HasValue)
            categoriaNome = (await _db.Categorias.FindAsync(categoriaId.Value))?.Nome ?? "Categoria não encontrada";

        static DateOnly DataCriterio(Lancamento l)
            => l.CartaoCreditoId != null ? (l.DataVencimentoCartao ?? l.Data) : l.Data;

        var confirmado = lista.Where(l => l.Confirmado).Sum(l => Math.Abs(l.Valor));
        var naoConfirmado = lista.Where(l => !l.Confirmado).Sum(l => Math.Abs(l.Valor));

        static string CategoriaDe(Lancamento l)
            => l.Subcategoria is null ? l.Categoria.Nome : $"{l.Categoria.Nome} > {l.Subcategoria.Nome}";

        var linhas = pessoaId.HasValue
            ? lista.Select(l => new DespesaLinha(l.Id, DataCriterio(l), l.CartaoCredito?.Banco, l.Conta?.Nome,
                l.ParcelaAtual, l.TotalParcelas, CategoriaDe(l),
                Math.Abs(l.Valor), l.Confirmado)).ToList()
            : new List<DespesaLinha>();

        var origens = pessoaId.HasValue
            ? lista.GroupBy(l => (Cartao: l.CartaoCredito?.Banco ?? "", Conta: l.Conta?.Nome ?? ""))
                .Select(g => new DespesaOrigemSubtotal(RotuloOrigem(g.Key.Cartao, g.Key.Conta),
                    g.Where(x => x.Confirmado).Sum(x => Math.Abs(x.Valor)),
                    g.Where(x => !x.Confirmado).Sum(x => Math.Abs(x.Valor)))).ToList()
            : new List<DespesaOrigemSubtotal>();

        var porPessoa = pessoaId.HasValue
            ? new List<DespesaPessoaSubtotal>()
            : lista.GroupBy(l => l.PessoaId)
                .Select(g => {
                    var p = g.First().Pessoa;
                    return new DespesaPessoaSubtotal(g.Key ?? Guid.Empty, p?.Nome ?? "Sem pessoa",
                        p?.CorDeExibicao, g.Where(x => x.Confirmado).Sum(x => Math.Abs(x.Valor)),
                        g.Where(x => !x.Confirmado).Sum(x => Math.Abs(x.Valor)));
                }).OrderBy(x => x.PessoaNome).ToList();

        var porCategoria = lista.GroupBy(CategoriaDe)
            .Select(g => new DespesaCategoriaSubtotal(g.Key,
                g.Where(x => x.Confirmado).Sum(x => Math.Abs(x.Valor)),
                g.Where(x => !x.Confirmado).Sum(x => Math.Abs(x.Valor))))
            .OrderBy(x => x.Rotulo).ToList();

        return new DespesaRelatorio(inicio, fim, pessoaId, pessoaNome, cartaoId, cartaoNome,
            categoriaId, categoriaNome,
            confirmado, naoConfirmado, linhas, origens, porCategoria, porPessoa);
    }

    private static string RotuloOrigem(string cartao, string conta)
    {
        if (!string.IsNullOrEmpty(cartao) && !string.IsNullOrEmpty(conta)) return $"Cartão {cartao} / Conta {conta}";
        if (!string.IsNullOrEmpty(cartao)) return $"Cartão {cartao}";
        if (!string.IsNullOrEmpty(conta)) return $"Conta {conta}";
        return "Sem origem";
    }

    public async Task<byte[]> GerarPdfBytesAsync(DateOnly inicio, DateOnly fim, Guid? pessoaId, Guid? cartaoId, Guid? categoriaId)
    {
        var r = await GerarAsync(inicio, fim, pessoaId, cartaoId, categoriaId);
        var agora = DateTime.Now;
        var tituloPessoa = pessoaId.HasValue ? r.PessoaNome : "Todas as pessoas";
        var tituloCartao = cartaoId.HasValue ? r.CartaoNome : "Todos os cartões";
        var tituloCategoria = categoriaId.HasValue ? r.CategoriaNome : "Todas as categorias";
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(595, 842);
                page.Header().Column(col =>
                {
                    col.Item().Element(c => RelatorioPdfHeader.Cabecalho(c, "Despesas por pessoa", tituloPessoa,
                        new[]
                        {
                            new RelatorioCabecalhoLinha(
                                $"Período: {r.Inicio:dd/MM/yyyy}–{r.Fim:dd/MM/yyyy}", 10, "#666666"),
                            new RelatorioCabecalhoLinha($"Cartão: {tituloCartao}", 10, "#666666"),
                            new RelatorioCabecalhoLinha($"Categoria: {tituloCategoria}", 10, "#666666")
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
                        col.Item().Text("Subtotais por categoria").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); });
                            t.Header(h =>
                            {
                                static QuestPDF.Infrastructure.IContainer Head(QuestPDF.Infrastructure.IContainer c)
                                    => c.Background("#f5f5f7").Padding(4);
                                h.Cell().Element(Head).Text("Categoria").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Confirmado").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Não confirmado").FontSize(9);
                            });
                            foreach (var s in r.SubtotaisPorCategoria)
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
                        col.Item().Text("Subtotais por categoria").FontSize(12).SemiBold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); c.ConstantColumn(80); });
                            t.Header(h =>
                            {
                                static QuestPDF.Infrastructure.IContainer Head(QuestPDF.Infrastructure.IContainer c)
                                    => c.Background("#f5f5f7").Padding(4);
                                h.Cell().Element(Head).Text("Categoria").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Confirmado").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Não confirmado").FontSize(9);
                            });
                            foreach (var s in r.SubtotaisPorCategoria)
                            {
                                t.Cell().Text(s.Rotulo).FontSize(9);
                                t.Cell().AlignRight().Text($"R$ {s.Confirmado:N2}").FontSize(9);
                                t.Cell().AlignRight().Text($"R$ {s.NaoConfirmado:N2}").FontSize(9);
                            }
                        });
                    }
                    else
                    {
                        col.Item().Text("Sem despesas no período.").FontSize(10).FontColor("#666666");
                    }
                });
            });
        }).GeneratePdf();
    }
}
