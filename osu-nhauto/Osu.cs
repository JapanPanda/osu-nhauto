using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace osu_nhauto
{
    public sealed class Osu
    {
        public Osu()
        {
            GetProcess();
            GetAddresses();
        }

        private void GetProcess()
        {
            Process[] processes = Process.GetProcessesByName("osu!");
            osuProcess = processes.Length > 0 ? processes[0] : null;

            memory = new Memory(processes.First());
        }

        private void GetAddresses()
        {
            int addressPtr = memory.FindSignature(new byte[] { 0xA3, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x35 }, 0x1000, 0x10000000, "x????xx");

            audioTime = memory.ReadInt32(addressPtr + 0x1);
            audioPlaying = audioTime + 0x24;
        }

        public string getWindowTitle()
        {
            osuProcess.Refresh();
            return osuProcess.MainWindowTitle;
        }

        public void SearchProcess() { this.GetProcess(); }
        public int GetAudioTime() => memory.ReadInt32(audioTime);
        public Process getOsuProcess() => this.osuProcess;
        private Process osuProcess;
        private Memory memory;
        private int audioTime;
        private int audioPlaying;
        
    }
}
