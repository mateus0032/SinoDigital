using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using static SinoDigital.Theme;

namespace SinoDigital
{
    public class MainForm : Form
    {
        [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint f);
        const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x00000001;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "SinoDigital";

        readonly AudioEngine audio = new AudioEngine();

        // topo
        Panel topbar;
        Comp brandMark;
        Label brandName, badge;
        readonly List<TabLink> tabs = new List<TabLink>();
        Button upcomingBtn, notifBtn, minBtn, maxBtn, fullBtn, closeBtn, powerBtn;

        // banner "tocando agora"
        Panel banner; Label npLabel, npSub; Button npStop;

        // telas
        ClockPanel home;
        Page schedPage, holPage, setPage;
        Stack schedCard, holForm, holList;
        string current = "home";
        bool topbarShown, pinned, fullscreen;
        DateTime lastHover = DateTime.MinValue;

        // toast
        Label toast; readonly Timer toastTimer = new Timer { Interval = 3200 };

        readonly Timer tick = new Timer { Interval = 250 };
        readonly Timer hoverTimer = new Timer { Interval = 100 };
        NotifyIcon tray;
        bool reallyExit;
        UpcomingDialog upcomingDlg;

        // formulário de feriado
        DateField hDate, hDateEnd; CheckBox hYearly; TextBox hDesc; Label hTitle; Button hSubmit, hCancel;
        string editingHolidayId;
        Switch swKeep, swStartup, swDuck;

        // redução de volume pelo microfone
        readonly Timer duckTimer = new Timer { Interval = 80 };
        readonly MicMeter mic = new MicMeter();
        DateTime lastVoice = DateTime.MinValue;
        double duckGain = 1.0;
        Label micLevel;
        NumBox duckVol, duckSens;

        void DuckTick()
        {
            var st = Store.Data.settings;
            bool watch = st.duckEnabled || (setPage != null && setPage.Visible);
            float peak = watch ? mic.Peak() : 0f;
            if (micLevel != null && setPage != null && setPage.Visible)
                micLevel.Text = mic.Available ? "Nível atual do microfone: " + (int)Math.Round(peak * 100) + "%" : "Nenhum microfone padrão encontrado.";

            double target = 1.0;
            if (st.duckEnabled && audio.Current != null)
            {
                var now = DateTime.Now;
                if (peak * 100 >= st.duckSensitivity) lastVoice = now;
                if ((now - lastVoice).TotalMilliseconds < 1500)
                    target = Math.Max(0, Math.Min(1, st.duckVolume / 100.0));
            }
            if (duckGain > target) duckGain = Math.Max(target, duckGain - 0.25);   // desce rápido
            else if (duckGain < target) duckGain = Math.Min(target, duckGain + 0.05); // volta suave
            if (Math.Abs(audio.Gain - duckGain) > 0.001) audio.Gain = duckGain;
        }

        public MainForm(string[] args)
        {
            Text = "Sino Digital";
            BackColor = cBg; ForeColor = cText; Font = Ui(10);
            KeyPreview = true;
            DoubleBuffered = true;
            StartPosition = FormStartPosition.Manual;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            home = new ClockPanel();
            home.Click += (s, e) => ToggleTopbar();
            Controls.Add(home);

            BuildSchedulePage();
            BuildHolidayPage();
            BuildSettingsPage();
            BuildTopbar();
            BuildBanner();
            BuildToast();
            BuildTray();

            audio.Changed += UpdateBanner;
            audio.Failed += m => Toast("Não foi possível tocar o áudio: " + m);

            tick.Tick += (s, e) => Tick();
            hoverTimer.Tick += (s, e) => HoverCheck();
            duckTimer.Tick += (s, e) => { try { DuckTick(); } catch (Exception ex) { Program.Log("duck: " + ex); } };
            toastTimer.Tick += (s, e) => { toastTimer.Stop(); toast.Visible = false; };

            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(S(900), S(560));
            SetFullscreen(true);
            Resize += (s, e) => DoLayout();
            Load += (s, e) =>
            {
                ShowScreen("home");
                RefreshAll();
                tick.Start(); hoverTimer.Start(); duckTimer.Start();
                ApplyKeepAlive();
                RefreshStartupEntry();
                Tick();
            };
            Shown += (s, e) => { Activate(); };
        }

        // ================== construção ==================
        void BuildTopbar()
        {
            topbar = new Panel { BackColor = cBg, Visible = false };
            topbar.Paint += (s, e) => { using (var p = new Pen(cLine)) e.Graphics.DrawLine(p, 0, topbar.Height - 1, topbar.Width, topbar.Height - 1); };

            brandMark = new Comp { Size = new Size(S(28), S(28)), BackColor = cBg };
            brandMark.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = RoundRect(new RectangleF(0.5f, 0.5f, S(27), S(27)), S(7)))
                {
                    using (var b = new SolidBrush(cCard)) e.Graphics.FillPath(b, p);
                    using (var pen = new Pen(cLine)) e.Graphics.DrawPath(pen, p);
                }
                TextRenderer.DrawText(e.Graphics, "🔔", Emoji(9), new Rectangle(0, 0, S(28), S(28)), cAmber,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            brandName = Lbl("SINO DIGITAL", Mono(10, true), cText);

            string[,] t = { { "home", "Início" }, { "schedule", "Agendamentos" }, { "holidays", "Feriados" }, { "settings", "Configurações" } };
            for (int i = 0; i < 4; i++)
            {
                var tl = new TabLink(t[i, 1]) { Tag = t[i, 0] };
                tl.Click += (s, e) => ShowScreen((string)((Control)s).Tag);
                tabs.Add(tl);
                topbar.Controls.Add(tl);
            }

            upcomingBtn = IconBtn("⏰", Emoji(10), cText, cAmberDim, cBg, "Próximos alarmes de hoje");
            upcomingBtn.Click += (s, e) => OpenUpcoming();
            notifBtn = IconBtn("🔕", Emoji(10), cText, cAmberDim, cBg, "Ativar notificações");
            notifBtn.Click += (s, e) =>
            {
                Store.Data.settings.notifications = !Store.Data.settings.notifications;
                Store.Save();
                RefreshNotifBtn();
                Toast(Store.Data.settings.notifications ? "Notificações ativadas." : "Notificações desativadas.");
            };
            minBtn = IconBtn("—", Sym(10), cText, cAmberDim, cBg, "Minimizar");
            minBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
            fullBtn = IconBtn("⛶", Sym(10), cText, cAmberDim, cBg, "Tela cheia / janela (F11)");
            fullBtn.Click += (s, e) => SetFullscreen(!fullscreen);
            maxBtn = IconBtn("□", Sym(10), cText, cAmberDim, cBg, "Maximizar / restaurar");
            maxBtn.Click += (s, e) => ToggleMaximize();
            closeBtn = IconBtn("✕", Sym(10), cText, cAmberDim, cBg, "Fechar (continua na bandeja do Windows)");
            closeBtn.Click += (s, e) => Close();
            powerBtn = IconBtn("⏻", Sym(10), cRed, cRed, cBg, "Desligar o Sino Digital (encerra de vez)");
            powerBtn.Click += (s, e) => ExitApp();
            foreach (Control c in new Control[] { topbar, brandMark, brandName })
            {
                c.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Clicks == 1) StartDrag(); };
                c.DoubleClick += (s, e) => ToggleMaximize();
            }

            badge = new Label
            {
                AutoSize = false, Size = new Size(S(17), S(17)), BackColor = cAmber, ForeColor = cInk,
                Font = Mono(7.5f, true), TextAlign = ContentAlignment.MiddleCenter, Visible = false, Text = "0"
            };
            using (var gp = new GraphicsPath()) { gp.AddEllipse(0, 0, S(17), S(17)); badge.Region = new Region(gp); }

            topbar.Controls.AddRange(new Control[] { brandMark, brandName, upcomingBtn, notifBtn, minBtn, maxBtn, fullBtn, closeBtn, powerBtn, badge });
            Controls.Add(topbar);
            RefreshNotifBtn();
        }

        void LayoutTopbar()
        {
            int h = topbar.Height, x = S(20);
            brandMark.Location = new Point(x, (h - brandMark.Height) / 2); x += brandMark.Width + S(9);
            brandName.Location = new Point(x, (h - brandName.Height) / 2); x += brandName.Width + S(30);
            foreach (var t in tabs) { t.Location = new Point(x, (h - t.Height) / 2); x += t.Width + S(18); }
            int r = topbar.Width - S(20);
            foreach (var b in new[] { powerBtn, closeBtn, fullBtn, maxBtn, minBtn, notifBtn, upcomingBtn })
            {
                r -= b.Width; b.Location = new Point(r, (h - b.Height) / 2); r -= S(8);
            }
            badge.Location = new Point(upcomingBtn.Right - badge.Width + S(5), upcomingBtn.Top - S(5));
            badge.BringToFront();
        }

        void BuildBanner()
        {
            banner = new Panel { BackColor = Color.FromArgb(20, 15, 4), Visible = false };
            banner.Paint += (s, e) => { using (var p = new Pen(cAmberDim)) e.Graphics.DrawLine(p, 0, banner.Height - 1, banner.Width, banner.Height - 1); };
            var ico = Lbl("🔔", Emoji(16), cAmber);
            npLabel = Lbl("—", Ui(11, true), cText);
            npSub = Lbl("Tocando agora", Mono(9.5f), cAmber);
            npStop = new Button
            {
                Text = "Parar", FlatStyle = FlatStyle.Flat, BackColor = cRed, ForeColor = Color.FromArgb(42, 10, 10),
                Font = Ui(10, true), Cursor = Cursors.Hand, Size = new Size(S(92), S(38)), UseVisualStyleBackColor = false
            };
            npStop.FlatAppearance.BorderSize = 0;
            npStop.Click += (s, e) => audio.Stop();
            banner.Controls.AddRange(new Control[] { ico, npLabel, npSub, npStop });
            banner.Resize += (s, e) =>
            {
                ico.Location = new Point(S(20), (banner.Height - ico.Height) / 2);
                npStop.Location = new Point(banner.Width - S(20) - npStop.Width, (banner.Height - npStop.Height) / 2);
                npLabel.Location = new Point(S(64), S(8));
                npSub.Location = new Point(S(64), S(8) + npLabel.Height);
            };
            Controls.Add(banner);
        }

        void BuildToast()
        {
            toast = new Label
            {
                AutoSize = false, Visible = false, BackColor = cCard, ForeColor = cAmber, Font = Mono(10),
                TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(S(14), 0, S(14), 0)
            };
            toast.Paint += (s, e) => { using (var p = new Pen(cAmberDim)) e.Graphics.DrawRectangle(p, 0, 0, toast.Width - 1, toast.Height - 1); };
            Controls.Add(toast);
        }

        void BuildTray()
        {
            tray = new NotifyIcon { Text = "Sino Digital", Visible = true };
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { tray.Icon = SystemIcons.Application; }
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir Sino Digital", null, (s, e) => RestoreWindow());
            menu.Items.Add("Sair", null, (s, e) => ExitApp());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => RestoreWindow();
        }

        public void RestoreWindow()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show(); Activate();
            if (fullscreen) SetFullscreen(true);
        }

        // ================== layout / telas ==================
        int Border() { return (!fullscreen && WindowState == FormWindowState.Normal) ? S(5) : 0; }

        void DoLayout()
        {
            if (topbar == null || WindowState == FormWindowState.Minimized) return;
            int b = Border();
            bool maximized = !fullscreen && WindowState == FormWindowState.Maximized;
            string glyph = maximized ? "❐" : "□";
            if (maxBtn.Text != glyph)
            {
                maxBtn.Text = glyph;
                Tip.SetToolTip(maxBtn, maximized ? "Restaurar" : "Maximizar");
            }
            BackColor = b > 0 ? cLine : cBg;
            var cs = new Size(ClientSize.Width - 2 * b, ClientSize.Height - 2 * b);
            int th = S(56);
            bool isHome = current == "home";
            var body = new Rectangle(b, b + th, cs.Width, Math.Max(0, cs.Height - th));
            home.Bounds = new Rectangle(b, b, cs.Width, cs.Height);
            schedPage.Bounds = body; holPage.Bounds = body; setPage.Bounds = body;
            topbar.SetBounds(b, b, cs.Width, th);
            topbar.Visible = !isHome || topbarShown;
            LayoutTopbar();
            topbar.BringToFront();
            banner.SetBounds(b, b, cs.Width, S(58));
            if (banner.Visible) banner.BringToFront();
            PositionToast();
        }

        void ShowScreen(string name)
        {
            current = name;
            foreach (var t in tabs) { t.Active = (string)t.Tag == name; t.Invalidate(); }
            home.Visible = name == "home";
            schedPage.Visible = name == "schedule";
            holPage.Visible = name == "holidays";
            setPage.Visible = name == "settings";
            if (name == "home") { topbarShown = false; pinned = false; }
            DoLayout();
        }

        void ToggleTopbar()
        {
            if (current != "home") return;
            topbarShown = !topbarShown;
            pinned = topbarShown;
            lastHover = DateTime.Now;
            DoLayout();
        }

        // passar o mouse no topo da tela mostra o menu (na tela do relógio)
        void HoverCheck()
        {
            if (current != "home" || WindowState == FormWindowState.Minimized) return;
            if (Form.ActiveForm != this) return;
            var p = PointToClient(Cursor.Position);
            int zone = topbarShown ? S(56) + S(14) : S(10);
            bool inTop = p.Y >= -S(4) && p.Y <= zone && p.X >= 0 && p.X <= ClientSize.Width;
            if (inTop)
            {
                lastHover = DateTime.Now;
                if (!topbarShown) { topbarShown = true; DoLayout(); }
            }
            else if (topbarShown && !pinned && (DateTime.Now - lastHover).TotalMilliseconds > 1800)
            {
                topbarShown = false; DoLayout();
            }
        }

        Rectangle normalBounds = Rectangle.Empty;

        void RememberNormal()
        {
            if (Visible && !fullscreen && WindowState == FormWindowState.Normal) normalBounds = Bounds;
        }

        void GoNormal()
        {
            fullscreen = false;
            WindowState = FormWindowState.Normal;
            var wa = Screen.FromControl(this).WorkingArea;
            if (normalBounds.IsEmpty || !wa.IntersectsWith(normalBounds))
            {
                int w = (int)(wa.Width * 0.8), h = (int)(wa.Height * 0.8);
                normalBounds = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
            }
            Bounds = normalBounds;
            DoLayout();
        }

        public void SetFullscreen(bool on)
        {
            if (on)
            {
                RememberNormal();
                WindowState = FormWindowState.Normal;
                fullscreen = true;
                Bounds = Screen.FromControl(this).Bounds;
                DoLayout();
            }
            else GoNormal();
        }

        void ToggleMaximize()
        {
            if (!fullscreen && WindowState == FormWindowState.Maximized) { GoNormal(); return; }
            RememberNormal();
            fullscreen = false;
            MaximizedBounds = Screen.FromControl(this).WorkingArea;
            WindowState = FormWindowState.Maximized;
            DoLayout();
        }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        void StartDrag()
        {
            if (fullscreen || WindowState != FormWindowState.Normal) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.Style |= 0x20000; cp.ClassStyle |= 0x20000; return cp; } // MINIMIZEBOX + sombra
        }

        // redimensionar pelas bordas (janela sem moldura do Windows)
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x84 && Border() > 0) // WM_NCHITTEST
            {
                long l = (long)m.LParam;
                var pt = PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
                int e = S(8), w = ClientSize.Width, h = ClientSize.Height;
                bool L = pt.X < e, R = pt.X >= w - e, T = pt.Y < e, B = pt.Y >= h - e;
                int ht = 0;
                if (T && L) ht = 13; else if (T && R) ht = 14; else if (B && L) ht = 16; else if (B && R) ht = 17;
                else if (L) ht = 10; else if (R) ht = 11; else if (T) ht = 12; else if (B) ht = 15;
                if (ht != 0) { m.Result = (IntPtr)ht; return; }
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11) { SetFullscreen(!fullscreen); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                // fechar = ir para a bandeja; só encerra via "Sair" da bandeja ou botão desligar
                e.Cancel = true;
                Hide();
                if (!trayTipShown)
                {
                    trayTipShown = true;
                    try { tray.ShowBalloonTip(4000, "Sino Digital", "Continua em segundo plano. Clique no ícone da bandeja para abrir ou use \"Sair\" (botão direito).", ToolTipIcon.Info); } catch { }
                }
                return;
            }
            reallyExit = true;
            audio.Stop();
            SetThreadExecutionState(ES_CONTINUOUS);
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            base.OnFormClosing(e);
        }

        bool trayTipShown;
        bool ConfirmExit()
        {
            return MessageBox.Show(this, "Sair do Sino Digital?\nOs alarmes deixarão de tocar enquanto o app estiver fechado.",
                "Sino Digital", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }
        // encerra de verdade (botão ✕ do menu e "Sair" da bandeja)
        void ExitApp()
        {
            if (!Visible) RestoreWindow();
            if (!ConfirmExit()) return;
            reallyExit = true;
            Close();
        }

        // ================== toast / notificações ==================
        void PositionToast()
        {
            if (toast == null || !toast.Visible) return;
            int w = Math.Min(ClientSize.Width - S(40), TextRenderer.MeasureText(toast.Text, toast.Font).Width + S(44));
            toast.SetBounds((ClientSize.Width - w) / 2, ClientSize.Height - S(40) - S(48), w, S(44));
            toast.BringToFront();
        }

        public void Toast(string msg)
        {
            toast.Text = msg;
            toast.Visible = true;
            PositionToast();
            toastTimer.Stop(); toastTimer.Start();
        }

        void RefreshNotifBtn()
        {
            bool on = Store.Data.settings.notifications;
            notifBtn.Text = on ? "🔔" : "🔕";
            notifBtn.Tag = on ? "on" : "";
            notifBtn.ForeColor = on ? cAmber : cMuted;
            notifBtn.FlatAppearance.BorderColor = on ? cAmberDim : cLine;
            Tip.SetToolTip(notifBtn, on ? "Notificações ativas (clique para desativar)" : "Ativar notificações");
        }

        // ================== agendamentos ==================
        void BuildSchedulePage()
        {
            schedPage = new Page(cBg);
            var toolbar = new Comp { Height = S(42) };
            var add = Primary("+ Adicionar agendamento");
            add.Click += (s, e) => OpenSchedule(null, false, "NOVO AGENDAMENTO");
            toolbar.Controls.Add(add);
            schedPage.Add(toolbar);
            schedCard = new Stack { Bordered = true };
            schedPage.Add(schedCard);
            Controls.Add(schedPage);
        }

        void OpenSchedule(Schedule src, bool edit, string title)
        {
            using (var dlg = new ScheduleDialog(src, edit, title))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Toast(edit ? "Agendamento atualizado." : "Agendamento salvo.");
                    RefreshAll();
                }
            }
        }

        Row BuildScheduleRow(Schedule s)
        {
            var am = Store.FindAudio(s.audioId);
            var row = new Row();
            row.Time = Lbl(s.time, Mono(13, true), cAmber);
            row.Name = Lbl(!string.IsNullOrEmpty(s.name) ? s.name : (am != null ? am.name : "Agendamento"), Ui(10.5f, true), cText);
            row.Sub = Lbl(Store.Describe(s) + " · 🔊 " + (am != null ? am.name : "(áudio removido)") +
                (s.loopSeconds.HasValue && s.loopSeconds.Value > 0 ? " · 🔁 " + s.loopSeconds + "s" : ""), Ui(8.5f), cMuted);

            var test = IconBtn("▶", Sym(9), cAmber, cAmberDim, cPanel2, "Testar áudio");
            test.Click += (x, y) => PlaySchedule(s);
            var copy = IconBtn("⧉", Sym(10), cBlue, Color.FromArgb(59, 77, 138), cPanel2, "Duplicar");
            copy.Click += (x, y) => OpenSchedule(CloneFor(s), false, "NOVO AGENDAMENTO (copiado)");
            var edit = IconBtn("✎", Sym(10), cAmber, cAmberDim, cPanel2, "Editar");
            edit.Click += (x, y) => OpenSchedule(s, true, "EDITANDO AGENDAMENTO");
            var sw = new Switch(); sw.Init(s.active);
            Tip.SetToolTip(sw, "Ativo/inativo");
            sw.CheckedChanged += (x, y) => { s.active = sw.Checked; Store.Save(); RefreshUpcoming(true); };
            var del = IconBtn("✕", Sym(10), cRed, cRed, cPanel2, "Excluir");
            del.Click += (x, y) => { Store.Data.schedules.Remove(s); Store.Save(); RefreshAll(); };
            row.Actions.AddRange(new Control[] { test, copy, edit, sw, del });
            row.Finish();
            return row;
        }

        static Schedule CloneFor(Schedule s)
        {
            return new Schedule
            {
                id = null, time = s.time, name = s.name, recurrence = s.recurrence, audioId = s.audioId,
                loopSeconds = s.loopSeconds, active = true, days = s.days == null ? null : new List<int>(s.days), date = s.date
            };
        }

        void RenderSchedules()
        {
            var c = schedCard;
            c.SuspendLayout();
            c.ClearAll();
            c.Add(Eyebrow("AGENDAMENTOS CADASTRADOS"), S(14));
            var sorted = Store.Data.schedules.OrderBy(s => s.time, StringComparer.Ordinal).ToList();
            if (sorted.Count == 0) c.Add(new Note("Nenhum agendamento cadastrado."), 0);
            else
            {
                string[] keys = { "morning", "afternoon", "night" };
                string[] names = { "MATUTINO", "VESPERTINO", "NOTURNO" };
                string[] rng = { "06:00–12:30", "12:31–18:30", "18:31–05:59" };
                for (int i = 0; i < 3; i++)
                {
                    c.Add(Lbl(names[i] + "   " + rng[i], Mono(9, true), cMuted), S(8));
                    var g = sorted.Where(s => Store.Classify(s.time) == keys[i]).ToList();
                    if (g.Count == 0) c.Add(new Note("Nenhum agendamento nesse período."), S(18));
                    else for (int k = 0; k < g.Count; k++) c.Add(BuildScheduleRow(g[k]), k == g.Count - 1 ? S(18) : S(8));
                }
            }
            c.ResumeLayout(true);
            schedPage.Arrange();
        }

        // ================== feriados ==================
        void BuildHolidayPage()
        {
            holPage = new Page(cBg);
            holForm = new Stack { Bordered = true };
            hTitle = Eyebrow("NOVA EXCEÇÃO (SEM TOQUE)");
            holForm.Add(hTitle, S(14));

            hDate = new DateField();
            hDateEnd = new DateField { AllowEmpty = true, EmptyText = "Sem data final (clique para definir)", Width = S(280) };
            hDateEnd.Value = null;
            holForm.Add(MakeField("Data (início)", hDate, false), S(12));
            holForm.Add(MakeField("Data final (opcional — para período)", hDateEnd, false), S(12));
            hYearly = new CheckBox { Text = "Repetir todo ano", AutoSize = true, ForeColor = cText, BackColor = Color.Transparent, Font = Ui(10) };
            holForm.Add(hYearly, S(12));
            hDesc = new TextBox { BorderStyle = BorderStyle.FixedSingle };
            StyleInput(hDesc);
            Cue(hDesc, "Ex: Feriado nacional, recesso...");
            holForm.Add(MakeField("Descrição (opcional)", hDesc, true), S(14));

            var actions = new Comp { Height = S(42) };
            hSubmit = Primary("Adicionar exceção");
            hSubmit.Location = new Point(0, 0);
            hCancel = Secondary("Cancelar edição", cPanel);
            hCancel.Visible = false;
            actions.Controls.Add(hSubmit); actions.Controls.Add(hCancel);
            hSubmit.Click += (s, e) => SubmitHoliday();
            hCancel.Click += (s, e) => { ResetHolidayForm(); };
            actions.Place = c => { hCancel.Location = new Point(hSubmit.Right + S(10), 0); };
            holForm.Add(actions, 0);
            holPage.Add(holForm);

            holList = new Stack { Bordered = true };
            holPage.Add(holList);
            Controls.Add(holPage);
        }

        static Comp MakeField(string caption, Control inner, bool full)
        {
            var f = new Comp();
            var cap = Lbl(caption, Ui(9, true), cMuted);
            cap.Location = new Point(0, 0);
            f.Controls.Add(cap);
            inner.Location = new Point(0, S(24));
            f.Controls.Add(inner);
            f.Height = S(24) + Math.Max(inner.Height, S(28)) + S(2);
            if (full) f.Place = c => { inner.Width = c.Width; };
            return f;
        }

        void ResetHolidayForm()
        {
            editingHolidayId = null;
            hDate.Value = DateTime.Today;
            hDateEnd.Value = null;
            hYearly.Checked = false; hDesc.Text = "";
            hTitle.Text = "NOVA EXCEÇÃO (SEM TOQUE)";
            hSubmit.Text = "Adicionar exceção";
            hSubmit.Width = TextRenderer.MeasureText(hSubmit.Text, hSubmit.Font).Width + S(44);
            hCancel.Visible = false;
            holPage.Arrange();
        }

        void FillHolidayForm(Holiday h)
        {
            hDate.Value = Store.ParseDate(h.date);
            hDateEnd.Value = string.IsNullOrEmpty(h.dateEnd) ? (DateTime?)null : Store.ParseDate(h.dateEnd);
            hYearly.Checked = h.yearly;
            hDesc.Text = h.description ?? "";
        }

        void SubmitHoliday()
        {
            string date = Store.ToDateStr(hDate.Value ?? DateTime.Today);
            string end = hDateEnd.Value.HasValue ? Store.ToDateStr(hDateEnd.Value.Value) : "";
            if (end != "" && string.CompareOrdinal(end, date) < 0)
            {
                Toast("A data final não pode ser anterior à data inicial.");
                return;
            }
            if (end == date) end = "";
            string desc = hDesc.Text.Trim();
            bool yearly = hYearly.Checked;
            if (editingHolidayId != null)
            {
                var ex = Store.Data.holidays.FirstOrDefault(h => h.id == editingHolidayId);
                if (ex != null) { ex.date = date; ex.dateEnd = end; ex.yearly = yearly; ex.description = desc; }
                Toast("Exceção atualizada.");
            }
            else
            {
                Store.Data.holidays.Add(new Holiday { id = Store.NewId(), date = date, dateEnd = end, yearly = yearly, description = desc });
                Toast("Exceção adicionada.");
            }
            Store.Save();
            ResetHolidayForm();
            RenderHolidays();
            RefreshUpcoming(true);
        }

        void RenderHolidays()
        {
            var c = holList;
            c.SuspendLayout();
            c.ClearAll();
            c.Add(Eyebrow("DATAS SEM TOQUE"), S(14));
            var sorted = Store.Data.holidays.OrderBy(h => h.date, StringComparer.Ordinal).ToList();
            if (sorted.Count == 0) c.Add(new Note("Nenhuma exceção cadastrada."), 0);
            else
                for (int k = 0; k < sorted.Count; k++)
                {
                    var h = sorted[k];
                    var row = new Row();
                    row.Name = Lbl(Store.HolidayLabel(h), Mono(11.5f, true), cAmber);
                    row.Sub = Lbl(string.IsNullOrEmpty(h.description) ? "Sem toques" : h.description, Ui(9), cText);
                    var copy = IconBtn("⧉", Sym(10), cBlue, Color.FromArgb(59, 77, 138), cPanel2, "Duplicar");
                    copy.Click += (x, y) =>
                    {
                        ResetHolidayForm(); FillHolidayForm(h);
                        hTitle.Text = "NOVA EXCEÇÃO (copiada)";
                        holPage.ScrollTo(0);
                        Toast("Dados copiados — ajuste a data e salve como nova exceção.");
                    };
                    var edit = IconBtn("✎", Sym(10), cAmber, cAmberDim, cPanel2, "Editar");
                    edit.Click += (x, y) =>
                    {
                        editingHolidayId = h.id; FillHolidayForm(h);
                        hTitle.Text = "EDITANDO EXCEÇÃO";
                        hSubmit.Text = "Salvar alterações";
                        hSubmit.Width = TextRenderer.MeasureText(hSubmit.Text, hSubmit.Font).Width + S(44);
                        hCancel.Visible = true;
                        holPage.Arrange();
                        holPage.ScrollTo(0);
                    };
                    var del = IconBtn("✕", Sym(10), cRed, cRed, cPanel2, "Excluir");
                    del.Click += (x, y) =>
                    {
                        Store.Data.holidays.Remove(h); Store.Save();
                        if (editingHolidayId == h.id) ResetHolidayForm();
                        RenderHolidays(); RefreshUpcoming(true);
                    };
                    row.Actions.AddRange(new Control[] { copy, edit, del });
                    row.Finish();
                    c.Add(row, k == sorted.Count - 1 ? 0 : S(8));
                }
            c.ResumeLayout(true);
            holPage.Arrange();
        }

        // ================== configurações ==================
        class SwitchRow : Comp
        {
            public SwitchRow(string title, string desc, Switch sw)
            {
                var t = Lbl(title, Ui(10.5f, true), cText);
                var d = new WrapLabel { Text = desc, Font = Ui(9), ForeColor = cMuted };
                Controls.Add(t); Controls.Add(d); Controls.Add(sw);
                Place = c =>
                {
                    sw.Location = new Point(c.Width - sw.Width, S(4));
                    t.Location = new Point(0, S(2));
                    d.Location = new Point(0, t.Bottom + S(2));
                    d.Fit(Math.Max(S(100), c.Width - sw.Width - S(30)));
                    c.Height = d.Bottom + S(4);
                };
                Height = S(60);
            }
        }

        void BuildSettingsPage()
        {
            setPage = new Page(cBg);

            var c1 = new Stack { Bordered = true };
            c1.Add(Eyebrow("SEGUNDO PLANO"), S(12));
            swKeep = new Switch(); swKeep.Init(Store.Data.settings.keepAlive);
            swKeep.CheckedChanged += (s, e) => { Store.Data.settings.keepAlive = swKeep.Checked; Store.Save(); ApplyKeepAlive(); };
            c1.Add(new SwitchRow("Manter ativo em 2º plano",
                "Impede o Windows de entrar em suspensão enquanto o Sino Digital estiver aberto, garantindo que os toques aconteçam no horário.", swKeep), 0);
            setPage.Add(c1);

            var cd = new Stack { Bordered = true };
            cd.Add(Eyebrow("MICROFONE"), S(12));
            swDuck = new Switch(); swDuck.Init(Store.Data.settings.duckEnabled);
            swDuck.CheckedChanged += (s, e) => { Store.Data.settings.duckEnabled = swDuck.Checked; Store.Save(); };
            cd.Add(new SwitchRow("Abaixar o volume quando houver voz no microfone",
                "Quando o microfone padrão do Windows captar som (alguém falando), o volume do toque diminui automaticamente e volta ao normal cerca de 1,5 s depois do silêncio. Use um microfone que não capte o som das caixas, senão o próprio toque faz o volume baixar.", swDuck), S(12));

            Func<string, NumBox, string, Comp> numRow = (label, box, suffix) =>
            {
                var r = new Comp { Height = S(40) };
                var l = new Label { Text = label, AutoSize = true, Font = Ui(9.5f), ForeColor = cText, BackColor = Color.Transparent };
                var u = new Label { Text = suffix, AutoSize = true, Font = Mono(9), ForeColor = cMuted, BackColor = Color.Transparent };
                StyleInput(box); box.Font = Mono(11, true); box.Width = S(70);
                r.Controls.Add(l); r.Controls.Add(box); r.Controls.Add(u);
                l.Location = new Point(0, S(10));
                r.Place = c => { box.Location = new Point(Math.Min(c.Width - S(120), S(340)), S(5)); u.Location = new Point(box.Right + S(8), S(11)); };
                return r;
            };
            duckVol = new NumBox { Minimum = 0, Maximum = 100, MaxLength = 3 }; duckVol.Value = Store.Data.settings.duckVolume;
            duckSens = new NumBox { Minimum = 1, Maximum = 100, MaxLength = 3 }; duckSens.Value = Store.Data.settings.duckSensitivity;
            duckVol.TextChanged += (s, e) => { Store.Data.settings.duckVolume = (int)duckVol.Value; };
            duckSens.TextChanged += (s, e) => { Store.Data.settings.duckSensitivity = (int)duckSens.Value; };
            duckVol.Leave += (s, e) => Store.Save();
            duckSens.Leave += (s, e) => Store.Save();
            cd.Add(numRow("Volume do toque enquanto houver voz", duckVol, "%"), 0);
            cd.Add(numRow("Sensibilidade (nível do microfone que ativa)", duckSens, "%"), 0);
            micLevel = new Label { AutoSize = true, Font = Mono(9), ForeColor = cAmber, BackColor = Color.Transparent, Text = "Nível atual do microfone: 0%" };
            cd.Add(micLevel, 0);
            setPage.Add(cd);

            var c2 = new Stack { Bordered = true };
            c2.Add(Eyebrow("INICIALIZAÇÃO"), S(12));
            swStartup = new Switch(); swStartup.Init(IsStartupEnabled());
            swStartup.CheckedChanged += (s, e) =>
            {
                try { SetStartup(swStartup.Checked); Toast(swStartup.Checked ? "O app abrirá junto com o Windows." : "Início automático desativado."); }
                catch (Exception ex) { Toast("Não foi possível alterar: " + ex.Message); swStartup.Init(IsStartupEnabled()); }
            };
            c2.Add(new SwitchRow("Iniciar com o Windows",
                "Abre o Sino Digital automaticamente em tela cheia quando você entra no Windows. Passe o mouse no topo da tela (ou clique no relógio) para ver o menu.", swStartup), 0);
            setPage.Add(c2);

            var c3 = new Stack { Bordered = true };
            c3.Add(Eyebrow("CONFIABILIDADE"), S(12));
            c3.Add(new WrapLabel
            {
                Font = Ui(9), ForeColor = cMuted,
                Text = "Mantenha este app aberto (ele pode ficar minimizado) para que os toques aconteçam. Os áudios, alarmes e feriados ficam salvos em: " + Store.Dir +
                       "\nUse o backup abaixo para guardar uma cópia ou levar tudo para outro computador."
            }, S(10));
            var link = new LinkLabel
            {
                Text = "Abrir pasta dos áudios", AutoSize = true, Font = Ui(9.5f, true),
                LinkColor = cAmber, ActiveLinkColor = cText, VisitedLinkColor = cAmber,
                LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Color.Transparent, Cursor = Cursors.Hand
            };
            link.LinkClicked += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(Store.AudioDir);
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + Store.AudioDir + "\"");
                }
                catch (Exception ex) { Toast("Não foi possível abrir a pasta: " + ex.Message); }
            };
            c3.Add(link, 0);
            setPage.Add(c3);

            var c4 = new Stack { Bordered = true };
            c4.Add(Eyebrow("BACKUP (ZIP)"), S(12));
            c4.Add(new WrapLabel
            {
                Font = Ui(9), ForeColor = cMuted,
                Text = "Baixa um arquivo .zip com todos os alarmes, feriados/exceções e a biblioteca de áudios. Use para guardar um backup ou levar as configurações para outro computador. Importar substitui tudo o que existe neste aplicativo. Compatível com os backups da versão web (sino-digital.html)."
            }, S(14));
            var btns = new Comp { Height = S(42) };
            var exp = Primary("Baixar configurações (.zip)");
            var imp = Secondary("Importar configurações (.zip)", cPanel);
            exp.Location = new Point(0, 0);
            btns.Controls.Add(exp); btns.Controls.Add(imp);
            btns.Place = c => { imp.Location = new Point(exp.Right + S(10), 0); };
            exp.Click += (s, e) => ExportBackup();
            imp.Click += (s, e) => ImportBackup();
            c4.Add(btns, 0);
            setPage.Add(c4);

            Controls.Add(setPage);
        }

        void ApplyKeepAlive()
        {
            SetThreadExecutionState(Store.Data.settings.keepAlive ? (ES_CONTINUOUS | ES_SYSTEM_REQUIRED) : ES_CONTINUOUS);
        }

        static bool IsStartupEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }
        static void SetStartup(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" --startup");
                else k.DeleteValue(RunName, false);
            }
        }
        void RefreshStartupEntry()
        {
            try { if (IsStartupEnabled()) SetStartup(true); } catch { } // mantém o caminho atualizado
        }

        void ExportBackup()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Backup ZIP|*.zip";
                dlg.FileName = "sino-digital-backup-" + Store.ToDateStr(DateTime.Now) + ".zip";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { Store.ExportBackup(dlg.FileName); Toast("Backup salvo."); }
                catch (Exception ex) { Program.Log("export: " + ex); Toast("Erro ao gerar o backup."); }
            }
        }

        void ImportBackup()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Backup ZIP|*.zip";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var p = Store.ReadBackup(dlg.FileName);
                    string msg = "Importar substitui TODOS os dados atuais por:\n• " + p.Schedules.Count + " alarme(s)\n• " +
                        p.Holidays.Count + " feriado(s)/exceção(ões)\n• " + p.AudioList.Count + " áudio(s)\n\nContinuar?";
                    if (MessageBox.Show(this, msg, "Importar configurações", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    audio.Stop();
                    Store.ApplyImport(p);
                    ResetHolidayForm();
                    RefreshAll();
                    Toast("Configurações importadas.");
                }
                catch (Exception ex) { Program.Log("import: " + ex); Toast("Falha ao importar: " + ex.Message); }
            }
        }

        // ================== próximos toques ==================
        public List<Schedule> UpcomingToday(out bool holiday)
        {
            var now = DateTime.Now;
            string today = Store.ToDateStr(now);
            holiday = Store.IsHoliday(today);
            if (holiday) return new List<Schedule>();
            string nowTime = Store.TimeStr(now);
            return Store.Data.schedules
                .Where(s => s.active && Store.RunsOn(s, now) && string.CompareOrdinal(s.time, nowTime) >= 0 && s.lastFiredDate != today)
                .OrderBy(s => s.time, StringComparer.Ordinal).ToList();
        }

        public string AudioStamp() { return Store.Data.audios.Count.ToString(); }

        void OpenUpcoming()
        {
            if (upcomingDlg != null) return;
            using (var d = new UpcomingDialog(this))
            {
                upcomingDlg = d;
                d.ShowDialog(this);
                upcomingDlg = null;
            }
        }

        string lastBadge = "";
        void RefreshUpcoming(bool force)
        {
            bool holiday;
            var list = UpcomingToday(out holiday);
            string txt = (holiday || list.Count == 0) ? "" : list.Count.ToString();
            if (txt != lastBadge || force)
            {
                lastBadge = txt;
                badge.Visible = txt != "";
                badge.Text = txt;
                badge.BringToFront();
            }
            if (upcomingDlg != null) upcomingDlg.Rebuild(force);
        }

        void RefreshAll()
        {
            RenderSchedules();
            RenderHolidays();
            RefreshUpcoming(true);
        }

        // ================== motor de alarmes ==================
        public void PlaySchedule(Schedule s)
        {
            var am = Store.FindAudio(s.audioId);
            if (am == null || !File.Exists(Store.AudioPath(am))) { Toast("Áudio não encontrado."); return; }
            string label = !string.IsNullOrEmpty(s.name) ? s.name : am.name;
            try { audio.Play(Store.AudioPath(am), label, s.loopSeconds); }
            catch (Exception ex) { Program.Log("play: " + ex); Toast("Não foi possível tocar o áudio."); }
        }

        void FireAlarm(Schedule s)
        {
            PlaySchedule(s);
            string label = !string.IsNullOrEmpty(s.name) ? s.name : "Agendamento";
            Toast("🔔 Tocando: " + label + " — " + s.time);
            if (Store.Data.settings.notifications)
            {
                try { tray.ShowBalloonTip(5000, "Sino Digital", label + " às " + s.time, ToolTipIcon.Info); } catch { }
            }
        }

        public void Anticipate(string id)
        {
            var s = Store.Data.schedules.FirstOrDefault(x => x.id == id);
            if (s == null) return;
            PlaySchedule(s);
            s.lastFiredDate = Store.ToDateStr(DateTime.Now);
            if (s.recurrence == "once") s.active = false;
            Store.Save();
            RenderSchedules();
            RefreshUpcoming(true);
            Toast("⏩ Antecipado: " + (string.IsNullOrEmpty(s.name) ? "Agendamento" : s.name));
        }

        void CheckAlarms()
        {
            var now = DateTime.Now;
            string dateStr = Store.ToDateStr(now), timeStr = Store.TimeStr(now);
            if (Store.IsHoliday(dateStr)) return;
            bool changed = false;
            foreach (var s in Store.Data.schedules.ToList())
            {
                if (!s.active || s.lastFiredDate == dateStr || s.time != timeStr) continue;
                if (!Store.RunsOn(s, now)) continue;
                FireAlarm(s);
                s.lastFiredDate = dateStr;
                if (s.recurrence == "once") s.active = false;
                changed = true;
            }
            if (changed) { Store.Save(); RenderSchedules(); RefreshUpcoming(true); }
        }

        // ================== banner "tocando agora" ==================
        string lastNp = "";
        void UpdateBanner()
        {
            var cur = audio.Current;
            if (cur == null)
            {
                if (banner.Visible) { banner.Visible = false; lastNp = ""; }
                return;
            }
            string sub = "Tocando agora";
            if (cur.End.HasValue)
            {
                double rem = (cur.End.Value - DateTime.Now).TotalSeconds;
                int secs = (int)Math.Ceiling(Math.Max(0, rem));
                sub = "Termina em " + (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            }
            if (!banner.Visible) { banner.Visible = true; DoLayout(); }
            banner.BringToFront();
            string key = cur.Label + "|" + sub;
            if (key != lastNp)
            {
                lastNp = key;
                npLabel.Text = cur.Label; npSub.Text = sub;
                npSub.Location = new Point(npSub.Left, npLabel.Top + npLabel.Height);
            }
        }

        // ================== loop principal ==================
        void Tick()
        {
            var now = DateTime.Now;
            home.SetTime(now);
            CheckAlarms();
            RefreshUpcoming(false);
            audio.Tick();
            UpdateBanner();
            if (banner.Visible)
            {
                double t = (Math.Sin(Environment.TickCount / 1000.0 * Math.PI) + 1) / 2;
                banner.BackColor = Color.FromArgb((int)(20 + 20 * t), (int)(15 + 12 * t), 4);
            }
        }
    }
}
