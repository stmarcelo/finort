using Finort.Data;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class MySqlAppDbContextTests
{
    [Fact]
    public void MySqlAppDbContext_IsDbContextOfAppDbContext()
    {
        var context = new MySqlAppDbContext(
            new DbContextOptionsBuilder<MySqlAppDbContext>().Options);

        Assert.IsAssignableFrom<AppDbContext>(context);
    }
}
