using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using osu_database_reader.Components.HitObjects;

namespace osu_nhauto
{

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

        private enum KeyPressed
        {
            None, Key1, Key2
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
            bool continueRunning = false;
            int nextTimingPtIndex = 0, nextHitObjIndex = 0;
            TimingPoint nextTimingPt = GetNextTimingPoint(ref nextTimingPtIndex);
            HitObject lastHitObject = null;
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            //HitObject nextHitObject = beatmap.GetHitObjects()[1];
            msPerQuarter = beatmap.GetTimingPoints()[0].MsPerQuarter;
            speedVelocity = 1;
            bool shouldPressSecondary = false;
            int lastTime = osuClient.GetAudioTime();
            float velX = 0, velY = 0;
            resConstants = CalculatePlayfieldResolution();
            center = CalculateCenter();
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
                        if (nextHitObjIndex == 0 && (currHitObject.Type & (HitObjectType)0b1000_1011) != HitObjectType.Spinner)
                            Mouse_Event(0x1 | 0x8000, (int)((currHitObject.X * resConstants[0] + resConstants[2]) * 65535 / 1920), (int)((currHitObject.Y * resConstants[1] + resConstants[3]) * 65535 / 1080), 0, 0);
                        else
                            AutoPilot(currHitObject, currentTime, velX, velY);

                        if (currHitObject.Time - currentTime <= 0)
                        {
                            if (currHitObject != lastHitObject)
                            {
                                Relax(currHitObject, currentTime, ref shouldPressSecondary);
                                lastHitObject = currHitObject;
                            }

                            if (currentTime >= GetHitObjectEndTime(currHitObject))
                                currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;

                            if (currHitObject != null && currHitObject != lastHitObject)
                            {
                                float xDiff, yDiff;
                                if ((currHitObject.Type & (HitObjectType)0b1000_1011) == HitObjectType.Spinner)
                                {
                                    xDiff = currHitObject.X * resConstants[0] + resConstants[2] - cursorPos.X;
                                    yDiff = currHitObject.Y * resConstants[1] + resConstants[3] - cursorPos.Y;
                                }
                                else
                                {
                                    xDiff = currHitObject.X - lastHitObject.X;
                                    yDiff = currHitObject.Y - lastHitObject.Y;
                                }
                                velX = xDiff / (currHitObject.Time - currentTime) * resConstants[0];
                                velY = yDiff / (currHitObject.Time - currentTime) * resConstants[1];
                                //Console.WriteLine("{0} : {1}, {2} x {3}", newY, cursorY, currHitObject.Time, currentTime);
                                Func<float, float> applyVelocityFactor = new Func<float, float>(i =>
                                {
                                    if (Math.Abs(i) >= 250)
                                        return 8.28f; // 11.8
                                    if (Math.Abs(i) >= 160)
                                        return 7.18f; // 9.6
                                    return 6.08f; // 8.2
                                });
                                velX *= applyVelocityFactor(xDiff);
                                velY *= applyVelocityFactor(yDiff);
                                //Console.WriteLine("New Vel: {0} x {1}", velX, velY);
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
            Console.WriteLine("CALCULATED PLAYFIELD: {0} x {1}", playfieldX, playfieldY);

            float playfieldOffsetX = (resX - playfieldX) / 2 + 3;
            float playfieldOffsetY = (resY - 0.95385f * playfieldY) / 2 + 32;
            Console.WriteLine("CALCULATED OFFSETS: {0} x {1}", playfieldOffsetX, playfieldOffsetY);

            float ratioX = playfieldX / 512;
            float ratioY = playfieldY / 384;
            Console.WriteLine($"CALCULATED RATIOS: {ratioX} x {ratioY}");

            float totalOffsetX = resolution.Left + playfieldOffsetX;
            float totalOffsetY = resolution.Top + playfieldOffsetY;
            return new float[4] { ratioX, ratioY, totalOffsetX, totalOffsetY };
        }

        private POINT CalculateCenter()
        {
            Osu.RECT resolution = this.osuClient.GetResolution();
            POINT center;
            float xOffset = (int)((resolution.Right - resolution.Left) / 2);
            float yOffset = (int)((resolution.Bottom - resolution.Top) / 2);
            center.X = (int)(resolution.Left + xOffset);
            center.Y = (int)(resolution.Top + yOffset + 29);
            Console.WriteLine("CALCULATED CENTER: {0} x {1}", center.X, center.Y);
            return center;
        }

        private bool IsCursorOnCircle(HitObject currHitObject)
        {
            float circlePxSize = 5;
            float dist = (float)Math.Sqrt(Math.Pow(cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]), 2) + Math.Pow(cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]), 2));
            Console.WriteLine("DIST: {0} | CIRCLESIZE: {1}", dist, circlePxSize);
            if (dist < circlePxSize)
            {
                Console.WriteLine("true");
                return true;
            }
            return false;
        }

        private void AutoPilotCircle(HitObject currHitObject, ref float velX, ref float velY)
        {
            if (currHitObject == null || IsCursorOnCircle(currHitObject))
                return;

            GetCursorPos(out cursorPos);
            //Console.WriteLine("{0} x {1} : {2} x {3}", cursorPos.X, cursorPos.Y, currHitObject.X * resConstants[0] + resConstants[2], (currHitObject.Y * resConstants[1] + resConstants[3]));
            float xDiff = cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]);
            float yDiff = cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]);
            float circlePxSize = (float)(54.4 - 4.48 * beatmap.CircleSize);
            Func<float, float> applyAutoCorrect = new Func<float, float>((f) =>
            {
                float dist = Math.Abs(f) - circlePxSize + 11;
                if (dist >= 40)
                    return 3.7f;
                if (dist >= 25)
                    return 1.8f;
                if (dist >= 10)
                    return 0.45f;
                if (dist >= 5)
                    return 0.18f;
                if (dist >= 3)
                    return 0.05f;

                return 0;
            });
            if (xDiff == 0 || (velX > 0 && xDiff >= 0) || (velX < 0 && xDiff <= 0))
                velX = -xDiff * applyAutoCorrect(xDiff) * (float)Math.Pow(resConstants[0], 0.825);

            if (yDiff == 0 || (velY > 0 && yDiff >= 0) || (velY < 0 && yDiff <= 0))
                velY = -yDiff * applyAutoCorrect(yDiff) * (float)Math.Pow(resConstants[1], 0.825);

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

        private double EclipseAngle = 0;

        double increment = Math.PI / 14;
        private void AutoPilotSpinner(ref float velX, ref float velY)
        {
            GetCursorPos(out cursorPos);
            float dist = (float)Math.Sqrt(Math.Pow(cursorPos.X - center.X, 2) + Math.Pow(cursorPos.Y - center.Y, 2));
            
            if (dist > 100)
            {
                
                float x = (center.X - cursorPos.X) / 15f;
                float y = (center.Y - cursorPos.Y) / 15f;
                Mouse_Event(0x1, (int)x, (int)y, 0, 0);
                if (Math.Floor(dist) <= 101)
                {
                    EclipseAngle = Math.Atan2((float)(cursorPos.Y - center.Y), (float)(cursorPos.X - center.X));
                }
            }
            else
            {
                float x = (center.X + (float)(98 * Math.Cos(EclipseAngle))) * 65535 / 1920;
                float y = (center.Y+ (float)(98 * Math.Sin(EclipseAngle))) * 65535 / 1080;
                EclipseAngle += increment;
                Mouse_Event(0x1 | 0x8000, (int)x, (int)y, 0, 0);
            }
        }

        private void AutoPilot(HitObject currHitObject, int currentTime, float velX, float velY)
        {
            switch (currHitObject.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Normal:
                    AutoPilotCircle(currHitObject, ref velX, ref velY);
                    break;
                case HitObjectType.Slider:
                    AutoPilotCircle(currHitObject, ref velX, ref velY);
                    break;
                case HitObjectType.Spinner:
                    AutoPilotSpinner(ref velX, ref velY);
                    break;
            }
        }

        private void Relax(HitObject currHitObject, int currentTime, ref bool shouldPressSecondary)
        {
            shouldPressSecondary = GetTimeDiffFromNextObj(currHitObject) < 116 ? !shouldPressSecondary : false;
            Thread.Sleep(2);
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            //Thread.Sleep(2);
            int offset = Math.Max(0, GetHitObjectEndTime(currHitObject) - currHitObject.Time);
            bool pressedSecondary = shouldPressSecondary;
            Task.Delay(offset + 16).ContinueWith(ant => inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1));
        }

        private int CalculateSliderDuration(HitObjectSlider obj) =>
            (int)Math.Ceiling(obj.Length * obj.RepeatCount / (100 * beatmap.SliderVelocity * speedVelocity / msPerQuarter));

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

            return beatmap.GetHitObjects()[index + 1].Time - GetHitObjectEndTime(hitObj);
        }

        private int GetHitObjectEndTime(HitObject hitObj)
        {
            int startTime = hitObj.Time;
            switch (hitObj.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Slider:
                    //return startTime;
                    return startTime + CalculateSliderDuration(hitObj as HitObjectSlider);
                case HitObjectType.Spinner:
                    return (hitObj as HitObjectSpinner).EndTime;
                default:
                    return startTime;
            }
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
        private float missingX, missingY;
        private float[] resConstants;
        private POINT center;
        private KeyPressed keyPressed = KeyPressed.None;
    }
}
