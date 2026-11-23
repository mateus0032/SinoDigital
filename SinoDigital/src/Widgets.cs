using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using static SinoDigital.Theme;

namespace SinoDigital
{
    public interface IScrollHost { void ScrollBy(int wheelDelta); }

    // =====================================================================
    //  Barra de rolagem temática (vertical)
    // =====================================================================
    public class ThemedScrollBar : Control
    {
        int max = 1, page = 1, val;
        bool hover, drag;
        int dragOff;
        public event EventHandler Scrolled;

        public ThemedScrollBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Width = S(12);
            BackColor = cBg;
        }

        public int Value
        {
            get { return val; }
            set { int v = Math.Max(0, Math.Min(value, Math.Max(0, max - page))); if (v != val) { val = v; Invalidate(); } }
        }

        public void Setup(int maximum, int pageSize, int value)
        {
            max = Math.Max(1, maximum); page = Math.Max(1, pageSize);
            val = Math.Max(0, Math.Min(value, Math.Max(0, max - page)));
            Invalidate();
        }

        int TrackH { get { return Math.Max(1, Height - S(4)); } }
        int ThumbH { get { return Math.Min(TrackH, Math.Max(S(36), (int)((long)TrackH * page / max))); } }
        Rectangle Thumb()
        {
            int range = Math.Max(1, max - page);
            int y = S(2) + (int)((long)(TrackH - ThumbH) * val / range);
            return new Rectangle(S(3), y, Math.Max(2, Width - S(6)), ThumbH);
        }

        void Raise() { if (Scrolled != null) Scrolled(this, EventArgs.Empty); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var t = Thumb();
            if (t.Contains(e.Location)) { drag = true; dragOff = e.Y - t.Y; Capture = true; Invalidate(); }
            else
            {
                Value = e.Y < t.Y ? val - page : val + page;
                Raise();
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (drag)
            {
                int free = Math.Max(1, TrackH - ThumbH);
                int ny = e.Y - dragOff - S(2);
                Value = (int)((long)Math.Max(0, Math.Min(free, ny)) * Math.Max(1, max - page) / free);
                Raise();
            }
            else
            {
                bool h = Thumb().Contains(e.Location);
                if (h != hover) { hover = h; Invalidate(); }
            }
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (drag) { drag = false; Capture = false; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover) { hover = false; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : cBg);
            var tr = new RectangleF(S(4), S(2), Width - S(8), Height - S(4));
            using (var p = RoundRect(tr, tr.Width / 2f))
            using (var b = new SolidBrush(Color.FromArgb(60, 36, 44, 61))) g.FillPath(b, p);
            var t = Thumb();
            Color c = drag ? cAmber : hover ? cAmberDim : Color.FromArgb(70, 82, 106);
            using (var p = RoundRect(new RectangleF(t.X, t.Y, t.Width, t.Height), t.Width / 2f))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }
    }

    // =====================================================================
    //  Página rolável com coluna central e barra temática
    // =====================================================================
    public class Page : Panel, IScrollHost
    {
        readonly Panel canvas = new Panel();
        readonly ThemedScrollBar sb = new ThemedScrollBar();
        readonly List<Control> items = new List<Control>();
        public int MaxCol = S(920);
        public int SidePad = S(20);
        public int TopPad = S(28);
        public int Gap = S(22);
        bool arranging;
        int scrollY;

        public Page(Color bg)
        {
            BackColor = bg;
            canvas.BackColor = bg;
            sb.BackColor = bg;
            sb.Visible = false;
            sb.Scrolled += (s, e) => SetScroll(sb.Value);
            Controls.Add(canvas);
            Controls.Add(sb);
        }

        public void Add(Control c) { items.Add(c); canvas.Controls.Add(c); Arrange(); }

        int LayoutItems(int w)
        {
            int colW = Math.Max(S(200), Math.Min(MaxCol, w - SidePad * 2));
            int x = (w - colW) / 2;
            int y = TopPad;
            foreach (var c in items)
            {
                var st = c as Stack;
                c.Width = colW;
                if (st != null) st.Relayout();
                c.Location = new Point(x, y);
                y += c.Height + Gap;
            }
            return y - Gap + S(38);
        }

        public void Arrange()
        {
            if (arranging) return;
            arranging = true;
            try
            {
                int vw = ClientSize.Width, vh = ClientSize.Height;
                int sbw = S(12);
                int w = vw;
                int h = LayoutItems(w);
                bool need = h > vh;
                if (need) { w = vw - sbw; h = LayoutItems(w); }
                scrollY = Math.Max(0, Math.Min(scrollY, Math.Max(0, h - vh)));
                canvas.SetBounds(0, -scrollY, w, h);
                sb.SetBounds(vw - sbw, 0, sbw, vh);
                sb.Setup(h, vh, scrollY);
                sb.Visible = need;
                if (need) sb.BringToFront();
            }
            finally { arranging = false; }
        }

        void SetScroll(int y)
        {
            int max = Math.Max(0, canvas.Height - ClientSize.Height);
            scrollY = Math.Max(0, Math.Min(y, max));
            canvas.Top = -scrollY;
            sb.Value = scrollY;
        }

        public void ScrollTo(int y) { SetScroll(y); }
        public void ScrollBy(int wheelDelta) { SetScroll(scrollY - wheelDelta * S(60) / 120); }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Arrange(); }
    }

    // =====================================================================
    //  Popup temático (ToolStripDropDown sem bordas nativas)
    // =====================================================================
    class FlatRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(cLine); }
    }

    public static class Pop
    {
        public static ToolStripDropDown Open(Control anchor, Control content)
        {
            var frame = new Panel { BackColor = cAmberDim, Padding = new Padding(1), Size = new Size(content.Width + 2, content.Height + 2) };
            content.Dock = DockStyle.Fill;
            frame.Controls.Add(content);
            var host = new ToolStripControlHost(frame) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = frame.Size };
            var dd = new ToolStripDropDown { DropShadowEnabled = false, Padding = Padding.Empty, Renderer = new FlatRenderer() };
            dd.Items.Add(host);
            dd.Show(anchor, new Point(0, anchor.Height + 2));
            return dd;
        }
    }

    // =====================================================================
    //  Campo base com lista/popup
    // =====================================================================
    public abstract class DropField : Control
    {
        bool hover, open;
        int closedAt;
        ToolStripDropDown dd;

        protected DropField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Height = S(34);
            Width = S(200);
            Font = Ui(10.5f);
            BackColor = cPanel2;
        }

        protected abstract string Display { get; }
        protected virtual bool Placeholder { get { return false; } }
        protected abstract Control BuildPopup();

        protected void ClosePopup() { if (dd != null) dd.Close(); }

        void Toggle()
        {
            if (open) { ClosePopup(); return; }
            if (unchecked(Environment.TickCount - closedAt) < 250) return;
            var c = BuildPopup();
            dd = Pop.Open(this, c);
            open = true;
            var me = dd;
            dd.Closed += (s, e) => { open = false; closedAt = Environment.TickCount; Invalidate(); };
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); if (e.Button == MouseButtons.Left) Toggle(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Down || k == Keys.Up || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.Down) { Toggle(); e.Handled = true; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : cPanel);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = RoundRect(r, S(7)))
            {
                using (var b = new SolidBrush(cPanel2)) g.FillPath(b, p);
                using (var pen = new Pen(open || Focused ? cAmber : hover ? cAmberDim : cLine)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Display, Font, new Rectangle(S(10), 0, Width - S(34), Height),
                Placeholder ? cMuted : cText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            // chevron
            int cx = Width - S(16), cy = Height / 2;
            var pts = open
                ? new[] { new Point(cx - S(4), cy + S(2)), new Point(cx + S(4), cy + S(2)), new Point(cx, cy - S(3)) }
                : new[] { new Point(cx - S(4), cy - S(2)), new Point(cx + S(4), cy - S(2)), new Point(cx, cy + S(3)) };
            using (var b = new SolidBrush(open ? cAmber : cMuted)) g.FillPolygon(b, pts);
        }
    }

    // =====================================================================
    //  Calendário
    // =====================================================================
    class CalendarView : Control
    {
        static readonly string[] Months = { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
        static readonly string[] Wd = { "D", "S", "T", "Q", "Q", "S", "S" };
        DateTime month;
        readonly DateTime? sel;
        readonly bool allowEmpty;
        int hover = -1;
        public event Action<DateTime?> Picked;

        int Pad { get { return S(10); } }
        int Cell { get { return S(34); } }
        int HeadH { get { return S(38); } }
        int WdH { get { return S(24); } }
        int RowH { get { return S(32); } }
        int FootH { get { return S(36); } }

        public CalendarView(DateTime? selected, bool allowEmpty)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            sel = selected; this.allowEmpty = allowEmpty;
            var d = selected ?? DateTime.Today;
            month = new DateTime(d.Year, d.Month, 1);
            BackColor = cPanel;
            Size = new Size(Cell * 7 + Pad * 2, Pad + HeadH + WdH + RowH * 6 + FootH + Pad / 2);
            Cursor = Cursors.Default;
        }

        Rectangle PrevR { get { return new Rectangle(Pad, Pad, S(34), HeadH - S(4)); } }
        Rectangle NextR { get { return new Rectangle(Width - Pad - S(34), Pad, S(34), HeadH - S(4)); } }
        Rectangle CellR(int i) { return new Rectangle(Pad + (i % 7) * Cell, Pad + HeadH + WdH + (i / 7) * RowH, Cell, RowH); }
        Rectangle TodayR { get { return new Rectangle(Pad, Height - FootH - Pad / 2 + S(2), S(70), FootH - S(4)); } }
        Rectangle ClearR { get { return new Rectangle(Width - Pad - S(70), Height - FootH - Pad / 2 + S(2), S(70), FootH - S(4)); } }
        DateTime CellDate(int i)
        {
            var start = month.AddDays(-(int)month.DayOfWeek);
            return start.AddDays(i);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            for (int i = 0; i < 42; i++) if (CellR(i).Contains(e.Location)) { h = i; break; }
            if (h != hover) { hover = h; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (PrevR.Contains(e.Location)) { month = month.AddMonths(-1); Invalidate(); return; }
            if (NextR.Contains(e.Location)) { month = month.AddMonths(1); Invalidate(); return; }
            if (TodayR.Contains(e.Location)) { if (Picked != null) Picked(DateTime.Today); return; }
            if (allowEmpty && ClearR.Contains(e.Location)) { if (Picked != null) Picked(null); return; }
            for (int i = 0; i < 42; i++)
                if (CellR(i).Contains(e.Location)) { if (Picked != null) Picked(CellDate(i)); return; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(cPanel);
            var cen = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

            // cabeçalho
            TextRenderer.DrawText(g, Months[month.Month - 1] + " " + month.Year, Ui(11, true),
                new Rectangle(Pad + S(40), Pad, Width - 2 * Pad - S(80), HeadH - S(4)), cText, cen);
            foreach (var pr in new[] { new { R = PrevR, T = "‹" }, new { R = NextR, T = "›" } })
            {
                using (var p = RoundRect(pr.R, S(7))) { using (var pen = new Pen(cLine)) g.DrawPath(pen, p); }
                TextRenderer.DrawText(g, pr.T, Ui(13, true), pr.R, cAmber, cen);
            }

            // dias da semana
            for (int c = 0; c < 7; c++)
                TextRenderer.DrawText(g, Wd[c], Mono(9, true), new Rectangle(Pad + c * Cell, Pad + HeadH, Cell, WdH), cMuted, cen);

            // dias
            for (int i = 0; i < 42; i++)
            {
                var d = CellDate(i);
                var r = CellR(i);
                r.Inflate(-S(2), -S(2));
                bool isSel = sel.HasValue && d.Date == sel.Value.Date;
                bool isToday = d.Date == DateTime.Today;
                bool inMonth = d.Month == month.Month;
                using (var p = RoundRect(r, S(7)))
                {
                    if (isSel) { using (var b = new SolidBrush(cAmber)) g.FillPath(b, p); }
                    else if (i == hover) { using (var b = new SolidBrush(cCard)) g.FillPath(b, p); }
                    if (isToday && !isSel) using (var pen = new Pen(cAmber)) g.DrawPath(pen, p);
                }
                Color tc = isSel ? cInk : inMonth ? cText : Color.FromArgb(78, 88, 108);
                TextRenderer.DrawText(g, d.Day.ToString(), isSel ? Ui(10, true) : Ui(10), r, tc, cen);
            }

            // rodapé
            using (var pen = new Pen(cLine)) g.DrawLine(pen, Pad, TodayR.Top - S(2), Width - Pad, TodayR.Top - S(2));
            TextRenderer.DrawText(g, "Hoje", Ui(9.5f, true), TodayR, cAmber, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            if (allowEmpty)
                TextRenderer.DrawText(g, "Limpar", Ui(9.5f, true), ClearR, cMuted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    public class DateField : DropField
    {
        DateTime? val = DateTime.Today;
        public bool AllowEmpty;
        public string EmptyText = "Sem data";
        public event EventHandler Changed;

        public DateTime? Value
        {
            get { return val; }
            set
            {
                DateTime? v = value.HasValue ? (DateTime?)value.Value.Date : null;
                if (val == v) return;
                val = v; Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }
        protected override string Display { get { return val.HasValue ? val.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : EmptyText; } }
        protected override bool Placeholder { get { return !val.HasValue; } }
        protected override Control BuildPopup()
        {
            var cv = new CalendarView(val, AllowEmpty);
            cv.Picked += d => { Value = d; ClosePopup(); };
            return cv;
        }
    }

    // =====================================================================
    //  Seletor de horário
    // =====================================================================
    class TimePanel : Control
    {
        int h, m;
        int hoverH = -1, hoverM = -1;
        readonly Action<int, int, bool> picked;

        int Pad { get { return S(10); } }
        int HW { get { return S(34); } }   // 8 colunas
        int HH { get { return S(30); } }
        int MW { get { return S(27); } }   // 10 colunas
        int MH { get { return S(28); } }
        int LabelH { get { return S(24); } }

        public TimePanel(int hour, int minute, Action<int, int, bool> picked)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            h = hour; m = minute; this.picked = picked;
            BackColor = cPanel;
            Size = new Size(HW * 8 + Pad * 2, Pad + LabelH + HH * 3 + S(8) + LabelH + MH * 6 + Pad);
        }

        Rectangle HR(int i) { return new Rectangle(Pad + (i % 8) * HW, Pad + LabelH + (i / 8) * HH, HW, HH); }
        Rectangle MR(int i) { return new Rectangle(Pad + (i % 10) * MW, Pad + LabelH + HH * 3 + S(8) + LabelH + (i / 10) * MH, MW, MH); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int a = -1, b = -1;
            for (int i = 0; i < 24; i++) if (HR(i).Contains(e.Location)) a = i;
            for (int i = 0; i < 60; i++) if (MR(i).Contains(e.Location)) b = i;
            if (a != hoverH || b != hoverM) { hoverH = a; hoverM = b; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverH = hoverM = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            for (int i = 0; i < 24; i++) if (HR(i).Contains(e.Location)) { h = i; picked(h, m, false); Invalidate(); return; }
            for (int i = 0; i < 60; i++) if (MR(i).Contains(e.Location)) { m = i; picked(h, m, true); return; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(cPanel);
            var cen = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, "HORA", Mono(8.5f, true), new Rectangle(Pad, Pad, S(100), LabelH), cMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "MINUTO", Mono(8.5f, true), new Rectangle(Pad, Pad + LabelH + HH * 3 + S(8), S(100), LabelH), cMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            for (int i = 0; i < 24; i++) Cell(g, HR(i), i.ToString("00"), i == h, i == hoverH, cen);
            for (int i = 0; i < 60; i++) Cell(g, MR(i), i.ToString("00"), i == m, i == hoverM, cen);
        }

        void Cell(Graphics g, Rectangle r, string t, bool sel, bool hov, TextFormatFlags f)
        {
            r.Inflate(-S(2), -S(2));
            using (var p = RoundRect(r, S(6)))
            {
                if (sel) { using (var b = new SolidBrush(cAmber)) g.FillPath(b, p); }
                else if (hov) { using (var b = new SolidBrush(cCard)) g.FillPath(b, p); }
            }
            TextRenderer.DrawText(g, t, Mono(9.5f, sel), r, sel ? cInk : cText, f);
        }
    }

    public class TimeField : DropField
    {
        int hh, mm;
        public event EventHandler Changed;

        public TimeField() { Width = S(130); }

        public string TimeText
        {
            get { return hh.ToString("00") + ":" + mm.ToString("00"); }
            set
            {
                int h, m;
                var p = (value ?? "").Split(':');
                if (p.Length == 2 && int.TryParse(p[0], out h) && int.TryParse(p[1], out m) && h >= 0 && h < 24 && m >= 0 && m < 60)
                { hh = h; mm = m; Invalidate(); }
            }
        }
        protected override string Display { get { return TimeText; } }
        protected override Control BuildPopup()
        {
            return new TimePanel(hh, mm, (h, m, close) =>
            {
                bool ch = h != hh || m != mm;
                hh = h; mm = m; Invalidate();
                if (ch && Changed != null) Changed(this, EventArgs.Empty);
                if (close) ClosePopup();
            });
        }
    }

    // =====================================================================
    //  Lista suspensa temática (substitui o ComboBox)
    // =====================================================================
    class ListPopup : Control, IScrollHost
    {
        readonly List<AudioMeta> items;
        readonly int selected;
        readonly ThemedScrollBar sb = new ThemedScrollBar();
        int offset, hover = -1;
        readonly int rowH = S(34);
        public event Action<int> Picked;

        public ListPopup(List<AudioMeta> items, int selected, int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.items = items; this.selected = selected;
            BackColor = cPanel;
            int visible = Math.Max(1, Math.Min(8, items.Count));
            Size = new Size(Math.Max(S(160), width), visible * rowH);
            if (items.Count > visible)
            {
                sb.BackColor = cPanel;
                sb.Setup(items.Count * rowH, Height, 0);
                sb.Scrolled += (s, e) => { offset = sb.Value; Invalidate(); };
                Controls.Add(sb);
            }
            Resize += (s, e) => { sb.SetBounds(Width - S(12), 0, S(12), Height); };
            if (selected > 3) { offset = Math.Min(Math.Max(0, items.Count * rowH - Height), (selected - 2) * rowH); sb.Value = offset; }
        }

        public void ScrollBy(int wheelDelta)
        {
            int max = Math.Max(0, items.Count * rowH - Height);
            offset = Math.Max(0, Math.Min(max, offset - wheelDelta * S(48) / 120));
            sb.Value = offset; Invalidate();
        }

        int Hit(Point p) { int i = (p.Y + offset) / rowH; return (i >= 0 && i < items.Count && p.X < (sb.Parent != null ? Width - S(12) : Width)) ? i : -1; }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = Hit(e.Location); if (h != hover) { hover = h; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int i = Hit(e.Location);
            if (e.Button == MouseButtons.Left && i >= 0 && Picked != null) Picked(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(cPanel);
            int w = sb.Parent != null ? Width - S(12) : Width;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rectangle(S(4), i * rowH - offset + S(2), w - S(8), rowH - S(4));
                if (r.Bottom < 0 || r.Top > Height) continue;
                if (i == hover) using (var p = RoundRect(r, S(6))) using (var b = new SolidBrush(cCard)) g.FillPath(b, p);
                bool isPh = string.IsNullOrEmpty(items[i].id);
                Color c = i == selected ? cAmber : isPh ? cMuted : cText;
                TextRenderer.DrawText(g, items[i].name, i == selected ? Ui(10, true) : Ui(10),
                    new Rectangle(r.X + S(8), r.Y, r.Width - S(12), r.Height), c,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    public class SelectField : DropField
    {
        public readonly List<AudioMeta> Items = new List<AudioMeta>();
        int idx = -1;
        public event EventHandler Changed;

        public int SelectedIndex
        {
            get { return idx; }
            set { int v = (value >= 0 && value < Items.Count) ? value : -1; if (v == idx) { Invalidate(); return; } idx = v; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        }
        public object SelectedItem { get { return idx >= 0 && idx < Items.Count ? Items[idx] : null; } }
        protected override string Display { get { var a = SelectedItem as AudioMeta; return a != null ? a.name : ""; } }
        protected override bool Placeholder { get { var a = SelectedItem as AudioMeta; return a == null || string.IsNullOrEmpty(a.id); } }
        protected override Control BuildPopup()
        {
            var lp = new ListPopup(Items, idx, Width);
            lp.Picked += i => { SelectedIndex = i; ClosePopup(); };
            return lp;
        }
    }

    // Campo numérico simples (substitui o NumericUpDown nativo)
    public class NumBox : TextBox
    {
        public decimal Minimum = 0, Maximum = 86400;
        public NumBox() { BorderStyle = BorderStyle.FixedSingle; TextAlign = HorizontalAlignment.Center; MaxLength = 6; Text = "0"; }
        public decimal Value
        {
            get { decimal v; return decimal.TryParse(Text, out v) ? Math.Max(Minimum, Math.Min(Maximum, v)) : Minimum; }
            set { Text = Math.Max(Minimum, Math.Min(Maximum, value)).ToString("0"); }
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); Value = Value; }
    }
}
