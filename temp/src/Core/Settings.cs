using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KeyboardGuitar
{
    // exe 폴더의 settings.ini (key=value)
    public class Settings
    {
        public const string FileName = "settings.ini";

        public string Scale = "";
        public bool BlockMode;
        public bool Preview = true;
        public int StartDegree = 1;
        public Direction Dir = Direction.Up;
        public int Interval = 2;
        public bool PairPattern;
        public int Opacity = 100;   // 10~100
        public int OverlayCorner;   // 0 오른쪽 아래, 1 왼쪽 아래, 2 오른쪽 위, 3 왼쪽 위
        public int ResultMode;      // 0 지판 + O/X, 1 O/X만
        public KeyMap Keys = KeyMap.CreateDefault();

        public static Settings Load(string dir)
        {
            var s = new Settings();
            string path = Path.Combine(dir, FileName);
            if (!File.Exists(path)) return s;
            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch (IOException) { return s; }
            catch (UnauthorizedAccessException) { return s; }
            var kv = new Dictionary<string, string>();
            foreach (string line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq > 0) kv[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            string v;
            if (kv.TryGetValue("scale", out v)) s.Scale = v;
            if (kv.TryGetValue("blockMode", out v)) s.BlockMode = v == "1";
            if (kv.TryGetValue("preview", out v)) s.Preview = v == "1";
            if (kv.TryGetValue("startDegree", out v)) s.StartDegree = ToInt(v, 1);
            if (kv.TryGetValue("direction", out v)) s.Dir = (Direction)Clamp(ToInt(v, 0), 0, 2);
            if (kv.TryGetValue("interval", out v)) s.Interval = ToInt(v, 2);
            if (kv.TryGetValue("pairPattern", out v)) s.PairPattern = v == "1";
            if (kv.TryGetValue("opacity", out v)) s.Opacity = ToInt(v, 100);
            if (kv.TryGetValue("overlayCorner", out v)) s.OverlayCorner = ToInt(v, 0);
            if (kv.TryGetValue("resultMode", out v)) s.ResultMode = ToInt(v, 0);
            if (kv.TryGetValue("keymap", out v)) s.Keys = KeyMap.Deserialize(v);
            s.Normalize();
            return s;
        }

        public const int MaxInterval = 9999;

        public void Normalize()
        {
            Interval = Clamp(Interval, 2, MaxInterval);
            if (StartDegree < 1) StartDegree = 1;
            Opacity = Clamp(Opacity, 10, 100);
            OverlayCorner = Clamp(OverlayCorner, 0, 3);
            ResultMode = Clamp(ResultMode, 0, 1);
        }

        public void Save(string dir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scale=" + Scale);
            sb.AppendLine("blockMode=" + (BlockMode ? 1 : 0));
            sb.AppendLine("preview=" + (Preview ? 1 : 0));
            sb.AppendLine("startDegree=" + StartDegree);
            sb.AppendLine("direction=" + (int)Dir);
            sb.AppendLine("interval=" + Interval);
            sb.AppendLine("pairPattern=" + (PairPattern ? 1 : 0));
            sb.AppendLine("opacity=" + Opacity);
            sb.AppendLine("overlayCorner=" + OverlayCorner);
            sb.AppendLine("resultMode=" + ResultMode);
            sb.AppendLine("keymap=" + Keys.Serialize());
            try { File.WriteAllText(Path.Combine(dir, FileName), sb.ToString(), Encoding.UTF8); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static int ToInt(string s, int def)
        {
            int n;
            return int.TryParse(s, out n) ? n : def;
        }

        static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }
    }
}
