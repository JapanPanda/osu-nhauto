using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using osu_database_reader.Components.HitObjects;

namespace osu_nhauto {

    public class Player
    {
        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", EntryPoint = "mouse_event", CallingConvention = CallingConvention.Winapi)]
        internal static extern void Mouse_Event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }


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
            /*
                Task.Run(() => {
                    AutoPilot();
                });
                */
            //new Thread(AutoPilot).Start();

            bool continueRunning = false;
            int nextTimingPtIndex = 0, nextHitObjIndex = 0;
            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            HitObject nextHitObject = beatmap.GetHitObjects()[1];
            msPerQuarter = beatmap.GetTimingPoints()[0].MsPerQuarter;

            bool shouldPressSecondary = false;
            int lastTime = osuClient.GetAudioTime();
            float velX = -1;
            float velY = -1;
            int relaxReleaseTime = int.MaxValue;
            float[] resConstants = CalculatePlayfieldResolution();
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                int currentTime = osuClient.GetAudioTime() + 4;
                if (currentTime > lastTime)
                {
                    lastTime = currentTime;
                    if (nextTimingPt != null && currentTime >= nextTimingPt.Time)
                    {
                        UpdateTimingSettings();
                        ++nextTimingPtIndex;
                        nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
                    }
                    if (currHitObject != null)
                    {
                        if (nextHitObjIndex == 0)
                        {
                            int newX = (int)((currHitObject.X * resConstants[0] + resConstants[2]) * 65535 / 1920);
                            int newY = (int)((currHitObject.Y * resConstants[1] + resConstants[3]) * 65535 / 1080);
                            Mouse_Event(0x1 | 0x8000, newX, newY, 0, 0);
                            cursorX = newX;
                            cursorY = newY;
                        }
                        else
                        {
                            AutoPilot(currHitObject, currentTime, resConstants, velX, velY);
                        }
                        if (currHitObject.Time - currentTime <= 0)
                        {
                            cursorX = (int)((currHitObject.X * resConstants[0] + resConstants[2]) * 65535 / 1920);
                            cursorY = (int)((currHitObject.Y * resConstants[1] + resConstants[3]) * 65535 / 1080);
                            Relax(currHitObject, ref shouldPressSecondary, currentTime, ref relaxReleaseTime);
                            HitObject lastHitObject = currHitObject;
                            currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                            if (currHitObject != null)
                            {
                                //Console.WriteLine("{0} : {1}, {2} x {3}", newY, cursorY, currHitObject.Time, currentTime);
                                velX = (float)(currHitObject.X - lastHitObject.X) / (currHitObject.Time - currentTime);
                                velY = (float)(currHitObject.Y - lastHitObject.Y) / (currHitObject.Time - currentTime);
                                Func<int, float> applyVelocityFactor = new Func<int, float>(i =>
                                {
                                    if (Math.Abs(i) >= 250)
                                        return 11.8f;
                                    if (Math.Abs(i) >= 160)
                                        return 9.6f;
                                    return 8.2f;
                                });

                                velX *= applyVelocityFactor(currHitObject.X - lastHitObject.X);
                                velY *= applyVelocityFactor(currHitObject.Y - lastHitObject.Y);

                                Console.WriteLine("New Vel: {0} x {1}", velX, velY);
                            }
                        }
                    }
                    else
                        return;

                    //Thread.Sleep(1);
                }
                else if (currentTime < lastTime)
                {
                    continueRunning = true;
                    break;
                }
            }

            if (continueRunning)
                Update();
        }

        private float[] CalculatePlayfieldResolution()
        {
            Osu.RECT resolution = this.osuClient.GetResolution();
            // TODO calculate border width and height for them borderless/fullscreen users rather than assume bordered window on Windows 10
            int resX = resolution.Right - resolution.Left - 6;
            int resY = resolution.Bottom - resolution.Top - 29 - 6;
            Console.WriteLine("Left: {0} x Right: {1} x Top: {2} x Bottom: {3}", resolution.Left, resolution.Right, resolution.Top, resolution.Bottom);
            Console.WriteLine("{0} x {1}", resolution.Right - resolution.Left - 6, resolution.Bottom - resolution.Top - 29 - 6);

            float playfieldY = 0.8f * resY;
            float playfieldX = playfieldY * 4 / 3;

            float playfieldOffsetX = (resX - playfieldX) / 2 + 3;
            float playfieldOffsetY = (resY - 0.95385f * playfieldY) / 2 + 32;

            Console.WriteLine("CALCULATED PLAYFIELD: {0} x {1}", playfieldX, playfieldY);
            Console.WriteLine("CALCULATED OFFSETS: {0} x {1}", playfieldOffsetX, playfieldOffsetY);

            float totalOffsetX = resolution.Left + playfieldOffsetX;
            float totalOffsetY = resolution.Top + playfieldOffsetY;
            float ratioX = playfieldX / 512;
            float ratioY = playfieldY / 384;

            return new float[4] { ratioX, ratioY, totalOffsetX, totalOffsetY };
        }

        private void AutoPilotCircle(HitObject currHitObject, float[] resConstants, ref float velX, ref float velY)
        {
            float xDiff = cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]);
            float yDiff = cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]);
            Func<float, float> applyAutoCorrect = new Func<float, float>((f) =>
            {
                if (Math.Abs(f) >= 40)
                    return 3.7f;
                if (Math.Abs(f) >= 25)
                    return 1.8f;
                if (Math.Abs(f) >= 10)
                    return 0.5f;
                if (Math.Abs(f) >= 5)
                    return 0.2f;
                if (Math.Abs(f) >= 3)
                    return 0.05f;

                return 0;
            });
            if (xDiff == 0 || (velX > 0 && xDiff >= 0) || (velX < 0 && xDiff <= 0))
                velX = -xDiff * applyAutoCorrect(xDiff);

            if (yDiff == 0 || (velY > 0 && yDiff >= 0) || (velY < 0 && yDiff <= 0))
                velY = -yDiff * applyAutoCorrect(yDiff);

            missingX += velX - (velX > 0 ? (int)Math.Floor(velX) : (int)Math.Ceiling(velX));
            missingY += velY - (velY > 0 ? (int)Math.Floor(velY) : (int)Math.Ceiling(velY));

            if (Math.Abs(missingX) >= 1)
            {
                int deltaX = missingX > 0 ? (int)Math.Floor(missingX) : (int)Math.Ceiling(missingX);
                velX += deltaX;
                missingX -= deltaX;
            }

            if (Math.Abs(missingY) >= 1)
            {
                int deltaY = missingY > 0 ? (int)Math.Floor(missingY) : (int)Math.Ceiling(missingY);
                velY += deltaY;
                missingY -= deltaY;
            }
        }

        private void AutoPilot(HitObject currHitObject, int currentTime, float[] resConstants, float velX, float velY)
        {
            if (currHitObject == null)
                return;

            GetCursorPos(out cursorPos);
            //Console.WriteLine("{0} x {1} : {2} x {3}", cursorPos.X, cursorPos.Y, currHitObject.X * resConstants[0] + resConstants[2], (currHitObject.Y * resConstants[1] + resConstants[3]));
            AutoPilotCircle(currHitObject, resConstants, ref velX, ref velY);
            Mouse_Event(0x1, (int)velX, (int)velY, 0, 0);           
        }

        private float missingX, missingY;

        private void Relax(HitObject currHitObject, ref bool shouldPressSecondary, int currentTime, ref int releaseTime)
        {
            shouldPressSecondary = GetTimeDiffFromNextObj(currHitObject) < 116 ? !shouldPressSecondary : false;
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            int delay = 16;
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

            bool pressedSecondary = shouldPressSecondary;
            Task.Delay(delay).ContinueWith(ant => inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1));
        }

        private int CalculateSliderDuration(HitObjectSlider obj) =>
            (int)Math.Ceiling(obj.Length * obj.RepeatCount / (100 * beatmap.sliderVelocity * this.speedVelocity / this.msPerQuarter));

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
        private POINT cursorPos;
        private int cursorX = -1;
        private int cursorY = -1;
    }
}

