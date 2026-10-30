using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    // 화면 구석의 작은 O/X 표시 (지판 + O/X 모드면 누른 음을 쌓은 지판도). 포커스를 가져가지 않고 클릭은 뒤로 통과한다.
    public class OverlayForm : Form
    {
        const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        const int BoxSize = 26, EdgeGap = 4;
        const int StringGap = 9, FretWidth = 16, BoardPad = 6, NumberHeight = 15;

        readonly Timer hideTimer = new Timer { Interval = 500 };
        bool held;   // ←를 누르고 있는 동안은 자동으로 숨기지 않는다
        string text = "";
        public int Corner;   // Settings.OverlayCorner
        Color color = Color.White;
        List<KeyValuePair<Pos, bool>> notes;   // null이면 글자만
        int minFret, maxFret;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(32, 32, 32);
            Size = new Size(BoxSize, BoxSize);
            DoubleBuffered = true;
            hideTimer.Tick += delegate { hideTimer.Stop(); Hide(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        // 클릭 통과(WS_EX_TRANSPARENT)는 레이어드 창에서만 동작하므로 불투명도를 1 미만으로 둔다.
        public void SetOpacity(int percent)
        {
            Opacity = Math.Min(0.99, percent / 100.0);
        }

        static readonly Color OkColor = Color.FromArgb(80, 220, 120), NgColor = Color.FromArgb(255, 90, 90);

        public void ShowResult(bool ok)
        {
            notes = null;
            Flash(ok ? "O" : "X", ok ? OkColor : NgColor, BoxSize, BoxSize);
        }

        // 지판(minFret~maxFret 프렛, 2~6현)에 누른 음을 점으로 찍고 오른쪽에 O/X (ok가 null이면 O/X 없이)
        public void ShowBoard(List<KeyValuePair<Pos, bool>> list, int min, int max, bool? ok)
        {
            notes = list;
            minFret = min;
            maxFret = max;
            int w = BoardPad * 2 + (max - min + 1) * FretWidth + BoxSize;
            int h = BoardPad * 2 + StringGap * 4 + NumberHeight;
            if (ok.HasValue) Flash(ok.Value ? "O" : "X", ok.Value ? OkColor : NgColor, w, h);
            else Flash("", Color.White, w, h);
        }

        // ← 누름/뗌. 누르는 동안 들어온 음도 지판에 바로 반영되고 사라지지 않는다.
        public void Hold(List<KeyValuePair<Pos, bool>> list, int min, int max, bool? ok)
        {
            held = true;
            ShowBoard(list, min, max, ok);
        }

        public void Release()
        {
            if (!held) return;
            held = false;
            hideTimer.Stop();
            Hide();
        }

        public void ShowState(bool active)
        {
            Release();
            notes = null;
            Flash(active ? "ON" : "OFF", Color.White, 40, BoxSize);
        }

        void Flash(string t, Color c, int width, int height)
        {
            text = t;
            color = c;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            bool left = Corner == 1 || Corner == 3, top = Corner >= 2;
            int x = left ? wa.Left + EdgeGap : wa.Right - width - EdgeGap;
            int y = top ? wa.Top + EdgeGap : wa.Bottom - height - EdgeGap;
            Bounds = new Rectangle(x, y, width, height);
            if (!Visible) Show();
            Invalidate();
            hideTimer.Stop();
            if (!held) hideTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Rectangle textRect = ClientRectangle;
            if (notes != null)
            {
                DrawBoard(e.Graphics);
                textRect = new Rectangle(Width - BoxSize, 0, BoxSize, Height);
            }
            using (var f = new Font("Segoe UI", text.Length > 1 ? 9f : 12f, FontStyle.Bold))
            using (var b = new SolidBrush(color))
            {
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(text, f, b, textRect, fmt);
            }
        }

        // 줄: 위가 2현, 아래가 6현. 0프렛 칸과 1프렛 칸 사이가 너트.
        void DrawBoard(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int frets = maxFret - minFret + 1;
            int left = BoardPad, right = left + frets * FretWidth, top = BoardPad, bottom = top + StringGap * 4;
            using (var line = new Pen(Color.FromArgb(110, 110, 110)))
            using (var nut = new Pen(Color.FromArgb(200, 200, 200), 2))
            using (var font = new Font("Segoe UI", 6.5f))
            using (var ok = new SolidBrush(OkColor))
            using (var ng = new SolidBrush(NgColor))
            {
                for (int s = 0; s < 5; s++)
                    g.DrawLine(line, left, top + s * StringGap, right, top + s * StringGap);
                var center = new StringFormat { Alignment = StringAlignment.Center };
                for (int f = 0; f <= frets; f++)
                {
                    int fx = left + f * FretWidth;
                    g.DrawLine(minFret + f == 1 ? nut : line, fx, top, fx, bottom);
                    if (f < frets)
                        g.DrawString((minFret + f).ToString(), font, Brushes.Gray, fx + FretWidth / 2f, bottom + 4, center);
                }
                const int r = 3;
                foreach (KeyValuePair<Pos, bool> n in notes)
                {
                    float cx = left + (n.Key.Fret - minFret) * FretWidth + FretWidth / 2f;
                    float cy = top + (n.Key.Str - Fretboard.MinString) * StringGap;
                    g.FillEllipse(n.Value ? ok : ng, cx - r, cy - r, r * 2, r * 2);
                }
            }
        }
    }
}
