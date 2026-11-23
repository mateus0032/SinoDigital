using System;
using System.Runtime.InteropServices;

namespace SinoDigital
{
    // Lê o nível de pico do microfone padrão do Windows (Core Audio, IAudioMeterInformation).
    // Não abre stream de gravação: só consulta o medidor do dispositivo.
    public class MicMeter
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            int GetPeakValue(out float peak);
        }

        IAudioMeterInformation meter;
        DateTime nextAcquire = DateTime.MinValue;
        public bool Available { get { return meter != null; } }

        void Acquire()
        {
            Release();
            try
            {
                var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice dev;
                // eCapture = 1, eConsole = 0
                if (en.GetDefaultAudioEndpoint(1, 0, out dev) != 0 || dev == null) return;
                var iid = typeof(IAudioMeterInformation).GUID;
                object o;
                // CLSCTX_ALL = 23
                if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) return;
                meter = (IAudioMeterInformation)o;
            }
            catch { meter = null; }
        }

        void Release()
        {
            if (meter != null) { try { Marshal.ReleaseComObject(meter); } catch { } meter = null; }
        }

        // 0..1 ; 0 se não houver microfone
        public float Peak()
        {
            if (meter == null && DateTime.Now >= nextAcquire)
            {
                Acquire();
                nextAcquire = DateTime.Now.AddSeconds(5);
            }
            if (meter == null) return 0;
            try
            {
                float p;
                if (meter.GetPeakValue(out p) != 0) { Release(); return 0; }
                return p;
            }
            catch { Release(); return 0; }
        }
    }
}
