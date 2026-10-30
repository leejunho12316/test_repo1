using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    // 트레이 아이콘, 훅, 판정, 화면을 묶는다.
    public class AppController : ApplicationContext
    {
        readonly string dir = AppDomain.CurrentDomain.BaseDirectory;
        readonly InputRouter router = new InputRouter();
        readonly KeyboardHook hook;
        readonly NotifyIcon tray;
        readonly ToolStripMenuItem toggleItem;
        readonly OverlayForm overlay = new OverlayForm();
        readonly Icon idleIcon = MakeIcon(Color.Gray), activeIcon = MakeIcon(Color.FromArgb(40, 180, 90));

        public readonly Settings Settings;
        public readonly List<ScaleDef> Scales;
        public readonly List<string> Warnings = new List<string>();
        Judge judge;
        SettingsForm settingsForm;
        KeyMapForm keyMapForm;

        public AppController()
        {
            Settings = Settings.Load(dir);
            Scales = Patterns.Load(dir, Warnings);
            ApplySettings();

            var menu = new ContextMenuStrip();
            menu.Items.Add("설정 열기", null, delegate { OpenSettings(); });
            toggleItem = new ToolStripMenuItem("연습 시작 (Ctrl+Enter)", null, delegate { Toggle(); });
            menu.Items.Add(toggleItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, delegate { ExitThread(); });

            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { OpenSettings(); };
            UpdateTray();

            router.ToggleRequested += Toggle;
            var h = overlay.Handle;  // BeginInvoke용 핸들을 미리 만든다
            router.ResetRequested += delegate { if (judge != null) judge.Reset(); };
            router.NoteKey += OnNoteKey;
            hook = new KeyboardHook(router);

            if (Warnings.Count > 0)
                tray.ShowBalloonTip(5000, "키보드 기타", string.Join("\n", Warnings.Take(3)), ToolTipIcon.Warning);
            OpenSettings();
        }

        public InputRouter Router { get { return router; } }

        // 선택된 스케일 (없으면 첫 번째)
        public ScaleDef CurrentScale
        {
            get
            {
                ScaleDef s = Scales.FirstOrDefault(x => x.Name == Settings.Scale);
                return s ?? Scales.FirstOrDefault();
            }
        }

        // 설정을 판정에 바로 반영하고 입력을 초기화한다.
        public void ApplySettings()
        {
            Settings.Normalize();
            ScaleDef scale = CurrentScale;
            if (scale != null)
            {
                Settings.Scale = scale.Name;
                if (Settings.StartDegree > scale.N) Settings.StartDegree = 1;
            }
            judge = scale == null ? null : new Judge(new JudgeConfig
            {
                Scale = scale,
                BlockMode = Settings.BlockMode,
                StartDegree = Settings.StartDegree,
                Dir = Settings.Dir,
                Interval = Settings.Interval,
                PairPattern = Settings.PairPattern
            });
            overlay.SetOpacity(Settings.Opacity);
            overlay.Corner = Settings.OverlayCorner;
            Settings.Save(dir);
        }

        void OpenKeyMap()
        {
            if (keyMapForm == null || keyMapForm.IsDisposed) keyMapForm = new KeyMapForm(this);
            keyMapForm.Show();
            keyMapForm.Activate();
        }

        public void SaveSettings()
        {
            Settings.Save(dir);
        }

        public void SetOpacity(int percent)
        {
            Settings.Opacity = percent;
            Settings.Normalize();
            overlay.SetOpacity(Settings.Opacity);
            if (keyMapForm != null && !keyMapForm.IsDisposed) keyMapForm.Opacity = Settings.Opacity / 100.0;
            Settings.Save(dir);
        }

        // O/X 위치를 바꾸고 새 위치에 O를 잠깐 보여준다.
        public void SetOverlayCorner(int corner)
        {
            Settings.OverlayCorner = corner;
            Settings.Normalize();
            overlay.Corner = Settings.OverlayCorner;
            overlay.ShowResult(true);
            Settings.Save(dir);
        }

        void OnNoteKey(int id)
        {
            Pos p;
            if (judge == null || !Settings.Keys.TryGetPos(id, out p)) return;
            bool ok = judge.Feed(p);
            // 훅 콜백을 빨리 끝내기 위해 화면 갱신은 나중에
            overlay.BeginInvoke((Action)(() => overlay.ShowResult(ok)));
        }

        // 훅 콜백에서도 불린다. 상태는 바로 바꾸고 화면 갱신은 나중에 한다.
        void Toggle()
        {
            router.Active = !router.Active;
            if (judge != null) judge.Reset();
            bool active = router.Active;
            overlay.BeginInvoke((Action)(() =>
            {
                UpdateTray();
                overlay.ShowState(active);
            }));
        }

        void UpdateTray()
        {
            tray.Icon = router.Active ? activeIcon : idleIcon;
            tray.Text = router.Active ? "키보드 기타 - 연습 중" : "키보드 기타 - 대기 중";
            toggleItem.Text = router.Active ? "연습 중지 (Ctrl+Enter)" : "연습 시작 (Ctrl+Enter)";
        }

        void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this);
                settingsForm.PrefsRequested += OpenKeyMap;
            }
            settingsForm.Show();
            settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        static Icon MakeIcon(Color c)
        {
            using (var bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                using (var b = new SolidBrush(c))
                using (var f = new Font("Segoe UI Symbol", 8f, FontStyle.Bold))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.FillEllipse(b, 1, 1, 14, 14);
                    g.DrawString("♪", f, Brushes.White, 1, 0);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        protected override void ExitThreadCore()
        {
            hook.Dispose();
            tray.Visible = false;
            tray.Dispose();
            overlay.Dispose();
            if (settingsForm != null) settingsForm.Dispose();
            base.ExitThreadCore();
        }
    }
}
