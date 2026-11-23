using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SinoDigital
{
    // Nomes de campos em minúsculas = mesmo formato do backup .zip gerado pelo sino-digital.html
    public class Schedule
    {
        public string id;
        public string time;
        public string name;
        public string recurrence;   // "weekly" | "once"
        public string audioId;
        public int? loopSeconds;
        public bool active;
        public string lastFiredDate;
        public List<int> days;
        public string date;
    }

    public class Holiday
    {
        public string id;
        public string date;
        public string dateEnd;
        public bool yearly;
        public string description;
    }

    public class AudioMeta
    {
        public string id;
        public string name;
        public string type;
        public string file;
        public override string ToString() { return name; }
    }

    public class Settings
    {
        public bool keepAlive = true;
        public bool notifications = true;
        public bool duckEnabled = false;   // abaixar o volume quando o microfone captar som
        public int duckVolume = 20;        // volume (%) enquanto houver som no microfone
        public int duckSensitivity = 8;    // nível do microfone (%) que dispara a redução
    }

    public class AppData
    {
        public List<Schedule> schedules = new List<Schedule>();
        public List<Holiday> holidays = new List<Holiday>();
        public List<AudioMeta> audios = new List<AudioMeta>();
        public Settings settings = new Settings();
    }

    public class BackupAudio { public string id; public string name; public string type; public string arquivo; }

    public class BackupFile
    {
        public string app;
        public int versao;
        public string exportadoEm;
        public List<Schedule> schedules;
        public List<Holiday> holidays;
        public List<BackupAudio> audios;
    }

    public class PendingImport
    {
        public List<Schedule> Schedules;
        public List<Holiday> Holidays;
        public List<BackupAudio> AudioList;
        public Dictionary<string, byte[]> Files;
    }

    public static class Store
    {
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SinoDigital");
        public static readonly string AudioDir = Path.Combine(Dir, "audios");
        public static readonly string DataFile = Path.Combine(Dir, "dados.json");
        public static AppData Data = new AppData();

        static JavaScriptSerializer Ser()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 100 };
        }

        public static void Load()
        {
            Directory.CreateDirectory(AudioDir);
            if (File.Exists(DataFile))
            {
                try
                {
                    Data = Ser().Deserialize<AppData>(File.ReadAllText(DataFile, Encoding.UTF8)) ?? new AppData();
                }
                catch
                {
                    try { File.Copy(DataFile, DataFile + ".corrompido-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
                    Data = new AppData();
                }
            }
            if (Data.schedules == null) Data.schedules = new List<Schedule>();
            if (Data.holidays == null) Data.holidays = new List<Holiday>();
            if (Data.audios == null) Data.audios = new List<AudioMeta>();
            if (Data.settings == null) Data.settings = new Settings();
            foreach (var s in Data.schedules) if (s.days == null) s.days = new List<int>();
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string tmp = DataFile + ".tmp";
                File.WriteAllText(tmp, Ser().Serialize(Data), new UTF8Encoding(false));
                if (File.Exists(DataFile)) File.Delete(DataFile);
                File.Move(tmp, DataFile);
            }
            catch (Exception ex) { Program.Log("Save: " + ex); }
        }

        public static string NewId() { return Guid.NewGuid().ToString(); }

        // ---------- datas ----------
        public static string ToDateStr(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static string ToBR(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 10) return s ?? "";
            return s.Substring(8, 2) + "/" + s.Substring(5, 2) + "/" + s.Substring(0, 4);
        }
        public static DateTime ParseDate(string s)
        {
            DateTime d;
            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            return DateTime.Today;
        }
        public static string TimeStr(DateTime d) { return d.ToString("HH:mm", CultureInfo.InvariantCulture); }

        // ---------- feriados ----------
        public static bool IsHoliday(string dateStr)
        {
            foreach (var h in Data.holidays)
            {
                string end = string.IsNullOrEmpty(h.dateEnd) ? h.date : h.dateEnd;
                if (!h.yearly)
                {
                    if (string.CompareOrdinal(dateStr, h.date) >= 0 && string.CompareOrdinal(dateStr, end) <= 0) return true;
                    continue;
                }
                if (string.CompareOrdinal(dateStr, h.date) < 0) continue;
                string md = dateStr.Substring(5), s = h.date.Substring(5), e = end.Substring(5);
                bool hit = string.CompareOrdinal(s, e) <= 0
                    ? (string.CompareOrdinal(md, s) >= 0 && string.CompareOrdinal(md, e) <= 0)
                    : (string.CompareOrdinal(md, s) >= 0 || string.CompareOrdinal(md, e) <= 0);
                if (hit) return true;
            }
            return false;
        }

        public static string HolidayLabel(Holiday h)
        {
            string b = (!string.IsNullOrEmpty(h.dateEnd) && h.dateEnd != h.date)
                ? ToBR(h.date) + " a " + ToBR(h.dateEnd) : ToBR(h.date);
            return h.yearly ? b + " · todo ano" : b;
        }

        // ---------- agendamentos ----------
        public static readonly string[] DaysShort = { "DOM", "SEG", "TER", "QUA", "QUI", "SEX", "SÁB" };

        public static string Describe(Schedule s)
        {
            if (s.recurrence == "once") return "Data única · " + ToBR(s.date);
            if (s.days == null || s.days.Count == 0) return "";
            if (s.days.Count == 7) return "Todos os dias";
            return string.Join(" · ", s.days.Select(d => DaysShort[d % 7]).ToArray());
        }

        public static bool RunsOn(Schedule s, DateTime now)
        {
            if (s.recurrence == "once") return s.date == ToDateStr(now);
            return s.days != null && s.days.Contains((int)now.DayOfWeek);
        }

        public static string Classify(string time)
        {
            if (string.CompareOrdinal(time, "06:00") >= 0 && string.CompareOrdinal(time, "12:30") <= 0) return "morning";
            if (string.CompareOrdinal(time, "12:31") >= 0 && string.CompareOrdinal(time, "18:30") <= 0) return "afternoon";
            return "night";
        }

        // ---------- áudios ----------
        public static AudioMeta FindAudio(string id)
        {
            return Data.audios.FirstOrDefault(a => a.id == id);
        }
        public static string AudioPath(AudioMeta a) { return Path.Combine(AudioDir, a.file); }

        static string MimeFor(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".mp3": return "audio/mpeg";
                case ".wav": return "audio/wav";
                case ".ogg": return "audio/ogg";
                case ".m4a": case ".mp4": return "audio/mp4";
                case ".aac": return "audio/aac";
                case ".flac": return "audio/flac";
                case ".wma": return "audio/x-ms-wma";
                default: return "audio/*";
            }
        }

        public static string SafeName(string n)
        {
            if (string.IsNullOrEmpty(n)) return "audio";
            var sb = new StringBuilder();
            char[] bad = Path.GetInvalidFileNameChars();
            foreach (char c in n) sb.Append((Array.IndexOf(bad, c) >= 0 || c == '/' || c == '\\') ? '_' : c);
            return sb.ToString();
        }

        public static AudioMeta AddAudio(string srcPath)
        {
            string id = NewId();
            string ext = Path.GetExtension(srcPath).ToLowerInvariant();
            string file = id + ext;
            Directory.CreateDirectory(AudioDir);
            File.Copy(srcPath, Path.Combine(AudioDir, file), true);
            var meta = new AudioMeta { id = id, name = Path.GetFileName(srcPath), type = MimeFor(ext), file = file };
            Data.audios.Add(meta);
            Save();
            return meta;
        }

        // ---------- backup ZIP (compatível com o sino-digital.html) ----------
        public static void ExportBackup(string zipPath)
        {
            using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var list = new List<BackupAudio>();
                int i = 0;
                foreach (var a in Data.audios)
                {
                    string p = AudioPath(a);
                    if (!File.Exists(p)) continue;
                    i++;
                    string entry = "audios/" + i + "_" + SafeName(a.name);
                    zip.CreateEntryFromFile(p, entry, CompressionLevel.NoCompression);
                    list.Add(new BackupAudio { id = a.id, name = a.name, type = a.type, arquivo = entry });
                }
                var dados = new BackupFile
                {
                    app = "sino-digital",
                    versao = 1,
                    exportadoEm = DateTime.UtcNow.ToString("o"),
                    schedules = Data.schedules,
                    holidays = Data.holidays,
                    audios = list
                };
                var e = zip.CreateEntry("dados.json");
                using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(Ser().Serialize(dados));
            }
        }

        public static PendingImport ReadBackup(string zipPath)
        {
            var files = new Dictionary<string, byte[]>();
            using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    using (var s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        files[e.FullName.Replace('\\', '/')] = ms.ToArray();
                    }
                }
            }
            byte[] json;
            if (!files.TryGetValue("dados.json", out json)) throw new Exception("dados.json não encontrado no ZIP.");
            var dados = Ser().Deserialize<BackupFile>(Encoding.UTF8.GetString(json));
            if (dados == null || dados.app != "sino-digital") throw new Exception("Este ZIP não é um backup do Sino Digital.");
            var p = new PendingImport
            {
                Schedules = dados.schedules ?? new List<Schedule>(),
                Holidays = dados.holidays ?? new List<Holiday>(),
                AudioList = dados.audios ?? new List<BackupAudio>(),
                Files = files
            };
            foreach (var a in p.AudioList)
                if (a.arquivo == null || !files.ContainsKey(a.arquivo.Replace('\\', '/')))
                    throw new Exception("Áudio ausente no ZIP: " + a.name);
            return p;
        }

        public static void ApplyImport(PendingImport p)
        {
            Directory.CreateDirectory(AudioDir);
            foreach (var f in Directory.GetFiles(AudioDir)) { try { File.Delete(f); } catch { } }
            var metas = new List<AudioMeta>();
            foreach (var a in p.AudioList)
            {
                string ext = Path.GetExtension(a.name ?? "").ToLowerInvariant();
                string file = SafeName(a.id) + ext;
                File.WriteAllBytes(Path.Combine(AudioDir, file), p.Files[a.arquivo.Replace('\\', '/')]);
                metas.Add(new AudioMeta { id = a.id, name = a.name, type = a.type, file = file });
            }
            Data.audios = metas;
            Data.schedules = p.Schedules;
            Data.holidays = p.Holidays;
            foreach (var s in Data.schedules) if (s.days == null) s.days = new List<int>();
            Save();
        }
    }
}
