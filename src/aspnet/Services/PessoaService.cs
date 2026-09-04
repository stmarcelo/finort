using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class PessoaService
{
    private readonly AppDbContext _db;

    public PessoaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Pessoa>> ListarAsync()
        => await _db.Pessoas.OrderBy(p => p.Nome).ToListAsync();

    public async Task<Pessoa?> ObterAsync(Guid id)
        => await _db.Pessoas.FindAsync(id);

    public async Task<Pessoa> CriarAsync(string nome, string? cor, string? observacao)
    {
        var pessoa = new Pessoa { Nome = nome, CorDeExibicao = cor, Observacao = observacao };
        _db.Pessoas.Add(pessoa);
        await _db.SaveChangesAsync();
        return pessoa;
    }

    public async Task AtualizarAsync(Pessoa pessoa, string nome, string? cor, string? observacao)
    {
        pessoa.Nome = nome;
        pessoa.CorDeExibicao = cor;
        pessoa.Observacao = observacao;
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var pessoa = await _db.Pessoas.FindAsync(id)
            ?? throw new InvalidOperationException("Pessoa não encontrada.");

        if (await _db.Lancamentos.AnyAsync(l => l.PessoaId == id))
            throw new InvalidOperationException("Esta pessoa possui lançamentos vinculados e não pode ser excluída.");

        if (await _db.Provisoes.AnyAsync(p => p.PessoaId == id))
            throw new InvalidOperationException("Esta pessoa possui provisões vinculadas e não pode ser excluída.");

        _db.Pessoas.Remove(pessoa);
        await _db.SaveChangesAsync();
    }
}
