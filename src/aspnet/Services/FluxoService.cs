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
    public Task<FluxoMensal> ObterCardAsync(int ano, int mes, int diasAntecipacao = 0)
        => ObterCardInternoAsync(ano, mes, diasAntecipacao, 0);

    private async Task<FluxoMensal> ObterCardInternoAsync(int ano, int mes, int diasAntecipacao, int profundidade)
    {
        var inicioMes = new DateOnly(ano, mes, 1);
        var fimMes = inicioMes.AddMonths(1).AddDays(-1);

        var proximoMes = mes == 12 ? 1 : mes + 1;
        var proximoAno = mes == 12 ? ano + 1 : ano;

        // Janela de despesas/reembolsos: mês atual a partir de D+1 + mês seguinte até D
        // Mês M mostra de M/(D+1) até (M+1)/D
        // M-1 mostra de M/1 até M/D (antecipados)
        DateOnly inicioJanela;
        DateOnly fimJanela;
        if (diasAntecipacao > 0)
        {
            inicioJanela = new DateOnly(ano, mes, Math.Min(diasAntecipacao + 1, DateTime.DaysInMonth(ano, mes)));
            var ultimoDiaProximo = DateTime.DaysInMonth(proximoAno, proximoMes);
            fimJanela = new DateOnly(proximoAno, proximoMes,
                Math.Min(diasAntecipacao, ultimoDiaProximo));
        }
        else
        {
            inicioJanela = inicioMes;
            fimJanela = fimMes;
        }

        var lancamentos = await _db.Lancamentos
            .Where(l => l.CartaoCreditoId != null
                ? l.DataVencimentoCartao <= fimJanela
                : l.Data <= fimJanela)
            .Select(l => new
            {
                l.Id,
                l.Data,
                l.DataVencimentoCartao,
                l.Tipo,
                l.Valor,
                l.Confirmado,
                l.ContaId,
                l.CartaoCreditoId,
                BancoCartao = l.CartaoCredito != null ? l.CartaoCredito.Banco : null,
                DigitosCartao = l.CartaoCredito != null ? l.CartaoCredito.Ultimos4Digitos : null
            })
            .ToListAsync();

        var projecoesMes = await ProvisaoAgenda.ProjetarAsync(_db, inicioJanela, fimJanela);

        // Despesas de conta: janela (da+1/m até da/m+1), SEM despesas de cartão
        // (faturas de cartão são exibidas separadamente e subtraídas do saldo do mês)
        var despesasDoMes = lancamentos
            .Where(l => l.Data >= inicioJanela && l.Data <= fimJanela
                        && l.Tipo == LancamentoTipo.Despesa && l.CartaoCreditoId == null)
            .ToList();

        // Receitas normais: janela (da+1/m até da/m+1).
        // Reembolsos pendentes vivem na tabela Reembolsos (sem receita até o fechamento).
        var receitasNormais = lancamentos
            .Where(l => l.Data >= inicioJanela && l.Data <= fimJanela
                        && l.Tipo == LancamentoTipo.Receita)
            .ToList();

        // Reembolsos pendentes por vencimento (previsão até o fechamento da fatura).
        var reembolsosPendentes = await _db.Reembolsos
            .Where(r => !r.Fechado && r.Vencimento >= inicioJanela && r.Vencimento <= fimJanela)
            .ToListAsync();
        var totalReembolsos = reembolsosPendentes.Sum(r => r.Valor);

        // Label para exibição
        var labelReembolsos = diasAntecipacao > 0
            ? $"Reembolsos (inclui até {fimJanela.Day:D2}/{fimJanela.Month:D2})"
            : "Reembolsos";

        var receitas = receitasNormais.Sum(l => l.Valor) + totalReembolsos
            + projecoesMes.Where(p => p.Data >= inicioJanela && p.Data <= fimJanela
                                      && p.Provisao.Onde == ProvisaoOnde.Receita)
                .Sum(p => p.Provisao.Valor);

        var receitasReais = receitasNormais;
        var receitasPagas = receitasReais.Count > 0 && receitasReais.All(l => l.Confirmado);
        var despesasPagas = despesasDoMes.Count > 0 && despesasDoMes.All(l => l.Confirmado);

        var despesas = -despesasDoMes.Sum(l => l.Valor)
            + projecoesMes.Where(p => p.Data >= inicioJanela && p.Data <= fimJanela
                                      && p.Provisao.Onde != ProvisaoOnde.Receita
                                      && p.Provisao.Onde != ProvisaoOnde.DebitoCartao)
                .Sum(p => p.Provisao.Valor);

        // Cartões de crédito: usar DataVencimentoCartao para atribuição ao mês
        var despesasCartaoMes = lancamentos
            .Where(l => l.CartaoCreditoId != null && l.Tipo == LancamentoTipo.Despesa &&
                        l.DataVencimentoCartao.HasValue &&
                        l.DataVencimentoCartao >= inicioJanela && l.DataVencimentoCartao <= fimJanela)
            .ToList();
        var projecoesCartaoMes = projecoesMes
            .Where(p => p.Provisao.Onde == ProvisaoOnde.DebitoCartao &&
                        p.Provisao.CartaoCreditoId != null &&
                        DataVencimentoCartao(p) >= inicioJanela && DataVencimentoCartao(p) <= fimJanela)
            .ToList();

        var itensCartao = despesasCartaoMes
            .Select(l => (Id: l.CartaoCreditoId!.Value, l.BancoCartao, l.DigitosCartao,
                Vencimento: l.DataVencimentoCartao!.Value, Magnitude: -l.Valor))
            .Concat(projecoesCartaoMes
                .Select(p => (Id: p.Provisao.CartaoCreditoId!.Value,
                    BancoCartao: p.Provisao.CartaoCredito != null ? p.Provisao.CartaoCredito.Banco : null,
                    DigitosCartao: p.Provisao.CartaoCredito != null ? p.Provisao.CartaoCredito.Ultimos4Digitos : null,
                    Vencimento: DataVencimentoCartao(p),
                    Magnitude: p.Provisao.Valor)))
            .GroupBy(x => x.Id)
            .Select(g => new
            {
                g.Key,
                Nome = RotuloCartao(g.First().BancoCartao, g.First().DigitosCartao),
                Total = g.Sum(x => x.Magnitude),
                MesesVencimento = g.Select(x => x.Vencimento).Distinct().ToList()
            })
            .OrderBy(x => x.Nome)
            .ToList();

        var itensComStatus = new List<TotalCartao>();
        foreach (var item in itensCartao)
        {
            var pago = true;
            foreach (var venc in item.MesesVencimento)
                if (!await FaturaPagaAsync(item.Key, venc.Year, venc.Month))
                {
                    pago = false;
                    break;
                }
            itensComStatus.Add(new TotalCartao(item.Key, item.Nome, item.Total, pago));
        }

        var mesAnterior = mes == 1 ? 12 : mes - 1;
        var anoAnterior = mes == 1 ? ano - 1 : ano;

        // Invariante do fluxo: SaldoAnterior(M) == SaldoAcumulado(M-1).
        // O saldo anterior é SEMPRE o acumulado do mês anterior (mesma janela de
        // antecipação). Fechamentos de conta (MesFechado) NÃO entram aqui: são
        // contabilidade por conta (FechamentoService/Extrato) enquanto o fluxo é
        // visão gerencial (janela D, cartões por vencimento, inclui pendentes e
        // projeções). Misturar os dois conceitos quebrava o transporte entre meses.
        decimal saldoAnterior;
        if (profundidade < 120)
        {
            var cardAnterior = await ObterCardInternoAsync(anoAnterior, mesAnterior, diasAntecipacao, profundidade + 1);
            saldoAnterior = cardAnterior.SaldoAcumulado;
        }
        else
        {
            saldoAnterior = 0m;
        }

        // Saldo do mês = receitas - despesas - faturas dos cartões
        var totalCartoes = itensComStatus.Sum(c => c.Total);
        var saldoMes = receitas - despesas - totalCartoes;

        return new FluxoMensal(ano, mes, receitas, despesas, itensComStatus,
            saldoAnterior, saldoMes, saldoAnterior + saldoMes,
            ReceitasPagas: receitasPagas, DespesasPagas: despesasPagas,
            TotalReembolsos: totalReembolsos,
            LabelReembolsos: labelReembolsos);
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

    private static DateOnly DataVencimentoCartao((DateOnly Data, Provisao Provisao) projecao)
        => projecao.Provisao.CartaoCredito is { } cartao
            ? new DateOnly(projecao.Data.Year, projecao.Data.Month,
                Math.Min(cartao.DiaVencimento, DateTime.DaysInMonth(projecao.Data.Year, projecao.Data.Month)))
            : projecao.Data;

    private static decimal SinalProjecao((DateOnly Data, Provisao Provisao) projecao)
        => projecao.Provisao.Onde == ProvisaoOnde.Receita
            ? projecao.Provisao.Valor
            : -projecao.Provisao.Valor;

    private static string RotuloCartao(string? banco, string? digitos)
        => $"{banco ?? "Cartão"} ••{digitos}";
}
