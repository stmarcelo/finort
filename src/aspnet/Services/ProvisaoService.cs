using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class ProvisaoService
{
    private readonly AppDbContext _db;

    public ProvisaoService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Provisao>> ListarAsync()
        => await _db.Provisoes
            .Include(p => p.Pessoa)
            .Include(p => p.Categoria)
            .Include(p => p.Subcategoria)
            .Include(p => p.Conta)
            .Include(p => p.CartaoCredito)
            .OrderBy(p => p.Dia)
            .ToListAsync();

    public async Task<Provisao?> ObterAsync(Guid id)
        => await _db.Provisoes.FindAsync(id);

    public async Task<Provisao> CriarAsync(Provisao provisao, bool lancarMesCorrente)
    {
        if (provisao.Valor <= 0)
            throw new ArgumentException("Informe um valor maior que zero.");
        if (provisao.Dia is < 1 or > 31)
            throw new ArgumentException("Dia deve estar entre 1 e 31.");

        _db.Provisoes.Add(provisao);
        await _db.SaveChangesAsync();

        if (lancarMesCorrente)
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            if (!await EstaFechadoAsync(provisao.ContaId, hoje.Year, hoje.Month))
                await LancarAsync(provisao, hoje.Year, hoje.Month);

            var proximoMes = hoje.AddMonths(1);
            if (!await EstaFechadoAsync(provisao.ContaId, proximoMes.Year, proximoMes.Month))
                await LancarAsync(provisao, proximoMes.Year, proximoMes.Month);

            provisao.UltimoMesLancado = proximoMes.Month;
            provisao.UltimoAnoLancado = proximoMes.Year;
            await _db.SaveChangesAsync();
        }

        return provisao;
    }

    public async Task AtualizarAsync(Provisao alvo, Provisao dados)
    {
        alvo.Onde = dados.Onde;
        alvo.Frequencia = dados.Frequencia;
        alvo.Dia = dados.Dia;
        alvo.PessoaId = dados.PessoaId;
        alvo.ContaId = dados.ContaId;
        alvo.CartaoCreditoId = dados.CartaoCreditoId;
        alvo.Valor = dados.Valor;
        alvo.ValorVariante = dados.ValorVariante;
        alvo.CategoriaId = dados.CategoriaId;
        alvo.SubcategoriaId = dados.SubcategoriaId;
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var provisao = await _db.Provisoes.FindAsync(id)
            ?? throw new InvalidOperationException("Provisão não encontrada.");
        _db.Provisoes.Remove(provisao);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lança as provisões nos meses abertos até o mês corrente. Retorna nº criados.</summary>
    public async Task<int> SincronizarAsync()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var provisoes = await _db.Provisoes.ToListAsync();
        var criados = 0;

        foreach (var provisao in provisoes)
        {
            if (provisao.Frequencia == ProvisaoFrequencia.Mensal)
            {
                var mesCorrente = new DateOnly(hoje.Year, hoje.Month, 1);
                if (provisao.UltimoAnoLancado is not null && provisao.UltimoMesLancado is not null)
                {
                    var ultimoLancamento = new DateOnly(
                        provisao.UltimoAnoLancado.Value, provisao.UltimoMesLancado.Value, 1);
                    if (ultimoLancamento > mesCorrente)
                        continue;

                    if (ultimoLancamento == mesCorrente)
                    {
                        var proximoMesJaLancado = hoje.AddMonths(1);
                        if (!await EstaFechadoAsync(provisao.ContaId, proximoMesJaLancado.Year, proximoMesJaLancado.Month))
                            criados += await LancarAsync(provisao, proximoMesJaLancado.Year, proximoMesJaLancado.Month);

                        provisao.UltimoMesLancado = proximoMesJaLancado.Month;
                        provisao.UltimoAnoLancado = proximoMesJaLancado.Year;
                        continue;
                    }
                }

                if (!await EstaFechadoAsync(provisao.ContaId, hoje.Year, hoje.Month))
                    criados += await LancarAsync(provisao, hoje.Year, hoje.Month);

                var proximoMesMensal = hoje.AddMonths(1);
                if (!await EstaFechadoAsync(provisao.ContaId, proximoMesMensal.Year, proximoMesMensal.Month))
                    criados += await LancarAsync(provisao, proximoMesMensal.Year, proximoMesMensal.Month);

                provisao.UltimoMesLancado = proximoMesMensal.Month;
                provisao.UltimoAnoLancado = proximoMesMensal.Year;
                continue;
            }

            if (provisao.UltimoAnoLancado is null || provisao.UltimoMesLancado is null)
            {
                if (!await EstaFechadoAsync(provisao.ContaId, hoje.Year, hoje.Month))
                    criados += await LancarAsync(provisao, hoje.Year, hoje.Month);

                var proximoMes = hoje.AddMonths(1);
                if (!await EstaFechadoAsync(provisao.ContaId, proximoMes.Year, proximoMes.Month))
                    criados += await LancarAsync(provisao, proximoMes.Year, proximoMes.Month);

                provisao.UltimoMesLancado = proximoMes.Month;
                provisao.UltimoAnoLancado = proximoMes.Year;
                continue;
            }

            var intervalo = ProvisaoAgenda.IntervaloEmMeses(provisao.Frequencia);
            var atual = new DateOnly(provisao.UltimoAnoLancado.Value, provisao.UltimoMesLancado.Value, 1)
                .AddMonths(intervalo);
            var limite = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1);

            while (atual <= limite)
            {
                if (!await EstaFechadoAsync(provisao.ContaId, atual.Year, atual.Month))
                    criados += await LancarAsync(provisao, atual.Year, atual.Month);

                provisao.UltimoMesLancado = atual.Month;
                provisao.UltimoAnoLancado = atual.Year;
                atual = atual.AddMonths(intervalo);
            }
        }

        await _db.SaveChangesAsync();
        return criados;
    }

    private async Task<int> LancarAsync(Provisao provisao, int ano, int mes)
    {
        var cartao = provisao.CartaoCreditoId.HasValue
            ? await _db.CartoesCredito.FindAsync(provisao.CartaoCreditoId.Value)
            : null;

        if (provisao.Onde == ProvisaoOnde.DebitoCartao && provisao.CartaoCreditoId.HasValue)
        {
            var faturaFechada = await _db.Faturas.AnyAsync(f =>
                f.CartaoCreditoId == provisao.CartaoCreditoId.Value &&
                f.AnoReferencia == ano &&
                f.MesReferencia == mes &&
                f.Fechada);
            if (faturaFechada)
                return 0;
        }

        var ultimoDia = DateTime.DaysInMonth(ano, mes);
        var data = new DateOnly(ano, mes, Math.Min(provisao.Dia, ultimoDia));

        if (provisao.Onde == ProvisaoOnde.DebitoCartao && provisao.CartaoCreditoId.HasValue)
        {
            var duplicado = await _db.Lancamentos.AnyAsync(l =>
                l.CartaoCreditoId == provisao.CartaoCreditoId.Value &&
                l.Data == data &&
                l.CategoriaId == provisao.CategoriaId &&
                l.SubcategoriaId == provisao.SubcategoriaId &&
                l.PessoaId == provisao.PessoaId &&
                l.Valor == -provisao.Valor);
            if (duplicado)
                return 0;
        }

        var lancamento = provisao.Onde switch
        {
            ProvisaoOnde.Receita => new Lancamento
            {
                Data = data,
                Tipo = LancamentoTipo.Receita,
                Valor = provisao.Valor,
                ContaId = provisao.ContaId
            },
            ProvisaoOnde.DebitoConta => new Lancamento
            {
                Data = data,
                Tipo = LancamentoTipo.Despesa,
                Valor = -provisao.Valor,
                ContaId = provisao.ContaId
            },
            ProvisaoOnde.DebitoCartao => new Lancamento
            {
                Data = data,
                Tipo = LancamentoTipo.Despesa,
                Valor = -provisao.Valor,
                CartaoCreditoId = provisao.CartaoCreditoId
            },
            ProvisaoOnde.DebitoSemConta => new Lancamento
            {
                Data = data,
                Tipo = LancamentoTipo.Despesa,
                Valor = -provisao.Valor
            },
            _ => throw new InvalidOperationException("Origem de provisão desconhecida.")
        };

        if (lancamento.CartaoCreditoId.HasValue && cartao is not null)
            lancamento.DataVencimentoCartao = new DateOnly(ano, mes,
                Math.Min(cartao.DiaVencimento, ultimoDia));

        lancamento.CategoriaId = provisao.CategoriaId;
        lancamento.SubcategoriaId = provisao.SubcategoriaId;
        lancamento.PessoaId = provisao.PessoaId;
        lancamento.ProvisaoId = provisao.Id;

        _db.Lancamentos.Add(lancamento);
        await _db.SaveChangesAsync();
        return 1;
    }

    private async Task<bool> EstaFechadoAsync(Guid? contaId, int ano, int mes)
    {
        if (contaId is null) return false;
        return await _db.MesesFechados.AnyAsync(m => m.ContaId == contaId.Value && m.Ano == ano && m.Mes == mes);
    }
}
