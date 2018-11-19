using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsInput;
using WindowsInput.Native;
using osu.Shared;
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

        public Player()
        {
            if (MainWindow.osu.GetProcess() != null)
            {
                string[] cfgFileArr = Directory.GetFiles(MainWindow.fileParser.GetBaseFilePath(), "osu!.*.cfg");
                if (cfgFileArr.Length == 0)
                    return;

                string regex = "[A-Z]|D[0-9]";
                string key1 = null, key2 = null;
                using (var sr = new StreamReader(File.OpenRead(cfgFileArr[0])))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("keyOsuLeft"))
                            key1 = line.Split('=')[1].ToUpper().Substring(1);
                        else if (line.StartsWith("keyOsuRight"))
                            key2 = line.Split('=')[1].ToUpper().Substring(1);
                    }
                }
                if (key1 != null && System.Text.RegularExpressions.Regex.IsMatch(key1, regex))
                    SetKey1(key1.ToCharArray()[0]);
                if (key2 != null && System.Text.RegularExpressions.Regex.IsMatch(key2, regex))
                    SetKey2(key2.ToCharArray()[0]);
            }
        }

        public void Initialize()
        {
            try
            {
                Update();
            }
            catch (ThreadAbortException)
            {
                PrintScoreData();
            }
        }

        public void Update()
        {
            BeatmapUtils.InitializeBeatmap(beatmap);
            bool continueRunning = false;
            int nextHitObjIndex = 0;
            HitObject lastHitObject = null;
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            bool shouldPressSecondary = false, initialVelocity = false;
            ResolutionUtils.CalculatePlayfieldResolution();
            speedMod = GetSpeedModifier();
            timeDiffThreshold = 116 * speedMod;
            velocity.Zero();
            missing.Zero();
            while (MainWindow.osu.GetAudioTime() == 0) { Thread.Sleep(1); }
            while (MainWindow.osu.GetAudioTime() <= currHitObject.Time - BeatmapUtils.TimeFadeIn / 2) { Thread.Sleep(1); }
            Console.WriteLine("Now listening for time changes");
            int lastTime = MainWindow.osu.GetAudioTime();
            scoreData = null;
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                currentTime = MainWindow.osu.GetAudioTime() + 6;
                if (currentTime > lastTime)
                {
                    lastTime = currentTime;
                    if (currHitObject != null)
                    {
                        if (nextHitObjIndex == 0 && !initialVelocity)
                        {
                            GetVelocities(currHitObject);
                            initialVelocity = true;
                        }
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
                                scoreData = MainWindow.osu.GetScoreData() ?? scoreData;
                                currHitObject = ++nextHitObjIndex < beatmap.GetHitObjects().Count ? beatmap.GetHitObjects()[nextHitObjIndex] : null;
                                if (currHitObject != null)
                                    GetVelocities(currHitObject);
                                else
                                {
                                    PrintScoreData();
                                    break;
                                }
                            }
                        }
                    }
                    else
                        return;
                }
                else if (currentTime < lastTime - 3)
                {
                    Console.WriteLine($"Detected possible reset: curr={currentTime} last={lastTime}");
                    continueRunning = true;
                    break;
                }

                Thread.Sleep(1);
            }
            inputSimulator.Keyboard.KeyUp(keyCode1);
            inputSimulator.Keyboard.KeyUp(keyCode2);
            if (continueRunning)
                Update();
        }

        private void AutoPilotCircle(HitObject currHitObject)
        {
            if (currHitObject == null)
                return;

            Vec2Float objDistVec = GetDistanceVectorFromObject(currHitObject);
            float objDist = objDistVec.Distance(0, 0);
            if (objDist <= BeatmapUtils.CirclePxRadius)
            {
                velocity.Multiply(objDist / BeatmapUtils.CirclePxRadius);
                if (objDist <= BeatmapUtils.CirclePxRadius / 2.75f)
                    velocity.Multiply(0.02f);
                return;
            }

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
            if (velocity.X * objDistVec.X >= 0)
                velocity.X = -objDistVec.X * applyAutoCorrect(objDistVec.X) * (float)Math.Pow(ResolutionUtils.Ratio.X, 0.825);

            if (velocity.Y * objDistVec.Y >= 0)
                velocity.Y = -objDistVec.Y * applyAutoCorrect(objDistVec.Y) * (float)Math.Pow(ResolutionUtils.Ratio.Y, 0.825);
            
            if (velocity.X != 0.0f)
                velocity.X = Math.Min(Math.Abs(velocity.X), Math.Abs(objDistVec.X)) * Math.Sign(velocity.X);

            if (velocity.Y != 0.0f)
                velocity.Y = Math.Min(Math.Abs(velocity.Y), Math.Abs(objDistVec.Y)) * Math.Sign(velocity.Y);
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
            int sign1 = rand.Next(0, 1) == 0 ? -1 : 1;
            int sign2 = rand.Next(0, 1) == 0 ? -1 : 1;
            x += (float)rand.NextDouble() * 150 * sign1;
            y += (float)rand.NextDouble() * 10 * sign2;
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
                if ((currHitObject as HitObjectSlider).RepeatCount > 1 && (currHitObject as HitObjectSlider).PixelLength <= 70)
                {
                    Console.WriteLine((currHitObject as HitObjectSlider).RepeatCount);
                    velocity.X = 0;
                    velocity.Y = 0;
                }
            }
        }

        private void AutoPilot(HitObject currHitObject)
        {
            if (!autopilotRunning)
                return;

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
            if (!relaxRunning)
                return;

            shouldPressSecondary = lastHitObject != null && currHitObject.Time - lastHitObject.EndTime < timeDiffThreshold ? !shouldPressSecondary : false;
            keyPressed = shouldPressSecondary ? KeyPressed.Key2 : KeyPressed.Key1;
            inputSimulator.Keyboard.KeyDown(shouldPressSecondary ? keyCode2 : keyCode1);
            //Thread.Sleep(2);
            bool pressedSecondary = shouldPressSecondary;
            Task.Delay((int)Math.Max((currHitObject.EndTime - currHitObject.Time) / speedMod, 16)).ContinueWith(ant =>
            {
                inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1);
                if ((pressedSecondary && keyPressed == KeyPressed.Key2) || (!pressedSecondary && keyPressed == KeyPressed.Key1))
                    keyPressed = KeyPressed.None;
            });
        }

        private void GetVelocities(HitObject currHitObject)
        {
            if (!autopilotRunning)
                return;

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
            velocity.Multiply(4.33f * (float)Math.Pow(ResolutionUtils.Ratio.X, 1.75)); // 7.1 8.6 (160) 9.8 (250)
            velocity.Multiply(Math.Max(1, velocity.Distance(0, 0) / 44f));
            velocity.Multiply(speedMod);
        }

        private Vec2Float GetDistanceVectorFromObject(HitObject hitObj)
        {
            GetCursorPos(out cursorPos);
            return new Vec2Float(cursorPos.X - ResolutionUtils.ConvertToScreenXCoord(hitObj.X), cursorPos.Y - ResolutionUtils.ConvertToScreenYCoord(hitObj.Y));
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

        private void PrintScoreData()
        {
            if (scoreData.HasValue)
            {
                Osu.SCORE_DATA fscoreData = scoreData.Value;
                Console.WriteLine($"300:   {fscoreData.score_300}\n100:   {fscoreData.score_100}\n50:    {fscoreData.score_50}\n0:     {fscoreData.score_0}\nScore: {fscoreData.current_score}\nCombo: {fscoreData.current_combo}");
                scoreData = null;
            }
        }

        public void ToggleAutoPilot() => autopilotRunning = !autopilotRunning;
        public void ToggleRelax() => relaxRunning = !relaxRunning;
        public char GetKey1() => (char)keyCode1;
        public char GetKey2() => (char)keyCode2;
        public void SetKey1(char key) => keyCode1 = (VirtualKeyCode)key;
        public void SetKey2(char key) => keyCode2 = (VirtualKeyCode)key;
        public bool IsAutoPilotRunning() => autopilotRunning;
        public bool IsRelaxRunning() => relaxRunning;
        public void SetBeatmap(CurrentBeatmap cb) => beatmap = cb;

        private Random rand = new Random();
        private VirtualKeyCode keyCode1 = (VirtualKeyCode)'Z';
        private VirtualKeyCode keyCode2 = (VirtualKeyCode)'X';
        private bool autopilotRunning = false;
        private bool relaxRunning = false;
        private double ellipseAngle = 0;
        private InputSimulator inputSimulator = new InputSimulator();
        private CurrentBeatmap beatmap;
        private POINT cursorPos, cursorPos2 = new POINT(-1, -1);
        private Vec2Float velocity, missing;
        private Osu.SCORE_DATA? scoreData = null;
        private float speedMod = 1;
        private float timeDiffThreshold;
        private int currentTime;
        private KeyPressed keyPressed = KeyPressed.None;

        private const double ANGLE_INCREMENT = Math.PI / 18;
        private const float TWO_PI = 2 * (float)Math.PI;

        private enum KeyPressed
        {
            None, Key1, Key2
        }
    }
}
