using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace KeyboardGuitar
{
    public class Block
    {
        // 모양 좌표 (실제 위치 아님). 루트 → 옥타브 위 루트, 상행 순서.
        public readonly List<Pos> Cells = new List<Pos>();
    }

    public class ScaleDef
    {
        public string Name;
        public readonly List<Block> Blocks = new List<Block>();
        // 루트 기준 반음 간격, 오름차순 (예: 메이저 0 2 4 5 7 9 11)
        public int[] Intervals;

        public int N { get { return Intervals.Length; } }
    }

    public static class Patterns
    {
        public const string FileName = "patterns.md";
        const string ResourceName = "KeyboardGuitar.patterns.md";

        // exe 폴더의 patterns.md를 읽고, 없거나 스케일을 하나도 못 읽으면 내장본을 쓴다.
        public static List<ScaleDef> Load(string dir, List<string> warnings)
        {
            string path = Path.Combine(dir, FileName);
            if (File.Exists(path))
            {
                string text = null;
                try { text = File.ReadAllText(path); }
                catch (Exception ex) { warnings.Add(FileName + "을 열 수 없습니다: " + ex.Message); }
                var scales = text == null ? new List<ScaleDef>() : Parse(text, warnings);
                if (scales.Count > 0) return scales;
                warnings.Add(FileName + "에서 스케일을 읽지 못해 내장 패턴을 사용합니다.");
            }
            else
            {
                warnings.Add(FileName + " 파일이 없어 내장 패턴을 사용합니다.");
            }
            return Parse(ReadEmbedded(), warnings);
        }

        public static string ReadEmbedded()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            {
                if (s == null) return "";
                using (var r = new StreamReader(s)) return r.ReadToEnd();
            }
        }

        public static List<ScaleDef> Parse(string text, List<string> warnings)
        {
            var result = new List<ScaleDef>();
            ScaleDef cur = null;
            string[] lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("## "))
                {
                    Finish(cur, result, warnings);
                    cur = new ScaleDef { Name = line.Substring(3).Trim() };
                    continue;
                }
                if (cur == null) continue;
                Block b = ParseBlock(line);
                if (b == null) continue;

                string err;
                int[] iv = Analyze(b, out err);
                if (iv == null)
                {
                    warnings.Add(string.Format("{0}행 ({1}): {2}", i + 1, cur.Name, err));
                    continue;
                }
                if (cur.Intervals == null) cur.Intervals = iv;
                else if (!cur.Intervals.SequenceEqual(iv))
                {
                    warnings.Add(string.Format("{0}행 ({1}): 첫 블록과 구성음이 달라 무시합니다.", i + 1, cur.Name));
                    continue;
                }
                cur.Blocks.Add(b);
            }
            Finish(cur, result, warnings);
            return result;
        }

        static void Finish(ScaleDef s, List<ScaleDef> result, List<string> warnings)
        {
            if (s == null) return;
            if (s.Blocks.Count == 0) warnings.Add(s.Name + ": 읽을 수 있는 블록이 없어 제외합니다.");
            else result.Add(s);
        }

        // "6:4 | 5:1,2,4 | 4:1,2,4 | 3:1" 형식이 아니면 null
        static Block ParseBlock(string line)
        {
            if (line.Length < 3 || !char.IsDigit(line[0])) return null;
            var b = new Block();
            foreach (string part in line.Split('|'))
            {
                string[] sf = part.Split(':');
                if (sf.Length != 2) return null;
                int str;
                if (!int.TryParse(sf[0].Trim(), out str)) return null;
                foreach (string f in sf[1].Split(','))
                {
                    int fret;
                    if (!int.TryParse(f.Trim(), out fret)) return null;
                    b.Cells.Add(new Pos(str, fret));
                }
            }
            return b;
        }

        // 블록이 올바르면 구성음 간격을 돌려준다.
        static int[] Analyze(Block b, out string err)
        {
            err = null;
            if (b.Cells.Count < 3) { err = "블록 음이 너무 적습니다."; return null; }
            foreach (Pos c in b.Cells)
            {
                if (!Fretboard.ValidString(c.Str) || c.Fret < 0) { err = "줄은 2~6, 프렛은 0 이상이어야 합니다."; return null; }
            }
            int root = Fretboard.ShapePitch(b.Cells[0].Str, b.Cells[0].Fret);
            int prev = int.MinValue;
            var set = new SortedSet<int>();
            foreach (Pos c in b.Cells)
            {
                int p = Fretboard.ShapePitch(c.Str, c.Fret);
                if (p <= prev) { err = "음이 상행 순서가 아닙니다."; return null; }
                prev = p;
                set.Add((p - root) % 12);
            }
            if (prev - root != 12) { err = "마지막 음이 루트의 옥타브가 아닙니다."; return null; }
            if (set.Count != b.Cells.Count - 1) { err = "같은 음이 두 번 있습니다."; return null; }
            return set.ToArray();
        }
    }
}
