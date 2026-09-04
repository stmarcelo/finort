namespace Finort.Models.Configuration;

public class DatabaseConfig
{
    public string Provider { get; set; } = "Sqlite";
    public DatabaseConnectionSettings Sqlite { get; set; } = new();
    public DatabaseConnectionSettings MySql { get; set; } = new();
}

public class DatabaseConnectionSettings
{
    public string ConnectionString { get; set; } = "";
}