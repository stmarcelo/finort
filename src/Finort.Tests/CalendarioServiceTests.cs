using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class CalendarioServiceTests
{
    private static Guid CategoriaId(AppDbContext db)
        => db.Categorias.First(c => c.Nome == "Contas de casa").Id;

    private static Provisao NovaProvisaoMensal(AppDbContext db, int dia, decimal valor)
        => new()
        {
            Onde = ProvisaoOnde.DebitoConta,
            Frequencia = ProvisaoFrequencia.Mensal,
            Dia = dia,
            Valor = valor,
            ValorVariante = false,
            CategoriaId = db.Categorias.First(c => c.Nome == "Contas de casa").Id
        };

    [Fact]
    public async Task ObterMesAsync_ComLancamentosNoMes_AgrupaPorDia()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cat = CategoriaId(db);
            db.Lancamentos.AddRange(
                new Lancamento { Data = inicioMes.AddDays(4), Valor = -100m, Tipo = LancamentoTipo.Despesa, CategoriaId = cat },
                new Lancamento { Data = inicioMes.AddDays(4), Valor = 50m, Tipo = LancamentoTipo.Receita, CategoriaId = cat },
                new Lancamento { Data = inicioMes.AddDays(11), Valor = -30m, Tipo = LancamentoTipo.Despesa, CategoriaId = cat, Confirmado = true });
            await db.SaveChangesAsync();
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(hoje.Year, hoje.Month);

            Assert.Equal(2, resultado.Dias.Count);
            var dia5 = Assert.Single(resultado.Dias, d => d.Data == inicioMes.AddDays(4));
            Assert.Equal(2, dia5.Itens.Count);
            Assert.Contains(dia5.Itens, i => i.Valor == -100m && !i.Projetada && !i.Confirmado);
            Assert.Contains(dia5.Itens, i => i.Valor == 50m && i.Tipo == LancamentoTipo.Receita);
            var dia12 = Assert.Single(resultado.Dias, d => d.Data == inicioMes.AddDays(11));
            Assert.True(dia12.Itens.Single().Confirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_SemLancamentos_RetornaMesVazioComZeros()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var proximo = DateOnly.FromDateTime(DateTime.Today).AddMonths(1);
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(proximo.Year, proximo.Month);

            Assert.Empty(resultado.Dias);
            Assert.Equal(0m, resultado.TotalApagar);
            Assert.Equal(0m, resultado.TotalAreceber);
            Assert.Equal(0m, resultado.SaldoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_ProvisaoMensalNaoLancada_GeraItemProjetado()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.Provisoes.Add(NovaProvisaoMensal(db, dia: 31, valor: 200m));
            await db.SaveChangesAsync();
            await new ProvisaoService(db).SincronizarAsync(); // materializa mês corrente e marca Ultimo
            var alvo = hoje.AddMonths(1);
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(alvo.Year, alvo.Month);

            var dia = Assert.Single(resultado.Dias);
            Assert.Equal(DateTime.DaysInMonth(alvo.Year, alvo.Month), dia.Data.Day); // Dia 31 clampado
            var item = Assert.Single(dia.Itens);
            Assert.True(item.Projetada);
            Assert.Null(item.LancamentoId);
            Assert.Equal(-200m, item.Valor);
            Assert.Equal(LancamentoTipo.Despesa, item.Tipo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_ProvisaoJaLancada_NaoProjetaDuplicado()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.Provisoes.Add(NovaProvisaoMensal(db, dia: 10, valor: 200m));
            await db.SaveChangesAsync();
            await new ProvisaoService(db).SincronizarAsync();
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(hoje.Year, hoje.Month);

            Assert.All(resultado.Dias.SelectMany(d => d.Itens), i => Assert.False(i.Projetada));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_MesFechado_NaoProjeta()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.Provisoes.Add(NovaProvisaoMensal(db, dia: 10, valor: 200m));
            await db.SaveChangesAsync();
            await new ProvisaoService(db).SincronizarAsync();
            var alvo = hoje.AddMonths(1);
            db.MesesFechados.Add(new MesFechado { Ano = alvo.Year, Mes = alvo.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(alvo.Year, alvo.Month);

            Assert.All(resultado.Dias.SelectMany(d => d.Itens), i => Assert.False(i.Projetada));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_ProvisaoNuncaSincronizada_NaoProjeta()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.Provisoes.Add(NovaProvisaoMensal(db, dia: 10, valor: 200m));
            await db.SaveChangesAsync();
            // Sem SincronizarAsync: UltimoMes/AnoLancado permanecem nulos.
            var alvo = hoje.AddMonths(1);
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(alvo.Year, alvo.Month);

            Assert.Empty(resultado.Dias);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_ProvisaoTrimestral_AlinhaIntervalo()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var anoHoje = DateTime.Today.Year;
            var provisao = NovaProvisaoMensal(db, dia: 10, valor: 300m);
            provisao.Frequencia = ProvisaoFrequencia.Trimestral;
            provisao.UltimoMesLancado = 1;
            provisao.UltimoAnoLancado = anoHoje;
            db.Provisoes.Add(provisao);
            await db.SaveChangesAsync();
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var abril = await service.ObterMesAsync(anoHoje, 4);   // delta 3 => projeta
            var maio = await service.ObterMesAsync(anoHoje, 5);    // delta 4 => não projeta

            Assert.All(abril.Dias.SelectMany(d => d.Itens), i => Assert.True(i.Projetada));
            Assert.All(maio.Dias.SelectMany(d => d.Itens), i => Assert.False(i.Projetada));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_Totais_IncluemReaisEProjetadas()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cat = CategoriaId(db);
            var alvo = hoje.AddMonths(1);
            var dataAlvo = new DateOnly(alvo.Year, alvo.Month, Math.Min(20, DateTime.DaysInMonth(alvo.Year, alvo.Month)));
            db.Lancamentos.Add(new Lancamento
            {
                Data = dataAlvo.AddDays(-(dataAlvo.Day - 1)),
                Valor = -100m,
                Tipo = LancamentoTipo.Despesa,
                CategoriaId = cat
            });
            db.Provisoes.Add(new Provisao
            {
                Onde = ProvisaoOnde.Receita,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 20,
                Valor = 250m,
                ValorVariante = false,
                CategoriaId = cat
            });
            await db.SaveChangesAsync();
            await new ProvisaoService(db).SincronizarAsync(); // lança só mês corrente; mês alvo fica projetado
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(alvo.Year, alvo.Month);

            Assert.Equal(100m, resultado.TotalApagar);
            Assert.Equal(250m, resultado.TotalAreceber);
            Assert.Equal(150m, resultado.SaldoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_LancamentoDeCartao_NaoAparece()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cartao = new CartaoCredito
            {
                Banco = "T", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 1000m, Ativo = true
            };
            db.CartoesCredito.Add(cartao);
            db.Lancamentos.Add(new Lancamento
            {
                Data = inicioMes.AddDays(4),
                Valor = -80m,
                Tipo = LancamentoTipo.Despesa,
                CategoriaId = CategoriaId(db),
                CartaoCreditoId = cartao.Id
            });
            db.Lancamentos.Add(new Lancamento { Data = inicioMes.AddDays(4), Valor = -30m, Tipo = LancamentoTipo.Despesa, CategoriaId = CategoriaId(db) });
            await db.SaveChangesAsync();
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(hoje.Year, hoje.Month);

            var item = Assert.Single(resultado.Dias.SelectMany(d => d.Itens));
            Assert.Equal(-30m, item.Valor);
            Assert.Equal(30m, resultado.TotalApagar);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_EntradaDeCartao_NaoAparece()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var inicioMes = new DateOnly(hoje.Year, hoje.Month, 1);
            var cartao = new CartaoCredito
            {
                Banco = "T", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 1000m, Ativo = true
            };
            db.CartoesCredito.Add(cartao);
            db.Lancamentos.Add(new Lancamento
            {
                Data = inicioMes.AddDays(4),
                Valor = 50m,
                Tipo = LancamentoTipo.Despesa,
                CategoriaId = CategoriaId(db),
                CartaoCreditoId = cartao.Id
            });
            db.Lancamentos.Add(new Lancamento { Data = inicioMes.AddDays(4), Valor = 45m, Tipo = LancamentoTipo.Receita, CategoriaId = CategoriaId(db) });
            await db.SaveChangesAsync();
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(hoje.Year, hoje.Month);

            var item = Assert.Single(resultado.Dias.SelectMany(d => d.Itens));
            Assert.Equal(45m, item.Valor);
            Assert.Equal(LancamentoTipo.Receita, item.Tipo);
            Assert.Equal(45m, resultado.TotalAreceber);
            Assert.Equal(0m, resultado.TotalApagar);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterMesAsync_ProvisaoDebitoCartao_NaoProjeta()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var cartao = new CartaoCredito
            {
                Banco = "T", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 1000m, Ativo = true
            };
            db.CartoesCredito.Add(cartao);
            var provisaoCartao = NovaProvisaoMensal(db, dia: 10, valor: 200m);
            provisaoCartao.Onde = ProvisaoOnde.DebitoCartao;
            provisaoCartao.CartaoCreditoId = cartao.Id;
            db.Provisoes.Add(provisaoCartao);
            db.Provisoes.Add(NovaProvisaoMensal(db, dia: 12, valor: 90m)); // DébitoConta: deve projetar
            await db.SaveChangesAsync();
            await new ProvisaoService(db).SincronizarAsync();
            var alvo = hoje.AddMonths(1);
            var service = new CalendarioService(db, new FaturaService(db), new LembreteService(db));

            var resultado = await service.ObterMesAsync(alvo.Year, alvo.Month);

            var itens = resultado.Dias.SelectMany(d => d.Itens).ToList();
            // A compra do sincronismo vira compromisso de fatura (informativo), nunca projeção de provisão de cartão.
            Assert.Contains(itens, i => i.Descricao.StartsWith("Fatura T") && !i.Projetada);
            var projetada = Assert.Single(itens.Where(i => i.Projetada));
            Assert.Equal(-90m, projetada.Valor);
            Assert.DoesNotContain(itens, i => i.Projetada && i.Valor == -200m);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
