using System;
using System.Collections.Generic;
using System.Text;

namespace KeyboardGuitar
{
    public struct Pos
    {
        public readonly int Str;
        public readonly int Fret;

        public Pos(int str, int fret)
        {
            Str = str;
            Fret = fret;
        }

        public int Code { get { return Str * 100 + Fret; } }

        public static Pos FromCode(int code)
        {
            return new Pos(code / 100, code % 100);
        }
    }

    public static class Fretboard
    {
        public const int MinString = 2, MaxString = 6, FretColumns = 14;

        // 표준 튜닝 개방현 음 높이 (MIDI 번호)
        public static int OpenPitch(int str)
        {
            switch (str)
            {
                case 6: return 40;
                case 5: return 45;
                case 4: return 50;
                case 3: return 55;
                case 2: return 59;
            }
            throw new ArgumentOutOfRangeException("str");
        }

        public static int Pitch(Pos p)
        {
            return OpenPitch(p.Str) + p.Fret;
        }

        // 모양 좌표는 모든 줄 간격이 4도인 것처럼 적는다. 실제 2현은 1프렛 높게 보정한다.
        public static int Correction(int str)
        {
            return str <= 2 ? 1 : 0;
        }

        // 모양 좌표에서의 음 높이 (6현 개방 = 0)
        public static int ShapePitch(int str, int fret)
        {
            return (6 - str) * 5 + fret;
        }

        public static bool ValidString(int str)
        {
            return str >= MinString && str <= MaxString;
        }
    }

    // 줄·프렛 칸 ↔ 물리 키. 한 키는 한 칸에만 할당된다.
    public class KeyMap
    {
        readonly Dictionary<int, int> posToKey = new Dictionary<int, int>();
        readonly Dictionary<int, int> keyToPos = new Dictionary<int, int>();

        public static KeyMap CreateDefault()
        {
            var m = new KeyMap();
            m.AddRow(2, "Esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12");
            m.AddRow(3, "` 1 2 3 4 5 6 7 8 9 0 - = Backspace");
            m.AddRow(4, "Tab Q W E R T Y U I O P [ ] \\");
            m.AddRow(5, "CapsLock A S D F G H J K L ; ' Enter");
            m.AddRow(6, "L_Shift Z X C V B N M , . / R_Shift");
            return m;
        }

        void AddRow(int str, string keys)
        {
            string[] parts = keys.Split(' ');
            for (int f = 0; f < parts.Length; f++)
                Assign(new Pos(str, f), KeyCodes.Parse(parts[f].Replace('_', ' ')));
        }

        public void Assign(Pos p, int key)
        {
            int oldPos;
            if (keyToPos.TryGetValue(key, out oldPos)) posToKey.Remove(oldPos);
            Clear(p);
            posToKey[p.Code] = key;
            keyToPos[key] = p.Code;
        }

        public void Clear(Pos p)
        {
            int oldKey;
            if (posToKey.TryGetValue(p.Code, out oldKey))
            {
                posToKey.Remove(p.Code);
                keyToPos.Remove(oldKey);
            }
        }

        // 할당 안 된 칸이면 -1
        public int KeyAt(Pos p)
        {
            int k;
            return posToKey.TryGetValue(p.Code, out k) ? k : -1;
        }

        public bool TryGetPos(int key, out Pos p)
        {
            int code;
            if (keyToPos.TryGetValue(key, out code))
            {
                p = Pos.FromCode(code);
                return true;
            }
            p = new Pos();
            return false;
        }

        // "2:0=1,2:1=3B,..." (키는 16진수)
        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in posToKey)
            {
                if (sb.Length > 0) sb.Append(',');
                Pos p = Pos.FromCode(kv.Key);
                sb.Append(p.Str).Append(':').Append(p.Fret).Append('=').Append(kv.Value.ToString("X"));
            }
            return sb.ToString();
        }

        public static KeyMap Deserialize(string s)
        {
            var m = new KeyMap();
            if (string.IsNullOrEmpty(s)) return m;
            foreach (string item in s.Split(','))
            {
                string[] kv = item.Split('=');
                if (kv.Length != 2) continue;
                string[] sf = kv[0].Split(':');
                int str, fret, key;
                if (sf.Length != 2 || !int.TryParse(sf[0], out str) || !int.TryParse(sf[1], out fret)) continue;
                if (!int.TryParse(kv[1], System.Globalization.NumberStyles.HexNumber, null, out key)) continue;
                if (!Fretboard.ValidString(str) || fret < 0 || fret >= Fretboard.FretColumns) continue;
                m.Assign(new Pos(str, fret), key);
            }
            return m;
        }
    }
}
