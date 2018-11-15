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
        [DllImport("user32.dll", EntryPoint = "mouse_event", CallingConvention = CallingConvention.Winapi)]
        internal static extern void Mouse_Event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

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


        public Player()
        {
            keyCode1 = (WindowsInput.Native.VirtualKeyCode)key1;
            keyCode2 = (WindowsInput.Native.VirtualKeyCode)key2;
        }


        public void Update()
        {
            BeatmapUtils.InitializeBeatmap(beatmap);
            bool continueRunning = false;
            int nextHitObjIndex = 0;
            HitObject lastHitObject = null;
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            bool shouldPressSecondary = false;
            int lastTime = MainWindow.osu.GetAudioTime();
            ResolutionUtils.CalculatePlayfieldResolution();
            speedMod = GetSpeedModifier();
            timeDiffThreshold = 116 * speedMod;
            velocity.Zero();
            missing.Zero();
            Console.WriteLine("Now listening for time changes");
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                currentTime = MainWindow.osu.GetAudioTime() + 6;
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
                            AutoPilot(currHitObject);

                        if (currHitObject.Time - currentTime <= 0)
                        {
                            if (currHitObject != lastHitObject)
                            {
                                Relax(currHitObject, lastHitObject, ref shouldPressSecondary);
                                lastHitObject = currHitObject;
                            }

                            if (currentTime >= currHitObject.EndTime + 3)
                            {
                                Osu.SCORE_DATA? scoreData = MainWindow.osu.GetScoreData();
                                if (scoreData != null)
                                {
                                    Osu.SCORE_DATA fscoreData = scoreData.Value;
                                    Console.WriteLine($"300s: {fscoreData.score_300} 100s: {fscoreData.score_100} 50s: {fscoreData.score_50} 0s: {fscoreData.score_0} Score: {fscoreData.current_score} Combo: {fscoreData.current_combo}");
                                }
                                currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                                if (currHitObject != null)
                                    GetVelocities(currHitObject, lastHitObject);
                            }
                        }
                    }
                    else
                        return;

                    Thread.Sleep(1);
                }
                else if (currentTime < lastTime - 3)
                {
                    Console.WriteLine($"Detected possible reset: curr={currentTime} last={lastTime}");
                    continueRunning = true;
                    break;
                }
                else if (!MainWindow.osu.IsAudioPlaying())
                {
                    if (keyPressed == KeyPressed.Key1)
                        inputSimulator.Keyboard.KeyUp(keyCode1);
                    if (keyPressed == KeyPressed.Key2)
                        inputSimulator.Keyboard.KeyUp(keyCode2);

                    keyPressed = KeyPressed.None;
                }
            }
            if (continueRunning)
                Update();
        }

        private void AutoPilotCircle(HitObject currHitObject)
        {
            if (currHitObject == null)
                return;

            if (IsCursorOnCircle(currHitObject))
            {
                velocity.X *= 0.33f;
                velocity.Y *= 0.33f;
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
            if (velocity.X * xDiff >= 0)
                velocity.X = -xDiff * applyAutoCorrect(xDiff) * (float)Math.Pow(ResolutionUtils.Ratio.X, 0.825);

            if (velocity.Y * yDiff >= 0)
                velocity.Y = -yDiff * applyAutoCorrect(yDiff) * (float)Math.Pow(ResolutionUtils.Ratio.Y, 0.825);
            
            if (velocity.X != 0.0f)
                velocity.X = Math.Min(Math.Abs(velocity.X), 1.25f * Math.Abs(cursorPos.X - ResolutionUtils.ConvertToScreenXCoord(currHitObject.X))) * Math.Sign(velocity.X);

            if (velocity.Y != 0.0f)
                velocity.Y = Math.Min(Math.Abs(velocity.Y), 1.25f * Math.Abs(cursorPos.Y - ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y))) * Math.Sign(velocity.Y);
        }

        private void AutoPilotSpinner()
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
            if (dist != 100)
            {
                int sign = Math.Sign(100 - dist);
                x += (float)(50 * Math.Cos(ellipseAngle)) * sign;
                y += (float)(50 * Math.Sin(ellipseAngle)) * sign;
            }
            Mouse_Event(0x1 | 0x8000, (int)x, (int)y, 0, 0);
        }
        
        private void AutoPilotSlider(HitObject currHitObject)
        {
            if (currentTime < currHitObject.Time)
            {
                AutoPilotCircle(currHitObject);
                cursorPos2 = cursorPos;
            }
            else
            {
                Vec2Float pos = (currHitObject as HitObjectSlider).GetOffset(currentTime);
                GetCursorPos(out cursorPos);
                velocity.X = pos.X * ResolutionUtils.Ratio.X + cursorPos2.X - cursorPos.X;
                velocity.Y = pos.Y * ResolutionUtils.Ratio.Y + cursorPos2.Y - cursorPos.Y;
            }
        }

        private void AutoPilot(HitObject currHitObject)
        {
            switch (currHitObject.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Normal:
                    AutoPilotCircle(currHitObject);
                    break;
                case HitObjectType.Slider:
                    AutoPilotSlider(currHitObject);
                    break;
                case HitObjectType.Spinner:
                    velocity.Zero();
                    if (currentTime >= currHitObject.Time - 50)
                        AutoPilotSpinner();
                    return;
            }

            missing.Add(velocity.X - (int)velocity.X, velocity.Y - (int)velocity.Y);
            osu_database_reader.Components.Vector2 delta = new osu_database_reader.Components.Vector2(0, 0);
            if (Math.Abs(missing.X) >= 1)
            {
                delta.X = (int)missing.X;
                velocity.X += delta.X;
                missing.X -= delta.X;
            }

            if (Math.Abs(missing.Y) >= 1)
            {
                delta.Y = (int)missing.Y;
                velocity.Y += delta.Y;
                missing.Y -= delta.Y;
            }

            Mouse_Event(0x1, (int)velocity.X, (int)velocity.Y, 0, 0);

            velocity.Subtract(delta.X, delta.Y);
        }

        private void Relax(HitObject currHitObject, HitObject lastHitObject, ref bool shouldPressSecondary)
        {
            shouldPressSecondary = lastHitObject != null && currHitObject.Time - lastHitObject.EndTime < timeDiffThreshold ? !shouldPressSecondary : false;
            keyPressed = shouldPressSecondary ? KeyPressed.Key2 : KeyPressed.Key1;
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            //Thread.Sleep(2);
            bool pressedSecondary = shouldPressSecondary;
            Task.Delay((int)((currHitObject.EndTime - currHitObject.Time) / speedMod) + 16).ContinueWith(ant =>
            {
                inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1);
                if ((pressedSecondary && keyPressed == KeyPressed.Key2) || (!pressedSecondary && keyPressed == KeyPressed.Key1))
                    keyPressed = KeyPressed.None;
            });
        }

        private void GetVelocities(HitObject currHitObject, HitObject lastHitObject)
        {
            if ((currHitObject.Type & (HitObjectType)0b1000_1011) == HitObjectType.Spinner)
            {
                velocity.Zero();
                return;
            }

            ellipseAngle = -1;
            GetCursorPos(out cursorPos);
            float xDiff = ResolutionUtils.ConvertToScreenXCoord(currHitObject.X) - cursorPos.X;
            float yDiff = ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y) - cursorPos.Y;
            velocity.X = xDiff / (currHitObject.Time - currentTime);
            velocity.Y = yDiff / (currHitObject.Time - currentTime);
            //float dist = (float)Math.Sqrt(Math.Pow(xDiff, 2) + Math.Pow(yDiff, 2));
            velocity.Multiply(4f * ResolutionUtils.Ratio.X); // 7.1
            float dist = velocity.Distance(0, 0);
            /*
            if (dist >= 250)
                velFactor = 9.8f;
            else if (velFactor >= 160)
                velFactor = 8.6f;
                */
            velocity.Multiply(Math.Max(0.8f, dist / 9.25f));
            velocity.Multiply(speedMod);
        }

        private bool IsCursorOnCircle(HitObject currHitObject)
        {
            GetCursorPos(out cursorPos);
            return (float)Math.Sqrt(Math.Pow(cursorPos.X - ResolutionUtils.ConvertToScreenXCoord(currHitObject.X), 2) + Math.Pow(cursorPos.Y - ResolutionUtils.ConvertToScreenYCoord(currHitObject.Y), 2))
                <= BeatmapUtils.CirclePxRadius / 3.75f;
        }

        private float GetSpeedModifier()
        {
            if (!beatmap.ModValue.HasValue)
                return 1;
            if ((beatmap.ModValue.Value & (int)(Mods.DoubleTime | Mods.Nightcore)) > 0)
            {
                Console.WriteLine("Detected DoubleTime");
                return 1.5f;
            }
            if ((beatmap.ModValue.Value & (int)Mods.HalfTime) > 0)
            {
                Console.WriteLine("Detected HalfTime");
                return 0.75f;
            }
            return 1;
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
        private Vec2Float velocity, missing;
        private float speedMod = 1;
        private float timeDiffThreshold;
        private int currentTime;
        private KeyPressed keyPressed = KeyPressed.None;

        private const double ANGLE_INCREMENT = Math.PI / 18;
        private const float TWO_PI = 2 * (float)Math.PI;
    }
}
