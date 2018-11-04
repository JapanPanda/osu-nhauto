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
            bool continueRunning = false;
            int nextTimingPtIndex = 0, nextHitObjIndex = 0;
            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            HitObject nextHitObject = beatmap.GetHitObjects()[1];
            msPerQuarter = beatmap.GetTimingPoints()[0].MsPerQuarter;

            bool shouldPressSecondary = false;
            int lastTime = osuClient.GetAudioTime();
            float velX = 0, velY = 0;
            resConstants = CalculatePlayfieldResolution();
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
                            cursorX = (int)(currHitObject.X * resConstants[0] + resConstants[2]) * 65535 / 1920;
                            cursorY = (int)(currHitObject.Y * resConstants[1] + resConstants[3]) * 65535 / 1080;
                            Mouse_Event(0x1 | 0x8000, cursorX, cursorY, 0, 0);
                        }
                        else
                        {
                            AutoPilot(currHitObject, currentTime, velX, velY);
                        }
                        if (currHitObject.Time - currentTime <= 0)
                        {
                            cursorX = (int)(currHitObject.X * resConstants[0] + resConstants[2]) * 65535 / 1920;
                            cursorY = (int)(currHitObject.Y * resConstants[1] + resConstants[3]) * 65535 / 1080;
                            Relax(currHitObject, ref shouldPressSecondary, currentTime);
                            HitObject lastHitObject = currHitObject;
                            currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                            if (currHitObject != null)
                            {
                                //Console.WriteLine("{0} : {1}, {2} x {3}", newY, cursorY, currHitObject.Time, currentTime);
                                velX = (float)(currHitObject.X - lastHitObject.X) / (currHitObject.Time - currentTime);
                                velY = (float)(currHitObject.Y - lastHitObject.Y) / (currHitObject.Time - currentTime);
                                Func<int, float> applyVelocityFactor = new Func<int, float>(i =>
                                {
                                    /*
                                    if (Math.Abs(i) >= 250)
                                        return 10.4f; // 11.8
                                    if (Math.Abs(i) >= 160)
                                        return 9.6f; */
                                    return 8.28f; // 8.2
                                });

                                float dist = (float)Math.Sqrt(Math.Pow(currHitObject.X - lastHitObject.X, 2) + Math.Pow(currHitObject.Y - lastHitObject.Y, 2));
                                velX *= applyVelocityFactor((int)dist);
                                velY *= applyVelocityFactor((int)dist);
                                Console.WriteLine("Velocity({0}, {1})", velX, velY);
                            }
                        }
                    }
                    else
                        return;

                    Thread.Sleep(1);
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

        private void AutoPilot(HitObject currHitObject, int currentTime, float velX, float velY)
        {
            if (currHitObject == null)
                return;

            POINT cursorPos = GetCursorPos();
            //Console.WriteLine("{0} x {1} : {2} x {3}", cursorPos.X, cursorPos.Y, currHitObject.X * resConstants[0] + resConstants[2], (currHitObject.Y * resConstants[1] + resConstants[3]));
            float xDiff = cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]);
            float yDiff = cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]);
            float circlePxSize = (float)(54.4 - 4.48 * beatmap.CircleSize);
            Func<float, float> applyAutoCorrect = new Func<float, float>((f) =>
            {
                float dist = Math.Abs(f) - circlePxSize + 11;
                if (dist >= 40)
                    return 3.7f;
                if (dist >= 30)
                    return 2.1f;
                if (dist >= 25)
                    return 1.8f;
                if (dist >= 15)
                    return 0.9f;
                if (dist >= 10)
                    return 0.5f;
                if (dist >= 5)
                    return 0.2f;
                if (dist >= 3)
                    return 0.05f;

                return 0;
            });
            if (xDiff == 0 || (velX > 0 && xDiff >= 0) || (velX < 0 && xDiff <= 0))
                velX = -xDiff * applyAutoCorrect(xDiff) * 1.08f;

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
            Mouse_Event(0x1, (int)velX, (int)velY, 0, 0);           
        }

        private float missingX, missingY;

        private void Relax(HitObject currHitObject, ref bool shouldPressSecondary, int currentTime)
        {
            //IsCursorHoveringOverObject(currHitObject);
            shouldPressSecondary = GetTimeDiffFromNextObj(currHitObject) < 116 ? !shouldPressSecondary : false;
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            Thread.Sleep(2);
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

        private bool IsCursorHoveringOverObject(HitObject hitObj)
        {
            POINT cursor = GetCursorPos();
            double dist = Math.Sqrt(Math.Pow(cursor.X - (hitObj.X * resConstants[0] + resConstants[2]), 2) + Math.Pow(cursor.Y - (hitObj.Y * resConstants[1] + resConstants[3]), 2));
            float circlePxSize = 54.4f - 4.48f * (float)beatmap.CircleSize;
            //Console.WriteLine($"Dist={dist}, Hovering={dist <= circlePxSize - 4}");
            return dist <= 54.4f - 4.48f * (float)beatmap.CircleSize;
        }

        private POINT GetCursorPos()
        {
            GetCursorPos(out POINT cursor);
            return cursor;
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
        private float[] resConstants;
        private InputSimulator inputSimulator = new InputSimulator();
        private CurrentBeatmap beatmap;
        private int cursorX = -1;
        private int cursorY = -1;
    }
}

