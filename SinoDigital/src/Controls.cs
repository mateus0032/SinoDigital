using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SinoDigital
{
    public static class Theme
    {
        public static readonly Color cBg = Color.FromArgb(9, 12, 17);
        public static readonly Color cPanel = Color.FromArgb(17, 22, 32);
        public static readonly Color cPanel2 = Color.FromArgb(22, 28, 40);
        public static readonly Color cCard = Color.FromArgb(27, 34, 49);
        public static readonly Color cLine = Color.FromArgb(36, 44, 61);
        public static readonly Color cAmber = Color.FromArgb(255, 176, 32);
        public static readonly Color cAmberDim = Color.FromArgb(138, 92, 18);
        public static readonly Color cText = Color.FromArgb(233, 237, 245);
        public static readonly Color cMuted = Color.FromArgb(124, 132, 148);
        public static readonly Color cGreen = Color.FromArgb(52, 211, 153);
        public static readonly Color cRed = Color.FromArgb(255, 93, 108);
        public static readonly Color cBlue = Color.FromArgb(122, 162, 255);
        public static readonly Color cInk = Color.FromArgb(26, 18, 4);

        public static float K = 1f;
        public static int S(int v) { return (int)Math.Round(v * K); }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public static Font F(string fam, float pt, bool bold)
        {
            string key = fam + "|" + pt + "|" + bold;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font(fam, pt, bold ? FontStyle.Bold : FontStyle.Regular);
                fonts[key] = f;
            }
            return f;
        }
        public static Font Ui(float pt, bool bold = false) { return F("Segoe UI", pt, bold); }
        public static Font Mono(float pt, bool bold = false) { return F("Consolas", pt, bold); }
        public static Font Emoji(float pt) { return F("Segoe UI Emoji", pt, false); }
        public static Font Sym(float pt) { return F("Segoe UI Symbol", pt, false); }

        public static readonly ToolTip Tip = new ToolTip { InitialDelay = 400 };

        public static Label Lbl(string text, Font f, Color c)
        {
            return new Label { Text = text, Font = f, ForeColor = c, AutoSize = true, BackColor = Color.Transparent };
        }

        public static Label Eyebrow(string text)
        {
            return Lbl(text, Mono(9.5f, true), cAmber);
        }

        static Button BaseBtn()
        {
            var b = new Button();
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            return b;
        }

        static void FitBtn(Button b, int padW, int h)
        {
            b.Size = new Size(TextRenderer.MeasureText(b.Text, b.Font).Width + padW, h);
        }

        public static Button Primary(string text)
        {
            var b = BaseBtn();
            b.Text = text; b.Font = Ui(10, true);
            b.BackColor = cAmber; b.ForeColor = cInk;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 192, 66);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(230, 158, 24);
            FitBtn(b, S(44), S(40));
            return b;
        }

        public static Button Secondary(string text, Color back)
        {
            var b = BaseBtn();
            b.Text = text; b.Font = Ui(10, true);
            b.BackColor = back; b.ForeColor = cMuted;
            b.FlatAppearance.BorderColor = cLine;
            b.FlatAppearance.MouseOverBackColor = cPanel2;
            b.MouseEnter += (s, e) => { b.ForeColor = cText; b.FlatAppearance.BorderColor = cAmberDim; };
            b.MouseLeave += (s, e) => { b.ForeColor = cMuted; b.FlatAppearance.BorderColor = cLine; };
            FitBtn(b, S(40), S(40));
            return b;
        }

        public static Button IconBtn(string glyph, Font font, Color hoverFg, Color hoverBorder, Color back, string tip)
        {
            var b = BaseBtn();
            b.Text = glyph; b.Font = font;
            b.BackColor = back; b.ForeColor = cMuted;
            b.FlatAppearance.BorderColor = cLine;
            b.FlatAppearance.MouseOverBackColor = back;
            b.Size = new Size(S(32), S(32));
            b.Padding = Padding.Empty;
            b.MouseEnter += (s, e) => { b.ForeColor = hoverFg; b.FlatAppearance.BorderColor = hoverBorder; };
            b.MouseLeave += (s, e) => { if (b.Tag as string != "on") b.ForeColor = cMuted; b.FlatAppearance.BorderColor = b.Tag as string == "on" ? cAmberDim : cLine; };
            if (tip != null) Tip.SetToolTip(b, tip);
            return b;
        }

        public static void StyleInput(Control c)
        {
            c.BackColor = cPanel2; c.ForeColor = cText; c.Font = Ui(10.5f);
        }

        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            if (d > r.Width) d = r.Width; if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        public static void Cue(TextBox tb, string text)
        {
            Action set = () => SendMessage(tb.Handle, 0x1501, IntPtr.Zero, text);
            if (tb.IsHandleCreated) set(); else tb.HandleCreated += (s, e) => set();
        }
    }

    // ---------- contêiner com callback de layout ----------
    public class Comp : Panel
    {
        public Action<Comp> Place;
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Place != null) Place(this);
        }
    }

    public class WrapLabel : Label
    {
        public WrapLabel() { AutoSize = false; BackColor = Color.Transparent; }
        public void Fit(int w)
        {
            Width = w;
            Height = Math.Max(Theme.S(16), TextRenderer.MeasureText(Text, Font, new Size(Math.Max(10, w), 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + Theme.S(6));
        }
    }

    public class Note : Label
    {
        public Note(string text)
        {
            Text = text; AutoSize = false; TextAlign = ContentAlignment.MiddleCenter;
            ForeColor = Theme.cMuted; Font = Theme.Ui(10); Height = Theme.S(58);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.cLine) { DashStyle = DashStyle.Dash })
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ---------- pilha vertical (card) ----------
    public class Stack : FlowLayoutPanel
    {
        public bool Bordered;
        public Stack()
        {
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = false;
            DoubleBuffered = true;
            BackColor = Theme.cPanel;
            Padding = new Padding(Theme.S(22));
            ResizeRedraw = true;
        }
        public void Add(Control c, int after)
        {
            c.Margin = new Padding(0, 0, 0, after);
            Controls.Add(c);
        }
        public void ClearAll()
        {
            var list = new List<Control>();
            foreach (Control c in Controls) list.Add(c);
            Controls.Clear();
            foreach (var c in list) c.Dispose();
        }
        public void Relayout()
        {
            int iw = Math.Max(10, Width - Padding.Horizontal);
            SuspendLayout();
            foreach (Control c in Controls)
            {
                var wl = c as WrapLabel;
                if (wl != null) wl.Fit(iw); else c.Width = iw;
            }
            ResumeLayout(true);
            Height = GetPreferredSize(new Size(Width, 0)).Height;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Bordered)
                using (var p = new Pen(Theme.cLine))
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ---------- linha de lista ----------
    public class Row : Panel
    {
        public Control Lead;
        public Label Time, Name, Sub;
        public readonly List<Control> Actions = new List<Control>();
        public int TimeW = Theme.S(76);

        public Row()
        {
            BackColor = Theme.cPanel2;
            Height = Theme.S(64);
            ResizeRedraw = true;
            DoubleBuffered = true;
        }
        public void Finish()
        {
            if (Lead != null) Controls.Add(Lead);
            if (Time != null) Controls.Add(Time);
            foreach (var a in Actions) Controls.Add(a);
            if (Name != null) { Name.AutoSize = false; Name.AutoEllipsis = true; Controls.Add(Name); }
            if (Sub != null) { Sub.AutoSize = false; Sub.AutoEllipsis = true; Controls.Add(Sub); }
            DoPlace();
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); DoPlace(); }
        void DoPlace()
        {
            int h = Height, pad = Theme.S(14), x = pad;
            if (Lead != null) { Lead.Location = new Point(x, (h - Lead.Height) / 2); x += Lead.Width + Theme.S(12); }
            if (Time != null) { Time.Location = new Point(x, (h - Time.Height) / 2); x += TimeW; }
            int r = Width - pad;
            for (int i = Actions.Count - 1; i >= 0; i--)
            {
                var c = Actions[i];
                r -= c.Width;
                c.Location = new Point(r, (h - c.Height) / 2);
                r -= Theme.S(8);
            }
            int mw = Math.Max(Theme.S(40), r - x);
            int nh = Theme.S(24), sh = Theme.S(18);
            if (Name != null && Sub != null)
            {
                int top = (h - nh - sh) / 2;
                Name.SetBounds(x, top, mw, nh);
                Sub.SetBounds(x, top + nh, mw, sh);
            }
            else if (Name != null) Name.SetBounds(x, (h - nh) / 2, mw, nh);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.cLine)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // ---------- toggle ----------
    public class Switch : Control
    {
        bool chk;
        public event EventHandler CheckedChanged;
        public Switch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(Theme.S(40), Theme.S(23));
            Cursor = Cursors.Hand;
        }
        public bool Checked
        {
            get { return chk; }
            set
            {
                if (chk == value) return;
                chk = value; Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }
        public void Init(bool v) { chk = v; Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
            using (var path = Theme.RoundRect(r, r.Height / 2))
            {
                using (var b = new SolidBrush(chk ? Color.FromArgb(46, 52, 211, 153) : Theme.cCard)) g.FillPath(b, path);
                using (var p = new Pen(chk ? Theme.cGreen : Theme.cLine)) g.DrawPath(p, path);
            }
            float d = Height - Theme.S(8);
            float x = chk ? Width - d - Theme.S(4) : Theme.S(4);
            using (var b = new SolidBrush(chk ? Theme.cGreen : Theme.cMuted)) g.FillEllipse(b, x, (Height - d) / 2f, d, d);
        }
    }

    // ---------- chip de dia da semana ----------
    public class Chip : Control
    {
        public bool Selected;
        public Chip(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.S(52), Theme.S(34));
            Cursor = Cursors.Hand;
            BackColor = Theme.cPanel;
        }
        public void SetSelected(bool v) { Selected = v; Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Selected = !Selected; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.cPanel);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundRect(r, Theme.S(8)))
            {
                using (var b = new SolidBrush(Selected ? Color.FromArgb(30, 255, 176, 32) : Theme.cPanel2)) g.FillPath(b, path);
                using (var p = new Pen(Selected ? Theme.cAmber : Theme.cLine)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Theme.Mono(9.5f, true), ClientRectangle, Selected ? Theme.cAmber : Theme.cMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    // ---------- aba de texto ----------
    public class TabLink : Control
    {
        public bool Active;
        bool hover;
        public TabLink(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Font = Theme.Mono(9.5f, true);
            Size = new Size(TextRenderer.MeasureText(text.ToUpperInvariant(), Font).Width + Theme.S(6), Theme.S(28));
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.cBg);
            Color c = Active ? Theme.cAmber : (hover ? Theme.cText : Theme.cMuted);
            TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), Font, new Rectangle(0, 0, Width, Height - 3), c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (Active) using (var b = new SolidBrush(Theme.cAmber)) e.Graphics.FillRectangle(b, 0, Height - 2, Width, 2);
        }
    }

    // ---------- relógio split-flap em tela cheia ----------
    public class ClockPanel : Control
    {
        readonly string[] digits = { "0", "0", "0", "0", "0", "0" };
        readonly int[] flipStart = new int[6];
        string dateText = "";
        readonly Timer anim = new Timer { Interval = 16 };
        const int FlipMs = 320;

        static readonly string[] Weekdays = { "DOMINGO", "SEGUNDA-FEIRA", "TERÇA-FEIRA", "QUARTA-FEIRA", "QUINTA-FEIRA", "SEXTA-FEIRA", "SÁBADO" };
        static readonly string[] Months = { "JANEIRO", "FEVEREIRO", "MARÇO", "ABRIL", "MAIO", "JUNHO", "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO" };

        public ClockPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.cBg;
            anim.Tick += (s, e) =>
            {
                bool any = false;
                int now = Environment.TickCount;
                for (int i = 0; i < 6; i++) if (flipStart[i] != 0 && unchecked(now - flipStart[i]) < FlipMs) any = true;
                if (!any) anim.Stop();
                Invalidate();
            };
        }

        public void SetTime(DateTime now)
        {
            string hh = now.ToString("HH"), mm = now.ToString("mm"), ss = now.ToString("ss");
            string[] n = { hh.Substring(0, 1), hh.Substring(1, 1), mm.Substring(0, 1), mm.Substring(1, 1), ss.Substring(0, 1), ss.Substring(1, 1) };
            bool changed = false;
            for (int i = 0; i < 6; i++)
                if (digits[i] != n[i])
                {
                    digits[i] = n[i];
                    flipStart[i] = Environment.TickCount | 1;
                    changed = true;
                }
            string d = Weekdays[(int)now.DayOfWeek] + ", " + now.Day + " DE " + Months[now.Month - 1] + " DE " + now.Year;
            if (d != dateText) { dateText = d; changed = true; }
            if (changed) { if (!anim.Enabled) anim.Start(); Invalidate(); }
        }

        // proporção de cada elemento em escala 1 (idêntica ao CSS original)
        const float AdvBig = 0.55f * 120 + 20, HBig = 160;
        const float AdvSec = 0.55f * 34 + 12, HSec = 50;
        const float WColon = 0.55f * 60, WColon2 = 0.55f * 22, Gap = 10;
        const float DateBlock = 12 * 1.4f + 9;

        float Flip(int i)
        {
            if (flipStart[i] == 0) return 1f;
            int el = unchecked(Environment.TickCount - flipStart[i]);
            if (el >= FlipMs) return 1f;
            double t = el / (double)FlipMs, ang;
            if (t < 0.45) ang = 85 * (t / 0.45);
            else if (t < 0.55) ang = 85;
            else ang = 85 * (1 - (t - 0.55) / 0.45);
            return (float)Math.Max(0.06, Math.Cos(ang * Math.PI / 180));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.cBg);
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(2, Height / 2)), Color.FromArgb(18, 255, 176, 32), Color.FromArgb(0, 255, 176, 32), 90f))
                g.FillRectangle(br, 0, 0, Width, Math.Max(2, Height / 2));

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            float totalW = 4 * AdvBig + 2 * AdvSec + WColon + WColon2 + 7 * Gap;
            float totalH = DateBlock + HBig;
            float margin = Theme.S(14);
            float sc = Math.Min((Width - 2 * margin) * 0.995f / totalW, (Height - 2 * margin) * 0.995f / totalH);
            if (sc < 0.25f) sc = 0.25f;

            float x = (Width - totalW * sc) / 2f;
            float top = (Height - totalH * sc) / 2f;

            using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // data
                using (var f = new Font("Consolas", 12 * sc, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Theme.cMuted))
                {
                    float step = (0.55f + 0.16f) * 12 * sc;
                    float sx = (Width - step * dateText.Length) / 2f;
                    for (int ci = 0; ci < dateText.Length; ci++)
                        g.DrawString(dateText[ci].ToString(), f, b, new RectangleF(sx + ci * step, top, step, DateBlock * sc), sf);
                }
                float faceTop = top + DateBlock * sc;
                float faceBottom = faceTop + HBig * sc;

                using (var fBig = new Font("Consolas", 120 * sc, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fSec = new Font("Consolas", 34 * sc, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fColon = new Font("Consolas", 60 * sc, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fColon2 = new Font("Consolas", 22 * sc, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    int di = 0;
                    // h1 h2
                    for (int k = 0; k < 2; k++) { DrawBox(g, sf, fBig, x, faceTop, AdvBig * sc, HBig * sc, 16 * sc, digits[di], di, true); x += (AdvBig + Gap) * sc; di++; }
                    DrawColon(g, sf, fColon, x, faceTop, WColon * sc, HBig * sc, Theme.cAmberDim); x += (WColon + Gap) * sc;
                    for (int k = 0; k < 2; k++) { DrawBox(g, sf, fBig, x, faceTop, AdvBig * sc, HBig * sc, 16 * sc, digits[di], di, true); x += (AdvBig + Gap) * sc; di++; }
                    // colon2: alinhado embaixo (margin-bottom 8)
                    float c2h = 22 * sc;
                    DrawColon(g, sf, fColon2, x, faceBottom - 8 * sc - c2h, WColon2 * sc, c2h, Theme.cMuted); x += (WColon2 + Gap) * sc;
                    for (int k = 0; k < 2; k++)
                    {
                        float yb = faceBottom - 7 * sc - HSec * sc;
                        DrawBox(g, sf, fSec, x, yb, AdvSec * sc, HSec * sc, 9 * sc, digits[di], di, false);
                        x += (AdvSec + Gap) * sc; di++;
                    }
                }
            }
        }

        static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) { sb.Append(c); if (c != ' ') sb.Append(' '); }
            return sb.ToString();
        }

        void DrawColon(Graphics g, StringFormat sf, Font f, float x, float y, float w, float h, Color c)
        {
            using (var b = new SolidBrush(c)) g.DrawString(":", f, b, new RectangleF(x, y - h * 0.04f, w, h), sf);
        }

        void DrawBox(Graphics g, StringFormat sf, Font f, float x, float y, float w, float h, float rad, string text, int idx, bool big)
        {
            var rect = new RectangleF(x, y, w, h);
            var state = g.Save();
            float sy = Flip(idx);
            g.TranslateTransform(x + w / 2f, y + h / 2f);
            g.ScaleTransform(1f, sy);
            g.TranslateTransform(-(x + w / 2f), -(y + h / 2f));

            // sombra
            var sh = rect; sh.Offset(0, h * 0.025f);
            using (var p = Theme.RoundRect(sh, rad))
            using (var b = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) g.FillPath(b, p);
            using (var p = Theme.RoundRect(rect, rad))
            {
                using (var lb = new LinearGradientBrush(rect, Theme.cCard, Color.FromArgb(21, 27, 40), 90f)) g.FillPath(lb, p);
            }
            Color tc = big ? Theme.cAmber : Theme.cAmberDim;
            var tr = new RectangleF(x, y - h * (big ? 0.02f : 0.0f), w, h);
            if (big)
            {
                float o = Math.Max(1.5f, h * 0.012f);
                using (var gb = new SolidBrush(Color.FromArgb(22, 255, 176, 32)))
                {
                    g.DrawString(text, f, gb, new RectangleF(tr.X - o, tr.Y, tr.Width, tr.Height), sf);
                    g.DrawString(text, f, gb, new RectangleF(tr.X + o, tr.Y, tr.Width, tr.Height), sf);
                    g.DrawString(text, f, gb, new RectangleF(tr.X, tr.Y - o, tr.Width, tr.Height), sf);
                    g.DrawString(text, f, gb, new RectangleF(tr.X, tr.Y + o, tr.Width, tr.Height), sf);
                }
            }
            using (var b = new SolidBrush(tc)) g.DrawString(text, f, b, tr, sf);
            // linha central
            using (var lp = new Pen(Color.FromArgb(140, 0, 0, 0), Math.Max(1.5f, h * 0.0125f)))
                g.DrawLine(lp, x + w * 0.06f, y + h / 2f, x + w * 0.94f, y + h / 2f);
            g.Restore(state);
        }
    }
}
