using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using static SinoDigital.Theme;

namespace SinoDigital
{
    // Base: janela sem borda, com cabeçalho (eyebrow + ✕) e corpo rolável
    public class ModalBase : Form
    {
        protected readonly Page body;
        protected readonly Label title;
        protected readonly Button closeBtn;

        public ModalBase(string titleText, int widthPx, int heightPx)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = cPanel;
            ForeColor = cText;
            Font = Ui(10);
            KeyPreview = true;
            DoubleBuffered = true;
            var wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(widthPx, wa.Width - S(40)), Math.Min(heightPx, wa.Height - S(60)));

            title = Eyebrow(titleText);
            title.Location = new Point(S(22), S(22));
            closeBtn = IconBtn("✕", Sym(10), cRed, cRed, cPanel, "Fechar");
            closeBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            body = new Page(cPanel) { MaxCol = int.MaxValue, SidePad = S(22), TopPad = S(4), Gap = S(10) };
            body.SetBounds(0, S(56), Width, Height - S(56));
            body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            closeBtn.Location = new Point(Width - S(22) - closeBtn.Width, S(16));
            closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(body);
            Controls.Add(title);
            Controls.Add(closeBtn);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(cLine)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
        protected override bool ShowWithoutActivation { get { return false; } }
    }

    // =====================================================================
    //  Próximos toques de hoje
    // =====================================================================
    public class UpcomingDialog : ModalBase
    {
        readonly MainForm app;
        readonly Stack stack;
        readonly Timer refresh = new Timer { Interval = 500 };
        string signature = "";

        public UpcomingDialog(MainForm app) : base("PRÓXIMOS TOQUES DE HOJE", S(580), S(720))
        {
            this.app = app;
            stack = new Stack { BackColor = cPanel };
            stack.Padding = new Padding(0);
            body.Add(stack);
            refresh.Tick += (s, e) => Rebuild(false);
            refresh.Start();
            Rebuild(true);
            FormClosed += (s, e) => refresh.Stop();
        }

        public void Rebuild(bool force)
        {
            bool holiday;
            var list = app.UpcomingToday(out holiday);
            string sig = (holiday ? "H" : "N") + "|" + string.Join(",", list.Select(x => x.id + x.time).ToArray()) + "|" + app.AudioStamp();
            if (!force && sig == signature) return;
            signature = sig;

            stack.SuspendLayout();
            stack.ClearAll();
            if (holiday)
            {
                var b = Lbl("FERIADO CADASTRADO — nenhum áudio tocará hoje", Mono(9, true), cRed);
                stack.Add(b, S(14));
            }
            else
            {
                string[] keys = { "morning", "afternoon", "night" };
                string[] names = { "MATUTINO", "VESPERTINO", "NOTURNO" };
                string[] rng = { "06:00–12:30", "12:31–18:30", "18:31–05:59" };
                for (int i = 0; i < 3; i++)
                {
                    stack.Add(Lbl(names[i] + "   " + rng[i], Mono(9, true), cMuted), S(8));
                    var g = list.Where(s => Store.Classify(s.time) == keys[i]).ToList();
                    if (g.Count == 0) stack.Add(new Note("Nenhum toque restante."), S(18));
                    else for (int k = 0; k < g.Count; k++) stack.Add(BuildAlarmRow(g[k]), k == g.Count - 1 ? S(18) : S(8));
                }
            }
            stack.ResumeLayout(true);
            body.Arrange();
        }

        Row BuildAlarmRow(Schedule s)
        {
            var am = Store.FindAudio(s.audioId);
            var row = new Row();
            var btn = IconBtn("⏩", Emoji(10), cGreen, cGreen, cPanel2, "Tocar agora (antecipar)");
            btn.ForeColor = cGreen;
            btn.Click += (x, y) => { app.Anticipate(s.id); Rebuild(true); };
            row.Lead = btn;
            row.Time = Lbl(s.time, Mono(13, true), cAmber);
            row.Name = Lbl(!string.IsNullOrEmpty(s.name) ? s.name : (am != null ? am.name : "Agendamento"), Ui(10.5f, true), cText);
            row.Sub = Lbl(Store.Describe(s), Ui(8.5f), cMuted);
            row.Finish();
            return row;
        }
    }

    // =====================================================================
    //  Formulário de agendamento (novo / editar / copiar)
    // =====================================================================
    public class ScheduleDialog : ModalBase
    {
        readonly Schedule editing;
        readonly TimeField fTime;
        readonly DateField fDate;
        readonly TextBox fName;
        readonly RadioButton rWeekly, rOnce;
        readonly Chip[] chips = new Chip[7];
        readonly SelectField fAudio;
        readonly NumBox fLoop;
        readonly Label status, err;
        readonly Stack form;
        readonly Comp weekly, once;

        public ScheduleDialog(Schedule src, bool isEdit, string titleText)
            : base(titleText, S(600), S(800))
        {
            editing = isEdit ? src : null;
            form = new Stack { BackColor = cPanel };
            form.Padding = new Padding(0);

            // horário
            fTime = new TimeField();
            fTime.TimeText = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
            form.Add(Field("Horário", fTime, false), S(14));

            // nome
            fName = new TextBox { BorderStyle = BorderStyle.FixedSingle };
            StyleInput(fName);
            Cue(fName, "Ex: Sinal de entrada");
            form.Add(Field("Nome (opcional)", fName, true), S(14));

            // recorrência
            var rec = new Comp { Height = S(30) };
            rWeekly = new RadioButton { Text = "Recorrente (dias da semana)", Checked = true, AutoSize = true, ForeColor = cText, BackColor = Color.Transparent };
            rOnce = new RadioButton { Text = "Data única", AutoSize = true, ForeColor = cText, BackColor = Color.Transparent };
            rec.Controls.Add(rWeekly); rec.Controls.Add(rOnce);
            rWeekly.Location = new Point(0, S(3));
            rec.Place = c => { rOnce.Location = new Point(rWeekly.Right + S(24), S(3)); };
            rWeekly.CheckedChanged += (s, e) => UpdateRecurrence();
            form.Add(rec, S(10));

            // dias
            weekly = new Comp { Height = S(116) };
            var cap = Lbl("Dias", Ui(9, true), cMuted); cap.Location = new Point(0, 0);
            weekly.Controls.Add(cap);
            for (int i = 0; i < 7; i++)
            {
                chips[i] = new Chip(Store.DaysShort[i]) { BackColor = cPanel };
                chips[i].Location = new Point(i * (S(52) + S(8)), S(24));
                weekly.Controls.Add(chips[i]);
            }
            var allBtn = Secondary("Marcar todos os dias", cPanel);
            allBtn.Height = S(34);
            allBtn.Location = new Point(0, S(70));
            allBtn.Click += (s, e) =>
            {
                bool all = chips.All(c => c.Selected);
                foreach (var c in chips) c.SetSelected(!all);
            };
            weekly.Controls.Add(allBtn);
            form.Add(weekly, S(10));

            // data única
            fDate = new DateField();
            once = Field("Data", fDate, false);
            once.Visible = false;
            form.Add(once, S(10));

            // áudio
            fAudio = new SelectField();
            var up = Secondary("Enviar novo áudio", cPanel);
            up.Click += (s, e) => UploadAudio();
            var audioRow = new Comp { Height = S(24) + S(36) };
            var acap = Lbl("Áudio", Ui(9, true), cMuted); acap.Location = new Point(0, 0);
            audioRow.Controls.Add(acap); audioRow.Controls.Add(fAudio); audioRow.Controls.Add(up);
            audioRow.Place = c =>
            {
                up.Location = new Point(c.Width - up.Width, S(22));
                up.Height = S(34);
                fAudio.SetBounds(0, S(24), Math.Max(S(120), c.Width - up.Width - S(10)), S(34));
            };
            form.Add(audioRow, S(2));
            status = Lbl("", Ui(8.5f), cMuted);
            form.Add(status, S(14));

            // loop
            var loopCap = Lbl("Repetir em loop por", Ui(9, true), cMuted);
            form.Add(loopCap, S(6));
            var stepper = new Comp { Height = S(40) };
            var minus = Secondary("−", cPanel); minus.Width = S(42); minus.Height = S(36); minus.ForeColor = cAmber;
            var plus = Secondary("+", cPanel); plus.Width = S(42); plus.Height = S(36); plus.ForeColor = cAmber;
            minus.MouseLeave += (s, e) => minus.ForeColor = cAmber;
            plus.MouseLeave += (s, e) => plus.ForeColor = cAmber;
            fLoop = new NumBox { Width = S(90) };
            StyleInput(fLoop);
            fLoop.Font = Mono(11, true);
            var suffix = Lbl("SEG", Mono(9), cMuted);
            stepper.Controls.Add(minus); stepper.Controls.Add(fLoop); stepper.Controls.Add(plus); stepper.Controls.Add(suffix);
            minus.Location = new Point(0, S(2)); fLoop.Location = new Point(S(46), S(5));
            plus.Location = new Point(S(46) + S(94), S(2)); suffix.Location = new Point(plus.Right + S(10), S(11));
            minus.Click += (s, e) => fLoop.Value = Math.Max(0, fLoop.Value - 5);
            plus.Click += (s, e) => fLoop.Value = Math.Min(fLoop.Maximum, fLoop.Value + 5);
            form.Add(stepper, S(2));
            form.Add(Lbl("0 ou vazio = toca uma vez. Use os botões ou digite direto.", Ui(8.5f), cMuted), S(18));

            err = Lbl("", Ui(9.5f, true), cRed);
            form.Add(err, S(6));

            // ações
            var actions = new Comp { Height = S(44) };
            var save = Primary(isEdit ? "Salvar alterações" : "Salvar agendamento");
            var cancel = Secondary("Cancelar", cPanel);
            save.Location = new Point(0, 0);
            cancel.Location = new Point(save.Width + S(10), 0);
            save.Click += (s, e) => Save();
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            actions.Controls.Add(save); actions.Controls.Add(cancel);
            form.Add(actions, 0);

            body.Add(form);
            RefreshAudios(null);

            if (src != null) Fill(src);
            UpdateRecurrence();
        }

        static Comp Field(string caption, Control inner, bool full)
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

        void UpdateRecurrence()
        {
            bool isOnce = rOnce.Checked;
            weekly.Visible = !isOnce;
            once.Visible = isOnce;
            body.Arrange();
        }

        void RefreshAudios(string selectId)
        {
            string cur = selectId ?? (fAudio.SelectedItem as AudioMeta != null ? ((AudioMeta)fAudio.SelectedItem).id : null);
            fAudio.Items.Clear();
            fAudio.Items.Add(new AudioMeta { id = "", name = "— selecionar da biblioteca —" });
            foreach (var a in Store.Data.audios) fAudio.Items.Add(a);
            fAudio.SelectedIndex = 0;
            if (cur != null)
                for (int i = 1; i < fAudio.Items.Count; i++)
                    if (((AudioMeta)fAudio.Items[i]).id == cur) { fAudio.SelectedIndex = i; break; }
        }

        void UploadAudio()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Enviar novo áudio";
                dlg.Filter = "Áudio|*.mp3;*.wav;*.ogg;*.m4a;*.mp4;*.aac;*.flac;*.wma|Todos os arquivos|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    status.Text = "Enviando...";
                    status.Refresh();
                    var meta = Store.AddAudio(dlg.FileName);
                    RefreshAudios(meta.id);
                    status.Text = "Áudio \"" + meta.name + "\" adicionado à biblioteca.";
                }
                catch (Exception ex) { status.Text = "Falha ao enviar: " + ex.Message; }
            }
        }

        void Fill(Schedule s)
        {
            fTime.TimeText = s.time;
            fName.Text = s.name ?? "";
            bool isOnce = s.recurrence == "once";
            rOnce.Checked = isOnce; rWeekly.Checked = !isOnce;
            if (!isOnce && s.days != null)
                for (int i = 0; i < 7; i++) chips[i].SetSelected(s.days.Contains(i));
            if (isOnce && !string.IsNullOrEmpty(s.date)) fDate.Value = Store.ParseDate(s.date);
            else fDate.Value = DateTime.Today;
            RefreshAudios(s.audioId);
            fLoop.Value = Math.Min(fLoop.Maximum, s.loopSeconds ?? 0);
        }

        void Fail(string m) { err.Text = m; body.Arrange(); }

        void Save()
        {
            err.Text = "";
            string time = fTime.TimeText;
            var am = fAudio.SelectedItem as AudioMeta;
            if (am == null || string.IsNullOrEmpty(am.id)) { Fail("Selecione ou envie um áudio."); return; }
            bool isOnce = rOnce.Checked;
            List<int> days = null; string date = null;
            if (!isOnce)
            {
                days = new List<int>();
                for (int i = 0; i < 7; i++) if (chips[i].Selected) days.Add(i);
                if (days.Count == 0) { Fail("Selecione ao menos um dia da semana."); return; }
            }
            else date = Store.ToDateStr(fDate.Value ?? DateTime.Today);

            int loop = (int)fLoop.Value;
            int? loopSeconds = loop > 0 ? (int?)loop : null;
            string name = fName.Text.Trim();

            if (editing != null)
            {
                editing.time = time; editing.name = name;
                editing.recurrence = isOnce ? "once" : "weekly";
                editing.audioId = am.id; editing.loopSeconds = loopSeconds;
                editing.lastFiredDate = null; // reativa após a edição
                editing.days = days ?? new List<int>();
                editing.date = date;
            }
            else
            {
                Store.Data.schedules.Add(new Schedule
                {
                    id = Store.NewId(), time = time, name = name,
                    recurrence = isOnce ? "once" : "weekly",
                    audioId = am.id, loopSeconds = loopSeconds, active = true,
                    lastFiredDate = null, days = days ?? new List<int>(), date = date
                });
            }
            Store.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
