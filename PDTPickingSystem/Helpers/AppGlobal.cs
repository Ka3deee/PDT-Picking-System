using Microsoft.Data.SqlClient;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using PDTPickingSystem.Helpers.Interfaces;
using AEnvironment = Android.OS.Environment;

namespace PDTPickingSystem.Helpers
{
    public static class AppGlobal
    {
        // ------------------------------
        // System info
        // ------------------------------
        public const string sysVersion = "2025.10.15";

        public static string PDTName =>
            System.Net.Dns.GetHostName().ToUpper().Trim();

        // ------------------------------
        // Formatting
        // ------------------------------
        public const string fmtNumber1 = "0.00";
        public const string fmtNumber2 = "0";
        public const string fmtTimeDate = "yy/MM/dd HH:mm:ss";


        // ------------------------------
        // User info
        // ------------------------------
        public static string sEENo { get; set; } = "";
        public static string sUserName { get; set; } = "";
        public static int ID_User { get; set; } = 0;
        public static long ID_SumHdr { get; set; } = 0;

        public static int isStocker { get; set; } = 0;
        public static int isChecker { get; set; } = 0;

        // Boolean wrapper properties
        public static bool IsStocker => isStocker == 1;
        public static bool IsChecker => isChecker == 1;

        // ------------------------------
        // Picking / session flags
        // ------------------------------
        public static string pPickNo { get; set; } = "";
        public static bool isLoaded { get; set; } = false;
        public static bool isBarcode { get; set; } = false;
        public static int isSummary { get; set; } = 0;

        // ------------------------------
        // Store / Department
        // ------------------------------
        public static string DeptStore { get; set; } = "";

        // ------------------------------
        // Server config
        // ------------------------------
        public static string sServer { get; set; } = "";
        public static string SqlUser { get; } = "sa";
        public static string SqlPass { get; } = "sa";

        private static string BuildConnectionString(string server) =>
            $"Server={server};Database=dbPicking3;User Id={SqlUser};Password={SqlPass};Encrypt=False;TrustServerCertificate=True;Connect Timeout=3;";

        // ------------------------------
        // SQL CONNECTION (MAUI-SAFE)
        // ------------------------------
        public static async Task<SqlConnection?> _SQL_Connect(string testServer = "")
        {
            try
            {
                string serverToUse =
                    string.IsNullOrWhiteSpace(testServer)
                        ? sServer
                        : testServer;

                var con = new SqlConnection(BuildConnectionString(serverToUse));

                // Connection timeout: 3s for SQL + 4s for Task.WhenAny = max 4 seconds total
                var openTask = con.OpenAsync();
                var completed = await Task.WhenAny(openTask, Task.Delay(4000));

                if (completed != openTask)
                {
                    con.Dispose();
                    return null;
                }

                return con;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<bool> _SQL_Connect_Exec(string testServer = "", bool isShowError = true)
        {
            var con = await _SQL_Connect(testServer);

            if (con != null)
                return true;

            if (isShowError)
            {
                await Shell.Current.DisplayAlert(
                    "Connection Error!",
                    "Please retry process and check connection...",
                    "OK");
            }

            return false;
        }

        // ------------------------------
        // USER LOGIN
        // ------------------------------
        public static async Task<bool> LoadUserInfoAsync(string userId)
        {
            using var con = await _SQL_Connect();
            if (con == null) return false;

            try
            {
                string sql =
                    "SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName, isChecker, isStocker " +
                    "FROM tblUsers WHERE EENo=@ID AND isActive=1";

                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@ID", userId);

                using var r = await cmd.ExecuteReaderAsync();
                if (!r.Read()) return false;

                ID_User = Convert.ToInt32(r["ID"]);
                sEENo = userId;
                sUserName = r["FullName"]?.ToString() ?? "";

                // <-- Set these too!
                isChecker = Convert.ToInt32(r["isChecker"]);
                isStocker = Convert.ToInt32(r["isStocker"]);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ======================================================================
        // STORAGE PATH (ANDROID + FALLBACK)
        // ======================================================================
        public static string GetExternalBackupFolder()
        {
#if ANDROID
            try
            {
                string root = AEnvironment.ExternalStorageDirectory?.AbsolutePath;
                if (!string.IsNullOrEmpty(root))
                {
                    string full = Path.Combine(
                        root,
                        "Android", "data",
                        AppInfo.PackageName,
                        "files",
                        "Backup", "PDTPicking"
                    );

                    Directory.CreateDirectory(full);
                    return full;
                }
            }
            catch
            {
                // fall through
            }
#endif
            string internalPath = Path.Combine(
                FileSystem.AppDataDirectory,
                "Backup", "PDTPicking");

            Directory.CreateDirectory(internalPath);
            return internalPath;
        }

        private static string ConfigFile =>
            Path.Combine(GetExternalBackupFolder(), "wifi.txt");

        // ======================================================================
        // LOAD / SAVE SERVER CONFIG
        // ======================================================================
        public static async Task LoadServerConfigAsync()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    await File.WriteAllLinesAsync(ConfigFile, new[]
                    {
                        sServer,
                        SqlUser,
                        SqlPass
                    });
                }

                var lines = await File.ReadAllLinesAsync(ConfigFile);
                if (lines.Length >= 1)
                    sServer = lines[0].Trim();
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
                    sServer,
                    SqlUser,
                    SqlPass
                });
            }
            catch
            {
                // ignore
            }
        }

        // ======================================================================
        // HELPER FUNCTIONS
        // ======================================================================
        public static string _FixNull(object vField) =>
            vField == null || vField == DBNull.Value
                ? ""
                : vField.ToString()?.Trim() ?? "";

        public static string _FixString(string sString) =>
            sString?.Replace("'", "''").Replace("\"", "\"\"").Trim() ?? "";

        public static void _SetUser(Label lblLabel)
        {
            lblLabel.Text = string.IsNullOrEmpty(sEENo)
                ? "Please set USER ID!... "
                : $"User: {sUserName}";
        }

        public static char _isAllowedNum(char sChar, bool isDecimal = false)
        {
            // Enter = '\r', Back = '\b', Escape = 27
            if (sChar == '\r' || sChar == '\b' || sChar == (char)27 || char.IsDigit(sChar) || (sChar == '.' && isDecimal))
                return sChar;

            return '\0';
        }

        public static async Task<string> _GetDateTime(bool isDay = false)
        {
            try
            {
                using var con = await _SQL_Connect();
                if (con != null)
                {
                    string sql = "SELECT CONVERT(VARCHAR(8), GETDATE(), 12) AS Today, " +
                                 "CONVERT(VARCHAR(8), GETDATE(), 114) AS ServerTime";

                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                        return isDay
                            ? reader["Today"]?.ToString() ?? ""
                            : reader["ServerTime"]?.ToString() ?? "";
                }
            }
            catch
            {
                // ignored, fallback to local time
            }

            // fallback to local machine time
            return isDay
                ? DateTime.Now.ToString("yyMMdd")
                : DateTime.Now.ToString("HH:mm:ss");
        }

        // ------------------------------
        // NEW: Get Dept Name
        // ------------------------------
        public static async Task<string> _GetDeptName(int iDept)
        {
            using var con = await _SQL_Connect();
            if (con != null)
            {
                try
                {
                    string sql = $"SELECT DptNam FROM invDPT WHERE IDEPT={iDept} AND ISDEPT=0";

                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                        return reader["DptNam"]?.ToString() ?? "";
                }
                catch
                {
                    // ignore
                }
            }
            return "";
        }

        // ------------------------------
        // NEW: Get Store No
        // ------------------------------
        public static async Task<string> _GetStoreNo()
        {
            using var con = await _SQL_Connect();
            if (con != null)
            {
                try
                {
                    string sql = $"SELECT DISTINCT ToLoc FROM tbl{pPickNo}PickDtl WHERE ID_SumHdr={ID_SumHdr}";

                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                        return reader[0]?.ToString() ?? "";
                }
                catch
                {
                    // ignore
                }
            }
            return "";
        }
        // ------------------------------
        // NEW: Check Option Stocker
        // ------------------------------
        public static async Task<bool> _CheckOption_StockerAsync()
        {
            using var con = await _SQL_Connect();
            if (con != null)
            {
                try
                {
                    string sql = "SELECT OptStocker FROM tblOptions";
                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        return Convert.ToInt32(reader["OptStocker"]) == 1;
                    }
                }
                catch
                {
                    // ignore exceptions
                }
            }

            return false;
        }
        public static async Task<bool> _CheckOption_User()
        {
            using var con = await _SQL_Connect();
            if (con != null)
            {
                try
                {
                    string sql = "SELECT OptUser FROM tblOptions";
                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        return Convert.ToInt32(reader["OptUser"]) == 1;
                    }
                }
                catch
                {
                    // ignore exceptions
                }
            }

            return false;
        }
        public static async Task<bool> _WorkQueryAsync(string sQuery, DataSet dsDatatoFill, string tblName)
        {
            try
            {
                using var con = await _SQL_Connect();
                if (con == null)
                    return false; // connection failed

                using var sqlAdp = new SqlDataAdapter(sQuery, con);

                // Execute Fill asynchronously to avoid blocking UI
                await Task.Run(() => sqlAdp.Fill(dsDatatoFill, tblName));

                return true; // success
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                Debug.WriteLine($"_WorkQueryAsync error: {ex.Message}");
                return false;
            }
        }
        public static async Task<string> _GetPickNo()
        {
            if (string.IsNullOrEmpty(pPickNo) || pPickNo == "0")
            {
                pPickNo = "0";
                return "";
            }

            using var con = await _SQL_Connect();
            if (con != null)
            {
                try
                {
                    string sql = $"SELECT SetRef, SetUp FROM tblOptions A " +
                                 $"LEFT JOIN tblPickRef B ON A.SetRef = B.PickRef " +
                                 $"WHERE A.SetRef = {pPickNo}";

                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        pPickNo = reader[0]?.ToString()?.Trim() ?? "0";
                        return reader[1]?.ToString()?.Trim() ?? "";
                    }
                    else
                    {
                        pPickNo = "0";
                        return "";
                    }
                }
                catch
                {
                    pPickNo = "0";
                    return "";
                }
            }
            else
            {
                pPickNo = "0";
                return "";
            }
        }
        public static void _FlushMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        public static async Task<int> _CheckUnfinishedPicking(int Userid, int Pickref)
        {
            int ID_SumHdr = 0;
            try
            {
                using var con = await _SQL_Connect();
                if (con != null)
                {
                    string sql = $"SELECT MIN(ID) AS LastHdr FROM tbl{Pickref}PickHdr " +
                                 $"WHERE (SKUPicked < TotSKU) AND User_ID = {Userid}";

                    using var cmd = new SqlCommand(sql, con);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        ID_SumHdr = reader["LastHdr"] != DBNull.Value
                            ? Convert.ToInt32(reader["LastHdr"])
                            : 0;
                    }
                }
            }
            catch
            {
                ID_SumHdr = 0;
            }

            return ID_SumHdr;
        }
        public static async Task _CheckUpdate(string sqlServer, string sqlUser, string sqlPass, string sqlDatabase, string sysAppName)
        {
            // Adjust path for cross-platform usage
            string appUpdater = Path.Combine(FileSystem.AppDataDirectory, "Programs", "Picking System", "PDT Exe Updater.exe");

            if (!File.Exists(appUpdater))
                return;

            var con = await _SQL_Connect();
            if (con == null)
                return;

            string sVersion = "";
            try
            {
                using var sqlCmd = new SqlCommand("SELECT fileversion FROM tblExeUpdate", con);
                var result = await sqlCmd.ExecuteScalarAsync();
                sVersion = result?.ToString() ?? "";
            }
            catch
            {
                return;
            }

            if (!string.IsNullOrEmpty(sVersion))
            {
                if (int.TryParse(sVersion.Replace(".", ""), out int newVer) &&
                    int.TryParse(sysVersion.Replace(".", ""), out int currVer) &&
                    newVer > currVer)
                {
                    bool answer = await Shell.Current.DisplayAlert(
                        "System Update!",
                        "New version detected!\n\nUpdate Application?",
                        "Yes",
                        "No");

                    if (answer)
                    {
                        string fileUpdateInfo = Path.Combine(FileSystem.AppDataDirectory, "Backup", "UpdateInfo.txt");
                        Directory.CreateDirectory(Path.GetDirectoryName(fileUpdateInfo)!);

                        await File.WriteAllLinesAsync(fileUpdateInfo, new[] { sqlServer, sqlUser, sqlPass, sqlDatabase, sysAppName });

                        // Launch external updater
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = appUpdater,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                            await Shell.Current.DisplayAlert("Error!", "Unable to start updater.", "OK");
                            return;
                        }

                        // Wait for a moment
                        await Task.Delay(3000);

#if WINDOWS
                    // Only quit app on Windows
                    Application.Current.Quit();
#endif
                    }
                }
            }
        }
        public static bool _IsInList(int col, string toFind, object lvObj)
        {
            if (lvObj is not Microsoft.Maui.Controls.ListView listView || listView.ItemsSource == null)
                return false;

            foreach (var item in listView.ItemsSource)
            {
                // Use reflection to get the property by column index
                var properties = item.GetType().GetProperties();
                if (col >= 0 && col < properties.Length)
                {
                    var value = properties[col].GetValue(item)?.ToString() ?? "";
                    if (value.Equals(toFind, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
        public static async Task<string> _GetUserName(string ee)
        {
            using var con = await _SQL_Connect();
            if (con == null)
                return "";

            try
            {
                string sql = $"SELECT ID, (LName + ', ' + FName + ' ' + MI) AS FullName " +
                             $"FROM tblUsers WHERE isActive=1 AND ID = {Convert.ToInt32(ee)}";

                using var cmd = new SqlCommand(sql, con);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return reader["FullName"]?.ToString()?.Trim() ?? "";
                }
            }
            catch
            {
                // optionally log exception
            }

            return "";
        }

        // ------------------------------
        // WiFi Service
        // ------------------------------
        private static IWifiService? _wifiService;
        public static IWifiService WifiService
        {
            get
            {
                if (_wifiService == null)
                {
                    _wifiService = Application.Current?.Handler?.MauiContext?.Services
                        .GetService<IWifiService>() ?? new WifiService_Default();
                }
                return _wifiService;
            }
        }

        /// <summary>
        /// Get current WiFi connection status
        /// </summary>
        public static string GetWifiStatus()
        {
            return WifiService.GetConnectedWifiName();
        }

        /// <summary>
        /// Check if connected to WiFi
        /// </summary>
        public static bool IsWifiConnected()
        {
            string status = GetWifiStatus();
            return !status.Contains("Not connected") &&
                   !status.Contains("unavailable");
        }
    }
}
