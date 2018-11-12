using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using osu.Shared;
using osu_database_reader.Components;
using osu_database_reader.Components.Beatmaps;
using osu_database_reader.Components.HitObjects;

namespace osu_nhauto
{

    public class Player
    {
        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", EntryPoint = "mouse_event", CallingConvention = CallingConvention.Winapi)]
        internal static extern void Mouse_Event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
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
            keyCode1 = (WindowsInput.Native.VirtualKeyCode)key1;
            keyCode2 = (WindowsInput.Native.VirtualKeyCode)key2;
            osuClient = osu;
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
            circlePxSize = (float)(54.4 - 4.48 * beatmap.CircleSize);
            speedVelocity = 1;
            bool shouldPressSecondary = false;
            int lastTime = osuClient.GetAudioTime();
            float velX = 0, velY = 0;
            resConstants = CalculatePlayfieldResolution();
            center = CalculateCenter();
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                currentTime = osuClient.GetAudioTime() + 6;
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
                        if (nextHitObjIndex == 0 && currentTime < currHitObject.Time && (currHitObject.Type & (HitObjectType)0b1000_1011) != HitObjectType.Spinner)
                        {
                            int x = (int)(currHitObject.X * resConstants[0] + resConstants[2]);
                            int y = (int)(currHitObject.Y * resConstants[1] + resConstants[3]);
                            cursorPos2 = new POINT(x, y);
                            Mouse_Event(0x1 | 0x8000, x * 65535 / 1920, y * 65535 / 1080, 0, 0);
                        }
                        else
                            AutoPilot(currHitObject, velX, velY);

                        if (currHitObject.Time - currentTime <= 0)
                        {
                            if (currHitObject != lastHitObject)
                            {
                                Relax(currHitObject, ref shouldPressSecondary, ref nextHitObjIndex);
                                lastHitObject = currHitObject;
                            }

                            if (currentTime >= GetHitObjectEndTime(currHitObject) + 3)
                            {
                                currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;

                                if (currHitObject != null)
                                {
                                    GetVelocities(currHitObject, lastHitObject, ref velX, ref velY);
                                    //Console.WriteLine("New Vel: {0} x {1}", velX, velY);
                                }
                            }
                            /*
                            if (osuClient.IsAudioPlaying() == 0)
                            {
                                continueRunning = true;
                                Thread.Sleep(1500);
                                break;
                            }
                            */
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
                else if (!osuClient.IsAudioPlaying())
                {
                    if (keyPressed == KeyPressed.Key1)
                        inputSimulator.Keyboard.KeyUp(keyCode1);
                    else if (keyPressed == KeyPressed.Key2)
                        inputSimulator.Keyboard.KeyUp(keyCode2);
                }
            }

            if (continueRunning)
                Update();
        }

        private void AutoPilotCircle(HitObject currHitObject, ref float velX, ref float velY)
        {
            if (currHitObject == null || IsCursorOnCircle(currHitObject))
                return;
            float xDiff = cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]);
            float yDiff = cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]);
            float circlePxSize = (float)(54.4 - 4.48 * beatmap.CircleSize);
            Func<float, float> applyAutoCorrect = new Func<float, float>((f) =>
            {
                float dist = Math.Abs(f) - circlePxSize / 1.2f;
                if (dist >= 40)
                    return 3.7f; // 3.7
                if (dist >= 25)
                    return 1.8f;
                if (dist >= 10)
                    return 0.45f;
                if (dist >= 5)
                    return 0.18f;
                if (dist >= 3)
                    return 0.05f;
                if (dist >= 1)
                    return 0.04f;
                return 0;
            });
            if (xDiff == 0 || (velX > 0 && xDiff >= 0) || (velX < 0 && xDiff <= 0))
                velX = -xDiff * applyAutoCorrect(xDiff) * (float)Math.Pow(resConstants[0], 0.825);

            if (yDiff == 0 || (velY > 0 && yDiff >= 0) || (velY < 0 && yDiff <= 0))
                velY = -yDiff * applyAutoCorrect(yDiff) * (float)Math.Pow(resConstants[1], 0.825);
            
            if (velX != 0.0f)
                velX = Math.Min(Math.Abs(velX), 1.25f * Math.Abs(cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]))) * Math.Sign(velX);

            if (velY != 0.0f)
                velY = Math.Min(Math.Abs(velY), 1.25f * Math.Abs(cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]))) * Math.Sign(velY);
        }

        private void AutoPilotSpinner(ref float velX, ref float velY)
        {
            GetCursorPos(out cursorPos);
            float dist = (float)Math.Sqrt(Math.Pow(cursorPos.X - center.X, 2) + Math.Pow(cursorPos.Y - center.Y, 2));
            if (ellipseAngle == -1)
                ellipseAngle = Math.Atan2(cursorPos.Y - center.Y, cursorPos.X - center.X);
            else
                ellipseAngle += ANGLE_INCREMENT;

            if (ellipseAngle > TWO_PI)
                ellipseAngle = ellipseAngle % TWO_PI;

            float x = (center.X + (float)(dist * Math.Cos(ellipseAngle))) * 65535 / 1920;
            float y = (center.Y + (float)(dist * Math.Sin(ellipseAngle))) * 65535 / 1080;
            if (dist >= 100)
            {
                x -= (float)(50 * Math.Cos(ellipseAngle));
                y -= (float)(50 * Math.Sin(ellipseAngle));
            }
            Mouse_Event(0x1 | 0x8000, (int)x, (int)y, 0, 0);
        }

        private void AutoPilotLinearSlider(HitObjectSlider currHitObject, ref float velX, ref float velY)
        {
            float angle = (float)Math.Atan2(currHitObject.Points[0].Y - currHitObject.Y, currHitObject.Points[0].X - currHitObject.X);
            float duration = (float)CalculateSliderDuration(currHitObject) / currHitObject.RepeatCount;
            float timeDiff = (currentTime - currHitObject.Time) % duration;
            int repeatNumber = (int)((currentTime - currHitObject.Time) / duration);
            if (repeatNumber % 2 == 1)
                timeDiff = duration - timeDiff;
            float expectedX = (float)(currHitObject.Length * Math.Cos(angle) * timeDiff / duration) * resConstants[0] + cursorPos2.X;
            float expectedY = (float)(currHitObject.Length * Math.Sin(angle) * timeDiff / duration) * resConstants[1] + cursorPos2.Y;

            GetCursorPos(out cursorPos);
            velX = expectedX - cursorPos.X;
            velY = expectedY - cursorPos.Y;

            /*
            if (currHitObject.RepeatCount > 1)
            {
                //int repeatNumber = (int)((currentTime - currHitObject.Time) / duration);
                if (repeatNumber % 2 == 1)
                {
                    velX *= -1;
                    velY *= -1;
                }
            }
            */
        }

        private void AutoPilotPerfectSlider(HitObjectSlider currHitObject, ref float velX, ref float velY)
        {
            Vec2Float midpt1 = new Vec2Float((currHitObject.X + currHitObject.Points[0].X) / 2f, (currHitObject.Y + currHitObject.Points[0].Y) / 2f);
            Vec2Float midpt2 = new Vec2Float((currHitObject.Points[0].X + currHitObject.Points[1].X) / 2f, (currHitObject.Points[0].Y + currHitObject.Points[1].Y) / 2f);
            Vec2Float norml1 = new Vec2Float(currHitObject.Points[0].X - currHitObject.X, currHitObject.Points[0].Y - currHitObject.Y).Normal();
            Vec2Float norml2 = new Vec2Float(currHitObject.Points[1].X - currHitObject.Points[0].X, currHitObject.Points[1].Y - currHitObject.Points[0].Y).Normal();
            Vec2Float center = Vec2Float.Intersect(midpt1, norml1, midpt2, norml2);

            float startAngle = (float)Math.Atan2(currHitObject.Y - center.Y, currHitObject.X - center.X);
            float midAngle = (float)Math.Atan2(currHitObject.Points[0].Y - center.Y, currHitObject.Points[0].X - center.X);
            float endAngle = (float)Math.Atan2(currHitObject.Points[1].Y - center.Y, currHitObject.Points[1].X - center.X);

            Func<float, float, float, bool> isInside = (a, b, c) => (b > a && b < c) || (b < a && b > c);
            if (!isInside(startAngle, midAngle, endAngle))
            {
                if (Math.Abs(startAngle + TWO_PI - endAngle) < TWO_PI && isInside(startAngle + TWO_PI, midAngle, endAngle))
                    startAngle += TWO_PI;
                else if (Math.Abs(startAngle - (endAngle + TWO_PI)) < TWO_PI && isInside(startAngle, midAngle, endAngle + TWO_PI))
                    endAngle += TWO_PI;
                else if (Math.Abs(startAngle - TWO_PI - endAngle) < TWO_PI && isInside(startAngle - TWO_PI, midAngle, endAngle))
                    startAngle -= TWO_PI;
                else if (Math.Abs(startAngle - (endAngle - TWO_PI)) < TWO_PI && isInside(startAngle, midAngle, endAngle - TWO_PI))
                    endAngle -= TWO_PI;
            }

            float radius = (float)Math.Sqrt(Math.Pow(currHitObject.X - center.X, 2) + Math.Pow(currHitObject.Y - center.Y, 2));
            float arcAngle = (float)currHitObject.Length / radius;
            endAngle = endAngle > startAngle ? startAngle + arcAngle : startAngle - arcAngle;

            Console.WriteLine($"start={startAngle * 180 / Math.PI}, end={endAngle * 180 / Math.PI}");

            float duration = (float)CalculateSliderDuration(currHitObject) / currHitObject.RepeatCount;
            float timeDiff = (currentTime - currHitObject.Time) % duration;
            int repeatNumber = (int)((currentTime - currHitObject.Time) / duration);
            if (repeatNumber % 2 == 1)
                timeDiff = duration - timeDiff;

            float currAngle = startAngle + (endAngle - startAngle) * timeDiff / duration;
            float expectedX = (float)(center.X - currHitObject.X + radius * Math.Cos(currAngle)) * resConstants[0] + cursorPos2.X;
            float expectedY = (float)(center.Y - currHitObject.Y + radius * Math.Sin(currAngle)) * resConstants[1] + cursorPos2.Y;
            GetCursorPos(out cursorPos);
            velX = expectedX - cursorPos.X;
            velY = expectedY - cursorPos.Y;
        }

        private void AutoPilotSlider(HitObject currHitObject, ref float velX, ref float velY)
        {
            if (currentTime < currHitObject.Time - 3)
            {
                AutoPilotCircle(currHitObject, ref velX, ref velY);
                cursorPos2 = cursorPos;
            }
            else
            {
                HitObjectSlider currSlider = currHitObject as HitObjectSlider;
                switch (currSlider.CurveType)
                {
                    case osu_database_reader.CurveType.Linear:
                        AutoPilotLinearSlider(currSlider, ref velX, ref velY);
                        break;
                    case osu_database_reader.CurveType.Perfect:
                        AutoPilotPerfectSlider(currSlider, ref velX, ref velY);
                        break;
                    case osu_database_reader.CurveType.Catmull:
                    case osu_database_reader.CurveType.Bezier:
                        AutoPilotPerfectSlider(currSlider, ref velX, ref velY);
                        break;
                }
            }
        }

        private void AutoPilot(HitObject currHitObject, float velX, float velY)
        {
            switch (currHitObject.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Normal:
                    AutoPilotCircle(currHitObject, ref velX, ref velY);
                    break;
                case HitObjectType.Slider:
                    AutoPilotSlider(currHitObject, ref velX, ref velY);
                    break;
                case HitObjectType.Spinner:
                    if (currentTime >= currHitObject.Time - 50)
                        AutoPilotSpinner(ref velX, ref velY);
                    break;
            }

            missingX += velX - (int)velX;
            missingY += velY - (int)velY;

            if (Math.Abs(missingX) >= 1)
            {
                int deltaX = (int)missingX;
                velX += deltaX;
                missingX -= deltaX;
            }

            if (Math.Abs(missingY) >= 1)
            {
                int deltaY = (int)missingY;
                velY += deltaY;
                missingY -= deltaY;
            }

            Mouse_Event(0x1, (int)velX, (int)velY, 0, 0);
        }

        private void Relax(HitObject currHitObject, ref bool shouldPressSecondary, ref int nextHitObjIndex)
        {
            shouldPressSecondary = GetTimeDiffFromNextObj(currHitObject) < 116 ? !shouldPressSecondary : false;
            keyPressed = shouldPressSecondary ? KeyPressed.Key2 : KeyPressed.Key1;
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            //Thread.Sleep(2);
            bool pressedSecondary = shouldPressSecondary;
            Task.Delay(GetHitObjectEndTime(currHitObject) - currHitObject.Time + 16).ContinueWith(ant =>
            {
                inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1);
                if ((pressedSecondary && keyPressed == KeyPressed.Key2) || (!pressedSecondary && keyPressed == KeyPressed.Key1))
                    keyPressed = KeyPressed.None;
            });
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
                    return startTime + CalculateSliderDuration(hitObj as HitObjectSlider);
                case HitObjectType.Spinner:
                    return (hitObj as HitObjectSpinner).EndTime;
                default:
                    return startTime;
            }
        }

        private float[] CalculatePlayfieldResolution()
        {
            Osu.RECT wResolution = osuClient.GetWindowResolution();
            Osu.RECT cResolution = osuClient.GetClientResolution();
            // TODO calculate border width and height for them borderless/fullscreen users rather than assume bordered window on Windows 10
            //int resY = wResolution.Bottom - wResolution.Top - 29 - 6;
            Console.WriteLine("Left: {0} x Right: {1} x Top: {2} x Bottom: {3}", wResolution.Left, wResolution.Right, wResolution.Top, wResolution.Bottom);
            Console.WriteLine("{0} x {1}", wResolution.Right - wResolution.Left - 6, wResolution.Bottom - wResolution.Top - 29 - 6);
            float borderThickness = (wResolution.Right - wResolution.Left - cResolution.Right) / 2;
            float titlebarHeight = wResolution.Bottom - wResolution.Top - cResolution.Bottom - 2 * borderThickness;
            float playfieldY = 0.8f * cResolution.Bottom;
            float playfieldX = playfieldY * 4 / 3;
            Console.WriteLine("CALCULATED PLAYFIELD: {0} x {1}", playfieldX, playfieldY);

            float playfieldOffsetX = (cResolution.Right - playfieldX) / 2 + borderThickness / 2 + 1;
            float playfieldOffsetY = (cResolution.Bottom - 0.95385f * playfieldY) / 2 + titlebarHeight + borderThickness / 2; // 0.95385
            Console.WriteLine("CALCULATED OFFSETS: {0} x {1}", playfieldOffsetX, playfieldOffsetY);

            float ratioX = playfieldX / 512;
            float ratioY = playfieldY / 384;
            Console.WriteLine($"CALCULATED RATIOS: {ratioX} x {ratioY}");

            float totalOffsetX = wResolution.Left + playfieldOffsetX;
            float totalOffsetY = wResolution.Top + playfieldOffsetY;
            return new float[4] { ratioX, ratioY, totalOffsetX, totalOffsetY };
        }

        private void GetVelocities(HitObject currHitObject, HitObject lastHitObject, ref float velX, ref float velY)
        {
            ellipseAngle = -1;
            float xDiff, yDiff;
            switch (lastHitObject.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Slider:
                case HitObjectType.Spinner:
                    GetCursorPos(out cursorPos);
                    xDiff = currHitObject.X * resConstants[0] + resConstants[2] - cursorPos.X;
                    yDiff = currHitObject.Y * resConstants[1] + resConstants[3] - cursorPos.Y;
                    break;
                default:
                    xDiff = (currHitObject.X - lastHitObject.X) * resConstants[0];
                    yDiff = (currHitObject.Y - lastHitObject.Y) * resConstants[1];
                    break;
            }
            velX = xDiff / (currHitObject.Time - currentTime);
            velY = yDiff / (currHitObject.Time - currentTime);
            Func<float, float> applyVelocityFactor = new Func<float, float>(i =>
            {
                if (Math.Abs(i) >= 250)
                    return 9.8f; // 11.8 // 8.28
                if (Math.Abs(i) >= 160)
                    return 8.6f; // 9.6
                return 7.1f; // 8.2
            });
            float dist = (float)Math.Sqrt(Math.Pow(xDiff, 2) + Math.Pow(yDiff, 2));
            velX *= applyVelocityFactor(dist);
            velY *= applyVelocityFactor(dist);
        }

        private POINT CalculateCenter()
        {
            Osu.RECT resolution = this.osuClient.GetWindowResolution();
            float xOffset = (resolution.Right - resolution.Left) / 2f;
            float yOffset = (resolution.Bottom - resolution.Top) / 2f;
            center.X = (int)(resolution.Left + xOffset);
            center.Y = (int)(resolution.Top + yOffset + 29);
            Console.WriteLine("CALCULATED CENTER: {0} x {1}", center.X, center.Y);
            return center;
        }

        private bool IsCursorOnCircle(HitObject currHitObject)
        {
            GetCursorPos(out cursorPos);
            return (float)Math.Sqrt(Math.Pow(cursorPos.X - (currHitObject.X * resConstants[0] + resConstants[2]), 2) + Math.Pow(cursorPos.Y - (currHitObject.Y * resConstants[1] + resConstants[3]), 2))
                <= circlePxSize / 2;
        }

        public void ToggleAutoPilot() => autopilotRunning = !autopilotRunning;
        public void ToggleRelax() => relaxRunning = !relaxRunning;
        public char GetKey1() => key1;
        public char GetKey2() => key2;
        public void SetKey1(char key) { this.keyCode1 = (WindowsInput.Native.VirtualKeyCode)key; this.key1 = key; }
        public void SetKey2(char key) { this.keyCode2 = (WindowsInput.Native.VirtualKeyCode)key; this.key2 = key; }
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
        private double ellipseAngle = 0;
        private double[] nextTimings = new double[2];
        private InputSimulator inputSimulator = new InputSimulator();
        private CurrentBeatmap beatmap;
        private POINT cursorPos, cursorPos2 = new POINT(-1, -1);
        private POINT center;
        private Osu osuClient;
        private float missingX, missingY;
        private float circlePxSize;
        private float[] resConstants;
        private int currentTime;
        private KeyPressed keyPressed = KeyPressed.None;

        private const double ANGLE_INCREMENT = Math.PI / 18;
        private const float HALF_PI = (float)Math.PI / 2;
        private const float TWO_PI = 2 * (float)Math.PI;
    }
}
