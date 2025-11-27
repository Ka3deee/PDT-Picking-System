using Microsoft.Data.SqlClient;

namespace PDTPickingSystem.Helpers
{
    public static class DatabaseHelpers
    {
        /// <summary>
        /// Get the full name of the user from SQL Server by ID.
        /// </summary>
        public static string GetUserName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return string.Empty;

            try
            {
                if (AppGlobal.SqlCon == null)
                    return string.Empty;

                if (AppGlobal.SqlCon.State != System.Data.ConnectionState.Open)
                    AppGlobal.SqlCon.Open();

                using var sqlCmd = new SqlCommand(
                    "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName " +
                    "FROM tblUsers WHERE isActive=1 AND ID=@UserID", AppGlobal.SqlCon);

                sqlCmd.Parameters.AddWithValue("@UserID", userId);

                using var reader = sqlCmd.ExecuteReader();
                if (reader.Read())
                {
                    return reader["FullName"].ToString().Trim();
                }
            }
            catch
            {
                // Log or handle errors as needed
            }

            return string.Empty;
        }
    }
}
