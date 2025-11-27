using Microsoft.Data.SqlClient;
using System;

namespace PDTPickingSystem.Services
{
    public class DatabaseService
    {
        private SqlConnection? _connection;

        public bool ConnectSql(string server = "10.6.0.18", string user = "sa", string pass = "sa", string database = "dbPicking3")
        {
            try
            {
                string connStr = $"Server={server};Database={database};User Id={user};Password={pass};TrustServerCertificate=True;";
                _connection = new SqlConnection(connStr);
                _connection.Open();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SQL connection error: {ex.Message}");
                return false;
            }
        }

        public SqlConnection? Connection => _connection;
    }
}
