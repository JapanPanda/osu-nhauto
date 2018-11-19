using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace osu_nhauto
{
    public sealed class Osu
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT Rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT Rect);

        public struct RECT
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        public struct SCORE_DATA
        {
            public int score_300;
            public int score_100;
            public int score_50;
            public int score_0;
            public int current_score;
            public int current_combo;
        }

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

            if (osuProcess != null && !osuProcess.HasExited)
            {
                Console.WriteLine("Found process");
                GetWindowResolution();
                memory = new Memory(osuProcess);
            }
        }

        public void ObtainAddresses()
        {
            try
            {
                while (osuProcess.MainWindowHandle.ToInt32() == 0) ;
                Console.WriteLine("Attempting to find signatures");
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                int primitiveAudioTime = memory.FindSignature(new string[] { "8B", "45", "E8", "A3", "??", "??", "??", "??", "8B", "35" }, 0xF000000, 0x1D000000, 0x500000); 
                stopwatch.Stop();

                Console.WriteLine("Elapsed time to obtain address 1: {0} ms", stopwatch.ElapsedMilliseconds);
                audioTime = memory.ReadInt32(primitiveAudioTime + 0x4);
                audioPlaying = audioTime + 0x24;
                Console.WriteLine($"audioTime={audioTime.ToString("X")}");
                stopwatch.Restart();
                int primitivePlaySession = memory.FindSignature(new string[] { "75", "30", "A1", "??", "??", "??", "??", "80", "B8" }, 0x2000000, 0x6000000);
                playSession = memory.ReadInt32(primitivePlaySession + 0x3;
                Console.WriteLine($"playSession={playSession.ToString("X")}");
                Console.WriteLine(primitivePlaySession.ToString("X"));
                stopwatch.Stop();
                Console.WriteLine("Elapsed time to obtain address 2: {0} ms", stopwatch.ElapsedMilliseconds);
                loadedAddresses = true;
            }
            catch (System.Exception e)
            {
                Console.WriteLine("Trying to load memory when osu! isn't fully loaded yet");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
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

        public int? GetModValue()
        {
            int currSess = memory.ReadInt32(playSession);
            if (currSess == 0)
                return null;

            int modStruct = memory.ReadInt32(currSess + 0x1C);
            uint thing1 = memory.ReadUInt32(modStruct + 0x8);
            uint thing2 = memory.ReadUInt32(modStruct + 0xC);
            return (int)(thing1 ^ thing2);
        }

        public SCORE_DATA? GetScoreData()
        {
            int currSess = memory.ReadInt32(playSession);
            if (currSess == 0)
                return null;
            
            SCORE_DATA scoreData;
            scoreData.score_300 = memory.ReadShort(currSess + 0x86);
            scoreData.score_100 = memory.ReadShort(currSess + 0x84);
            scoreData.score_50 = memory.ReadShort(currSess + 0x88);
            scoreData.score_0 = memory.ReadShort(currSess + 0x8E);
            scoreData.current_combo = memory.ReadShort(currSess + 0x90);
            scoreData.current_score = memory.ReadInt32(currSess + 0x74);
            return scoreData;
        }

        public int GetAudioTime() => memory.ReadInt32(audioTime);
        public bool IsAudioPlaying() => memory.ReadInt32(audioPlaying) != 0;
        public bool IsAddressesLoaded() => this.loadedAddresses;
        public bool IsOpen() => osuProcess != null && !osuProcess.HasExited;
        public float GetSpeedMultiplier() => 1;
        public Process GetProcess() => this.osuProcess;
        public RECT GetWindowResolution() { GetWindowRect(osuProcess.MainWindowHandle, out RECT resolution); return resolution; }
        public RECT GetClientResolution() { GetClientRect(osuProcess.MainWindowHandle, out RECT resolution); return resolution; }
        private Process osuProcess;
        private Memory memory;
        private int audioTime;
        private int audioPlaying;
        private int playSession;
        private bool loadedAddresses;
    }
}
