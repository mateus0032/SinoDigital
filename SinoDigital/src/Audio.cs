using System;
using System.Windows.Media;

namespace SinoDigital
{
    // Reprodução via Media Foundation (WPF MediaPlayer): mp3, wav, aac, m4a, wma, flac...
    public class AudioEngine
    {
        public class Playing
        {
            public string Label;
            public MediaPlayer Player;
            public DateTime? End;
            public bool Loop;
        }

        public Playing Current;

        // multiplicador de volume (1 = normal); usado para abaixar quando o microfone capta voz
        double gain = 1.0;
        public double Gain
        {
            get { return gain; }
            set
            {
                gain = Math.Max(0, Math.Min(1, value));
                var c = Current;
                if (c != null) { try { c.Player.Volume = gain; } catch { } }
            }
        }
        public event Action Changed;
        public event Action<string> Failed;

        void Raise() { if (Changed != null) Changed(); }

        public void Play(string path, string label, int? loopSeconds)
        {
            Stop();
            var pl = new MediaPlayer();
            var cur = new Playing { Label = label, Player = pl };
            pl.Volume = gain;
            Current = cur;
            if (loopSeconds.HasValue && loopSeconds.Value > 0)
            {
                cur.Loop = true;
                cur.End = DateTime.Now.AddSeconds(loopSeconds.Value);
            }
            pl.MediaOpened += (s, e) =>
            {
                if (Current != cur) return;
                if (!cur.Loop && pl.NaturalDuration.HasTimeSpan)
                    cur.End = DateTime.Now + pl.NaturalDuration.TimeSpan;
                Raise();
            };
            pl.MediaEnded += (s, e) =>
            {
                if (Current != cur) return;
                if (cur.Loop && cur.End.HasValue && DateTime.Now < cur.End.Value)
                {
                    pl.Position = TimeSpan.Zero;
                    pl.Play();
                }
                else Stop();
            };
            pl.MediaFailed += (s, e) =>
            {
                if (Current != cur) return;
                Stop();
                if (Failed != null) Failed(e.ErrorException != null ? e.ErrorException.Message : "erro desconhecido");
            };
            pl.Open(new Uri(path));
            pl.Play();
            Raise();
        }

        public void Stop()
        {
            var c = Current;
            if (c == null) return;
            Current = null;
            try { c.Player.Stop(); c.Player.Close(); } catch { }
            Raise();
        }

        public void Tick()
        {
            var c = Current;
            if (c != null && c.End.HasValue && DateTime.Now >= c.End.Value) Stop();
        }
    }
}
