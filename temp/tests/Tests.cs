using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KeyboardGuitar
{
    static class Tests
    {
        static int passed, failed;
        static List<ScaleDef> scales;
        static readonly KeyMap keys = KeyMap.CreateDefault();

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string patternsPath = args.Length > 0 ? args[0] : "patterns.md";
            var warnings = new List<string>();
            scales = Patterns.Parse(File.ReadAllText(patternsPath), warnings);
            foreach (string w in warnings) Console.WriteLine("  경고: " + w);

            PatternTests();
            KeyMapTests();
            FullModeTests();
            BlockModeTests();
            RouterTests();
            RouterRepeatTests();
            AltTabTests();
            SettingsTests();

            Console.WriteLine();
            Console.WriteLine("통과 {0} / 실패 {1}", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        static void Check(bool cond, string name)
        {
            if (cond) passed++;
            else { failed++; Console.WriteLine("  실패: " + name); }
        }

        static ScaleDef Scale(string prefix)
        {
            return scales.First(s => s.Name.StartsWith(prefix));
        }

        // "z c v" → 실제 줄·프렛
        static List<Pos> PosList(string seq)
        {
            var list = new List<Pos>();
            foreach (string k in seq.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int id = KeyCodes.Parse(k);
                Pos p;
                if (id < 0 || !keys.TryGetPos(id, out p)) throw new Exception("키 없음: " + k);
                list.Add(p);
            }
            return list;
        }

        // 각 입력의 O/X를 "OOOX" 문자열로
        static string Run(JudgeConfig cfg, string seq)
        {
            var j = new Judge(cfg);
            var sb = new StringBuilder();
            foreach (Pos p in PosList(seq)) sb.Append(j.Feed(p) ? 'O' : 'X');
            return sb.ToString();
        }

        static string All(char c, string seq)
        {
            return new string(c, seq.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length);
        }

        static void PatternTests()
        {
            Check(scales.Count == 12, "스케일 12개");
            Check(Scale("Natural Minor").Intervals.SequenceEqual(new[] { 0, 2, 3, 5, 7, 8, 10 }), "마이너 구성음");
            Check(Scale("Major (").Intervals.SequenceEqual(new[] { 0, 2, 4, 5, 7, 9, 11 }), "메이저 구성음");
            Check(Scale("Major Pentatonic").N == 5, "메이저 펜타토닉 5음");
            Check(Scale("Blues").N == 6, "블루스 6음");
            Check(scales.All(s => s.Blocks.All(b => b.Cells.Count == s.N + 1)), "블록 = N+1음");
            Check(Scale("Natural Minor").Blocks.Count == 3 && Scale("Major Pentatonic").Blocks.Count == 2, "블록 개수");

            var w = new List<string>();
            var bad = Patterns.Parse("## Bad\n6:1,3,4 | 5:1\n설명 줄\n## Good\n6:1,3,4 | 5:1,3,4 | 4:1,3\n", w);
            Check(bad.Count == 1 && bad[0].Name == "Good" && w.Count == 2, "잘못된 블록·빈 스케일은 경고 후 제외");
        }

        static void KeyMapTests()
        {
            // keyboard.md 5장 표 전체
            string[] table =
            {
                "2|Esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12",
                "3|` 1 2 3 4 5 6 7 8 9 0 - = Backspace",
                "4|Tab Q W E R T Y U I O P [ ] \\",
                "5|CapsLock A S D F G H J K L ; ' Enter",
                "6|L_Shift Z X C V B N M , . / R_Shift",
            };
            bool tableOk = true;
            foreach (string row in table)
            {
                int str = row[0] - '0';
                string[] names = row.Substring(2).Split(' ');
                for (int f = 0; f < Fretboard.FretColumns; f++)
                {
                    int expect = f < names.Length ? KeyCodes.Parse(names[f].Replace('_', ' ')) : -1;
                    if (keys.KeyAt(new Pos(str, f)) != expect) { tableOk = false; Console.WriteLine("  다름: {0}현 {1}프렛", str, f); }
                }
            }
            Check(tableOk, "기본 키 매칭 = 5장 표 전체");

            Check(keys.KeyAt(new Pos(2, 12)) == KeyCodes.Parse("F12"), "2현 12프렛 = F12");
            Check(keys.KeyAt(new Pos(5, 12)) == KeyCodes.Enter, "5현 12프렛 = Enter");
            Check(keys.KeyAt(new Pos(6, 11)) == KeyCodes.RShift, "6현 11프렛 = R Shift");
            Check(keys.KeyAt(new Pos(4, 13)) == KeyCodes.Parse("\\"), "4현 13프렛 = \\");

            var m = KeyMap.CreateDefault();
            int z = KeyCodes.Parse("Z");
            m.Assign(new Pos(2, 13), z);
            Pos p;
            Check(m.KeyAt(new Pos(6, 1)) == -1 && m.TryGetPos(z, out p) && p.Code == new Pos(2, 13).Code, "중복 키는 기존 칸을 비움");
            var round = KeyMap.Deserialize(m.Serialize());
            Check(round.KeyAt(new Pos(2, 13)) == z && round.KeyAt(new Pos(6, 1)) == -1, "키 매칭 저장/복원");
        }

        static JudgeConfig Cfg(string scale, Direction dir, int interval, bool pair, int start)
        {
            return new JudgeConfig { Scale = Scale(scale), Dir = dir, Interval = interval, PairPattern = pair, StartDegree = start };
        }

        static void FullModeTests()
        {
            var minorUp = Cfg("Natural Minor", Direction.Up, 2, false, 1);
            string ex = "z c v a d f q e t y 3 5 6 F4 F6 F8";
            Check(Run(minorUp, ex) == All('O', ex), "마이너 상행 2도 예시 (y = 1 같은 음)");
            Check(Run(minorUp, "z c v a d f q e t 1 3 5 F2 F4 F6") == "OOOOOOOOOOOOOOO", "마이너 전체 문자열");
            Check(Run(minorUp, "z c v a d f q e t y 3 5 6 F4 F6 F7") == "OOOOOOOOOOOOOOOX", "F7(F#)은 X");
            Check(Run(minorUp, "z c x v a") == "OOXXX", "틀리면 계속 X");
            Check(Run(Cfg("Major (", Direction.Up, 2, false, 1), "c b m d g j r t u 4 5 7 F5 F7 F8") == "OOOOOOOOOOOOOOO", "메이저 전체 문자열");

            Check(Run(Cfg("Natural Minor", Direction.Down, 2, false, 1), "e q f d a v c z") == "OOOOOOOO", "마이너 하행");
            Check(Run(Cfg("Natural Minor", Direction.Down, 2, false, 1), "z c") == "OX", "하행에서 위로 가면 X");
            Check(Run(Cfg("Natural Minor", Direction.Mixed, 2, false, 1), "z c v c z c") == "OOOOOO", "혼합: 오르내림 모두 O");
            Check(Run(Cfg("Natural Minor", Direction.Mixed, 2, false, 1), "z v") == "OX", "혼합에서도 간격은 지켜야 함");

            // 시작 음 3 (F minor의 3번째 음 = Ab, 6현 4프렛 v) → 루트 F
            Check(Run(Cfg("Natural Minor", Direction.Up, 2, false, 3), "v a d f q e") == "OOOOOO", "시작 음 3에서 상행");
            Check(Run(Cfg("Natural Minor", Direction.Up, 2, false, 3), "v a s") == "OOX", "시작 음 3: 루트 역산 후 B는 X");

            // F minor 3도 건너뛰기: F Ab C Eb G Bb Db F
            Check(Run(Cfg("Natural Minor", Direction.Up, 3, false, 1), "z v d q t 3 6 F6") == "OOOOOOOO", "3도 건너뛰기");
            // 3도 짝 패턴: F-Ab, G-Bb, Ab-C, Bb-Db
            Check(Run(Cfg("Natural Minor", Direction.Up, 3, true, 1), "z v c a v d a f") == "OOOOOOOO", "3도 짝 패턴");
            Check(Run(Cfg("Natural Minor", Direction.Up, 3, true, 1), "z v d") == "OOX", "짝 패턴에서 건너뛰기로 치면 X");
            // 3도 짝 패턴 하행: F-Db, Eb-C, Db
            Check(Run(Cfg("Natural Minor", Direction.Down, 3, true, 1), "e f q d f") == "OOOOO", "3도 짝 패턴 하행");
            // 4도 건너뛰기 하행: F(4현3) C(5현3) G(6현3)
            Check(Run(Cfg("Natural Minor", Direction.Down, 4, false, 1), "e d c") == "OOO", "4도 하행");

            // 펜타토닉: F minor pentatonic F Ab Bb C Eb F
            Check(Run(Cfg("Minor Pentatonic", Direction.Up, 2, false, 1), "z v a d q e") == "OOOOOO", "마이너 펜타토닉 상행");
            Check(Run(Cfg("Minor Pentatonic", Direction.Up, 2, false, 1), "z c") == "OX", "펜타토닉에 없는 G는 X");
        }

        static JudgeConfig BlockCfg(string scale, Direction dir, int start)
        {
            return new JudgeConfig { Scale = Scale(scale), BlockMode = true, Dir = dir, StartDegree = start, Interval = 2 };
        }

        static void BlockModeTests()
        {
            var minor = BlockCfg("Natural Minor", Direction.Up, 1);
            Check(Run(minor, "v a s f q w r 1") == "OOOOOOOO", "마이너 1번 블록");
            Check(Run(minor, "v n s f h w r y") == "OOOOOOOO", "마이너 2번 블록");
            Check(Run(minor, "x v b s f g w r") == "OOOOOOOO", "마이너 3번 블록");
            Check(Run(minor, "b s d g w e t 2") == "OOOOOOOO", "프렛을 옮겨도 모양이 같으면 O");
            Check(Run(minor, "z c v a d f q r") == "OOOOOOOX", "모양이 다르면 X");
            Check(Run(minor, "z c v a d f q e t y 3 5 6 F4 F6") == "OOOOOOOOOOOOOOO", "블록 이어치기 (7.2 예시, 2현 보정)");
            Check(Run(minor, "z c v a d f q e t 1 3 5 F2 F4 F6") == "OOOOOOOOOOOOOOO", "이어칠 때 다른 블록(2번)도 인정");
            Check(Run(minor, "e t y 3 5 6 F4 F6") == "OOOOOOOO", "4현에서 시작한 블록 (2현 보정)");
            Check(Run(minor, "e t y 3 5 6 F3 F5") == "OOOOOOXX", "2현 보정 안 하면 X");
            Check(Run(BlockCfg("Natural Minor", Direction.Down, 1), "e q f d a v c z") == "OOOOOOOO", "블록 하행");
            Check(Run(BlockCfg("Natural Minor", Direction.Down, 1), "e t") == "OX", "블록 하행에서 위로 가면 X");
            Check(Run(BlockCfg("Natural Minor", Direction.Down, 1), "z") == "X", "6현 루트에서는 내려갈 블록이 없어 X");
            Check(Run(BlockCfg("Natural Minor", Direction.Mixed, 1), "z c v c z c v") == "OOOOOOO", "블록 혼합");
            Check(Run(BlockCfg("Natural Minor", Direction.Up, 3), "v a d f q e") == "OOOOOO", "블록 시작 음 3");
            Check(Run(BlockCfg("Natural Minor", Direction.Up, 3), "v a d f q e t y 3") == "OOOOOOOOO", "블록 시작 음 3에서 이어치기");
            Check(Run(BlockCfg("Natural Minor", Direction.Down, 1), "F6 F4 F2 5 3 1 t e q f d a v c z") == "OOOOOOOOOOOOOOO", "블록 하행 이어치기 (2번 → 3번)");
            Check(Run(BlockCfg("Natural Minor", Direction.Down, 3), "v c z") == "OOO", "블록 하행 시작 음 3");
            Check(Run(BlockCfg("Natural Minor", Direction.Down, 1), "a") == "X", "5현 루트에서는 내려갈 블록이 없어 X");
            Check(Run(BlockCfg("Natural Minor", Direction.Up, 1), "1") == "X", "3현 루트에서는 올라갈 블록이 없어 X");
            // 혼합: 옥타브(e)까지 올라갔다 내려왔다 다시 올라가 다음 블록으로
            Check(Run(BlockCfg("Natural Minor", Direction.Mixed, 1), "z c v a d f q e q e t 1 3") == "OOOOOOOOOOOOO", "혼합: 옥타브에서 오르내리기와 다른 블록(2번)으로 이어 가기");

            var major = BlockCfg("Major (", Direction.Up, 1);
            Check(Run(major, "b s f g w r 1 2") == "OOOOOOOO", "메이저 블록 bsfgwr12");
            Check(Run(major, "v n d f h e t y") == "OOOOOOOO", "메이저 블록 vndfhety");
            Check(Run(major, "z c b a d g w e") == "OOOOOOOO", "메이저 블록 zcbadgwe");
            Check(Run(major, "c b m d g j r t") == "OOOOOOOO", "메이저 예시 cbmdgjrt (7.2)");

            var j = new Judge(minor);
            foreach (Pos p in PosList("z x")) j.Feed(p);
            j.Reset();
            Check(j.Feed(PosList("z")[0]) && j.Feed(PosList("c")[0]), "방향키(Reset) 후 다시 O");
        }

        static void RouterTests()
        {
            var r = new InputRouter();
            int toggles = 0, resets = 0;
            var notes = new List<int>();
            var captured = new List<int>();
            r.ToggleRequested += delegate { toggles++; r.Active = !r.Active; };
            r.ResetRequested += delegate { resets++; };
            r.NoteKey += notes.Add;
            r.CaptureKey += captured.Add;
            int z = KeyCodes.Parse("Z");

            Check(!r.Process(z, 0x5A, true) && !r.Process(z, 0x5A, false), "비활성: Z 통과");
            Check(!r.Process(KeyCodes.LCtrl, 0xA2, true), "비활성: Ctrl 통과");
            Check(r.Process(KeyCodes.Enter, 0x0D, true) && toggles == 1 && r.Active, "Ctrl+Enter → 활성");
            Check(r.Process(KeyCodes.Enter, 0x0D, false), "토글한 Enter는 뗄 때도 막음");
            Check(!r.Process(KeyCodes.LCtrl, 0xA2, false), "활성 전에 누른 Ctrl은 뗄 때 통과");

            Check(r.Process(z, 0x5A, true) && notes.Count == 1, "활성: Z 막고 판정");
            Check(r.Process(z, 0x5A, true) && notes.Count == 1, "자동 반복 무시");
            Check(r.Process(z, 0x5A, false), "활성: Z 뗄 때도 막음");
            Check(r.Process(KeyCodes.Enter, 0x0D, true) && notes.Count == 2 && toggles == 1, "Ctrl 없는 Enter = 음");
            r.Process(KeyCodes.Enter, 0x0D, false);
            Check(r.Process(KeyCodes.Up, 0x26, true) && resets == 1 && notes.Count == 2, "방향키 = 초기화");
            r.Process(KeyCodes.Up, 0x26, false);
            Check(!r.Process(KeyCodes.LWin, 0x5B, true), "Win 통과");
            r.Process(KeyCodes.LWin, 0x5B, false);
            Check(!r.Process(KeyCodes.LAlt, 0xA4, true) && !r.Process(KeyCodes.Tab, 0x09, true), "Alt+Tab 통과");
            r.Process(KeyCodes.Tab, 0x09, false);
            r.Process(KeyCodes.LAlt, 0xA4, false);
            Check(r.Process(KeyCodes.Tab, 0x09, true) && notes.Count == 3, "Alt 없는 Tab = 음");
            r.Process(KeyCodes.Tab, 0x09, false);

            r.Process(KeyCodes.LCtrl, 0xA2, true);
            Check(r.Process(KeyCodes.Enter, 0x0D, true) && !r.Active && toggles == 2, "Ctrl+Enter → 비활성");
            r.Process(KeyCodes.Enter, 0x0D, false);
            Check(r.Process(KeyCodes.LCtrl, 0xA2, false), "활성 중 누른 Ctrl은 뗄 때도 막음");

            r.Capturing = true;
            Check(r.Process(z, 0x5A, true) && captured.Count == 1 && captured[0] == z, "캡처: 단일 키 할당");
            int x = KeyCodes.Parse("X");
            Check(r.Process(x, 0x58, true) && captured.Count == 1, "캡처: 두 키 조합은 무시");
            r.Process(x, 0x58, false); r.Process(z, 0x5A, false);
            Check(r.Process(KeyCodes.Left, 0x25, true) && captured.Count == 1, "캡처: 방향키 무시");
            r.Process(KeyCodes.Left, 0x25, false);
        }

        static void RouterRepeatTests()
        {
            int t = 0, toggles = 0;
            var notes = new List<int>();
            var r = new InputRouter();
            r.Clock = () => t;
            r.ToggleRequested += delegate { toggles++; r.Active = !r.Active; };
            r.NoteKey += notes.Add;

            // 켜기 전에 누른 Ctrl을 오래 누르고 있어도, 반복은 처음 결정(통과)을 따라 뗌까지 통과한다.
            Check(!r.Process(KeyCodes.LCtrl, 0xA2, true), "Ctrl 누름 통과");
            Check(r.Process(KeyCodes.Enter, 0x0D, true) && r.Active, "Ctrl+Enter 활성");
            t = 300;
            Check(r.Process(KeyCodes.Enter, 0x0D, true) && toggles == 1, "Enter 반복은 다시 토글하지 않음");
            r.Process(KeyCodes.Enter, 0x0D, false);
            t = 600;
            Check(!r.Process(KeyCodes.LCtrl, 0xA2, true), "Ctrl 반복도 통과");
            Check(!r.Process(KeyCodes.LCtrl, 0xA2, false), "Ctrl 뗌 통과 (눌린 채로 남지 않음)");

            // 활성 중 누른 Ctrl의 뗌을 놓쳐도, 오래 지나면 버려서 Enter가 다시 음으로 동작한다.
            t = 1000;
            r.Process(KeyCodes.LCtrl, 0xA2, true);
            t = 5000;
            int before = notes.Count;
            Check(r.Process(KeyCodes.Enter, 0x0D, true) && toggles == 1 && notes.Count == before + 1, "뗌을 놓친 Ctrl은 오래되면 무시");
            r.Process(KeyCodes.Enter, 0x0D, false);
        }

        static void AltTabTests()
        {
            int t = 0;
            bool altPhysical = true;
            var notes = new List<int>();
            var r = new InputRouter { Active = true };
            r.Clock = () => t;
            r.IsPhysicallyDown = vk => vk == 0xA4 && altPhysical;
            r.NoteKey += notes.Add;

            Check(!r.Process(KeyCodes.LAlt, 0xA4, true), "Alt 통과");
            Check(!r.Process(KeyCodes.Tab, 0x09, true), "Alt+Tab 통과");
            r.Process(KeyCodes.Tab, 0x09, false);
            t = 3000;   // 창 목록을 오래 보다가 Tab을 다시 누름
            Check(!r.Process(KeyCodes.Tab, 0x09, true) && notes.Count == 0, "Alt를 오래 누르고 있어도 Alt+Tab 유지");
            r.Process(KeyCodes.Tab, 0x09, false);

            altPhysical = false;  // Alt 뗌을 놓친 경우
            t = 6000;
            Check(r.Process(KeyCodes.Tab, 0x09, true) && notes.Count == 1, "실제로 뗀 Alt는 정리되어 Tab = 음");
        }

        static void SettingsTests()
        {
            var big = new Settings { Interval = 50000 };
            big.Normalize();
            Check(big.Interval == Settings.MaxInterval, "간격 상한 보정 (입력칸 최대값과 같음)");

            string dir = Path.Combine(Path.GetTempPath(), "kg_settings_test");
            Directory.CreateDirectory(dir);
            var s = new Settings { Scale = "Dorian (수정 필요)", BlockMode = true, Dir = Direction.Mixed, Interval = 1, StartDegree = 0, Opacity = 5, PairPattern = true };
            s.Keys.Assign(new Pos(2, 13), KeyCodes.Parse("Z"));
            s.Save(dir);
            Settings l = Settings.Load(dir);
            Check(l.Scale == s.Scale && l.BlockMode && l.Dir == Direction.Mixed && l.PairPattern, "설정 저장/불러오기");
            Check(l.Interval == 2 && l.StartDegree == 1 && l.Opacity == 10, "불러올 때 범위 보정 (간격 2 미만 → 2, 투명도 10~100)");
            Check(l.Keys.KeyAt(new Pos(2, 13)) == KeyCodes.Parse("Z") && l.Keys.KeyAt(new Pos(6, 1)) == -1, "키 매칭도 저장");
            File.Delete(Path.Combine(dir, Settings.FileName));
        }
    }
}
