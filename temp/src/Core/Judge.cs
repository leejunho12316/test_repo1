using System;
using System.Collections.Generic;

namespace KeyboardGuitar
{
    public enum Direction { Up = 0, Down = 1, Mixed = 2 }

    public class JudgeConfig
    {
        public ScaleDef Scale;
        public bool BlockMode;
        public int StartDegree = 1;     // 1~N
        public Direction Dir = Direction.Up;
        public int Interval = 2;        // 2 이상
        public bool PairPattern;        // 간격 3 이상일 때만 의미 있음
    }

    // 입력 한 음마다 O/X를 판정한다. 틀리면 Reset 전까지 계속 X.
    public class Judge
    {
        readonly JudgeConfig cfg;
        readonly BlockJudge block;
        bool started, failed;

        // 전체 모드: 스케일 음 번호 s (0 = 루트, N = 옥타브 위 루트, 음수 = 아래 옥타브)
        int rootPitch;
        List<FullState> full = new List<FullState>();

        struct FullState
        {
            public int Base;   // 건너뛰기: 현재 음, 짝 패턴: 짝의 기준 음
            public int Phase;  // 짝 패턴: 0 = 기준 음을 친 상태, 1 = 짝 음을 친 상태
            public FullState(int b, int ph) { Base = b; Phase = ph; }
        }

        public Judge(JudgeConfig cfg)
        {
            this.cfg = cfg;
            if (cfg.BlockMode) block = new BlockJudge(cfg);
        }

        public bool Failed { get { return failed; } }

        public void Reset()
        {
            started = false;
            failed = false;
            full.Clear();
            if (block != null) block.Reset();
        }

        // pos = 실제 줄·프렛. true = O
        public bool Feed(Pos pos)
        {
            if (failed) return false;
            bool ok = block != null ? block.Feed(pos) : FeedFull(pos);
            if (!ok) failed = true;
            return ok;
        }

        int[] Dirs()
        {
            if (cfg.Dir == Direction.Up) return new[] { 1 };
            if (cfg.Dir == Direction.Down) return new[] { -1 };
            return new[] { 1, -1 };
        }

        bool Pair { get { return cfg.PairPattern && cfg.Interval >= 3; } }

        bool FeedFull(Pos pos)
        {
            int[] iv = cfg.Scale.Intervals;
            int pitch = Fretboard.Pitch(pos);
            if (!started)
            {
                started = true;
                int s0 = cfg.StartDegree - 1;
                rootPitch = pitch - iv[s0];
                full.Clear();
                full.Add(new FullState(s0, 0));
                return true;
            }

            int step = Math.Max(2, cfg.Interval) - 1;
            var next = new List<FullState>();
            foreach (FullState st in full)
            {
                foreach (int dir in Dirs())
                {
                    int s;
                    FullState ns;
                    if (!Pair) { s = st.Base + dir * step; ns = new FullState(s, 0); }
                    else if (st.Phase == 0) { s = st.Base + dir * step; ns = new FullState(st.Base, 1); }
                    else { s = st.Base + dir; ns = new FullState(s, 0); }

                    if (PitchOf(s) == pitch && !next.Contains(ns)) next.Add(ns);
                }
            }
            full = next;
            return next.Count > 0;
        }

        int PitchOf(int s)
        {
            int n = cfg.Scale.N;
            return rootPitch + 12 * FloorDiv(s, n) + cfg.Scale.Intervals[Mod(s, n)];
        }

        static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        static int Mod(int a, int b)
        {
            int m = a % b;
            return m < 0 ? m + b : m;
        }
    }
}
