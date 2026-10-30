using System;
using System.Collections.Generic;

namespace KeyboardGuitar
{
    // 블록 모드 판정. 블록 모양이 맞으면 프렛·줄 위치와 상관없이 인정하고, 블록이 여러 개면 아무 블록이나 인정한다.
    // 간격 설정은 쓰지 않는다 (keyboard.md 11장 기본값 8).
    public class BlockJudge
    {
        readonly JudgeConfig cfg;
        bool started;
        List<State> states = new List<State>();

        // 블록 하나를 지판에 놓은 위치와 그 안에서 마지막으로 친 칸
        struct State
        {
            public int B;    // 블록 번호
            public int Ds;   // 줄 이동: 실제 줄 = 모양 줄 - Ds (Ds > 0 이면 가는 줄 쪽)
            public int Df;   // 프렛 이동 (2현 보정 전)
            public int Idx;  // 블록 안의 칸 번호
        }

        public BlockJudge(JudgeConfig cfg)
        {
            this.cfg = cfg;
        }

        public void Reset()
        {
            started = false;
            states.Clear();
        }

        List<Block> Blocks { get { return cfg.Scale.Blocks; } }

        // 블록 b의 i번째 칸이 실제로 놓이는 줄·프렛
        Pos Actual(int b, int ds, int df, int i)
        {
            Pos c = Blocks[b].Cells[i];
            int str = c.Str - ds;
            return new Pos(str, c.Fret + df + Fretboard.Correction(str));
        }

        // 블록 b의 i번째 칸이 실제 위치 p에 오도록 놓는다. 줄이 지판을 벗어나면 false.
        bool Place(int b, int i, Pos p, out State st)
        {
            Pos c = Blocks[b].Cells[i];
            st = new State { B = b, Ds = c.Str - p.Str, Idx = i };
            st.Df = p.Fret - c.Fret - Fretboard.Correction(p.Str);
            foreach (Pos cell in Blocks[b].Cells)
                if (!Fretboard.ValidString(cell.Str - st.Ds)) return false;
            return true;
        }

        public bool Feed(Pos p)
        {
            var next = new List<State>();
            if (!started)
            {
                started = true;
                for (int b = 0; b < Blocks.Count; b++)
                    foreach (int i in StartCells(b))
                    {
                        State st;
                        if (Place(b, i, p, out st) && !next.Contains(st)) next.Add(st);
                    }
            }
            else
            {
                foreach (State st in states)
                    foreach (State ns in Steps(st))
                        if (Actual(ns.B, ns.Ds, ns.Df, ns.Idx).Code == p.Code && !next.Contains(ns)) next.Add(ns);
            }
            states = next;
            return next.Count > 0;
        }

        // 시작 음의 칸: 시작 음 번호 - 1. 루트는 맨 아래 칸이나 맨 위 옥타브 칸일 수 있다.
        // 하행에서 루트는 맨 위 칸에서 내려오고, 혼합은 둘 다 가능하다.
        IEnumerable<int> StartCells(int b)
        {
            int k = cfg.StartDegree - 1, last = Blocks[b].Cells.Count - 1;
            if (k > 0 || cfg.Dir != Direction.Down) yield return k;
            if (k == 0 && cfg.Dir != Direction.Up) yield return last;
        }

        // 다음에 칠 수 있는 칸. 루트·옥타브 칸(블록 끝)에 있으면 그 자리를 루트로 하는
        // 아무 블록으로나 갈아탈 수 있다. (블록을 다 치면 이어 가기, 혼합에서 다른 블록으로 되돌아가기)
        IEnumerable<State> Steps(State st)
        {
            int last = Blocks[st.B].Cells.Count - 1;
            var from = new List<State> { st };
            if (st.Idx == 0 || st.Idx == last)
            {
                Pos here = Actual(st.B, st.Ds, st.Df, st.Idx);
                for (int b = 0; b < Blocks.Count; b++)
                    foreach (int r in new[] { 0, Blocks[b].Cells.Count - 1 })
                    {
                        State ns;
                        if (Place(b, r, here, out ns) && !from.Contains(ns)) from.Add(ns);
                    }
            }
            foreach (State s in from)
                foreach (int dir in Dirs())
                {
                    int ni = s.Idx + dir;
                    if (ni < 0 || ni > Blocks[s.B].Cells.Count - 1) continue;
                    State ns = s;
                    ns.Idx = ni;
                    yield return ns;
                }
        }

        int[] Dirs()
        {
            if (cfg.Dir == Direction.Up) return new[] { 1 };
            if (cfg.Dir == Direction.Down) return new[] { -1 };
            return new[] { 1, -1 };
        }
    }
}
