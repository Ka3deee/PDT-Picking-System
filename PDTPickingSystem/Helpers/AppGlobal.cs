using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using AEnvironment = Android.OS.Environment;   // <--- Fix for ambiguous Environment

namespace PDTPickingSystem.Helpers
{
    public static class AppGlobal
    {
        // ------------------------------
        // System version
        // ------------------------------
        public const string SysVersion = "2019.05.03";

        //Set Ref Info
        public static ImageSource MenuSignalImage { get; set; }

        // ------------------------------
        // User info
        // ------------------------------
        public static string UserID { get; set; } = "";
        public static string UserName { get; set; } = "";
        public static int ID_User { get; set; } = 0;
        public static long ID_SumHdr { get; set; } = 0;
        public static bool IsStocker { get; set; } = false;
        public static bool IsChecker { get; set; } = false;

        // ------------------------------
        // NEW: Store / Department info
        // ------------------------------
        public static string DeptStore { get; set; } = "";   // <--- ADDED

        // ------------------------------
        // Server config
        // ------------------------------
        public static string Server { get; set; } = "";
        public static string SqlUser { get; private set; } = "sa";
        public static string SqlPass { get; private set; } = "sa";

        // ------------------------------
        // SQL Connection
        // ------------------------------
        public static SqlConnection SqlCon { get; private set; }

        public static string ConnectionString =>
            $"Server={Server};Database=dbPicking3;User Id={SqlUser};Password={SqlPass};Encrypt=False;TrustServerCertificate=True;Connect Timeout=3;";

        // ------------------------------
        // Picking info
        // ------------------------------
        public static string PickNo { get; set; } = "";
        public static bool IsLoaded { get; set; } = false;

        // ------------------------------
        // SQL CONNECTION
        // ------------------------------
        public static async Task<bool> ConnectSqlAsync()
        {
            try
            {
                if (SqlCon != null && SqlCon.State == ConnectionState.Open)
                    return true;

                SqlCon = new SqlConnection(ConnectionString);

                var openTask = SqlCon.OpenAsync();
                var completed = await Task.WhenAny(openTask, Task.Delay(4000));

                if (completed != openTask)
                {
                    SqlCon.Dispose();
                    SqlCon = null;
                    return false;
                }

                await openTask;
                return true;
            }
            catch
            {
                SqlCon = null;
                return false;
            }
        }

        // ------------------------------
        // USER LOGIN
        // ------------------------------
        public static async Task<bool> LoadUserInfoAsync(string userId)
        {
            if (!await ConnectSqlAsync()) return false;

            try
            {
                string sql =
                    "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName " +
                    "FROM tblUsers WHERE EENo=@ID AND isActive=1";

                using var cmd = new SqlCommand(sql, SqlCon);
                cmd.Parameters.AddWithValue("@ID", userId);

                using var r = await cmd.ExecuteReaderAsync();
                if (!r.Read()) return false;

                ID_User = Convert.ToInt32(r["ID"]);
                UserID = userId;
                UserName = r["FullName"].ToString() ?? "";

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ======================================================================
        //  STORAGE PATH (ANDROID + FALLBACK)
        // ======================================================================

        public static string GetExternalBackupFolder()
        {
#if ANDROID
            try
            {
                string root = "/storage/emulated/0/";

                string full = Path.Combine(
                    root,
                    "Android", "data",
                    "com.companyname.pdtpickingsystem",
                    "Backup", "PDTPicking"
                );

                if (!Directory.Exists(full))
                    Directory.CreateDirectory(full);

                return full;
            }
            catch
            {
                // Fall through to internal storage
            }
#endif
            string internalPath = Path.Combine(FileSystem.AppDataDirectory, "Backup", "PDTPicking");
            if (!Directory.Exists(internalPath))
                Directory.CreateDirectory(internalPath);

            return internalPath;
        }

        private static string ConfigFile => Path.Combine(GetExternalBackupFolder(), "wifi.txt");

        // ======================================================================
        //  LOAD / SAVE SERVER CONFIG
        // ======================================================================
        public static async Task LoadServerConfigAsync()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    await File.WriteAllLinesAsync(ConfigFile, new[]
                    {
                        Server,
                        SqlUser,
                        SqlPass
                    });
                }

                var lines = await File.ReadAllLinesAsync(ConfigFile);
                if (lines.Length >= 1)
                    Server = lines[0].Trim();
            }
            catch
            {
                // ignore
            }
        }

        public static async Task SaveServerConfigAsync()
        {
            try
            {
                await File.WriteAllLinesAsync(ConfigFile, new[]
                {
                    Server,
                    SqlUser,
                    SqlPass
                });
            }
            catch
            {
                // ignore failures
            }
        }

        // ======================================================================
        // HELPER FUNCTIONS
        // ======================================================================
        public static string FixNull(object field) =>
            field == null || field == DBNull.Value ? "" : field.ToString().Trim();

        public static string FixString(string s) =>
            s?.Replace("'", "''").Trim() ?? "";

        public static void SetUser(Label lbl)
        {
            lbl.Text = string.IsNullOrEmpty(UserName)
                ? "Please set USER ID!... "
                : $"User: {UserName}";
        }
    }
}
