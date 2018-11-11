﻿using System;
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
                int addressPtr = memory.FindSignature(new byte[] { 0x8B, 0x45, 0xE8, 0xA3, 0x00, 0x00, 0x00, 0x00, 0x8B, 0x35 }, "xxxx????xx", 0x06000000);
                audioTime = memory.ReadInt32(addressPtr + 0x4);
                audioPlaying = audioTime + 0x24;

                Console.WriteLine($"audioTime={audioTime.ToString("X")}");
                //34 C2 2F 05 50 9B 34 07 90 09 2D 07 50 9B 34 07 00 00 80 3F
                /*
                addressPtr = memory.FindSignature(new byte[] { 0x34, 0xC2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F },
                0x1000, 0x10000000, "xx??????????????xxxx");

                timeMod = memory.ReadInt32(addressPtr + 0x4) + 0x10; */

                stopwatch.Stop();
                Console.WriteLine("Elapsed time to obtain addresses: {0} ms", stopwatch.ElapsedMilliseconds);
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

        public int GetAudioTime() => memory.ReadInt32(audioTime);
        public bool IsAudioPlaying() => memory.ReadInt32(audioPlaying) != 0;
        public bool IsAddressesLoaded() => this.loadedAddresses;
        public bool IsOpen() => osuProcess != null && !osuProcess.HasExited;
        public float GetSpeedMultiplier() => memory.ReadSingle(timeMod);
        public Process GetProcess() => this.osuProcess;
        public RECT GetWindowResolution() { GetWindowRect(osuProcess.MainWindowHandle, out RECT resolution); return resolution; }
        public RECT GetClientResolution() { GetClientRect(osuProcess.MainWindowHandle, out RECT resolution); return resolution; }
        private Process osuProcess;
        private Memory memory;
        private int audioTime;
        private int audioPlaying;
        private int timeMod;
        private bool loadedAddresses;
    }
}
