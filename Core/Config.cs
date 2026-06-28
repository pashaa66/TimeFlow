using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TimeFlow
{
    public static class Config
    {
        public const string AppName = "TimeFlow";
        public const string AppVersion = "2.0.0";
        public const string OrgName = "TimeFlowTeam";


        public static string AppDir => AppDomain.CurrentDomain.BaseDirectory;
        public static string DataDir => Path.Combine(AppDir, "data");
        public static string TasksFile => Path.Combine(DataDir, "tasks.json");
        public static string HistoryFile => Path.Combine(DataDir, "history.json");
        public static string SettingsFile => Path.Combine(DataDir, "settings.ini");
        public static string ExportDir => Path.Combine(DataDir, "exports");

        public static void EnsureDirs()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(ExportDir);
        }

        public static readonly Dictionary<string, string> DefaultCategories = new()
        {
            { "Учёба", "#4A90D9" },
            { "Работа", "#E67E22" },
            { "Отдых", "#27AE60" },
        };

        public static readonly string[] ColorPalette =
        {
            "#4A90D9","#5B9BD5","#6BAED6","#7BC0DE","#8FD0E8",
            "#2171B5","#08519C","#08306B","#3182BD","#6BAED6",
            "#E67E22","#F39C12","#F1C40F","#F4D03F","#F7DC6F",
            "#E74C3C","#C0392B","#A93226","#922B21","#7B241C",
            "#27AE60","#2ECC71","#58D68D","#82E0AA","#ABEBC6",
            "#16A085","#117A65","#0E6655","#148F77","#1ABC9C",
            "#8E44AD","#9B59B6","#AF7AC5","#BB8FCE","#C39BD3",
            "#D35400","#CA6F1E","#BA4A00","#A04000","#873600",
            "#34495E","#2C3E50","#5D6D7E","#85929E","#AEB6BF",
            "#E84393","#FD79A8","#FAB1A0","#FDCB6E","#55EFC4",
        };

        public static string NowIso() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
        public static string TodayStr() => DateTime.Today.ToString("yyyy-MM-dd");

        public static JsonDocument LoadJson(string path)
        {
            if (!File.Exists(path)) return null;
            string raw = File.ReadAllText(path, Encoding.UTF8);
            return JsonDocument.Parse(raw);
        }

        public static void SaveJson(string path, object data)
        {
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(path, JsonSerializer.Serialize(data, opts), Encoding.UTF8);
        }
    }
}