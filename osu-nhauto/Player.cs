using System;
using System.Threading;
using System.Runtime.InteropServices;
using osu_database_reader.Components.HitObjects;

namespace osu_nhauto {

    public class Player
    {
	    public Player(Osu osu)
	    {
            osuClient = osu;
        }

        public void Update()
        {
            int lastTime = osuClient.GetAudioTime();
            int nextHitObjIndex = 0;
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                int currentTime = osuClient.GetAudioTime();
                if (currentTime > lastTime)
                {
                    // map is playing
                    HitObject nextHitObject = beatmap.GetHitObjects()[nextHitObjIndex];
                    if (currentTime >= nextHitObject.Time)
                    {
                        INPUT[] input = new INPUT[1];
                        //Console.WriteLine("Type: {0} | X: {1}, Y: {2} | ms: {3}", nextHitObject.Type.ToString(), nextHitObject.X, nextHitObject.Y, nextHitObject.Time);
                        ++nextHitObjIndex;
                    }
                }
                else if (currentTime < lastTime)
                {
                    // map restarted
                    nextHitObjIndex = 0;
                }
                lastTime = currentTime;
                Thread.Sleep(1);
            }
        }

        [DllImport("user32.dll")]
        public static extern int SendInput(uint cInputs, INPUT[] inputs, int cbSize);

        [DllImport("user32.dll")]
        static extern short GetKeyState(int nVirtKey);

        private Osu osuClient;

        public void ToggleAutoPilot() => autopilotRunning = !autopilotRunning;
        public void ToggleRelax() => relaxRunning = !relaxRunning;
        public char GetKey1() => key1;
        public char GetKey2() => key2;
        public void SetKey1(char key) => key1 = key;
        public void SetKey2(char key) => key2 = key;
        public bool IsAutoPilotRunning() => autopilotRunning;
        public bool IsRelaxRunning() => relaxRunning;
        public void SetBeatmap(CurrentBeatmap cb) => beatmap = cb;

        private char key1 = 'Z';
        private char key2 = 'X';
        private bool autopilotRunning = false;
        private bool relaxRunning = false;
        private CurrentBeatmap beatmap;
    }

    public struct MOUSEINPUT
    {
        int dx;
        int dy;
        uint mouseData;
        uint dwFlags;
        uint time;
        IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        uint uMsg;
        ushort wParamL;
        ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT
    {
        [FieldOffset(0)]
        public int type;
        [FieldOffset(4)]
        public MOUSEINPUT mi;
        [FieldOffset(4)]
        public KEYBDINPUT ki;
        [FieldOffset(4)]
        public HARDWAREINPUT hi;
    }
}

