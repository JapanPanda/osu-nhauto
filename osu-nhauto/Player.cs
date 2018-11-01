using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using osu_database_reader.Components.HitObjects;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace osu_nhauto {

    public class Player
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern short VkKeyScanEx(char ch, IntPtr dwhkl);
        [System.Runtime.InteropServices.DllImportAttribute("user32.dll", EntryPoint = "SetCursorPos")]
        [return: System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        public Player(Osu osu)
	    {
            this.keyCode1 = (WindowsInput.Native.VirtualKeyCode)(this.key1);
            this.keyCode2 = (WindowsInput.Native.VirtualKeyCode)(this.key2);
            this.osuClient = osu;
        }

        public void Update()
        {
            //Mods timeMod = osuClient.GetTimeMod();

            //if (this.autopilotRunning) 
                Task.Run(() => {
                    AutoPilot();
                });

            if (this.relaxRunning)
                Relax();
        }

        public async void AutoPilot()
        {
            int nextTimingPtIndex = 0, nextHitObjIndex = 0;
            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            msPerQuarter = currHitObject.Time;
            Osu.RECT resolution = this.osuClient.GetResolution();
            int resX = resolution.Right - resolution.Left - 6;
            int resY = resolution.Bottom - resolution.Top - 29;
            Console.WriteLine("Left: {0} x Right: {1} x Top: {2} x Bottom: {3}", resolution.Left, resolution.Right, resolution.Top, resolution.Bottom);
            Console.WriteLine("{0} x {1}", resolution.Right - resolution.Left - 6, resolution.Bottom - resolution.Top - 29);
            int lastTime = osuClient.GetAudioTime();
            // calculate how big playfield is going to be
            int modifiedXRes = (int)((float)4 / 3 * (resolution.Bottom - resolution.Top));
            int playfieldX = (int)(resY + 55);
            int playfieldY = (int)(0.75f * resY + 39);
            int playfieldOffsetX = (int)(resX - playfieldX) / 2;

            int playfieldOffsetY = (int)(resY - playfieldY + 59) / 2;
            Console.WriteLine("CALCULATED PLAYFIELD: {0} x {1}", playfieldX, playfieldY);
            Console.WriteLine("CALCULATED OFFSETS: {0} x {1}", playfieldOffsetX, playfieldOffsetY);

            // calculate the left side offset and how much to move
            int totalOffsetX = resolution.Left + playfieldOffsetX;
            int totalOffsetY = resolution.Top + playfieldOffsetY;
            float ratioX = (float)playfieldX / 512;
            float ratioY = (float)playfieldY / 383;

            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                int currentTime = osuClient.GetAudioTime();
                if (currentTime > lastTime)
                {
                    lastTime = currentTime;
                    if (currHitObject != null && currentTime >= currHitObject.Time - 300)
                    {
                        
                        Console.WriteLine("SETTING TO: {0} x {1}", (int)(currHitObject.X * ratioX) + totalOffsetX, (int)(currHitObject.Y * ratioY) + totalOffsetY);
                        SetCursorPos((int)(currHitObject.X * ratioX) + totalOffsetX, (int)(currHitObject.Y * ratioY) + totalOffsetY);
                        currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                    }
                    await Task.Delay(10);
                }
                else if (currentTime < lastTime)
                {
                    Update();
                    break;
                }

            }
        }

        public void Relax()
        {
            int nextTimingPtIndex = 0, nextHitObjIndex = 0;
            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            msPerQuarter = currHitObject.Time;

            bool shouldPressSecondary = false;
            int lastTime = osuClient.GetAudioTime();


            // Relax 
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                int currentTime = osuClient.GetAudioTime();
                if (this.relaxRunning && currentTime > lastTime)
                {
                    lastTime = currentTime;
                    if (nextTimingPt != null && currentTime >= nextTimingPt.Time)
                    {
                        UpdateTimingSettings();
                        ++nextTimingPtIndex;
                        nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
                    }
                    if (currHitObject != null && currentTime >= currHitObject.Time)
                    {
                        shouldPressSecondary = GetTimeDiffFromNextObj(currHitObject) < 116 ? !shouldPressSecondary : false;
                        inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
                        int delay = 30;
                        switch (currHitObject.Type & (HitObjectType)0b1000_1011)
                        {
                            case HitObjectType.Slider:
                                HitObjectSlider slider = currHitObject as HitObjectSlider;
                                delay += CalculateSliderDuration(slider);
                                break;
                            case HitObjectType.Spinner:
                                HitObjectSpinner spinner = currHitObject as HitObjectSpinner;
                                delay += spinner.EndTime - spinner.Time;
                                break;
                            default:
                                break;
                        }
                        Thread.Sleep(delay);
                        inputSimulator.Keyboard.KeyUp(shouldPressSecondary ? keyCode2 : keyCode1);
                        currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                    }
                }
                else if (currentTime < lastTime)
                {
                    Update();
                    break;
                }
                Thread.Sleep(1);
            }
        }

        private int CalculateSliderDuration(HitObjectSlider obj) =>
            (int)Math.Ceiling(obj.Length * obj.RepeatCount / (100 * beatmap.SliderVelocity * this.speedVelocity / this.msPerQuarter));

        private TimingPoint GetNextTimingPoint(ref int index)
        {
            if (index >= beatmap.GetTimingPoints().Count)
                return null;

            for (; index < beatmap.GetTimingPoints().Count; ++index)
            {
                TimingPoint next = beatmap.GetTimingPoints()[index];
                TimingPoint after = index + 1 >= beatmap.GetTimingPoints().Count ? null : beatmap.GetTimingPoints()[index + 1];

                if (next.MsPerQuarter > 0)
                {
                    nextTimings[0] = next.MsPerQuarter;
                    nextTimings[1] = 1;
                }
                else if (next.MsPerQuarter < 0)
                    nextTimings[1] = -100 / next.MsPerQuarter;

                if (after == null || after.Time > next.Time)
                    return next;
            }
            return null;
        }

        private void UpdateTimingSettings()
        {
            Console.WriteLine("{0}ms/quarter | {1}x speed vel", nextTimings[0], nextTimings[1]);
            msPerQuarter = nextTimings[0];
            speedVelocity = nextTimings[1];
        }

        private int GetTimeDiffFromNextObj(HitObject hitObj)
        {
            int index = beatmap.GetHitObjects().IndexOf(hitObj);
            if (index >= beatmap.GetHitObjects().Count - 1)
                return int.MaxValue;

            int currEndTime = hitObj.Time;
            switch (hitObj.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Slider:
                    currEndTime += CalculateSliderDuration(hitObj as HitObjectSlider);
                    break;
                case HitObjectType.Spinner:
                    currEndTime = (hitObj as HitObjectSpinner).EndTime;
                    break;
            }
            HitObject next = beatmap.GetHitObjects()[index + 1];

            return beatmap.GetHitObjects()[index + 1].Time - currEndTime;
        }

        private Osu osuClient;

        public void ToggleAutoPilot() => autopilotRunning = !autopilotRunning;
        public void ToggleRelax() => relaxRunning = !relaxRunning;
        public char GetKey1() => key1;
        public char GetKey2() => key2;
        public void SetKey1(char key) { this.keyCode1 = (WindowsInput.Native.VirtualKeyCode)(key); this.key1 = key; }
        public void SetKey2(char key) { this.keyCode2 = (WindowsInput.Native.VirtualKeyCode)(key); this.key2 = key; }
        
        public bool IsAutoPilotRunning() => autopilotRunning;
        public bool IsRelaxRunning() => relaxRunning;
        public void SetBeatmap(CurrentBeatmap cb) => beatmap = cb;

        private WindowsInput.Native.VirtualKeyCode keyCode1;
        private WindowsInput.Native.VirtualKeyCode keyCode2;
        private char key1 = 'Z';
        private char key2 = 'X';
        private bool autopilotRunning = false;
        private bool relaxRunning = false;
        private double msPerQuarter = 1000;
        private double speedVelocity = 1;
        private double[] nextTimings = new double[2];
        private InputSimulator inputSimulator = new InputSimulator();
        private CurrentBeatmap beatmap;
    }
}

