using System.Diagnostics;
using osu.Shared;

namespace osu_nhauto
{
    public sealed class Osu
    {
        public Osu()
        {
            ObtainProcess();

            if (osuProcess != null)
                ObtainAddresses();
        }

        public void ObtainProcess()
        {
            Process[] processes = Process.GetProcessesByName("osu!");
            osuProcess = processes.Length > 0 ? processes[0] : null;

            if (osuProcess != null)
            {
                System.Console.WriteLine("Found process");
                memory = new Memory(osuProcess);
            }
        }

        public void ObtainAddresses()
        {
            try
            {
                while (osuProcess.MainWindowHandle.ToInt32() == 0) ;
                System.Console.WriteLine("Attempting to find signatures");
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                int addressPtr = memory.FindSignature(new byte[] { 0x8B, 0x45, 0xE8, 0xA3, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x35  }, "xxxx????xx", 0x06000000);
                audioTime = memory.ReadInt32(addressPtr + 0x4);
                audioPlaying = audioTime + 0x24;
                //34 C2 2F 05 50 9B 34 07 90 09 2D 07 50 9B 34 07 00 00 80 3F
                /*
                addressPtr = memory.FindSignature(new byte[] { 0x34, 0xC2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F },
                0x1000, 0x10000000, "xx??????????????xxxx");

                timeMod = memory.ReadInt32(addressPtr + 0x4) + 0x10; */

                stopwatch.Stop();
                System.Console.WriteLine("Elapsed time to obtain addresses: {0} ms", stopwatch.ElapsedMilliseconds);
                loadedAddresses = true;
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine("Trying to load memory when osu! isn't fully loaded yet");
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                //loadedAddresses = false;
                loadedAddresses = false;
            }
        }

        public string GetWindowTitle()
        {
            if (!IsOpen())
                return string.Empty;

            osuProcess.Refresh();
            return osuProcess.MainWindowTitle;
        }

        public void UpdateWindowStatus()
        {
            if (osuProcess != null && osuProcess.HasExited)
                osuProcess = null;

            if (!IsOpen())
                ObtainProcess();
        }

        public Mods GetTimeMod()
        {
            System.Single speedMultiplier = memory.ReadSingle(timeMod);
            switch (speedMultiplier)
            {
                case (System.Single)0.75:
                    return Mods.HalfTime;
                case 1:
                    return Mods.None;
                case (System.Single)1.5:
                    return Mods.DoubleTime;
                default:
                    throw new System.Exception("Unable to find speed mod");
            }
        }

        public int GetAudioTime() => memory.ReadInt32(audioTime);
        public bool IsAddressesLoaded() => loadedAddresses;
        public bool IsOpen() => osuProcess != null && !osuProcess.HasExited;
        public Process GetProcess() => this.osuProcess;
        private Process osuProcess;
        private Memory memory;
        private int audioTime;
        private int audioPlaying;
        private int timeMod;
        private bool loadedAddresses;
    }
}
