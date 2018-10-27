using System.Diagnostics;

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
                memory = new Memory(osuProcess);
        }

        public void ObtainAddresses()
        {
            int addressPtr = memory.FindSignature(new byte[] { 0xA3, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x35 }, 0x1000, 0x10000000, "x????xx");

            audioTime = memory.ReadInt32(addressPtr + 0x1);
            audioPlaying = audioTime + 0x24;
        }

        public string GetWindowTitle()
        {
            osuProcess.Refresh();
            return osuProcess.MainWindowTitle;
        }

        public void UpdateWindowStatus()
        {
            if (osuProcess == null || osuProcess.HasExited)
                ObtainProcess();
        }

        public int GetAudioTime() => memory.ReadInt32(audioTime);
        public Process GetProcess() => this.osuProcess;
        private Process osuProcess;
        private Memory memory;
        private int audioTime;
        private int audioPlaying;       
    }
}
