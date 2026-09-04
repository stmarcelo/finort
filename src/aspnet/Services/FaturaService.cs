using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class FaturaComPendentesException : InvalidOperationException
{
    public IReadOnlyList<Lancamento> Pendentes { get; }

    public FaturaComPendentesException(IReadOnlyList<Lancamento> pendentes)
        : base("Existem lançamentos não confirmados nesta fatura.")
    {
        Pendentes = pendentes;
    }
}

public class FaturaService
{
    private readonly AppDbContext _db;

    public FaturaService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Fecha a fatura usando o mês como período inteiro (compatibilidade).</summary>
    public Task<Fatura> FecharAsync(Guid cartaoId, int ano, int mes)
        => FecharAsync(cartaoId, ano, mes,
            new DateOnly(ano, mes, 1),
            new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes)));

    /// <summary>Fecha a fatura: exige todos confirmados; persiste ValorTotal, Fechada e DataFechamento.
    /// NÃO cria MesFechado (fechamento de mês é ação explícita da Fase 4d).</summary>
    public async Task<Fatura> FecharAsync(Guid cartaoId, int ano, int mes, DateOnly inicioPeriodo, DateOnly fimPeriodo)
    {
        var pendentes = await _db.Lancamentos
            .Where(l => l.CartaoCreditoId == cartaoId &&
                        (l.DataVencimentoCartao ?? l.Data) >= inicioPeriodo && (l.DataVencimentoCartao ?? l.Data) <= fimPeriodo && !l.Confirmado)
            .ToListAsync();

        if (pendentes.Count > 0)
            throw new FaturaComPendentesException(pendentes);

        var total = await _db.Lancamentos
            .Where(l => l.CartaoCreditoId == cartaoId &&
                        (l.DataVencimentoCartao ?? l.Data) >= inicioPeriodo && (l.DataVencimentoCartao ?? l.Data) <= fimPeriodo)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;

        var fatura = await _db.Faturas.FirstOrDefaultAsync(f =>
            f.CartaoCreditoId == cartaoId && f.AnoReferencia == ano && f.MesReferencia == mes);

        if (fatura is null)
        {
            fatura = new Fatura { CartaoCreditoId = cartaoId, AnoReferencia = ano, MesReferencia = mes };
            _db.Faturas.Add(fatura);
        }

        fatura.ValorTotal = total;
        fatura.Fechada = true;
        fatura.DataFechamento = DateTime.Now;
        await _db.SaveChangesAsync();
        return fatura;
    }

    public async Task<List<Lancamento>> ObterLancamentosAsync(Guid cartaoId, int ano, int mes)
        => await ObterLancamentosAsync(cartaoId, new DateOnly(ano, mes, 1), new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes)));

    public async Task<List<Lancamento>> ObterLancamentosAsync(Guid cartaoId, DateOnly inicio, DateOnly fim)
        => await _db.Lancamentos
            .Include(l => l.Categoria)
            .Include(l => l.Subcategoria)
            .Include(l => l.Pessoa)
            .Include(l => l.CartaoCredito)
            .Where(l => l.CartaoCreditoId == cartaoId &&
                        (l.DataVencimentoCartao ?? l.Data) >= inicio && (l.DataVencimentoCartao ?? l.Data) <= fim)
            .OrderBy(l => l.Data)
            .ToListAsync();

    public Task<decimal> SomarAsync(Guid cartaoId, int ano, int mes)
        => SomarAsync(cartaoId, new DateOnly(ano, mes, 1), new DateOnly(ano, mes, DateTime.DaysInMonth(ano, mes)));

    public Task<decimal> SomarAsync(Guid cartaoId, DateOnly inicio, DateOnly fim)
        => _db.Lancamentos
            .Where(l => l.CartaoCreditoId == cartaoId &&
                        (l.DataVencimentoCartao ?? l.Data) >= inicio && (l.DataVencimentoCartao ?? l.Data) <= fim)
            .SumAsync(l => (decimal?)l.Valor)
            .ContinueWith(t => t.Result ?? 0m);

    public Task<bool> EhFechadaAsync(Guid cartaoId, int ano, int mes)
        => _db.Faturas.AnyAsync(f =>
            f.CartaoCreditoId == cartaoId && f.AnoReferencia == ano &&
            f.MesReferencia == mes && f.Fechada);

    public async Task<Fatura?> ObterFechadaAsync(Guid cartaoId, int ano, int mes)
        => await _db.Faturas.FirstOrDefaultAsync(f =>
            f.CartaoCreditoId == cartaoId && f.AnoReferencia == ano &&
            f.MesReferencia == mes && f.Fechada);

    public async Task<List<Fatura>> ListarFechadasAsync(Guid cartaoId)
        => await _db.Faturas
            .Where(f => f.CartaoCreditoId == cartaoId && f.Fechada)
            .OrderByDescending(f => f.AnoReferencia)
            .ThenByDescending(f => f.MesReferencia)
            .ToListAsync();

    /// <summary>
    /// Registra o pagamento de uma fatura FECHADA: par estilo transferência (origem negativa
    /// na conta bancária, destino positivo na fatura de referência) + rollover da diferença
    /// para a próxima fatura aberta. Reabre em cascata os meses a partir do piso
    /// min(mês da data, mês de referência). Caminho dedicado: NÃO passa pelas guardas genéricas.
    /// </summary>
    public async Task<List<Lancamento>> PagarAsync(
        Guid cartaoId, int anoRef, int mesRef, Guid contaOrigemId, DateOnly data, decimal valorPago)
    {
        if (valorPago <= 0m)
            throw new InvalidOperationException("Informe um valor maior que zero.");

        var fatura = await _db.Faturas.FirstOrDefaultAsync(f =>
            f.CartaoCreditoId == cartaoId && f.AnoReferencia == anoRef &&
            f.MesReferencia == mesRef && f.Fechada)
            ?? throw new InvalidOperationException("Somente faturas fechadas podem ser pagas.");

        if (!await _db.Contas.AnyAsync(c => c.Id == contaOrigemId))
            throw new InvalidOperationException("Conta de origem não encontrada.");

        var pagamentoSub = await _db.Subcategorias.AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsProtected && s.Nome == "Cartão de crédito")
            ?? throw new InvalidOperationException("Subcategoria de pagamento de fatura não encontrada no seed.");

        var inicioRef = new DateOnly(anoRef, mesRef, 1);
        var fimRef = inicioRef.AddMonths(1).AddDays(-1);
        var mesData = new DateOnly(data.Year, data.Month, 1);
        var piso = mesData < inicioRef ? mesData : inicioRef;
        var referenciaId = Guid.NewGuid();

        Lancamento? rollover = null;
        var totalAbsoluto = Math.Abs(fatura.ValorTotal);
        var pagoAnterior = await _db.Lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId == cartaoId &&
                        l.Valor > 0m && l.Data >= inicioRef && l.Data <= fimRef)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;
        var restante = totalAbsoluto - pagoAnterior - valorPago;
        if (restante > 0m)
        {
            var diferenca = restante;
            var candidato = inicioRef.AddMonths(1);
            var limite = inicioRef.AddYears(10);
            DateOnly? alvoRollover = null;
            while (candidato <= limite)
            {
                var ocupado = await _db.Faturas.AnyAsync(f =>
                    f.CartaoCreditoId == cartaoId && f.AnoReferencia == candidato.Year &&
                    f.MesReferencia == candidato.Month && f.Fechada);
                if (!ocupado) { alvoRollover = candidato; break; }
                candidato = candidato.AddMonths(1);
            }
            if (alvoRollover is null)
                throw new InvalidOperationException("Não há mês com fatura aberta para lançar a diferença do pagamento.");

            var diaRollover = Math.Min(data.Day,
                DateTime.DaysInMonth(alvoRollover.Value.Year, alvoRollover.Value.Month));
            rollover = new Lancamento
            {
                Data = new DateOnly(alvoRollover.Value.Year, alvoRollover.Value.Month, diaRollover),
                Tipo = LancamentoTipo.Transferencia,
                Valor = -diferenca,
                CartaoCreditoId = cartaoId,
                CategoriaId = pagamentoSub.CategoriaId,
                SubcategoriaId = pagamentoSub.Id,
                Confirmado = true,
                ReferenciaId = referenciaId
            };
        }

        var dataDestino = data < inicioRef ? inicioRef : data > fimRef ? fimRef : data;

        var origem = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Transferencia,
            Valor = -valorPago,
            ContaId = contaOrigemId,
            CategoriaId = pagamentoSub.CategoriaId,
            SubcategoriaId = pagamentoSub.Id,
            Confirmado = true,
            ReferenciaId = referenciaId
        };
        var destino = new Lancamento
        {
            Data = dataDestino,
            Tipo = LancamentoTipo.Transferencia,
            Valor = valorPago,
            CartaoCreditoId = cartaoId,
            CategoriaId = pagamentoSub.CategoriaId,
            SubcategoriaId = pagamentoSub.Id,
            Confirmado = true,
            ReferenciaId = referenciaId
        };

        await ReabrirMesesAPartirDeAsync(piso);
        _db.Lancamentos.AddRange(origem, destino);
        if (rollover is not null) _db.Lancamentos.Add(rollover);
        await _db.SaveChangesAsync();

        var idsPernas = rollover is null
            ? new[] { origem.Id, destino.Id }
            : new[] { origem.Id, destino.Id, rollover.Id };
        var pernasPorId = await _db.Lancamentos
            .Include(l => l.Categoria)
            .Include(l => l.Subcategoria)
            .Where(l => idsPernas.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id);

        return idsPernas.Select(id => pernasPorId[id]).ToList();
    }

    /// <summary>Reabre o mês do piso e todos os posteriores, removendo MesFechado em cascata.</summary>
    private async Task ReabrirMesesAPartirDeAsync(DateOnly piso)
    {
        var chave = piso.Year * 12 + piso.Month;
        var alvo = await _db.MesesFechados
            .Where(m => m.Ano * 12 + m.Mes >= chave)
            .ToListAsync();
        _db.MesesFechados.RemoveRange(alvo);
    }

    /// <summary>
    /// Lista os pagamentos do mês de referência: grupos com perna destino positiva
    /// (Tipo=Transferencia, CartaoCreditoId, Valor > 0) dentro do mês, com a conta origem.
    /// </summary>
    public async Task<List<PagamentoFatura>> ObterPagamentosAsync(Guid cartaoId, int anoRef, int mesRef)
    {
        var inicio = new DateOnly(anoRef, mesRef, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var destinos = await _db.Lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId == cartaoId &&
                        l.Valor > 0m && l.Data >= inicio && l.Data <= fim && l.ReferenciaId != null)
            .ToListAsync();
        if (destinos.Count == 0) return new List<PagamentoFatura>();

        var refs = destinos.Select(d => d.ReferenciaId!.Value).Distinct().ToList();
        var origens = await _db.Lancamentos
            .Include(l => l.Conta)
            .Where(l => l.ReferenciaId != null && refs.Contains(l.ReferenciaId.Value) && l.ContaId != null)
            .ToListAsync();
        var mapaOrigens = origens.GroupBy(l => l.ReferenciaId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return destinos.Select(d =>
        {
            mapaOrigens.TryGetValue(d.ReferenciaId!.Value, out var origem);
            return new PagamentoFatura(d.ReferenciaId.Value, origem?.Data ?? d.Data, d.Valor, origem?.Conta?.Nome);
        })
        .OrderByDescending(p => p.DataPagamento)
        .ToList();
    }

    /// <summary>Remove todas as pernas de um pagamento (origem, destino e rollover)
    /// e reabre os meses afetados com a mesma cascata do pagamento.</summary>
    public async Task EstornarAsync(Guid referenciaId)
    {
        var pernas = await _db.Lancamentos
            .Where(l => l.ReferenciaId == referenciaId)
            .ToListAsync();
        if (pernas.Count == 0)
            throw new InvalidOperationException("Pagamento não encontrado.");

        var piso = pernas.Min(l => new DateOnly(l.Data.Year, l.Data.Month, 1));
        _db.Lancamentos.RemoveRange(pernas);
        await ReabrirMesesAPartirDeAsync(piso);
        await _db.SaveChangesAsync();
    }

    /// <summary>Faturas a vencer no mês exibido: referência M−1, sempre presentes quando houver conteúdo.</summary>
    public async Task<List<FaturaResumoCalendario>> ObterResumosParaCalendarioAsync(
        List<CartaoCredito> cartoes, int anoExibido, int mesExibido)
    {
        var resultado = new List<FaturaResumoCalendario>();
        var referencia = new DateOnly(anoExibido, mesExibido, 1).AddMonths(-1);
        var inicioRef = referencia;
        var fimRef = inicioRef.AddMonths(1);

        foreach (var cartao in cartoes.Where(c => c.Ativo))
        {
            var vencimento = new DateOnly(anoExibido, mesExibido,
                Math.Min(cartao.DiaVencimento, DateTime.DaysInMonth(anoExibido, mesExibido)));

            var fatura = await _db.Faturas.AsNoTracking().FirstOrDefaultAsync(f =>
                f.CartaoCreditoId == cartao.Id && f.AnoReferencia == referencia.Year &&
                f.MesReferencia == referencia.Month && f.Fechada);

            if (fatura is not null)
            {
                var pago = await _db.Lancamentos
                    .Where(l => l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId == cartao.Id &&
                                l.Valor > 0m && l.Data >= inicioRef && l.Data < fimRef)
                    .SumAsync(l => (decimal?)l.Valor) ?? 0m;
                var totalAbs = Math.Abs(fatura.ValorTotal);
                resultado.Add(new FaturaResumoCalendario(cartao.Id, vencimento, totalAbs, pago >= totalAbs));
                continue;
            }

            var saldo = await SomarAsync(cartao.Id, referencia.Year, referencia.Month);
            if (saldo != 0m)
                resultado.Add(new FaturaResumoCalendario(cartao.Id, vencimento, Math.Abs(saldo), false));
        }

        return resultado;
    }

    /// <summary>Situação consolidada das faturas fechadas do cartão: soma dos destinos positivos
    /// (pagamentos) por mês de referência, para alimentar o histórico.</summary>
    public async Task<List<FaturaSituacao>> ObterSituacoesAsync(Guid cartaoId)
    {
        var faturas = await ListarFechadasAsync(cartaoId);

        var pagos = await _db.Lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId == cartaoId && l.Valor > 0m)
            .GroupBy(l => new { l.Data.Year, l.Data.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(l => l.Valor) })
            .ToListAsync();
        var mapaPagos = pagos.ToDictionary(p => (p.Year, p.Month), p => p.Total);

        return faturas.Select(f => new FaturaSituacao(
            f.Id,
            f.AnoReferencia,
            f.MesReferencia,
            f.ValorTotal,
            mapaPagos.TryGetValue((f.AnoReferencia, f.MesReferencia), out var pago) ? pago : 0m,
            f.DataFechamento))
            .ToList();
    }
}
