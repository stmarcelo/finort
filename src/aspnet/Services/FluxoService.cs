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
                l.ReembolsoId,
                BancoCartao = l.CartaoCredito != null ? l.CartaoCredito.Banco : null,
                DigitosCartao = l.CartaoCredito != null ? l.CartaoCredito.Ultimos4Digitos : null
            })
            .ToListAsync();

        var projecoesMes = await ProvisaoAgenda.ProjetarAsync(_db, inicioJanela, fimJanela);
        var projecoesAnteriores = await ProvisaoAgenda.ProjetarAsync(_db, PisoHistorico, inicioMes.AddDays(-1));

        // Despesas de conta: janela (da+1/m até da/m+1), SEM despesas de cartão
        // (faturas de cartão são exibidas separadamente e subtraídas do saldo do mês)
        var despesasDoMes = lancamentos
            .Where(l => l.Data >= inicioJanela && l.Data <= fimJanela
                        && l.Tipo == LancamentoTipo.Despesa && l.CartaoCreditoId == null)
            .ToList();

        // IDs de receitas que são reembolsos (apontadas por ReembolsoId de despesas de cartão)
        var idsReembolso = lancamentos
            .Where(l => l.ReembolsoId.HasValue)
            .Select(l => l.ReembolsoId!.Value)
            .ToHashSet();

        // Receitas normais: janela (da+1/m até da/m+1), sem reembolsos
        var receitasNormais = lancamentos
            .Where(l => l.Data >= inicioJanela && l.Data <= fimJanela
                        && l.Tipo == LancamentoTipo.Receita && !idsReembolso.Contains(l.Id))
            .ToList();

        // Reembolsos: buscar despesas de cartão na janela e pegar seus ReembolsoId
        // (o reembolso acompanha o vencimento do cartão, não sua própria data)
        var despesasCartaoNaJanela = lancamentos
            .Where(l => l.CartaoCreditoId != null && l.Tipo == LancamentoTipo.Despesa
                        && l.DataVencimentoCartao >= inicioJanela && l.DataVencimentoCartao <= fimJanela
                        && l.ReembolsoId.HasValue)
            .Select(l => l.ReembolsoId!.Value)
            .ToHashSet();

        var reembolsosLista = lancamentos
            .Where(l => despesasCartaoNaJanela.Contains(l.Id))
            .ToList();
        var totalReembolsos = reembolsosLista.Sum(l => l.Valor);

        // Label para exibição
        var labelReembolsos = diasAntecipacao > 0
            ? $"Reembolsos (inclui até {fimJanela.Day:D2}/{fimJanela.Month:D2})"
            : "Reembolsos";

        var receitasDoMes = receitasNormais.Concat(reembolsosLista).ToList();

        var receitas = receitasDoMes.Sum(l => l.Valor)
            + projecoesMes.Where(p => p.Data >= inicioJanela && p.Data <= fimJanela
                                      && p.Provisao.Onde == ProvisaoOnde.Receita)
                .Sum(p => p.Provisao.Valor);

        var receitasReais = receitasDoMes;
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

        // Saldo anterior: por conta. Fechada → SaldoAcumulado do último fechamento;
        // aberta → lançamentos do último fechamento (ou início) até o fim do mês -1.
        var chave = ano * 12 + mes;
        var fimMesAnterior = new DateOnly(anoAnterior, mesAnterior, DateTime.DaysInMonth(anoAnterior, mesAnterior));
        var contas = await _db.Contas.Select(c => c.Id).ToListAsync();
        var fechamentosMesAnterior = await _db.MesesFechados
            .Where(f => f.Ano == anoAnterior && f.Mes == mesAnterior)
            .ToListAsync();
        var fechamentos = await _db.MesesFechados
            .Where(m => m.Ano * 12 + m.Mes < chave)
            .OrderBy(m => m.Ano).ThenBy(m => m.Mes)
            .ToListAsync();

        decimal saldoAnterior;
        if (fechamentosMesAnterior.Count == 0 && profundidade < 120)
        {
            var cardAnterior = await ObterCardInternoAsync(anoAnterior, mesAnterior, diasAntecipacao, profundidade + 1);
            saldoAnterior = cardAnterior.SaldoAcumulado;
        }
        else
        {
            saldoAnterior = 0m;
            foreach (var contaId in contas)
            {
                var ultimoFechamento = fechamentos.LastOrDefault(f => f.ContaId == contaId);
                if (ultimoFechamento is not null)
                {
                    saldoAnterior += ultimoFechamento.SaldoAcumulado;
                    var dataUltimoFechamento = new DateOnly(ultimoFechamento.Ano, ultimoFechamento.Mes, 1)
                        .AddMonths(1);
                    saldoAnterior += lancamentos
                        .Where(l => l.ContaId == contaId && l.Data >= dataUltimoFechamento && l.Data <= fimMesAnterior)
                        .Sum(l => l.Valor);
                }
                else
                {
                    saldoAnterior += lancamentos
                        .Where(l => l.ContaId == contaId && l.Data < inicioMes)
                        .Sum(l => l.Valor);
                }
            }

            // Lançamentos sem conta (ex.: DebitoSemConta) contam no saldo anterior;
            // compras de cartão não (impacto aparece na fatura do mês de vencimento)
            saldoAnterior += lancamentos
                .Where(l => l.ContaId == null && l.CartaoCreditoId == null && l.Data < inicioMes)
                .Sum(l => l.Valor);

            saldoAnterior += projecoesAnteriores.Sum(SinalProjecao);
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
