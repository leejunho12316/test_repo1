using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    static class WindowPlacement
    {
        // 마우스가 있는 모니터의 작업 영역 가운데에 두고, 화면 밖으로 나가지 않게 한다.
        public static void CenterOnScreen(Form f)
        {
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = wa.Left + (wa.Width - f.Width) / 2;
            int y = wa.Top + (wa.Height - f.Height) / 2;
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - f.Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - f.Height));
            f.Location = new Point(x, y);
        }
    }
}
