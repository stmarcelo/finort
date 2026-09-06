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
            _logger.LogWarning(ex, "Migrate failed, attempting EnsureCreated...");
            try
            {
                _db.Database.EnsureCreated();
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Failed to apply database migrations");
                throw;
            }
        }
    }
}