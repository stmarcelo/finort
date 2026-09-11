using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public record ExtratoContaResultado(
    Guid ContaId,
    string NomeConta,
    int Ano,
    int Mes,
    bool Fechado,
    DateTime? DataFechamento,
    decimal SaldoMes,
    decimal AcumuladoReal,
    decimal AcumuladoPrevisto,
    List<Lancamento> Lancamentos,
    List<FaturaVinculadaResumo> Faturas);

/// <summary>Fatura do mês de um cartão vinculado à conta: somente leitura no extrato.
/// Contribuição no previsto (fatura do mês visto): aberta = soma live dos itens
/// (inclui eventual rollover recebido); fechada = ValorTotal gravado + pagamentos
/// (0 quando paga). Faturas de outros meses nunca entram: o débito do pagamento já
/// está nos lançamentos da conta e o rollover viaja dentro das faturas seguintes.</summary>
public record FaturaVinculadaResumo(
    Guid CartaoId,
    string Banco,
    string Ultimos4Digitos,
    bool Fechada,
    DateTime? DataFechamento,
    decimal Total,
    decimal Pago,
    decimal ContribuicaoPrevisto,
    List<Lancamento> Itens)
{
    public decimal Restante => -ContribuicaoPrevisto;
    public bool Paga => Fechada && ContribuicaoPrevisto == 0m && Pago > 0m;
}

public record GrupoExtrato(
    DateOnly Data,
    Guid? PessoaId,
    string? PessoaNome,
    bool Confirmado,
    List<Lancamento> Itens)
{
    public decimal Soma => Itens.Sum(l => l.Valor);
}

public class ExtratoContaService
{
    private readonly AppDbContext _db;
    private readonly FaturaService _faturas;

    public ExtratoContaService(AppDbContext db, FaturaService faturas)
    {
        _db = db;
        _faturas = faturas;
    }

    public static string ChaveGrupo(Lancamento l)
        => $"{l.Data:yyyy-MM-dd}|{(l.PessoaId?.ToString() ?? "sem")}|{(l.Confirmado ? "C" : "P")}";

    public static List<GrupoExtrato> Agrupar(List<Lancamento> lancamentos)
        => AgruparPor(lancamentos, l => l.Data);

    /// <summary>Agrupamento da fatura: chave de data é o vencimento do cartão.</summary>
    public static List<GrupoExtrato> AgruparPorVencimento(List<Lancamento> lancamentos)
        => AgruparPor(lancamentos, l => l.DataVencimentoCartao ?? l.Data);

    private static List<GrupoExtrato> AgruparPor(
        List<Lancamento> lancamentos, Func<Lancamento, DateOnly> chaveData)
        => lancamentos
            .GroupBy(l => new { Data = chaveData(l), l.PessoaId, l.Confirmado })
            .OrderBy(g => g.Key.Data)
            .Select(g => new GrupoExtrato(
                g.Key.Data,
                g.Key.PessoaId,
                g.First().Pessoa?.Nome,
                g.Key.Confirmado,
                g.OrderBy(l => l.Data).ToList()))
            .ToList();

    public async Task<ExtratoContaResultado> ObterAsync(Guid contaId, int ano, int mes)
    {
        var conta = await _db.Contas.FindAsync(contaId)
            ?? throw new InvalidOperationException("Conta não encontrada.");
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var lancamentos = await _db.Lancamentos
            .Include(l => l.Categoria)
            .Include(l => l.Subcategoria)
            .Include(l => l.Pessoa)
            .Where(l => l.ContaId == contaId && l.Data >= inicio && l.Data <= fim)
            .OrderBy(l => l.Data)
            .ToListAsync();

        var saldoMes = lancamentos.Sum(l => l.Valor);

        var fechadoAtual = await _db.MesesFechados
            .FirstOrDefaultAsync(m => m.ContaId == contaId && m.Ano == ano && m.Mes == mes);

        var baseFechamento = await _db.MesesFechados
            .Where(m => m.ContaId == contaId && (m.Ano < ano || (m.Ano == ano && m.Mes <= mes)))
            .OrderByDescending(m => m.Ano).ThenByDescending(m => m.Mes)
            .FirstOrDefaultAsync();

        var baseValor = baseFechamento?.SaldoAcumulado ?? 0m;
        var baseFim = baseFechamento is null
            ? DateOnly.MinValue
            : new DateOnly(baseFechamento.Ano, baseFechamento.Mes, 1).AddMonths(1).AddDays(-1);

        decimal acumuladoReal;
        if (fechadoAtual is not null)
        {
            acumuladoReal = fechadoAtual.SaldoAcumulado;
        }
        else
        {
            acumuladoReal = baseValor + await _db.Lancamentos
                .Where(l => l.ContaId == contaId && l.Confirmado && l.Data > baseFim && l.Data <= fim)
                .SumAsync(l => (decimal?)l.Valor) ?? 0m;
        }

        var acumuladoPrevisto = baseValor + await _db.Lancamentos
            .Where(l => l.ContaId == contaId && l.Data > baseFim && l.Data <= fim)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;

        var faturas = await ObterFaturasVinculadasAsync(contaId, inicio, fim);
        var impactoFaturas = faturas.Sum(f => f.ContribuicaoPrevisto);
        saldoMes += impactoFaturas;
        acumuladoPrevisto += impactoFaturas;

        return new ExtratoContaResultado(
            conta.Id, conta.Nome, ano, mes,
            fechadoAtual is not null, fechadoAtual?.DataFechamento,
            saldoMes, acumuladoReal, acumuladoPrevisto, lancamentos, faturas);
    }

    private async Task<List<FaturaVinculadaResumo>> ObterFaturasVinculadasAsync(
        Guid contaId, DateOnly inicio, DateOnly fim)
    {
        var cartoes = await _db.CartoesCredito
            .Where(c => c.ContaId == contaId)
            .OrderBy(c => c.Banco)
            .ToListAsync();

        var resultado = new List<FaturaVinculadaResumo>();
        foreach (var cartao in cartoes)
        {
            var itens = (await _faturas.ObterLancamentosAsync(cartao.Id, inicio, fim))
                .Where(l => l.DataVencimentoCartao.HasValue
                    && l.DataVencimentoCartao.Value >= inicio
                    && l.DataVencimentoCartao.Value <= fim)
                .ToList();
            var fechada = await _faturas.ObterFechadaAsync(cartao.Id, inicio.Year, inicio.Month);
            if (itens.Count == 0 && fechada is null)
                continue;

            decimal total;
            decimal pago = 0m;
            decimal contribuicao;
            if (fechada is null)
            {
                total = itens.Sum(l => l.Valor);
                contribuicao = total;
            }
            else
            {
                total = fechada.ValorTotal;
                pago = (await _faturas.ObterPagamentosAsync(cartao.Id, inicio.Year, inicio.Month))
                    .Sum(p => p.ValorPago);
                contribuicao = fechada.ValorTotal + pago;
            }

            resultado.Add(new FaturaVinculadaResumo(
                cartao.Id, cartao.Banco, cartao.Ultimos4Digitos,
                fechada is not null, fechada?.DataFechamento,
                total, pago, contribuicao, itens));
        }

        return resultado;
    }
}
