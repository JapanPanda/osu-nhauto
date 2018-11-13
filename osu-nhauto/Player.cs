﻿using System;
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
            keyCode1 = (WindowsInput.Native.VirtualKeyCode)key1;
            keyCode2 = (WindowsInput.Native.VirtualKeyCode)key2;
            osuClient = osu;
        }


        public void Update()
        {
            //Mods timeMod = osuClient.GetTimeMod();
            BeatmapUtils.InitializeBeatmap(beatmap);
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
                if (currSlider.Curve == CurveType.Bezier)
                {
                    HitObjectSliderBezier bezierSlider = currSlider as HitObjectSliderBezier;
                    bezierSlider.CheckForUpdate(cursorPos, ref cursorPos2, currentTime);
                }
                Vec2Float pos = currSlider.GetOffset(currentTime);
                GetCursorPos(out cursorPos);
                velX = pos.X * ResolutionUtils.Ratio.X + cursorPos2.X - cursorPos.X;
                velY = pos.Y * ResolutionUtils.Ratio.Y + cursorPos2.Y - cursorPos.Y;
                

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
