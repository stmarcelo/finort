using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class FluxoService
{
    /// <summary>Piso da janela histórica de projeções (o loop começa na fronteira do sincronismo).</summary>
    private static readonly DateOnly PisoHistorico = new(2000, 1, 1);

    private readonly AppDbContext _db;

    public FluxoService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Agregados do mês: totais sem transferências, por cartão, saldo anterior e acumulado.</summary>
    public async Task<FluxoMensal> ObterCardAsync(int ano, int mes, int diasAntecipacao = 0)
    {
        var inicioMes = new DateOnly(ano, mes, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        // Janela de despesas: deslocada D dias para evitar dupla contagem entre meses
        // Mês M mostra de M/1+D até M/last+D (dias 1..D ficam no mês anterior)
        var inicioMesDespesas = inicioMes.AddDays(diasAntecipacao);
        var fimMesDespesas = fimMes.AddDays(diasAntecipacao);

        var lancamentos = await _db.Lancamentos
            .Where(l => l.Data <= fimMesDespesas)
            .Select(l => new
            {
                l.Data,
                l.DataVencimentoCartao,
                l.Tipo,
                l.Valor,
                l.Confirmado,
                l.CartaoCreditoId,
                BancoCartao = l.CartaoCredito != null ? l.CartaoCredito.Banco : null,
                DigitosCartao = l.CartaoCredito != null ? l.CartaoCredito.Ultimos4Digitos : null
            })
            .ToListAsync();

        var projecoesMes = await ProvisaoAgenda.ProjetarAsync(_db, inicioMes, fimMesDespesas);
        var projecoesAnteriores = await ProvisaoAgenda.ProjetarAsync(_db, PisoHistorico, inicioMesDespesas.AddDays(-1));

        // Despesas de conta: janela deslocada por D
        var despesasDoMes = lancamentos
            .Where(l => l.Data >= inicioMesDespesas && l.Data <= fimMesDespesas && l.Tipo == LancamentoTipo.Despesa)
            .ToList();

        // Receitas: sempre no mês original (sem antecipação)
        var receitasDoMes = lancamentos
            .Where(l => l.Data >= inicioMes && l.Data <= fimMes && l.Tipo == LancamentoTipo.Receita)
            .ToList();

        var receitas = receitasDoMes.Sum(l => l.Valor)
            + projecoesMes.Where(p => p.Data <= fimMes).Sum(p => p.Provisao.Onde == ProvisaoOnde.Receita ? p.Provisao.Valor : 0m);

        var receitasReais = receitasDoMes;
        var receitasPagas = receitasReais.Count > 0 && receitasReais.All(l => l.Confirmado);
        var despesasPagas = despesasDoMes.Count > 0 && despesasDoMes.All(l => l.Confirmado);

        var despesas = -despesasDoMes.Sum(l => l.Valor)
            + projecoesMes.Where(p => p.Data >= inicioMesDespesas && p.Data <= fimMesDespesas && p.Provisao.Onde != ProvisaoOnde.Receita)
                .Sum(p => p.Provisao.Valor);

        // Cartões de crédito: usar DataVencimentoCartao para atribuição ao mês
        var despesasCartaoMes = lancamentos
            .Where(l => l.CartaoCreditoId != null && l.Tipo == LancamentoTipo.Despesa &&
                        l.DataVencimentoCartao >= inicioMesDespesas && l.DataVencimentoCartao <= fimMesDespesas)
            .ToList();
        var projecoesCartaoMes = projecoesMes
            .Where(p => p.Data >= inicioMesDespesas && p.Data <= fimMesDespesas &&
                        p.Provisao.Onde == ProvisaoOnde.DebitoCartao && p.Provisao.CartaoCreditoId != null)
            .ToList();

        var itensCartao = despesasCartaoMes
            .Select(l => (Id: l.CartaoCreditoId!.Value, l.BancoCartao, l.DigitosCartao, Magnitude: -l.Valor))
            .Concat(projecoesCartaoMes
                .Select(p => (Id: p.Provisao.CartaoCreditoId!.Value,
                    BancoCartao: p.Provisao.CartaoCredito != null ? p.Provisao.CartaoCredito.Banco : null,
                    DigitosCartao: p.Provisao.CartaoCredito != null ? p.Provisao.CartaoCredito.Ultimos4Digitos : null,
                    Magnitude: p.Provisao.Valor)))
            .GroupBy(x => x.Id)
            .Select(g => new TotalCartao(
                CartaoId: g.Key,
                Nome: RotuloCartao(g.First().BancoCartao, g.First().DigitosCartao),
                Total: g.Sum(x => x.Magnitude)))
            .OrderBy(t => t.Nome)
            .ToList();

        var itensComStatus = new List<TotalCartao>();
        foreach (var item in itensCartao)
            itensComStatus.Add(item with { Pago = await FaturaPagaAsync(item.CartaoId, ano, mes) });

        var mesAnterior = mes == 1 ? 12 : mes - 1;
        var anoAnterior = mes == 1 ? ano - 1 : ano;

        var mesFechadoAnterior = await _db.MesesFechados
            .FirstOrDefaultAsync(m => m.Mes == mesAnterior && m.Ano == anoAnterior);

        decimal saldoAnterior;
        if (mesFechadoAnterior != null)
        {
            saldoAnterior = mesFechadoAnterior.SaldoAcumulado;
        }
        else
        {
            saldoAnterior = lancamentos.Where(l => l.Data < inicioMesDespesas).Sum(l => l.Valor)
                + projecoesAnteriores.Sum(SinalProjecao);
        }

        var saldoMes = receitas - despesas;

        return new FluxoMensal(ano, mes, receitas, despesas, itensComStatus,
            saldoAnterior, saldoMes, saldoAnterior + saldoMes,
            ReceitasPagas: receitasPagas, DespesasPagas: despesasPagas);
    }

    private async Task<bool> FaturaPagaAsync(Guid cartaoId, int ano, int mes)
    {
        var fatura = await _db.Faturas.AsNoTracking().FirstOrDefaultAsync(f =>
            f.CartaoCreditoId == cartaoId && f.AnoReferencia == ano &&
            f.MesReferencia == mes && f.Fechada);
        if (fatura is null) return false;

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1);
        var pago = await _db.Lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId == cartaoId &&
                        l.Valor > 0m && l.Data >= inicio && l.Data < fim)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;
        return pago >= Math.Abs(fatura.ValorTotal);
    }

    private static decimal SinalProjecao((DateOnly Data, Provisao Provisao) projecao)
        => projecao.Provisao.Onde == ProvisaoOnde.Receita
            ? projecao.Provisao.Valor
            : -projecao.Provisao.Valor;

    private static string RotuloCartao(string? banco, string? digitos)
        => $"{banco ?? "Cartão"} ••{digitos}";
}
