namespace ClawOS.Infrastructure.Persistence;

public class DatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=ClawOS.sqlite";
    public string DatabaseName { get; set; } = "WedaTemplate";
}
