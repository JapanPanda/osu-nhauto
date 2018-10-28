using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using osu_database_reader.Components.HitObjects;
using System.Runtime.InteropServices;

namespace osu_nhauto {

    public class Player
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern short VkKeyScanEx(char ch, IntPtr dwhkl);

        public Player(Osu osu)
	    {
            osuClient = osu;
        }

        public void Update()
        {
            if (osuClient.GetWindowTitle() == null)
            {
                return;
            }
            int lastTime = osuClient.GetAudioTime();
            Mods timeMod = osuClient.GetTimeMod();
            int nextTimingPtIndex = 0;
            int nextHitObjIndex = 0;
            Console.WriteLine(timeMod);
            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                int currentTime = osuClient.GetAudioTime();
                if (currentTime > lastTime)
                {
                    if (nextTimingPt != null && currentTime >= nextTimingPt.Time)
                    {
                        UpdateTimingSettings();
                        ++nextTimingPtIndex;
                        nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
                        Console.WriteLine("Current time at {0}\n", currentTime);
                    }
                    if (currHitObject != null && currentTime >= currHitObject.Time)
                    {
                        inputSimulator.Keyboard.KeyDown(WindowsInput.Native.VirtualKeyCode.VK_Q);
                        int delay = 10;
                        
                        switch (currHitObject.Type & (HitObjectType)0b1000_1011)
                        {
                            case HitObjectType.Slider:
                                HitObjectSlider slider = currHitObject as HitObjectSlider;
                                int calc = calculateSliderDuration(slider);
                                delay += calc + 25;
                                break;
                            case HitObjectType.Spinner:
                                HitObjectSpinner spinner = currHitObject as HitObjectSpinner;
                                delay += spinner.EndTime - spinner.Time;
                                break;
                            default:
                                break;
                        }
                        
                        Thread.Sleep(delay);
                        inputSimulator.Keyboard.KeyUp(this.keyCode1);

                        currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                    }
                    lastTime = currentTime;
                }
                else if (currentTime < lastTime)
                {
                    Update();
                    break;
                }
                Thread.Sleep(1);
            }
        }

        private int calculateSliderDuration(HitObjectSlider obj) =>
            (int)Math.Ceiling(obj.Length * obj.RepeatCount / (100 * beatmap.SliderVelocity * speedVelocity / msPerQuarter));

        private TimingPoint GetNextTimingPoint(ref int index)
        {
            if (index >= beatmap.GetTimingPoints().Count)
                return null;

            for (; index < beatmap.GetTimingPoints().Count; ++index)
            {
                TimingPoint next = beatmap.GetTimingPoints()[index];
                TimingPoint after = index + 1 >= beatmap.GetTimingPoints().Count ?  null : beatmap.GetTimingPoints()[index + 1];

                if (next.MsPerQuarter > 0)
                    nextTimings[0] = next.MsPerQuarter;
                else if (next.MsPerQuarter < 0)
                    nextTimings[1] = -100 / next.MsPerQuarter;

                if (after == null || after.Time > next.Time)
                {
                    Console.WriteLine("Next timing point at {0}", next.Time);
                    return next;
                }
            }
            return null;
        }

        private void UpdateTimingSettings()
        {
            msPerQuarter = nextTimings[0];
            speedVelocity = nextTimings[1];
        }

        private Osu osuClient;

        public void ToggleAutoPilot() => autopilotRunning = !autopilotRunning;
        public void ToggleRelax() => relaxRunning = !relaxRunning;
        public char GetKey1() => key1;
        public char GetKey2() => key2;
        public void SetKey1(char key) {
            WindowsInput.Native.VirtualKeyCode key1;
            if (Enum.TryParse<WindowsInput.Native.VirtualKeyCode>("VK_" + key, out key1))
            {
                this.keyCode1 = key1;
                this.key1 = key;

            }
        }
        public void SetKey2(char key)
        {
            WindowsInput.Native.VirtualKeyCode key2;
            if (Enum.TryParse<WindowsInput.Native.VirtualKeyCode>("VK_" + key, out key2))
            {
                this.keyCode2 = key2;
                this.key2 = key;
            }
        }
        
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

