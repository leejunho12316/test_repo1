using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    // 화면 오른쪽 맨 아래의 작은 O/X 표시. 포커스를 가져가지 않고 클릭은 뒤로 통과한다.
    public class OverlayForm : Form
    {
        const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        const int BoxSize = 26, EdgeGap = 4;

        readonly Timer hideTimer = new Timer { Interval = 500 };
        string text = "";
        Color color = Color.White;

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

        public void ShowResult(bool ok)
        {
            Flash(ok ? "O" : "X", ok ? Color.FromArgb(80, 220, 120) : Color.FromArgb(255, 90, 90), BoxSize);
        }

        public void ShowState(bool active)
        {
            Flash(active ? "ON" : "OFF", Color.White, 40);
        }

        void Flash(string t, Color c, int width)
        {
            text = t;
            color = c;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Bounds = new Rectangle(wa.Right - width - EdgeGap, wa.Bottom - BoxSize - EdgeGap, width, BoxSize);
            if (!Visible) Show();
            Invalidate();
            hideTimer.Stop();
            hideTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var f = new Font("Segoe UI", text.Length > 1 ? 9f : 12f, FontStyle.Bold))
            using (var b = new SolidBrush(color))
            {
                var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(text, f, b, ClientRectangle, fmt);
            }
        }
    }
}
