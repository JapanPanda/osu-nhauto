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

        public void ConfigureDefaultKeybinds()
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

        public void Initialize()
        {
            try
            {
                BeatmapUtils.InitializeBeatmap(beatmap);
                speedMod = GetSpeedModifier();
                timeDiffThreshold = 116 * speedMod;
                Update();
            }
            catch (ThreadAbortException)
            {
                PrintScoreData();
            }
        }

        public void Update()
        {
            bool continueRunning = false;
            int nextHitObjIndex = 0;
            HitObject lastHitObject = null;
            HitObject currHitObject = beatmap.GetHitObjects()[0];
            bool shouldPressSecondary = false, initialVelocity = false;
            ResolutionUtils.CalculatePlayfieldResolution();
            velocity.Zero();
            missing.Zero();
            while (MainWindow.osu.GetAudioTime() == 0) { Thread.Sleep(1); }
            while (MainWindow.osu.GetAudioTime() <= currHitObject.Time - BeatmapUtils.TimeFadeIn / 2) { Thread.Sleep(1); }
            Console.WriteLine("Now listening for time changes");
            int lastTime = MainWindow.osu.GetAudioTime();
            scoreData = null;
            POINT lastPos = new POINT(0, 0);
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                GetCursorPos(out cursorPos);
                //Console.WriteLine($"{cursorPos.X} {cursorPos.Y}");
                lastPos = cursorPos;
                currentTime = MainWindow.osu.GetAudioTime() + 6;
                if (currentTime > lastTime)
                {
                    if (currHitObject != null)
                    {
                        if (nextHitObjIndex == 0 && !initialVelocity)
                        {
                            GetCursorPos(out cursorPos);
                            //Mouse_Event(0x1 | 0x8000, cursorPos.X * 65535 / 1920, cursorPos.Y * 65535 / 1080, 0, 0);
                            GetVelocities(currHitObject);
                            initialVelocity = true;
                        }
                        AutoPilot(currHitObject, currentTime - lastTime);

                        if (currHitObject.Time - currentTime <= 0)
                        {
                            if (currHitObject != lastHitObject)
                            {
                                Relax(currHitObject, lastHitObject, ref shouldPressSecondary);
                                lastHitObject = currHitObject;
                            }

                            if (currentTime >= currHitObject.EndTime + 8)
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
                    lastTime = currentTime;
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

        private void AutoPilotCircle(HitObject currHitObject, int offset)
        {
            if (currHitObject == null)
                return;

            if (velocity.Distance(0, 0) < 1e-5)
            {
                velocity.Zero();
                return;
            }

            Vec2Float objDistVec = GetDistanceVectorFromObject(currHitObject);
            float maxDrawnRadius = BeatmapUtils.CirclePxRadius * ResolutionUtils.Ratio.X;
            float objDist = objDistVec.Distance(0, 0);
            velocity.Multiply(offset);
            
            if (velocity.X * objDistVec.X <= 0)
                velocity.X = -objDistVec.X * 0.5f;

            if (velocity.Y * objDistVec.Y <= 0)
                velocity.Y = -objDistVec.Y * 0.5f;
            
            if (velocity.X != 0.0f)
                velocity.X = Math.Min(Math.Abs(velocity.X), Math.Abs(objDistVec.X)) * Math.Sign(velocity.X);

            if (velocity.Y != 0.0f)
                velocity.Y = Math.Min(Math.Abs(velocity.Y), Math.Abs(objDistVec.Y)) * Math.Sign(velocity.Y);

            if (objDist <= 5)
                velocity.Multiply(objDist / maxDrawnRadius);
        }

        private void AutoPilotSpinner()
        {
            GetCursorPos(out cursorPos);
            Vec2Float center = ResolutionUtils.CenterPos;
            float dist = (float)Math.Sqrt(Math.Pow(cursorPos.X - center.X, 2) + Math.Pow(cursorPos.Y - center.Y, 2));
            ellipseAngle += ANGLE_INCREMENT;

            if (ellipseAngle > TWO_PI)
                ellipseAngle = ellipseAngle % TWO_PI;

            float x = (center.X + (float)(dist * Math.Cos(ellipseAngle))) * 65535 / 1920;
            float y = (center.Y + (float)(dist * Math.Sin(ellipseAngle))) * 65535 / 1080;
            if (dist != 33)
            {
                int sign = Math.Sign(33 - dist);
                x += (float)(50 * Math.Cos(ellipseAngle)) * sign;
                y += (float)(50 * Math.Sin(ellipseAngle)) * sign;
            }
            Mouse_Event(0x1 | 0x8000, (int)x, (int)y, 0, 0);
        }
        
        private void AutoPilotSlider(HitObject currHitObject, int offset)
        {
            if (currentTime < currHitObject.Time)
            {
                AutoPilotCircle(currHitObject, offset);
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

        private void AutoPilot(HitObject currHitObject, int offset)
        {
            if (!autopilotRunning)
                return;

            bool hitCircle = false;
            switch (currHitObject.Type & (HitObjectType)0b1000_1011)
            {
                case HitObjectType.Normal:
                    AutoPilotCircle(currHitObject, offset);
                    hitCircle = true;
                    break;
                case HitObjectType.Slider:
                    if (currentTime < currHitObject.Time)
                        hitCircle = true;
                    AutoPilotSlider(currHitObject, offset);
                    break;
                case HitObjectType.Spinner:
                    velocity.Zero();
                    missing.Zero();
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
            //Console.WriteLine($"{velocity.X} {velocity.Y} {velocity.Distance(0, 0)}");
            Mouse_Event(0x1, (int)velocity.X, (int)velocity.Y, 0, 0);

            velocity.Subtract(delta.X, delta.Y);
            if (hitCircle)
                velocity.Multiply(1.0f / offset);
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

            missing.Zero();
            GetCursorPos(out cursorPos);
            if ((currHitObject.Type & (HitObjectType)0b1000_1011) == HitObjectType.Spinner)
            {
                Vec2Float center = ResolutionUtils.CenterPos;
                ellipseAngle = Math.Atan2(cursorPos.Y - center.Y, cursorPos.X - center.X);
                velocity.Zero();
                return;
            }

            // TODO check if in middle of stream
            int timeDiff = currHitObject.Time - currentTime - 100;
            velocity = GetDistanceVectorFromObject(currHitObject).Multiply(1.0f / Math.Max(1, timeDiff));
        }

        private Vec2Float GetDistanceVectorFromObject(HitObject hitObj)
        {
            GetCursorPos(out cursorPos);
            return new Vec2Float(ResolutionUtils.ConvertToScreenXCoord(hitObj.X) - cursorPos.X, ResolutionUtils.ConvertToScreenYCoord(hitObj.Y) - cursorPos.Y);
        }

        private Vec2Float GetDistanceVectorFromTwoObjects(HitObject obj1, HitObject obj2) =>
            new Vec2Float(ResolutionUtils.ConvertToScreenXCoord(obj2.X - obj1.X), ResolutionUtils.ConvertToScreenYCoord(obj2.Y - obj1.Y));

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
