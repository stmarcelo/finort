using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class ProjetoService
{
    private readonly AppDbContext _db;

    public ProjetoService(AppDbContext db) => _db = db;

    public Task<List<Projeto>> ListarAsync()
        => _db.Projetos.Include(p => p.Pessoa)
            .OrderByDescending(p => p.DataContratacao).ToListAsync();

    /// <summary>Projetos ativos (não concluídos) para seleção em lançamentos.</summary>
    public Task<List<Projeto>> ListarAtivosAsync()
        => _db.Projetos.Where(p => !p.Concluido)
            .OrderByDescending(p => p.DataContratacao).ToListAsync();

    public Task<List<Projeto>> ListarUltimosAsync(int quantidade)
        => _db.Projetos.OrderByDescending(p => p.DataContratacao)
            .Take(quantidade).ToListAsync();

    public Task<List<Projeto>> ListarPorPessoaAsync(Guid pessoaId)
        => _db.Projetos.Where(p => p.PessoaId == pessoaId)
            .OrderByDescending(p => p.DataContratacao).ToListAsync();

    /// <summary>Projetos ativos (não concluídos) da pessoa, para seleção em lançamentos.</summary>
    public Task<List<Projeto>> ListarAtivosPorPessoaAsync(Guid pessoaId)
        => _db.Projetos.Where(p => p.PessoaId == pessoaId && !p.Concluido)
            .OrderByDescending(p => p.DataContratacao).ToListAsync();

    public Task<int> ContarPorPessoaAsync(Guid pessoaId)
        => _db.Projetos.CountAsync(p => p.PessoaId == pessoaId);

    public Task<Projeto?> ObterAsync(Guid id)
        => _db.Projetos.FindAsync(id).AsTask();

    public async Task<Projeto> CriarAsync(string descricao, DateOnly data, decimal valor, Guid pessoaId)
    {
        var projeto = new Projeto { Descricao = descricao, DataContratacao = data, ValorContratado = valor, PessoaId = pessoaId };
        _db.Projetos.Add(projeto);
        await _db.SaveChangesAsync();
        return projeto;
    }

    public async Task AtualizarAsync(Projeto projeto, string descricao, DateOnly data, decimal valor, Guid pessoaId)
    {
        projeto.Descricao = descricao;
        projeto.DataContratacao = data;
        projeto.ValorContratado = valor;
        projeto.PessoaId = pessoaId;
        await _db.SaveChangesAsync();
    }

    public async Task MarcarConcluidoAsync(Guid id, DateOnly dataConclusao)
    {
        var projeto = await _db.Projetos.FindAsync(id)
            ?? throw new InvalidOperationException("Projeto não encontrado.");

        projeto.Concluido = true;
        projeto.DataConclusao = dataConclusao;
        await _db.SaveChangesAsync();
    }

    public async Task ReabrirAsync(Guid id)
    {
        var projeto = await _db.Projetos.FindAsync(id)
            ?? throw new InvalidOperationException("Projeto não encontrado.");

        projeto.Concluido = false;
        projeto.DataConclusao = null;
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var projeto = await _db.Projetos.FindAsync(id)
            ?? throw new InvalidOperationException("Projeto não encontrado.");

        if (await _db.Lancamentos.AnyAsync(l => l.ProjetoId == id))
            throw new InvalidOperationException("Este projeto possui lançamentos vinculados e não pode ser excluído.");

        _db.Projetos.Remove(projeto);
        await _db.SaveChangesAsync();
    }
}
