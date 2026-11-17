using Finort.Data;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class ProjetoPrazoServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly ProjetoService _svc;

    public ProjetoPrazoServiceTests()
    {
        _svc = new ProjetoService(_ctx.Db);
        _ctx.Db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "João" });
        _ctx.Db.SaveChanges();
    }

    private Guid PessoaId() => _ctx.Db.Pessoas.First(p => p.Nome == "João").Id;

    [Fact]
    public async Task Criar_ComPrazo_PersistePrazo()
    {
        var projeto = await _svc.CriarAsync("Site", new(2026, 5, 10), 1000m, PessoaId(), new(2026, 11, 30));

        var gravado = await _ctx.Db.Projetos.AsNoTracking().FirstAsync(p => p.Id == projeto.Id);
        Assert.Equal(new DateOnly(2026, 11, 30), gravado.Prazo);
    }

    [Fact]
    public async Task Criar_SemPrazo_PermiteNulo()
    {
        var projeto = await _svc.CriarAsync("Site", new(2026, 5, 10), 1000m, PessoaId(), null);

        var gravado = await _ctx.Db.Projetos.AsNoTracking().FirstAsync(p => p.Id == projeto.Id);
        Assert.Null(gravado.Prazo);
    }

    [Fact]
    public async Task Atualizar_ComPrazo_AtualizaPrazo()
    {
        var projeto = await _svc.CriarAsync("Site", new(2026, 5, 10), 1000m, PessoaId(), null);

        await _svc.AtualizarAsync(projeto, "Site v2", new(2026, 5, 10), 1200m, PessoaId(), new(2026, 12, 15));

        var gravado = await _ctx.Db.Projetos.AsNoTracking().FirstAsync(p => p.Id == projeto.Id);
        Assert.Equal(new DateOnly(2026, 12, 15), gravado.Prazo);
    }

    [Fact]
    public async Task Atualizar_SemPrazo_LimpaPrazo()
    {
        var projeto = await _svc.CriarAsync("Site", new(2026, 5, 10), 1000m, PessoaId(), new(2026, 11, 30));

        await _svc.AtualizarAsync(projeto, "Site", new(2026, 5, 10), 1000m, PessoaId(), null);

        var gravado = await _ctx.Db.Projetos.AsNoTracking().FirstAsync(p => p.Id == projeto.Id);
        Assert.Null(gravado.Prazo);
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
