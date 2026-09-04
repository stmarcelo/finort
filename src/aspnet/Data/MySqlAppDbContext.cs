using Microsoft.EntityFrameworkCore;

namespace Finort.Data;

public class MySqlAppDbContext : AppDbContext
{
    public MySqlAppDbContext(DbContextOptions<MySqlAppDbContext> options) : base(options) { }
}
