using System;
using System.Collections.Generic;

namespace KeyboardGuitar
{
    // 물리 키 식별자 = 스캔코드 | (확장 키면 0x100). 한/영 상태와 무관하다.
    public static class KeyCodes
    {
        public const int Esc = 0x01, Backspace = 0x0E, Tab = 0x0F, Enter = 0x1C;
        public const int LCtrl = 0x1D, RCtrl = 0x11D, LShift = 0x2A, RShift = 0x36, LAlt = 0x38, RAlt = 0x138;
        public const int CapsLock = 0x3A, Up = 0x148, Left = 0x14B, Right = 0x14D, Down = 0x150;
        public const int LWin = 0x15B, RWin = 0x15C;

        static readonly Dictionary<int, string> names = new Dictionary<int, string>();
        static readonly Dictionary<string, int> byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        static KeyCodes()
        {
            Add(0x01, "Esc");
            AddRow(0x02, "1234567890");
            Add(0x0C, "-"); Add(0x0D, "="); Add(0x0E, "Backspace"); Add(0x0F, "Tab");
            AddRow(0x10, "QWERTYUIOP");
            Add(0x1A, "["); Add(0x1B, "]"); Add(0x1C, "Enter"); Add(0x1D, "L Ctrl");
            AddRow(0x1E, "ASDFGHJKL");
            Add(0x27, ";"); Add(0x28, "'"); Add(0x29, "`"); Add(0x2A, "L Shift"); Add(0x2B, "\\");
            AddRow(0x2C, "ZXCVBNM");
            Add(0x33, ","); Add(0x34, "."); Add(0x35, "/"); Add(0x36, "R Shift"); Add(0x37, "Num *");
            Add(0x38, "L Alt"); Add(0x39, "Space"); Add(0x3A, "CapsLock");
            for (int i = 0; i < 10; i++) Add(0x3B + i, "F" + (i + 1));
            Add(0x45, "NumLock"); Add(0x46, "ScrollLock");
            Add(0x47, "Num 7"); Add(0x48, "Num 8"); Add(0x49, "Num 9"); Add(0x4A, "Num -");
            Add(0x4B, "Num 4"); Add(0x4C, "Num 5"); Add(0x4D, "Num 6"); Add(0x4E, "Num +");
            Add(0x4F, "Num 1"); Add(0x50, "Num 2"); Add(0x51, "Num 3"); Add(0x52, "Num 0"); Add(0x53, "Num .");
            Add(0x57, "F11"); Add(0x58, "F12");
            Add(0x11C, "Num Enter"); Add(0x11D, "R Ctrl"); Add(0x135, "Num /"); Add(0x138, "R Alt");
            Add(0x147, "Home"); Add(0x148, "↑"); Add(0x149, "PgUp"); Add(0x14B, "←"); Add(0x14D, "→");
            Add(0x14F, "End"); Add(0x150, "↓"); Add(0x151, "PgDn"); Add(0x152, "Insert"); Add(0x153, "Delete");
            Add(0x15B, "L Win"); Add(0x15C, "R Win"); Add(0x15D, "Menu");
        }

        static void AddRow(int first, string keys)
        {
            for (int i = 0; i < keys.Length; i++) Add(first + i, keys[i].ToString());
        }

        static void Add(int id, string name)
        {
            names[id] = name;
            byName[name] = id;
        }

        public static int FromScan(int scan, bool extended)
        {
            // Shift는 키보드에 따라 확장 플래그가 붙기도 해서 무시한다.
            if (scan == LShift || scan == RShift) extended = false;
            return (scan & 0xFF) | (extended ? 0x100 : 0);
        }

        public static string Name(int id)
        {
            string n;
            if (names.TryGetValue(id, out n)) return n;
            return "0x" + id.ToString("X");
        }

        // 테스트·기본 매칭용. 없으면 -1.
        public static int Parse(string name)
        {
            int id;
            return byName.TryGetValue(name, out id) ? id : -1;
        }

        public static bool IsArrow(int id)
        {
            return id == Up || id == Down || id == Left || id == Right;
        }

        // 키 매칭 창에서 할당하지 않는 키: 시작 신호(→)·지판 보기(←) 등 방향키, 활성화 키(Ctrl), OS 키(Alt, Win)
        public static bool IsUnassignable(int id)
        {
            return IsArrow(id) || id == LCtrl || id == RCtrl || id == LAlt || id == RAlt || id == LWin || id == RWin;
        }
    }
}
