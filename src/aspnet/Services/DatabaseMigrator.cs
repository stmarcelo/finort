using Finort.Data;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class DatabaseMigrator
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(AppDbContext db, ILogger<DatabaseMigrator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void Migrate()
    {
        try
        {
            _logger.LogInformation("Applying database migrations...");
            _db.Database.Migrate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            throw;
        }
    }
}