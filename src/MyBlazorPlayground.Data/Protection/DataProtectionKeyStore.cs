using Dapper;

namespace MyBlazorPlayground.Data.Protection;

public sealed class DataProtectionKeyStore(Database database)
{
    public IReadOnlyList<string> All()
    {
        using var connection = database.Open();
        return connection.Query<string>("SELECT xml FROM data_protection_key ORDER BY created_at").AsList();
    }

    public void Store(string friendlyName, string xml)
    {
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO data_protection_key (friendly_name, xml) VALUES (@friendlyName, @xml) ON CONFLICT (friendly_name) DO UPDATE SET xml = excluded.xml",
            new { friendlyName, xml });
    }
}
