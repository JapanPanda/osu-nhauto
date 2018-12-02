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
                beatmap.Parse();
                Update();
            }
            catch (ThreadAbortException)
            {
                PrintScoreData();
            }
        }

        public void Update()
        {
            velocity.Zero();
            missing.Zero();
            bool continueRunning = false;
            int avgHumanReaction = (int)beatmap.TimePreempt + 190;
            var hitObjIterator = beatmap.GetHitObjects().GetEnumerator();
            hitObjIterator.MoveNext();
            HitObject lastHitObject = null, currHitObject = hitObjIterator.Current;
            bool shouldPressSecondary = false, initialVelocity = false, shouldGetVelocitiesBeforeClick = false;
            ResolutionUtils.CalculatePlayfieldResolution();
            while (MainWindow.osu.GetAudioTime() == 0) { Thread.Sleep(1); }
            while (MainWindow.osu.GetAudioTime() <= currHitObject.Time - avgHumanReaction) { Thread.Sleep(1); }
            Console.WriteLine("Now listening for time changes");
            int lastTime = MainWindow.osu.GetAudioTime();
            double randomLate = GetNextRandomError();
            while (MainWindow.statusHandler.GetGameState() == GameState.Playing)
            {
                Thread.Sleep(1);
                currentTime = MainWindow.osu.GetAudioTime() + 6;
                if (currentTime > lastTime)
                {
                    if (currHitObject != null)
                    {
                        if (!initialVelocity)
                        {
                            GetVelocities(currHitObject);
                            initialVelocity = true;
                        }
                        AutoPilot(currHitObject, currentTime - lastTime);

                        if (!shouldGetVelocitiesBeforeClick && currentTime - currHitObject.Time >= randomLate)
                        {
                            if (currHitObject != lastHitObject)
                            {
                                if (!Relax(currHitObject, lastHitObject, ref shouldPressSecondary))
                                    continue;
                                
                                lastHitObject = currHitObject;
                            }

                            if (currentTime >= currHitObject.EndTime + 8)
                            {
                                if (hitObjIterator.MoveNext())
                                {
                                    currHitObject = hitObjIterator.Current;
                                    shouldGetVelocitiesBeforeClick = true;
                                    randomLate = currHitObject.Type != HitObjectType.Spinner ? GetNextRandomError() : avgHumanReaction;
                                    if (currHitObject.Type != HitObjectType.Spinner && lastHitObject != null && lastHitObject.Type == HitObjectType.Spinner)
                                        velocity.Zero();
                                }
                                else
                                    break;
                            }
                        }

                        if (shouldGetVelocitiesBeforeClick && currentTime >= currHitObject.Time - avgHumanReaction)
                        {
                            GetVelocities(currHitObject);
                            shouldGetVelocitiesBeforeClick = false;
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
            }
            inputSimulator.Keyboard.KeyUp(keyCode1);
            inputSimulator.Keyboard.KeyUp(keyCode2);

            if (continueRunning)
                Update();
            else
                PrintScoreData();
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
            float maxDrawnRadius = beatmap.CirclePxRadius * ResolutionUtils.Ratio.X;
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

            if (ellipseAngle > TWO_PI)
            {
                ellipseAngle = ellipseAngle % TWO_PI;
                ellipseRotAngle += rand.Next(-150, 150) * 0.001f;
                ellipseRotation.X = (float)Math.Cos(ellipseRotAngle);
                ellipseRotation.Y = (float)Math.Sin(ellipseRotAngle);
                ellipseRadii.X += -0.5f + (float)rand.NextDouble();
                ellipseRadii.Y += -0.5f + (float)rand.NextDouble();
                ellipseTranslation.X += rand.Next(-8 - (int)ellipseTranslation.X, 8 - (int)ellipseTranslation.X) * ResolutionUtils.Ratio.X;
                ellipseTranslation.Y += rand.Next(-8 - (int)ellipseTranslation.Y, 8 - (int)ellipseTranslation.Y) * ResolutionUtils.Ratio.Y;
            }

            float xn = ellipseRadii.X * (float)Math.Cos(ellipseAngle) * ResolutionUtils.Ratio.X;
            float yn = -ellipseRadii.Y * (float)Math.Sin(ellipseAngle) * ResolutionUtils.Ratio.Y;
            float x = xn * ellipseRotation.X - yn * ellipseRotation.Y;
            float y = xn * ellipseRotation.Y + yn * ellipseRotation.X;
            Vec2Float signs = new Vec2Float(Math.Sign(66 - ellipseRadii.X), Math.Sign(55 - ellipseRadii.Y));
            ellipseRadii.X += signs.X * 5;
            ellipseRadii.Y += signs.Y * 5;
            ellipseAngle += ANGLE_INCREMENT;

            velocity.X = x - cursorPos.X + center.X + ellipseTranslation.X;
            velocity.Y = y - cursorPos.Y + center.Y + ellipseTranslation.Y;
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
                Vec2Float pos = (currHitObject as HitObjectSlider).GetRelativePosition(currentTime);
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
                    break;
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
            if (hitCircle)
                velocity.Multiply(1.0f / offset);
        }

        private double GetNextRandomError()
        {
            double num1 = 1 - rand.NextDouble();
            double num2 = 1 - rand.NextDouble();
            double randStdNorm = Math.Sqrt(-2.0 * Math.Log(num1)) * Math.Sin(2.0 * Math.PI * num2);
            return 4.5 * randStdNorm;
        }

        private bool Relax(HitObject currHitObject, HitObject lastHitObject, ref bool shouldPressSecondary)
        {
            if (!relaxRunning)
                return true;

            if (GetDistanceVectorFromObject(currHitObject).Length() > beatmap.CirclePxRadius && currentTime - currHitObject.Time <= beatmap.JudgementWindowSize)
                return false;

            shouldPressSecondary = lastHitObject != null && currHitObject.Time - lastHitObject.EndTime < beatmap.TimeDiffThreshold ? !shouldPressSecondary : false;
            bool pressedSecondary = shouldPressSecondary;
            if (inputSimulator.InputDeviceState.IsKeyDown(pressedSecondary ? keyCode2 : keyCode1))
                pressedSecondary = !pressedSecondary;

            keyPressed = pressedSecondary ? KeyPressed.Key2 : KeyPressed.Key1;
            inputSimulator.Keyboard.KeyDown(pressedSecondary ? keyCode2 : keyCode1);

            Task.Delay((int)Math.Max((currHitObject.EndTime - currentTime) / beatmap.SpeedModifier, 16)).ContinueWith(ant =>
            {
                inputSimulator.Keyboard.KeyUp(pressedSecondary ? keyCode2 : keyCode1);
                if ((pressedSecondary && keyPressed == KeyPressed.Key2) || (!pressedSecondary && keyPressed == KeyPressed.Key1))
                    keyPressed = KeyPressed.None;
            });
            return true;
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
                float centerDist = (float)Math.Sqrt(Math.Pow(cursorPos.X - center.X, 2) + Math.Pow(cursorPos.Y - center.Y, 2));
                ellipseRadii = new Vec2Float(centerDist, centerDist);
                ellipseTranslation.Zero();
                ellipseRotation = new Vec2Float(1, 0);
                ellipseRotAngle = 0;
                ellipseAngle = Math.Atan2(center.Y - cursorPos.Y, cursorPos.X - center.X);
                velocity.Zero();
                return;
            }
            sliderBallRandSettings.Zero();
            int timeDiff = currHitObject.Time - currentTime - (currHitObject.Streamable ? 5 : 65);
            velocity = GetDistanceVectorFromObject(currHitObject).Multiply(1.0f / Math.Min(250, Math.Max(1, timeDiff)));
        }

        private Vec2Float GetDistanceVectorFromObject(HitObject hitObj)
        {
            GetCursorPos(out cursorPos);
            return new Vec2Float(ResolutionUtils.ConvertToScreenXCoord(hitObj.X) - cursorPos.X, ResolutionUtils.ConvertToScreenYCoord(hitObj.Y) - cursorPos.Y);
        }

        private Vec2Float GetDistanceVectorFromTwoObjects(HitObject obj1, HitObject obj2) =>
            new Vec2Float(ResolutionUtils.ConvertToScreenXCoord(obj2.X - obj1.X), ResolutionUtils.ConvertToScreenYCoord(obj2.Y - obj1.Y));

        private void PrintScoreData()
        {
            Osu.SCORE_DATA? scoreData = MainWindow.osu.GetScoreData();
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
        private Vec2Float ellipseRadii;
        private Vec2Float ellipseRotation, ellipseTranslation;
        private Vec2Float sliderBallRandSettings;
        private float ellipseRotAngle = 0.79f;
        private int currentTime;
        private KeyPressed keyPressed = KeyPressed.None;

        private const double ANGLE_INCREMENT = Math.PI / 16;
        private const float TWO_PI = 2 * (float)Math.PI;

        private enum KeyPressed
        {
            None, Key1, Key2
        }
    }
}
