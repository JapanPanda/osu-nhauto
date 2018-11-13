using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using osu.Shared;
using osu_database_reader;
using osu_database_reader.Components.Beatmaps;
using osu_nhauto.HitObjects;

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

        private struct FPOINT
        {
            public float X;
            public float Y;

            public FPOINT(float x, float y)
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
            BeatmapUtils.InitializeBeatmap(beatmap);
            currStep = 0;
            bool continueRunning = false;
            int nextHitObjIndex = 0;
            HitObject lastHitObject = null;
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            bool shouldPressSecondary = false;
            int lastTime = osuClient.GetAudioTime();
            float velX = 0, velY = 0;
            ResolutionUtils.CalculatePlayfieldResolution();
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                currentTime = osuClient.GetAudioTime() + 6;
                if (currentTime > lastTime)
                {
                    lastTime = currentTime;
                    if (currHitObject != null)
                    {
                        if (nextHitObjIndex == 0 && currentTime < currHitObject.Time && (currHitObject.Type & (HitObjectType)0b1000_1011) != HitObjectType.Spinner)
                        {
                            int x = (int)ResolutionUtils.ConvertToScreenXCoord(currHitObject.X);
                            int y = (int)ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y);
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

                            if (currentTime >= currHitObject.EndTime + 3)
                            {
                                currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                                currStep = 0;
                                if (currHitObject != null)
                                    GetVelocities(currHitObject, lastHitObject, ref velX, ref velY);
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
            if (currHitObject == null)
                return;

            if (IsCursorOnCircle(currHitObject))
            {
                velX *= 0.01f;
                velY *= 0.01f;
                return;
            }

            float xDiff = cursorPos.X - ResolutionUtils.ConvertToScreenXCoord(currHitObject.X);
            float yDiff = cursorPos.Y - ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y);
            Func<float, float> applyAutoCorrect = new Func<float, float>((f) =>
            {
                float dist = Math.Abs(f) - BeatmapUtils.CirclePxRadius;
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
            if (velX * xDiff >= 0)
                velX = -xDiff * applyAutoCorrect(xDiff) * (float)Math.Pow(ResolutionUtils.Ratio.X, 0.825);

            if (velY * yDiff >= 0)
                velY = -yDiff * applyAutoCorrect(yDiff) * (float)Math.Pow(ResolutionUtils.Ratio.Y, 0.825);
            
            if (velX != 0.0f)
                velX = Math.Min(Math.Abs(velX), 1.25f * Math.Abs(cursorPos.X - ResolutionUtils.ConvertToScreenXCoord(currHitObject.X))) * Math.Sign(velX);

            if (velY != 0.0f)
                velY = Math.Min(Math.Abs(velY), 1.25f * Math.Abs(cursorPos.Y - ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y))) * Math.Sign(velY);
        }

        private void AutoPilotSpinner(ref float velX, ref float velY)
        {
            GetCursorPos(out cursorPos);
            Vec2Float center = ResolutionUtils.CenterPos;
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

        //FPOINT prevPoint;
        bool test = false;
        bool test2 = false;
        float currStep = 0;
        FPOINT currBezPoint;
        FPOINT prevBezPoint;
        int prevTime;
        private void AutoPilotBezierSlider(HitObjectSlider currHitObject, ref float velX, ref float velY)
        {
            // Calculation of points in bezier slider
            if (!test)
            {
                for (float i = 0; i <= 1; i += 0.01f)
                {
                    FPOINT test = GetBezierPoint(currHitObject, i);
                    Console.WriteLine($"{test.X} x {test.Y}");

                }
                test = true;
            }
            if (test2)
                return;
            float timeDiff;
            float duration = (float)BeatmapUtils.CalculateSliderDuration(currHitObject) / currHitObject.RepeatCount;
            if (currStep == 0)
            {
                prevBezPoint = GetBezierPoint(currHitObject, currStep);
                currStep += 0.01f;
                currBezPoint = GetBezierPoint(currHitObject, currStep);
                Console.WriteLine($"Initialize: {currStep}: {currBezPoint.X} x {currBezPoint.Y} || {prevBezPoint.X} x {prevBezPoint.Y}");
                timeDiff = (0.01f * duration) % duration;
                prevTime = currentTime;
            }
            else if (cursorPos.X >= ResolutionUtils.ConvertToScreenXCoord(currBezPoint.X) - 20 && (cursorPos.X <= ResolutionUtils.ConvertToScreenXCoord(currBezPoint.X) + 20)
                && cursorPos.Y >= ResolutionUtils.ConvertToScreenYCoord(currBezPoint.Y) - 20 && cursorPos.Y <= ResolutionUtils.ConvertToScreenYCoord(currBezPoint.Y) + 20)
            {
                currStep += 0.01f;
                prevBezPoint = currBezPoint;
                currBezPoint = GetBezierPoint(currHitObject, currStep);
                cursorPos2.X = cursorPos.X;
                cursorPos2.Y = cursorPos.Y;
                GetCursorPos(out cursorPos);
                Console.WriteLine($"New Bezier Point: {currStep}: {currBezPoint.X} x {currBezPoint.Y}");
                Console.WriteLine($"NEW!!! {cursorPos.X} x {cursorPos.Y} || {cursorPos2.X} x {cursorPos2.Y} || {ResolutionUtils.ConvertToScreenXCoord(currBezPoint.X)} x {ResolutionUtils.ConvertToScreenYCoord(currBezPoint.Y)}");
                timeDiff = (currentTime - prevTime) % duration;
                prevTime = currentTime;
            }
            else
            {
                if (currStep > 1)
                {
                    return;
                }
                timeDiff = (currentTime - prevTime) % duration;
                Console.WriteLine($"{cursorPos.X} x {cursorPos.Y} || {ResolutionUtils.ConvertToScreenXCoord(currBezPoint.X)} x {ResolutionUtils.ConvertToScreenYCoord(currBezPoint.Y)}");
                //Console.WriteLine($"Distance: {currBezPoint.X} x {currBezPoint.Y} || {prevBezPoint.X} x {prevBezPoint.Y} = {distance}");
                
            }

            float angle = (float)Math.Atan2(currBezPoint.Y - prevBezPoint.Y, currBezPoint.X - prevBezPoint.X);
            float distance = (float)Math.Sqrt(Math.Pow(currBezPoint.Y - prevBezPoint.Y, 2) + Math.Pow(currBezPoint.X - prevBezPoint.X, 2));

            float expectedX = (float)(distance * Math.Cos(angle) * timeDiff / (0.01f * duration)) * ResolutionUtils.Ratio.X + cursorPos2.X;
            float expectedY = (float)(distance * Math.Sin(angle) * timeDiff / (0.01f * duration)) * ResolutionUtils.Ratio.Y + cursorPos2.Y;

            GetCursorPos(out cursorPos);
            velX = expectedX - cursorPos.X;
            velY = expectedY - cursorPos.Y;
            Console.WriteLine($"Velocity: {velX} x {velY}");
            Console.WriteLine($"Expected: {expectedX} x {expectedY}");

        }

        private FPOINT GetBezierPoint(HitObjectSlider currHitObject, float step)
        {
            FPOINT point = new FPOINT();
            int points = currHitObject.Points.Count;
            //point.X = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points - 0) * Math.Pow(step, 0) * currHitObject.X);
            //point.Y = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points - 0) * Math.Pow(step, 0) * currHitObject.Y);
            //for (int i = 0; i <= points; i++)
            //{
            //    point.X += (float)(GetBinomialCoefficient(points, i + 1) * Math.Pow(1 - step, points - i - 1) * Math.Pow(step, i - 1) * currHitObject.Points[i].X);
            //    point.Y += (float)(GetBinomialCoefficient(points, i + 1) * Math.Pow(1 - step, points - i - 1) * Math.Pow(step, i - 1) * currHitObject.Points[i].Y);
            //}
            point.X = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points - 0) * Math.Pow(step, 0) * currHitObject.X);
            point.Y = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points - 0) * Math.Pow(step, 0) * currHitObject.Y);
            for (int i = 0; i <= points - 1; i++)
            {
                point.X += (float)(GetBinomialCoefficient(points, i + 1) * Math.Pow(1 - step, points - i - 1) * Math.Pow(step, i + 1) * currHitObject.Points[i].X);
                point.Y += (float)(GetBinomialCoefficient(points, i + 1) * Math.Pow(1 - step, points - i - 1) * Math.Pow(step, i + 1) * currHitObject.Points[i].Y);
                
            }
            //point.X = (1 - step) * (1 - step) * (1 - step) * currHitObject.Points[0].X + 3 * (1 - step) * (1 - step) * step * currHitObject.Points[1].X + 3 * (1 - step) * step * step * currHitObject.Points[2].X + step * step * step * currHitObject.Points[3].X;
            return point;
        }

        private int GetBinomialCoefficient(int n, int k)
        {
            int res = 1;

            if (k > n - k)
                k = n - k;

            for (int i = 0; i < k; ++i)
            {
                res *= (n - i);
                res /= (i + 1);
            }
            return res;
        }


        private void AutoPilotSlider(HitObject currHitObject, ref float velX, ref float velY)
        {
            if (currentTime < currHitObject.Time)
            {
                AutoPilotCircle(currHitObject, ref velX, ref velY);
                cursorPos2 = cursorPos;
            }
            else
            {
                HitObjectSlider currSlider = currHitObject as HitObjectSlider;
                Vec2Float pos = currSlider.GetOffset(currentTime);
                GetCursorPos(out cursorPos);
                velX = pos.X * ResolutionUtils.Ratio.X + cursorPos2.X - cursorPos.X;
                velY = pos.Y * ResolutionUtils.Ratio.Y + cursorPos2.Y - cursorPos.Y;
                /*
                switch (currSlider.Curve)
                {
                    case CurveType.Linear:
                        AutoPilotLinearSlider(currSlider, ref velX, ref velY);
                        break;
                    case CurveType.Perfect:
                        AutoPilotPerfectSlider(currSlider, ref velX, ref velY);
                        break;
                    case CurveType.Catmull:
                    case CurveType.Bezier:
                        AutoPilotBezierSlider(currSlider, ref velX, ref velY);
                        break;
                }
                */
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
            shouldPressSecondary = BeatmapUtils.GetTimeDiffFromNextObj(currHitObject) < 116 ? !shouldPressSecondary : false;
            keyPressed = shouldPressSecondary ? KeyPressed.Key2 : KeyPressed.Key1;
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            //Thread.Sleep(2);
            bool pressedSecondary = shouldPressSecondary;
            Task.Delay(currHitObject.EndTime - currHitObject.Time + 16).ContinueWith(ant =>
            {
                inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1);
                if ((pressedSecondary && keyPressed == KeyPressed.Key2) || (!pressedSecondary && keyPressed == KeyPressed.Key1))
                    keyPressed = KeyPressed.None;
            });
        }

        private void GetVelocities(HitObject currHitObject, HitObject lastHitObject, ref float velX, ref float velY)
        {
            if ((currHitObject.Type & (HitObjectType)0b1000_1011) == HitObjectType.Spinner)
            {
                velX = 0;
                velY = 0;
                return;
            }

            ellipseAngle = -1;
            float xDiff, yDiff;
            switch (lastHitObject.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Normal:
                case HitObjectType.Slider:
                case HitObjectType.Spinner:
                    GetCursorPos(out cursorPos);
                    xDiff = ResolutionUtils.ConvertToScreenXCoord(currHitObject.X) - cursorPos.X;
                    yDiff = ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y) - cursorPos.Y;
                    break;
                default:
                    xDiff = (currHitObject.X - lastHitObject.X) * ResolutionUtils.Ratio.X;
                    yDiff = (currHitObject.Y - lastHitObject.Y) * ResolutionUtils.Ratio.Y;
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

        private bool IsCursorOnCircle(HitObject currHitObject)
        {
            GetCursorPos(out cursorPos);
            return (float)Math.Sqrt(Math.Pow(cursorPos.X - ResolutionUtils.ConvertToScreenXCoord(currHitObject.X), 2) + Math.Pow(cursorPos.Y - ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y), 2))
                <= BeatmapUtils.CirclePxRadius / 5;
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
        private double ellipseAngle = 0;
        private InputSimulator inputSimulator = new InputSimulator();
        private CurrentBeatmap beatmap;
        private POINT cursorPos, cursorPos2 = new POINT(-1, -1);
        private Osu osuClient;
        private float missingX, missingY;
        private int currentTime;
        private KeyPressed keyPressed = KeyPressed.None;

        private const double ANGLE_INCREMENT = Math.PI / 18;
        private const float TWO_PI = 2 * (float)Math.PI;
    }
}
