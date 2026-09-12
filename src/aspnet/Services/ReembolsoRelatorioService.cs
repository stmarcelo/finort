using Finort.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace Finort.Services;

public sealed record ReembolsoLinha(Guid Id, DateOnly Vencimento, string CartaoNome, string PessoaNome,
    int? ParcelaAtual, int? TotalParcelas, decimal Valor, bool Fechado);
public sealed record ReembolsoRelatorio(DateOnly Inicio, DateOnly Fim, Guid? CartaoId, Guid? PessoaId,
    decimal TotalFechado, decimal TotalPendente, IReadOnlyList<ReembolsoLinha> Linhas);

public class ReembolsoRelatorioService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    public ReembolsoRelatorioService(AppDbContext db, IWebHostEnvironment env) => (_db, _env) = (db, env);

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

    public async Task<ReembolsoRelatorio> GerarAsync(DateOnly inicio, DateOnly fim, Guid? cartaoId, Guid? pessoaId)
    {
        var query = _db.Reembolsos
            .Include(r => r.Pessoa).Include(r => r.CartaoCredito)
            .Where(r => r.Vencimento >= inicio && r.Vencimento <= fim);
        if (cartaoId.HasValue) query = query.Where(r => r.CartaoCreditoId == cartaoId.Value);
        if (pessoaId.HasValue) query = query.Where(r => r.PessoaId == pessoaId.Value);
        var lista = await query.OrderBy(r => r.Vencimento).ToListAsync();

        var linhas = lista.Select(r => new ReembolsoLinha(r.Id, r.Vencimento,
            r.CartaoCredito?.Banco ?? "—", r.Pessoa?.Nome ?? "Sem pessoa",
            r.ParcelaAtual, r.TotalParcelas, r.Valor, r.Fechado)).ToList();

        return new ReembolsoRelatorio(inicio, fim, cartaoId, pessoaId,
            lista.Where(r => r.Fechado).Sum(r => r.Valor),
            lista.Where(r => !r.Fechado).Sum(r => r.Valor),
            linhas);
    }

    public async Task<byte[]> GerarPdfBytesAsync(DateOnly inicio, DateOnly fim, Guid? cartaoId, Guid? pessoaId)
    {
        var r = await GerarAsync(inicio, fim, cartaoId, pessoaId);
        string pessoaNome = "Todas as pessoas";
        if (pessoaId.HasValue)
            pessoaNome = (await _db.Pessoas.FindAsync(pessoaId.Value))?.Nome ?? "Pessoa não encontrada";
        string cartaoNome = "Todos os cartões";
        if (cartaoId.HasValue)
        {
            var cartao = await _db.CartoesCredito.FindAsync(cartaoId.Value);
            cartaoNome = cartao is null ? "Cartão não encontrado" : $"{cartao.Banco} (****{cartao.Ultimos4Digitos})";
        }
        var agora = DateTime.Now;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.Size(595, 842);
                page.Header().Column(col =>
                {
                    col.Item().Element(c => RelatorioPdfHeader.Cabecalho(c, "Reembolsos", "Por vencimento",
                        new[]
                        {
                            new RelatorioCabecalhoLinha(
                                $"Período: {r.Inicio:dd/MM/yyyy}–{r.Fim:dd/MM/yyyy}", 10, "#666666"),
                            new RelatorioCabecalhoLinha($"Pessoa: {pessoaNome}", 10, "#666666"),
                            new RelatorioCabecalhoLinha($"Cartão: {cartaoNome}", 10, "#666666")
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
                        Card("Total pendente", r.TotalPendente, "#F5A623");
                        Card("Total fechado", r.TotalFechado, "#248A3D");
                    });
                    if (r.Linhas.Count > 0)
                    {
                        col.Item().Text("Reembolsos").FontSize(12).SemiBold();
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
                                h.Cell().Element(Head).Text("Pessoa").FontSize(9);
                                h.Cell().Element(Head).Text("P").FontSize(9);
                                h.Cell().Element(Head).AlignRight().Text("Valor").FontSize(9);
                                h.Cell().Element(Head).PaddingLeft(4).Text("Status").FontSize(9);
                            });
                            var linha = 0;
                            foreach (var l in r.Linhas)
                            {
                                var cor = l.Fechado ? "#444444" : "#248A3D";
                                var i = linha++;
                                Func<QuestPDF.Infrastructure.IContainer, QuestPDF.Infrastructure.IContainer> Zebrar =
                                    c => i % 2 == 1 ? c.Background("#F2F2F7").PaddingVertical(2) : c.PaddingVertical(2);
                                t.Cell().Element(Zebrar).Text(l.Vencimento.ToString("dd/MM/yyyy")).FontSize(9);
                                t.Cell().Element(Zebrar).Text(l.CartaoNome).FontSize(9);
                                t.Cell().Element(Zebrar).Text(l.PessoaNome).FontSize(9);
                                t.Cell().Element(Zebrar).Text(l.ParcelaAtual.HasValue && l.TotalParcelas.HasValue ? $"{l.ParcelaAtual}/{l.TotalParcelas}" : "—").FontSize(9);
                                t.Cell().Element(Zebrar).AlignRight().Text($"R$ {l.Valor:N2}").FontSize(9).FontColor(cor);
                                t.Cell().Element(Zebrar).PaddingLeft(4).Text(l.Fechado ? "Fechado" : "Pendente").FontSize(9).FontColor(cor);
                            }
                        });
                    }
                    else
                    {
                        col.Item().Text("Sem reembolsos no período.").FontSize(10).FontColor("#666666");
                    }
                });
            });
        }).GeneratePdf();
    }
}
