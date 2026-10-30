using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardGuitar
{
    // 설정 페이지. 값을 바꾸면 바로 반영·저장하고 입력을 초기화한다. 닫기는 트레이로 숨기기.
    public class SettingsForm : Form
    {
        readonly AppController app;
        readonly TrackBar opacityBar = new TrackBar();
        readonly Label opacityLabel = new Label();
        readonly Button prefsButton = new Button();
        readonly ComboBox scaleBox = new ComboBox();
        readonly CheckBox blockBox = new CheckBox();
        readonly ComboBox startBox = new ComboBox();
        readonly ComboBox dirBox = new ComboBox();
        readonly NumericUpDown intervalBox = new NumericUpDown();
        readonly CheckBox pairBox = new CheckBox();
        readonly TableLayoutPanel body = new TableLayoutPanel();
        readonly Button previewButton = new Button();
        readonly Panel previewHost = new Panel();
        readonly FretboardView preview = new FretboardView();
        bool loading;

        public event Action PrefsRequested;

        public SettingsForm(AppController app)
        {
            this.app = app;
            Text = "키보드 기타";
            Font = new Font("맑은 고딕", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(300, 260);

            BuildTopMenu();
            BuildBody();
            LoadValues();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FitHeight();
            WindowPlacement.CenterOnScreen(this);
        }

        protected void FitHeight()
        {
            // 창을 띄우기 전에는 Visible이 항상 false라서 높이를 직접 더한다.
            int h = TopMenu.Height + body.PreferredSize.Height + 6;
            if (app.Settings.Preview) h += previewHost.Height;
            ClientSize = new Size(ClientSize.Width, h);
        }

        // 미리보기: 블록 모드면 블록들, 아니면 스케일 전체 음
        void UpdatePreview()
        {
            bool on = app.Settings.Preview;
            previewButton.Text = on ? "끄기" : "켜기";
            previewHost.Visible = on;
            ScaleDef scale = app.CurrentScale;
            if (on && scale != null)
            {
                preview.Width = ClientSize.Width - 16;
                preview.SetDiagrams(app.Settings.BlockMode ? FretboardView.ForBlocks(scale) : FretboardView.ForScale(scale));
                previewHost.Height = preview.Height + 8;
            }
            FitHeight();
        }

        protected Panel TopMenu { get; private set; }
        protected TableLayoutPanel Body { get { return body; } }

        void BuildTopMenu()
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = SystemColors.ControlLight };
            var label = new Label { Text = "투명도", AutoSize = true, Location = new Point(8, 11) };
            opacityBar.Minimum = 10;
            opacityBar.Maximum = 100;
            opacityBar.TickFrequency = 10;
            opacityBar.SmallChange = 5;
            opacityBar.LargeChange = 10;
            opacityBar.TickStyle = TickStyle.None;
            opacityBar.AutoSize = false;
            opacityBar.SetBounds(52, 6, 120, 24);
            opacityBar.ValueChanged += delegate { OnOpacityChanged(); };
            opacityLabel.AutoSize = true;
            opacityLabel.Location = new Point(174, 11);
            prefsButton.Text = "환경설정";
            prefsButton.SetBounds(220, 6, 74, 24);
            prefsButton.Click += delegate { if (PrefsRequested != null) PrefsRequested(); };
            top.Controls.AddRange(new Control[] { label, opacityBar, opacityLabel, prefsButton });
            Controls.Add(top);
            TopMenu = top;
        }

        void BuildBody()
        {
            body.Dock = DockStyle.Top;
            body.AutoSize = true;
            body.ColumnCount = 2;
            body.Padding = new Padding(8, 8, 8, 4);
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            scaleBox.DropDownStyle = ComboBoxStyle.DropDownList;
            scaleBox.Dock = DockStyle.Fill;
            foreach (ScaleDef s in app.Scales) scaleBox.Items.Add(s.Name);
            scaleBox.SelectedIndexChanged += delegate { FillStartDegrees(); OnChanged(); };
            AddRow("스케일", scaleBox);

            blockBox.Text = "블록 모드";
            blockBox.AutoSize = true;
            blockBox.CheckedChanged += delegate { OnChanged(); };
            AddRow("", blockBox);

            startBox.DropDownStyle = ComboBoxStyle.DropDownList;
            startBox.Width = 90;
            startBox.SelectedIndexChanged += delegate { OnChanged(); };
            AddRow("시작 음", startBox);

            dirBox.DropDownStyle = ComboBoxStyle.DropDownList;
            dirBox.Width = 90;
            dirBox.Items.AddRange(new object[] { "상행", "하행", "혼합" });
            dirBox.SelectedIndexChanged += delegate { OnChanged(); };
            AddRow("방향", dirBox);

            // 2 미만을 입력하면 NumericUpDown이 최솟값 2로 바꾼다. 상한은 사실상 없음.
            intervalBox.Minimum = 2;
            intervalBox.Maximum = Settings.MaxInterval;
            intervalBox.Width = 60;
            intervalBox.ValueChanged += delegate { OnChanged(); };
            pairBox.Text = "짝 패턴";
            pairBox.AutoSize = true;
            pairBox.CheckedChanged += delegate { OnChanged(); };
            var intervalRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            intervalRow.Controls.Add(intervalBox);
            intervalRow.Controls.Add(new Label { Text = "도", AutoSize = true, Margin = new Padding(2, 6, 10, 0) });
            intervalRow.Controls.Add(pairBox);
            AddRow("간격", intervalRow);

            previewButton.AutoSize = true;
            previewButton.Click += delegate
            {
                app.Settings.Preview = !app.Settings.Preview;
                app.SaveSettings();
                UpdatePreview();
            };
            AddRow("미리보기", previewButton);

            var hint = new Label
            {
                Text = "Ctrl+Enter: 연습 시작/중지\n방향키: 처음부터 다시",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 8, 3, 0)
            };
            body.Controls.Add(hint);
            body.SetColumnSpan(hint, 2);

            Controls.Add(body);
            body.BringToFront();

            previewHost.Dock = DockStyle.Top;
            previewHost.Padding = new Padding(8, 4, 8, 4);
            preview.Location = new Point(8, 4);
            previewHost.Controls.Add(preview);
            Controls.Add(previewHost);
            previewHost.BringToFront();
        }

        void AddRow(string label, Control c)
        {
            body.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) });
            c.Margin = new Padding(3, 3, 3, 3);
            body.Controls.Add(c);
        }

        void LoadValues()
        {
            loading = true;
            Settings s = app.Settings;
            opacityBar.Value = s.Opacity;
            scaleBox.SelectedItem = app.CurrentScale == null ? null : app.CurrentScale.Name;
            FillStartDegrees();
            blockBox.Checked = s.BlockMode;
            dirBox.SelectedIndex = (int)s.Dir;
            intervalBox.Value = s.Interval;
            pairBox.Checked = s.PairPattern;
            loading = false;
            UpdateEnabled();
            UpdatePreview();
        }

        // 시작 음 목록 = 1 ~ N (스케일 구성음 수)
        void FillStartDegrees()
        {
            ScaleDef scale = app.Scales.Find(x => x.Name == (string)scaleBox.SelectedItem);
            int n = scale == null ? 7 : scale.N;
            int keep = startBox.SelectedIndex >= 0 ? startBox.SelectedIndex + 1 : app.Settings.StartDegree;
            bool wasLoading = loading;
            loading = true;
            startBox.Items.Clear();
            for (int i = 1; i <= n; i++) startBox.Items.Add(i == 1 ? "1 (루트)" : i.ToString());
            startBox.SelectedIndex = keep <= n ? keep - 1 : 0;
            loading = wasLoading;
        }

        void UpdateEnabled()
        {
            opacityLabel.Text = opacityBar.Value + "%";
            Opacity = opacityBar.Value / 100.0;
            // 블록 모드는 간격 설정을 쓰지 않는다 (11장 기본값 8). 짝 패턴은 3도 이상일 때만.
            intervalBox.Enabled = !blockBox.Checked;
            pairBox.Enabled = !blockBox.Checked && intervalBox.Value >= 3;
        }

        // 투명도는 판정과 무관하므로 입력을 초기화하지 않는다.
        void OnOpacityChanged()
        {
            if (loading) return;
            UpdateEnabled();
            app.SetOpacity(opacityBar.Value);
        }

        void OnChanged()
        {
            if (loading) return;
            UpdateEnabled();
            Settings s = app.Settings;
            if (scaleBox.SelectedItem != null) s.Scale = (string)scaleBox.SelectedItem;
            s.BlockMode = blockBox.Checked;
            s.StartDegree = startBox.SelectedIndex + 1;
            s.Dir = (Direction)Math.Max(0, dirBox.SelectedIndex);
            s.Interval = (int)intervalBox.Value;
            s.PairPattern = pairBox.Checked;
            app.ApplySettings();
            UpdatePreview();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }
    }
}
