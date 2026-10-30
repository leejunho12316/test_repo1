using System;
using System.Collections.Generic;

namespace KeyboardGuitar
{
    // 지판 결과 표시용 기록: 입력을 초기화하기 전까지 누른 음과 O/X. 같은 칸은 마지막 결과로 덮는다.
    public class NoteHistory
    {
        readonly List<Pos> order = new List<Pos>();
        readonly Dictionary<int, bool> results = new Dictionary<int, bool>();   // Pos.Code -> O/X

        public int Count { get { return order.Count; } }

        public void Add(Pos p, bool ok)
        {
            if (!results.ContainsKey(p.Code)) order.Add(p);
            results[p.Code] = ok;
        }

        public void Clear()
        {
            order.Clear();
            results.Clear();
        }

        // 화면 갱신을 나중에 하므로 복사본을 넘긴다.
        public List<KeyValuePair<Pos, bool>> Snapshot()
        {
            var list = new List<KeyValuePair<Pos, bool>>();
            foreach (Pos p in order) list.Add(new KeyValuePair<Pos, bool>(p, results[p.Code]));
            return list;
        }

        // 보여줄 프렛 범위: 누른 음이 있는 프렛 앞뒤로 1프렛 여유, 지판(0 ~ FretColumns-1) 안으로 자른다.
        public void FretRange(out int min, out int max)
        {
            min = 0;
            max = Fretboard.FretColumns - 1;
            if (order.Count == 0) return;
            int lo = int.MaxValue, hi = int.MinValue;
            foreach (Pos p in order)
            {
                lo = Math.Min(lo, p.Fret);
                hi = Math.Max(hi, p.Fret);
            }
            min = Math.Max(0, lo - 1);
            max = Math.Min(Fretboard.FretColumns - 1, hi + 1);
        }
    }
}
