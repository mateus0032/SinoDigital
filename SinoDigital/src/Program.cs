using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SinoDigital
{
    static class Program
    {
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);

        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(Store.Dir);
                File.AppendAllText(Path.Combine(Store.Dir, "erros.log"), DateTime.Now.ToString("s") + " " + msg + Environment.NewLine);
            }
            catch { }
        }

        // rolagem com a roda do mouse nas páginas (sem depender de foco)
        class WheelFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x020A) return false;
                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                Control c = Control.FromHandle(WindowFromPoint(Cursor.Position));
                while (c != null)
                {
                    var h = c as IScrollHost;
                    if (h != null)
                    {
                        var p = c as Page;
                        if (p != null)
                        {
                            var f = p.FindForm();
                            if (f == null || f != Form.ActiveForm) return false;
                        }
                        h.ScrollBy(delta);
                        return true;
                    }
                    c = c.Parent;
                }
                return false;
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "SinoDigital.SingleInstance", out created))
            {
                if (!created) return; // já está rodando
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log(e.Exception.ToString());
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject.ToString());

                Store.Load();
                using (var probe = new Form()) { Theme.K = probe.DeviceDpi / 96f; }
                Application.AddMessageFilter(new WheelFilter());
                Application.Run(new MainForm(args));
            }
        }
    }
}
