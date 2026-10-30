using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    // 스케일 미리보기 지판. 키 이름 없이 모양만, 루트는 빨간색.
    public class FretboardView : Control
    {
        public class Diagram
        {
            public readonly List<Pos> Dots = new List<Pos>();
            public readonly HashSet<int> Roots = new HashSet<int>();   // Pos.Code
            public int MinFret, MaxFret;
        }

        const int StringGap = 11, FretWidth = 20, TopPad = 6, NumberHeight = 14, DiagramGap = 10;
        List<Diagram> diagrams = new List<Diagram>();

        public FretboardView()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        public void SetDiagrams(List<Diagram> list)
        {
            diagrams = list;
            Height = DiagramHeight * Math.Max(1, Rows(Width)) + (Rows(Width) - 1) * DiagramGap + 4;
            Invalidate();
        }

        static int DiagramHeight { get { return TopPad + StringGap * 4 + NumberHeight + 6; } }

        static int DiagramWidth(Diagram d)
        {
            return (d.MaxFret - d.MinFret + 1) * FretWidth + 8;
        }

        // 좌우로 나란히 놓고 넘치면 다음 줄로
        int Rows(int width)
        {
            int rows = 1, x = 0;
            foreach (Diagram d in diagrams)
            {
                int w = DiagramWidth(d);
                if (x > 0 && x + w > width) { rows++; x = 0; }
                x += w + DiagramGap;
            }
            return rows;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int x = 0, y = 0;
            foreach (Diagram d in diagrams)
            {
                int w = DiagramWidth(d);
                if (x > 0 && x + w > Width) { x = 0; y += DiagramHeight + DiagramGap; }
                Draw(g, d, x, y);
                x += w + DiagramGap;
            }
        }

        void Draw(Graphics g, Diagram d, int ox, int oy)
        {
            int frets = d.MaxFret - d.MinFret + 1;
            int left = ox + 4, right = left + frets * FretWidth;
            using (var line = new Pen(Color.FromArgb(150, 150, 150)))
            using (var nut = new Pen(Color.FromArgb(60, 60, 60), 3))
            using (var font = new Font("맑은 고딕", 7f))
            using (var root = new SolidBrush(Color.FromArgb(220, 50, 50)))
            using (var dot = new SolidBrush(Color.FromArgb(50, 50, 60)))
            {
                // 줄: 위가 2현, 아래가 6현
                for (int s = 0; s < 5; s++)
                    g.DrawLine(line, left, oy + TopPad + s * StringGap, right, oy + TopPad + s * StringGap);
                for (int f = 0; f <= frets; f++)
                {
                    int fx = left + f * FretWidth;
                    bool isNut = d.MinFret + f == 1;
                    g.DrawLine(isNut ? nut : line, fx, oy + TopPad, fx, oy + TopPad + 4 * StringGap);
                }
                var center = new StringFormat { Alignment = StringAlignment.Center };
                for (int f = 0; f < frets; f++)
                    g.DrawString((d.MinFret + f).ToString(), font, Brushes.Gray,
                        left + f * FretWidth + FretWidth / 2f, oy + TopPad + 4 * StringGap + 3, center);

                const int r = 4;
                foreach (Pos p in d.Dots)
                {
                    float cx = left + (p.Fret - d.MinFret) * FretWidth + FretWidth / 2f;
                    float cy = oy + TopPad + (p.Str - 2) * StringGap;
                    g.FillEllipse(d.Roots.Contains(p.Code) ? root : dot, cx - r, cy - r, r * 2, r * 2);
                }
            }
        }

        // 전체 모드: 루트를 6현 5프렛(A)에 두고 0~12프렛의 스케일 음 전체
        public static List<Diagram> ForScale(ScaleDef scale)
        {
            var d = new Diagram { MinFret = 0, MaxFret = 12 };
            int rootPitch = Fretboard.Pitch(new Pos(6, 5));
            for (int str = Fretboard.MinString; str <= Fretboard.MaxString; str++)
                for (int f = 0; f <= 12; f++)
                {
                    var p = new Pos(str, f);
                    int rel = ((Fretboard.Pitch(p) - rootPitch) % 12 + 12) % 12;
                    if (!scale.Intervals.Contains(rel)) continue;
                    d.Dots.Add(p);
                    if (rel == 0) d.Roots.Add(p.Code);
                }
            return new List<Diagram> { d };
        }

        // 블록 모드: 블록마다 하나씩, 가장 낮은 프렛이 1이 되게 놓는다.
        public static List<Diagram> ForBlocks(ScaleDef scale)
        {
            var list = new List<Diagram>();
            foreach (Block b in scale.Blocks)
            {
                int shift = 1 - b.Cells.Min(c => c.Fret);
                var d = new Diagram();
                var real = b.Cells.Select(c => new Pos(c.Str, c.Fret + shift + Fretboard.Correction(c.Str))).ToList();
                int rootPitch = Fretboard.Pitch(real[0]);
                foreach (Pos p in real)
                {
                    d.Dots.Add(p);
                    if ((Fretboard.Pitch(p) - rootPitch) % 12 == 0) d.Roots.Add(p.Code);
                }
                d.MinFret = Math.Max(0, real.Min(p => p.Fret) - 1);
                d.MaxFret = real.Max(p => p.Fret);
                list.Add(d);
            }
            return list;
        }
    }
}
