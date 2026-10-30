using System;
using System.Threading;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "KeyboardGuitar_SingleInstance", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    Application.Run(new AppController());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "키보드 기타", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
