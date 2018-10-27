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

        public void testKeyPress()
        {

            inputSimulator.Keyboard.KeyDown(this.keyCode1);
            Thread.Sleep(100);
            inputSimulator.Keyboard.KeyUp(this.keyCode1);
        }

        public void Update()
        {
            int lastTime = osuClient.GetAudioTime();
            int nextTimingPtIndex = 0;
            int nextHitObjIndex = 0;

            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                int currentTime = osuClient.GetAudioTime();
                if (currentTime > lastTime)
                {
                    //TimingPoint currTimingPt = nextTimingPtIndex < beatmap.GetTimingPoints().Count ? beatmap.GetTimingPoints()[nextTimingPtIndex] : null; ;
                    if (currHitObject != null && currentTime >= currHitObject.Time)
                    {
                        //inputSimulator.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.VK_Q);
                        //inputSimulator.Keyboard.KeyUp(WindowsInput.Native.VirtualKeyCode.VK_Q);
                        inputSimulator.Keyboard.KeyDown(this.keyCode1);
                        int delay = 10;
                        
                        switch (currHitObject.Type & (HitObjectType)0b1000_1011)
                        {
                            case HitObjectType.Slider:
                                HitObjectSlider slider = currHitObject as HitObjectSlider;
                                int calc = calculateSliderDuration(slider, nextTimingPt);
                                Console.Write("Slider Calc: {0}, ", calc);
                                delay += calc - 10;
                                break;
                            case HitObjectType.Spinner:
                                HitObjectSpinner spinner = currHitObject as HitObjectSpinner;
                                delay += spinner.EndTime - spinner.Time;
                                break;
                            default:
                                break;
                        }
                        
                        Console.WriteLine("Current: {0}, Object: {1}", currentTime, currHitObject.Time);
                        Thread.Sleep(delay);
                        inputSimulator.Keyboard.KeyUp(this.keyCode1);

                        currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;

                        /*
                        Console.WriteLine("Type: {0} | X: {1}, Y: {2} | ms: {3}", currHitObject.Type.ToString(), currHitObject.X, currHitObject.Y, currHitObject.Time);
                        switch (currHitObject.Type & (HitObjectType)0b1000_1011)
                        {
                            case HitObjectType.Slider:
                                HitObjectSlider slider = currHitObject as HitObjectSlider;
                                Console.WriteLine("Repeat: {0} | Length: {1}", slider.RepeatCount, slider.Length);
                                break;
                            default:
                                break;
                        }
                        */

                        //currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                    }
                    if (nextTimingPt != null && currentTime > nextTimingPt.Time)
                    {
                        ++nextTimingPtIndex;
                        nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
                    }
                    lastTime = currentTime;
                }
                else if (currentTime < lastTime)
                {
                    // map restarted
                    nextTimingPtIndex = 0;
                    nextHitObjIndex = 0;
                    lastTime = currentTime;
                }
                Thread.Sleep(1);
            }
        }

        private int calculateSliderDuration(HitObjectSlider obj, TimingPoint tp)
        {
            //Console.WriteLine(realMsPerQuarter);
            //Console.WriteLine(tp.MsPerQuarter);
            //Console.WriteLine(tp.Time);
            //Console.WriteLine(beatmap.SliderVelocity);

            double speedVelocity = 1;
            if (tp.MsPerQuarter < 0)
                speedVelocity = -100 / tp.MsPerQuarter;

            //Console.WriteLine(speedVelocity);

            double overallVelocity = 100 * beatmap.SliderVelocity * speedVelocity / realMsPerQuarter;
            return (int)Math.Ceiling(obj.Length * obj.RepeatCount / overallVelocity);
        }

        private TimingPoint GetNextTimingPoint(ref int index)
        {
            TimingPoint tp = beatmap.GetTimingPoints()[index];
            if (tp.MsPerQuarter > 0)
                realMsPerQuarter = tp.MsPerQuarter;
            for (; index < beatmap.GetTimingPoints().Count - 1; ++index)
            {
                Console.Write("{0} ", index);
                if (beatmap.GetTimingPoints()[index + 1].Time > beatmap.GetTimingPoints()[index].Time)
                    return beatmap.GetTimingPoints()[index];
            }
            return tp;
        }

        private TimingPoint GetPreviousTimingPoint(TimingPoint tp)
        {
            return beatmap.GetTimingPoints()[beatmap.GetTimingPoints().IndexOf(tp) - 1];
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
        private double realMsPerQuarter = 1000.0;
        private InputSimulator inputSimulator = new InputSimulator();
        private CurrentBeatmap beatmap;
    }
}

