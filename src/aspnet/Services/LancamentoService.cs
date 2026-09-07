using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class LancamentoConfirmadoException : InvalidOperationException
{
    public IReadOnlyList<Lancamento> Confirmados { get; }

    public LancamentoConfirmadoException(IReadOnlyList<Lancamento> confirmados)
        : base("Não é possível alterar lançamentos confirmados.")
    {
        Confirmados = confirmados;
    }
}

public class LancamentoService
{
    private readonly AppDbContext _db;

    public LancamentoService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Lancamento> CriarReceitaAsync(
        Guid contaId, DateOnly data, decimal valor, Guid categoriaId, Guid? subcategoriaId, Guid? pessoaId,
        Guid? projetoId = null)
    {
        Validar(valor, data);
        await GarantirMesAbertoAsync(data, contaId);
        var lancamento = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Receita,
            Valor = Math.Abs(valor),
            ContaId = contaId,
            CategoriaId = categoriaId,
            SubcategoriaId = subcategoriaId,
            PessoaId = pessoaId,
            ProjetoId = projetoId
        };
        _db.Lancamentos.Add(lancamento);
        await _db.SaveChangesAsync();
        return lancamento;
    }

    public async Task<Lancamento> CriarDespesaAsync(
        Guid contaId, DateOnly data, decimal valor, Guid categoriaId, Guid? subcategoriaId, Guid? pessoaId,
        Guid? projetoId = null)
    {
        Validar(valor, data);
        await GarantirMesAbertoAsync(data, contaId);
        var lancamento = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Despesa,
            Valor = -Math.Abs(valor),
            ContaId = contaId,
            CategoriaId = categoriaId,
            SubcategoriaId = subcategoriaId,
            PessoaId = pessoaId,
            ProjetoId = projetoId
        };
        _db.Lancamentos.Add(lancamento);
        await _db.SaveChangesAsync();
        return lancamento;
    }

    public async Task<(Lancamento Origem, Lancamento Destino)> CriarTransferenciaAsync(
        Guid contaOrigemId, Guid contaDestinoId, DateOnly data, decimal valor)
    {
        Validar(valor, data);
        await GarantirMesAbertoAsync(data, contaOrigemId);
        var transferencia = await _db.Subcategorias.FirstOrDefaultAsync(s => s.IsProtected && s.Nome == "Transferência")
            ?? throw new InvalidOperationException("Categoria de transferência não encontrada no seed.");

        var referenciaId = Guid.NewGuid();
        var origem = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Transferencia,
            Valor = -Math.Abs(valor),
            ContaId = contaOrigemId,
            CategoriaId = transferencia.CategoriaId,
            SubcategoriaId = transferencia.Id,
            ReferenciaId = referenciaId
        };
        var destino = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Transferencia,
            Valor = Math.Abs(valor),
            ContaId = contaDestinoId,
            CategoriaId = transferencia.CategoriaId,
            SubcategoriaId = transferencia.Id,
            ReferenciaId = referenciaId
        };
        _db.Lancamentos.AddRange(origem, destino);
        await _db.SaveChangesAsync();
        return (origem, destino);
    }

    public async Task<Lancamento?> ObterAsync(Guid id)
        => await _db.Lancamentos.FindAsync(id);

    public async Task<bool> TemLancamentosFuturosNoGrupoAsync(Guid lancamentoId, Guid grupoId)
    {
        return await _db.Lancamentos.AnyAsync(l =>
            l.Id != lancamentoId
            && (l.ParcelamentoId == grupoId || l.RecorrenciaId == grupoId)
            && !l.Confirmado);
    }

    public async Task<List<Lancamento>> ObterPernasAsync(Guid id)
    {
        var lancamento = await _db.Lancamentos.FindAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        if (lancamento.ReferenciaId is null) return new List<Lancamento> { lancamento };

        return await _db.Lancamentos
            .Where(l => l.ReferenciaId == lancamento.ReferenciaId)
            .ToListAsync();
    }

    public async Task AtualizarReceitaDespesaAsync(
        Guid id, Guid contaId, DateOnly data, decimal valor, Guid categoriaId, Guid? subcategoriaId, Guid? pessoaId,
        Guid? projetoId = null, bool atualizarFuturos = false)
    {
        Validar(valor, data);
        var lancamento = await _db.Lancamentos.FindAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");

        await GarantirNaoConfirmadoAsync(new[] { lancamento });
        await GarantirMesAbertoAsync(lancamento.Data, lancamento.ContaId);
        await GarantirMesAbertoAsync(data, contaId);

        lancamento.Data = data;
        lancamento.Valor = lancamento.Tipo == LancamentoTipo.Despesa ? -Math.Abs(valor) : Math.Abs(valor);
        lancamento.ContaId = contaId;
        lancamento.CategoriaId = categoriaId;
        lancamento.SubcategoriaId = subcategoriaId;
        lancamento.PessoaId = pessoaId;
        lancamento.ProjetoId = projetoId;
        await GarantirFaturaAbertaAsync(lancamento);
        await AtualizarReembolsoAsync(lancamento, data, valor, pessoaId, projetoId);

        if (atualizarFuturos)
        {
            Guid? grupoId = lancamento.ParcelamentoId ?? lancamento.RecorrenciaId;
            if (grupoId.HasValue)
            {
                var futuros = await _db.Lancamentos
                    .Where(l => l.Id != lancamento.Id
                        && ((l.ParcelamentoId == grupoId) || (l.RecorrenciaId == grupoId))
                        && !l.Confirmado
                        && l.Data > lancamento.Data)
                    .ToListAsync();

                foreach (var f in futuros)
                {
                    f.Valor = f.Tipo == LancamentoTipo.Despesa ? -Math.Abs(valor) : Math.Abs(valor);
                    f.CategoriaId = categoriaId;
                    f.SubcategoriaId = subcategoriaId;
                    f.PessoaId = pessoaId;
                    f.ProjetoId = projetoId;
                    await AtualizarReembolsoAsync(f, f.Data, valor, pessoaId, projetoId);
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task AtualizarReembolsoAsync(Lancamento lancamento, DateOnly data, decimal valor, Guid? pessoaId, Guid? projetoId)
    {
        if (lancamento.ReembolsoId is null || lancamento.Confirmado) return;

        var reembolso = await _db.Lancamentos.FindAsync(lancamento.ReembolsoId.Value);
        if (reembolso is null || reembolso.Confirmado) return;

        await GarantirMesAbertoAsync(reembolso.Data, reembolso.ContaId);
        reembolso.Data = data;
        reembolso.Valor = Math.Abs(valor);
        reembolso.PessoaId = pessoaId;
        reembolso.ProjetoId = projetoId;
    }

    public async Task AtualizarTransferenciaAsync(
        Guid id, Guid contaOrigemId, Guid contaDestinoId, DateOnly data, decimal valor)
    {
        Validar(valor, data);
        var pernas = await ObterPernasAsync(id);
        if (pernas.Count != 2)
            throw new InvalidOperationException("Lançamento não é uma transferência.");
        await GarantirNaoPagamentoFaturaAsync(pernas);

        await GarantirNaoConfirmadoAsync(pernas);
        await GarantirMesAbertoAsync(pernas[0].Data, contaOrigemId);
        await GarantirMesAbertoAsync(pernas[1].Data, contaDestinoId);
        await GarantirMesAbertoAsync(data, contaOrigemId);
        await GarantirMesAbertoAsync(data, contaDestinoId);

        foreach (var perna in pernas)
        {
            perna.Data = data;
            perna.ContaId = perna.Valor < 0 ? contaOrigemId : contaDestinoId;
            perna.Valor = perna.Valor < 0 ? -Math.Abs(valor) : Math.Abs(valor);
        }
        foreach (var perna in pernas) await GarantirFaturaAbertaAsync(perna);
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var pernas = await ObterPernasAsync(id);
        await GarantirNaoPagamentoFaturaAsync(pernas);
        foreach (var perna in pernas) await GarantirFaturaAbertaAsync(perna);
        foreach (var perna in pernas) await GarantirMesAbertoAsync(perna.Data, perna.ContaId);
        await GarantirNaoConfirmadoAsync(pernas);

        var proventoIds = pernas.Select(p => p.Id).ToList();
        var proventos = await _db.InvestimentosProventos
            .Where(p => p.LancamentoId.HasValue && proventoIds.Contains(p.LancamentoId.Value))
            .ToListAsync();
        _db.InvestimentosProventos.RemoveRange(proventos);

        var reembolsoIds = pernas.Where(p => p.ReembolsoId.HasValue)
            .Select(p => p.ReembolsoId!.Value).ToList();
        var reembolsos = await _db.Lancamentos
            .Where(l => reembolsoIds.Contains(l.Id))
            .ToListAsync();
        foreach (var reembolso in reembolsos.Where(r => !r.Confirmado))
            await GarantirMesAbertoAsync(reembolso.Data, reembolso.ContaId);

        _db.Lancamentos.RemoveRange(pernas.Concat(reembolsos.Where(r => !r.Confirmado)));
        await _db.SaveChangesAsync();
    }

    public async Task AlternarConfirmadoAsync(Guid id)
    {
        var lancamento = await _db.Lancamentos.FindAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");
        if (!lancamento.Confirmado && lancamento.ContaId is null && lancamento.CartaoCreditoId is null)
            throw new InvalidOperationException(
                "Vincule uma conta ou um cartão antes de confirmar o lançamento.");
        await GarantirNaoPagamentoFaturaAsync(new[] { lancamento });
        await GarantirFaturaAbertaAsync(lancamento);
        await GarantirMesAbertoAsync(lancamento.Data, lancamento.ContaId);
        lancamento.Confirmado = !lancamento.Confirmado;
        await _db.SaveChangesAsync();
    }

    public async Task<List<Lancamento>> ListarAsync(
        Guid? contaId = null, LancamentoTipo? tipo = null, int? mes = null, int? ano = null,
        bool? confirmado = null, Guid? pessoaId = null, Guid? cartaoId = null)
    {
        var query = _db.Lancamentos
            .Include(l => l.Conta)
            .Include(l => l.Pessoa)
            .Include(l => l.Categoria)
            .Include(l => l.Subcategoria)
            .Include(l => l.CartaoCredito)
            .Include(l => l.Projeto)
            .AsQueryable();

        if (contaId.HasValue) query = query.Where(l => l.ContaId == contaId.Value);
        if (tipo.HasValue) query = query.Where(l => l.Tipo == tipo.Value);
        if (mes.HasValue) query = query.Where(l => l.Data.Month == mes.Value);
        if (ano.HasValue) query = query.Where(l => l.Data.Year == ano.Value);
        if (confirmado.HasValue) query = query.Where(l => l.Confirmado == confirmado.Value);
        if (pessoaId.HasValue) query = query.Where(l => l.PessoaId == pessoaId.Value);
        if (cartaoId.HasValue) query = query.Where(l => l.CartaoCreditoId == cartaoId.Value);

        return await query.OrderBy(l => l.Data).ToListAsync();
    }

    // ---------- Fase 4a ----------

    public async Task<List<Lancamento>> CriarDespesaCartaoAsync(
        Guid cartaoId, DateOnly dataCompra, decimal valorTotal, Guid categoriaId, Guid? subcategoriaId,
        Guid? pessoaId, int? parcelas, Guid? reembolsoPessoaId, DateOnly? reembolsoVencimento,
        DateOnly? vencimentoExato = null, Guid? reembolsoContaId = null, bool ehEntrada = false,
        Guid? projetoId = null, Guid? reembolsoCategoriaId = null, Guid? reembolsoSubcategoriaId = null)
    {
        Validar(valorTotal, dataCompra);
        if (ehEntrada && (parcelas is not null || reembolsoPessoaId is not null))
            throw new ArgumentException("Entrada na fatura não suporta parcelamento nem reembolso.");
        if (parcelas is < 1 or > 48)
            throw new ArgumentException("Quantidade de parcelas inválida.");

        var cartao = await _db.CartoesCredito.FindAsync(cartaoId)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        var quantidade = parcelas ?? 1;
        var grupoId = quantidade > 1 ? Guid.NewGuid() : (Guid?)null;
        var valores = DividirValor(valorTotal, quantidade);
        var renda = await _db.Categorias.AsNoTracking().SingleAsync(c => c.Nome == "Receita");

        var datasVencimento = new List<DateOnly>();
        var baseVencimento = vencimentoExato ?? CartaoCreditoService.CalcularVencimento(cartao, dataCompra);
        for (var i = 0; i < quantidade; i++)
        {
            datasVencimento.Add(baseVencimento.AddMonths(i));
        }

        foreach (var data in datasVencimento) await GarantirMesAbertoAsync(data, null);
        if (reembolsoPessoaId.HasValue)
            foreach (var i in Enumerable.Range(0, quantidade))
            {
                var vencimentoReembolso = reembolsoVencimento?.AddMonths(i) ?? datasVencimento[i].AddDays(-1);
                await GarantirMesAbertoAsync(vencimentoReembolso, reembolsoContaId);
            }

        var criados = new List<Lancamento>();
        for (var i = 0; i < quantidade; i++)
        {
            var dataVencimento = datasVencimento[i];
            var dataLancamento = dataCompra.AddMonths(i);
            await GarantirFaturaAbertaAsync(new Lancamento { CartaoCreditoId = cartaoId, Data = dataVencimento, DataVencimentoCartao = dataVencimento });
            var despesa = new Lancamento
            {
                Data = dataLancamento,
                DataCompra = dataCompra,
                DataVencimentoCartao = dataVencimento,
                Tipo = LancamentoTipo.Despesa,
                Valor = ehEntrada ? valores[i] : -valores[i],
                CartaoCreditoId = cartaoId,
                CategoriaId = categoriaId,
                SubcategoriaId = subcategoriaId,
                PessoaId = pessoaId,
                ParcelamentoId = grupoId,
                ParcelaAtual = quantidade > 1 ? i + 1 : null,
                TotalParcelas = quantidade > 1 ? quantidade : null,
                ProjetoId = projetoId
            };

            if (reembolsoPessoaId.HasValue)
            {
                var vencimentoReembolso = reembolsoVencimento?.AddMonths(i) ?? dataVencimento.AddDays(-1);
                var categoriaReembolsoId = reembolsoCategoriaId ?? renda.Id;
                var reembolso = new Lancamento
                {
                    Data = vencimentoReembolso,
                    Tipo = LancamentoTipo.Receita,
                    Valor = valores[i],
                    ContaId = reembolsoContaId,
                    CategoriaId = categoriaReembolsoId,
                    SubcategoriaId = reembolsoSubcategoriaId,
                    PessoaId = reembolsoPessoaId,
                    ProjetoId = projetoId,
                    ParcelaAtual = quantidade > 1 ? i + 1 : null,
                    TotalParcelas = quantidade > 1 ? quantidade : null
                };
                _db.Lancamentos.Add(reembolso);
                await _db.SaveChangesAsync();
                despesa.ReembolsoId = reembolso.Id;
                despesa.ReembolsoCategoriaId = reembolsoCategoriaId;
                despesa.ReembolsoSubcategoriaId = reembolsoSubcategoriaId;
            }

            _db.Lancamentos.Add(despesa);
            await _db.SaveChangesAsync();
            criados.Add(despesa);
        }

        return criados;
    }

    public async Task<List<Lancamento>> AtualizarDespesaCartaoAsync(
        Guid lancamentoId, Guid cartaoId, DateOnly dataCompra, decimal valorTotal,
        Guid categoriaId, Guid? subcategoriaId, Guid? pessoaId, int? parcelas,
        Guid? reembolsoPessoaId, DateOnly? reembolsoVencimento,
        DateOnly? vencimentoExato = null, Guid? reembolsoContaId = null, bool ehEntrada = false,
        Guid? projetoId = null, Guid? reembolsoCategoriaId = null, Guid? reembolsoSubcategoriaId = null)
    {
        var antigo = await _db.Lancamentos.FindAsync(lancamentoId)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");
        if (antigo.Tipo != LancamentoTipo.Despesa || antigo.CartaoCreditoId is null)
            throw new InvalidOperationException("Lançamento não é despesa de cartão.");

        if (ehEntrada && (parcelas is not null || reembolsoPessoaId is not null))
            throw new ArgumentException("Entrada na fatura não suporta parcelamento nem reembolso.");
        if (parcelas is < 1 or > 48)
            throw new ArgumentException("Quantidade de parcelas inválida.");

        var cartao = await _db.CartoesCredito.FindAsync(cartaoId)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        // Collect all lancamentos in the same parcelamento group to delete
        var lancamentosParaExcluir = new List<Lancamento>();
        if (antigo.ParcelamentoId.HasValue)
        {
            lancamentosParaExcluir = await _db.Lancamentos
                .Where(l => l.ParcelamentoId == antigo.ParcelamentoId.Value)
                .ToListAsync();
        }
        else
        {
            lancamentosParaExcluir.Add(antigo);
        }

        // Delete linked reimbursements first
        foreach (var l in lancamentosParaExcluir)
        {
            if (l.ReembolsoId.HasValue)
            {
                var reembolso = await _db.Lancamentos.FindAsync(l.ReembolsoId.Value);
                if (reembolso is not null)
                    _db.Lancamentos.Remove(reembolso);
            }
        }
        _db.Lancamentos.RemoveRange(lancamentosParaExcluir);
        await _db.SaveChangesAsync();

        // Create new installments with updated data
        return await CriarDespesaCartaoAsync(
            cartaoId, dataCompra, valorTotal, categoriaId, subcategoriaId,
            pessoaId, parcelas, reembolsoPessoaId, reembolsoVencimento,
            vencimentoExato, reembolsoContaId, ehEntrada, projetoId,
            reembolsoCategoriaId, reembolsoSubcategoriaId);
    }

    public async Task<List<Lancamento>> CriarParceladoAsync(
        LancamentoTipo tipo, Guid contaId, DateOnly primeiraData, decimal valorTotal, int parcelas,
        Guid categoriaId, Guid? subcategoriaId, Guid? pessoaId, Guid? projetoId = null)
    {
        Validar(valorTotal, primeiraData);
        if (parcelas < 1 || parcelas > 48)
            throw new ArgumentException("Quantidade de parcelas inválida.");

        var grupoId = parcelas > 1 ? Guid.NewGuid() : (Guid?)null;
        var valores = DividirValor(valorTotal, parcelas);
        var criados = new List<Lancamento>();

        for (var i = 0; i < parcelas; i++) await GarantirMesAbertoAsync(primeiraData.AddMonths(i), contaId);

        for (var i = 0; i < parcelas; i++)
        {
            var lancamento = new Lancamento
            {
                Data = primeiraData.AddMonths(i),
                Tipo = tipo,
                Valor = tipo == LancamentoTipo.Despesa ? -valores[i] : valores[i],
                ContaId = contaId,
                CategoriaId = categoriaId,
                SubcategoriaId = subcategoriaId,
                PessoaId = pessoaId,
                ParcelamentoId = grupoId,
                ParcelaAtual = parcelas > 1 ? i + 1 : null,
                TotalParcelas = parcelas > 1 ? parcelas : null,
                ProjetoId = projetoId
            };
            _db.Lancamentos.Add(lancamento);
            criados.Add(lancamento);
        }

        await _db.SaveChangesAsync();
        return criados;
    }

    public async Task<List<Lancamento>> CriarRecorrenteAsync(
        LancamentoTipo tipo, Guid contaId, DateOnly primeiraData, decimal valor,
        RecorrenciaFrequencia frequencia, int repeticoes, Guid categoriaId, Guid? subcategoriaId, Guid? pessoaId,
        Guid? projetoId = null)
    {
        Validar(valor, primeiraData);
        if (repeticoes < 1 || repeticoes > 120)
            throw new ArgumentException("Quantidade de repetições inválida.");

        var intervalo = frequencia switch
        {
            RecorrenciaFrequencia.Trimestral => 3,
            RecorrenciaFrequencia.Semestral => 6,
            RecorrenciaFrequencia.Anual => 12,
            _ => 1
        };
        var recorrenciaId = repeticoes > 1 ? Guid.NewGuid() : (Guid?)null;
        var criados = new List<Lancamento>();

        for (var i = 0; i < repeticoes; i++) await GarantirMesAbertoAsync(primeiraData.AddMonths(intervalo * i), contaId);

        for (var i = 0; i < repeticoes; i++)
        {
            var lancamento = new Lancamento
            {
                Data = primeiraData.AddMonths(intervalo * i),
                Tipo = tipo,
                Valor = tipo == LancamentoTipo.Despesa ? -Math.Abs(valor) : Math.Abs(valor),
                ContaId = contaId,
                CategoriaId = categoriaId,
                SubcategoriaId = subcategoriaId,
                PessoaId = pessoaId,
                RecorrenciaId = recorrenciaId,
                ParcelaAtual = repeticoes > 1 ? i + 1 : null,
                TotalParcelas = repeticoes > 1 ? repeticoes : null,
                ProjetoId = projetoId
            };
            _db.Lancamentos.Add(lancamento);
            criados.Add(lancamento);
        }

        await _db.SaveChangesAsync();
        return criados;
    }

    public async Task AtualizarValorAsync(Guid id, decimal valorPositivo)
    {
        var lancamento = await _db.Lancamentos.FindAsync(id)
            ?? throw new InvalidOperationException("Lançamento não encontrado.");
        if (valorPositivo <= 0)
            throw new ArgumentException("Informe um valor maior que zero.");

        await GarantirFaturaAbertaAsync(lancamento);
        await GarantirMesAbertoAsync(lancamento.Data, lancamento.ContaId);
        await GarantirNaoConfirmadoAsync(new[] { lancamento });

        lancamento.Valor = lancamento.Valor < 0
            ? -Math.Abs(valorPositivo)
            : Math.Abs(valorPositivo);
        await SincronizarReembolsoAsync(lancamento);
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirGrupoAsync(Guid grupoId, bool ehParcelamento)
    {
        var grupo = ehParcelamento
            ? await _db.Lancamentos.Where(l => l.ParcelamentoId == grupoId).ToListAsync()
            : await _db.Lancamentos.Where(l => l.RecorrenciaId == grupoId).ToListAsync();

        if (grupo.Count == 0)
            throw new InvalidOperationException("Grupo não encontrado.");

        foreach (var item in grupo) await GarantirFaturaAbertaAsync(item);
        foreach (var item in grupo) await GarantirMesAbertoAsync(item.Data, item.ContaId);

        var removiveis = grupo.Where(g => !g.Confirmado).ToList();
        if (removiveis.Count == 0)
            throw new LancamentoConfirmadoException(grupo);

        var reembolsoIds = removiveis.Where(g => g.ReembolsoId.HasValue)
            .Select(g => g.ReembolsoId!.Value).ToList();
        var reembolsos = await _db.Lancamentos
            .Where(l => reembolsoIds.Contains(l.Id))
            .ToListAsync();
        foreach (var reembolso in reembolsos.Where(r => !r.Confirmado))
            await GarantirMesAbertoAsync(reembolso.Data, reembolso.ContaId);

        _db.Lancamentos.RemoveRange(removiveis.Concat(reembolsos.Where(r => !r.Confirmado)));
        await _db.SaveChangesAsync();
    }

    private async Task<Guid?> SubcategoriaPagamentoIdAsync()
        => await _db.Subcategorias.AsNoTracking()
            .Where(s => s.IsProtected && s.Nome == "Cartão de crédito")
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync();

    private async Task GarantirNaoPagamentoFaturaAsync(IReadOnlyCollection<Lancamento> pernas)
    {
        var pagamentoId = await SubcategoriaPagamentoIdAsync();
        if (pagamentoId is not null &&
            pernas.Any(p => p.SubcategoriaId == pagamentoId && p.Tipo == LancamentoTipo.Transferencia))
            throw new InvalidOperationException(
                "Lançamento de pagamento de fatura só pode ser cancelado na fatura do cartão.");
    }

    private async Task GarantirFaturaAbertaAsync(Lancamento lancamento)
    {
        if (lancamento.CartaoCreditoId is null) return;

        DateOnly vencimento;
        if (lancamento.DataVencimentoCartao.HasValue)
        {
            vencimento = lancamento.DataVencimentoCartao.Value;
        }
        else
        {
            var dataRef = lancamento.Data;
            var cartao = await _db.CartoesCredito.FindAsync(lancamento.CartaoCreditoId);
            if (cartao is null) return;
            vencimento = CartaoCreditoService.CalcularVencimento(cartao, dataRef);
        }

        var fechada = await _db.Faturas.AnyAsync(f =>
            f.CartaoCreditoId == lancamento.CartaoCreditoId &&
            f.AnoReferencia == vencimento.Year &&
            f.MesReferencia == vencimento.Month &&
            f.Fechada);

        if (fechada)
            throw new InvalidOperationException("A fatura deste lançamento já está fechada.");
    }

    private async Task GarantirMesAbertoAsync(DateOnly data, Guid? contaId)
    {
        if (contaId is null) return;
        var fechado = await _db.MesesFechados.AnyAsync(m => m.ContaId == contaId.Value && m.Ano == data.Year && m.Mes == data.Month);
        if (fechado)
            throw new InvalidOperationException("Este mês está fechado para esta conta e não pode mais ser alterado.");
    }

    private async Task SincronizarReembolsoAsync(Lancamento despesa)
    {
        if (despesa.ReembolsoId is null) return;

        var reembolso = await _db.Lancamentos.FindAsync(despesa.ReembolsoId.Value);
        if (reembolso is null || reembolso.Confirmado) return;
        await GarantirMesAbertoAsync(reembolso.Data, reembolso.ContaId);

        reembolso.Valor = Math.Abs(despesa.Valor);
    }

    private static decimal[] DividirValor(decimal valorTotal, int parcelas)
    {
        var valorBase = Math.Round(valorTotal / parcelas, 2, MidpointRounding.AwayFromZero);
        var resultado = new decimal[parcelas];
        for (var i = 0; i < parcelas - 1; i++) resultado[i] = valorBase;
        resultado[parcelas - 1] = valorTotal - valorBase * (parcelas - 1);
        return resultado;
    }

    private static void Validar(decimal valor, DateOnly data)
    {
        if (data == default) throw new ArgumentException("Informe a data do lançamento.");
        if (valor <= 0) throw new ArgumentException("Informe um valor maior que zero.");
    }

    private Task GarantirNaoConfirmadoAsync(IEnumerable<Lancamento> pernas)
    {
        var confirmados = pernas.Where(p => p.Confirmado).ToList();
        if (confirmados.Count > 0)
            throw new LancamentoConfirmadoException(confirmados);
        return Task.CompletedTask;
    }
}
