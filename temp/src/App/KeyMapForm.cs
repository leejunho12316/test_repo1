using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    // 환경설정: 키 매칭. 칸 클릭 → 키 입력으로 할당, 우클릭으로 비우기, 변경 즉시 반영.
    public class KeyMapForm : Form
    {
        const int CellW = 48, CellH = 30, LeftW = 44, HeaderH = 22;

        readonly AppController app;
        readonly Panel grid = new DoubleBufferedPanel();
        Pos? selected;
        bool isActive;

        class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel() { DoubleBuffered = true; }
        }

        public KeyMapForm(AppController app)
        {
            this.app = app;
            Text = "환경설정 - 키 매칭";
            Font = new Font("맑은 고딕", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;

            grid.SetBounds(8, 8, LeftW + CellW * Fretboard.FretColumns + 1, HeaderH + CellH * 5 + 1);
            grid.BackColor = Color.White;
            grid.Paint += PaintGrid;
            grid.MouseDown += OnGridMouseDown;

            var help = new Label
            {
                Text = "칸을 클릭한 뒤 키를 누르면 할당됩니다.  우클릭: 비우기",
                AutoSize = true,
                Location = new Point(8, grid.Bottom + 12)
            };
            var reset = new Button { Text = "기본값 복원", Size = new Size(100, 26) };
            reset.Location = new Point(grid.Right - reset.Width, grid.Bottom + 6);
            reset.Click += delegate
            {
                app.Settings.Keys = KeyMap.CreateDefault();
                Changed();
            };
            Controls.AddRange(new Control[] { grid, help, reset });
            ClientSize = new Size(grid.Right + 8, reset.Bottom + 8);

            Load += delegate { WindowPlacement.CenterOnScreen(this); };
            app.Router.CaptureKey += OnCaptureKey;
            Activated += delegate { isActive = true; UpdateCapturing(); };
            Deactivate += delegate { isActive = false; UpdateCapturing(); };
        }

        // 이 창이 활성이고 칸을 골랐을 때만 키를 가로챈다.
        void UpdateCapturing()
        {
            app.Router.Capturing = selected.HasValue && isActive;
        }

        // 훅 콜백에서 불린다. 할당은 나중에 UI 스레드에서.
        void OnCaptureKey(int id)
        {
            if (!selected.HasValue || IsDisposed) return;
            Pos p = selected.Value;
            BeginInvoke((Action)(() =>
            {
                app.Settings.Keys.Assign(p, id);
                // 할당하면 선택을 풀어 다음 키가 같은 칸을 덮어쓰지 않게 한다.
                selected = null;
                UpdateCapturing();
                Changed();
            }));
        }

        void Changed()
        {
            app.ApplySettings();
            grid.Invalidate();
        }

        static Pos? CellAt(Point pt)
        {
            int col = (pt.X - LeftW) / CellW, row = (pt.Y - HeaderH) / CellH;
            if (pt.X < LeftW || pt.Y < HeaderH || col >= Fretboard.FretColumns || row >= 5) return null;
            return new Pos(Fretboard.MinString + row, col);
        }

        void OnGridMouseDown(object sender, MouseEventArgs e)
        {
            Pos? p = CellAt(e.Location);
            if (!p.HasValue) return;
            if (e.Button == MouseButtons.Right)
            {
                app.Settings.Keys.Clear(p.Value);
                Changed();
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                selected = p;
                UpdateCapturing();
                grid.Invalidate();
            }
        }

        void PaintGrid(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var small = new Font("맑은 고딕", 8f))
            using (var sel = new SolidBrush(Color.FromArgb(255, 236, 150)))
            using (var head = new SolidBrush(Color.FromArgb(240, 240, 240)))
            {
                g.FillRectangle(head, 0, 0, grid.Width, HeaderH);
                g.FillRectangle(head, 0, 0, LeftW, grid.Height);
                for (int f = 0; f < Fretboard.FretColumns; f++)
                    g.DrawString(f.ToString(), small, Brushes.DimGray, new RectangleF(LeftW + f * CellW, 0, CellW, HeaderH), center);

                for (int row = 0; row < 5; row++)
                {
                    int str = Fretboard.MinString + row;
                    int y = HeaderH + row * CellH;
                    g.DrawString(str + "현", Font, Brushes.Black, new RectangleF(0, y, LeftW, CellH), center);
                    for (int f = 0; f < Fretboard.FretColumns; f++)
                    {
                        var p = new Pos(str, f);
                        var rect = new Rectangle(LeftW + f * CellW, y, CellW, CellH);
                        if (selected.HasValue && selected.Value.Code == p.Code) g.FillRectangle(sel, rect);
                        g.DrawRectangle(Pens.LightGray, rect);
                        int key = app.Settings.Keys.KeyAt(p);
                        if (key >= 0) g.DrawString(ShortName(key), small, Brushes.Black, rect, center);
                    }
                }
            }
        }

        // 칸이 좁아 긴 이름은 줄여 쓴다.
        static string ShortName(int key)
        {
            if (key == KeyCodes.Backspace) return "BkSp";
            if (key == KeyCodes.CapsLock) return "Caps";
            return KeyCodes.Name(key);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            app.Router.CaptureKey -= OnCaptureKey;
            app.Router.Capturing = false;
            base.OnFormClosed(e);
        }
    }
}
