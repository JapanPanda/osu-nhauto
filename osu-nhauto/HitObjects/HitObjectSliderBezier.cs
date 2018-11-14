using osu_database_reader.Components.Beatmaps;
using System;
using System.Collections.ObjectModel;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderBezier : HitObjectSlider
    {
        
        public HitObjectSliderBezier(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity,
            System.Collections.Generic.List<osu_database_reader.Components.Beatmaps.TimingPoint> timingPoints, bool vInvert) : base(hollyObj, sliderVelocity, timingPoints, vInvert)
        {

        }

        private Vec2Float prevBezPoint;
        private Vec2Float currBezPoint;
        private float currStep = 0;
        private bool test = false;
        private bool test2 = false;
        private int prevTime;
        public override Vec2Float GetOffset(int currentTime)
        {
            // Calculation of points in bezier slider
            if (!test)
            {
                for (float i = 0; i <= 1; i += 0.015f)
                {
                    Vec2Float test = GetBezierPoint(i);
                    //Console.WriteLine($"{test.X} x {test.Y}");

                }
                test = true;
            }
            if (test2)
                return new Vec2Float(0, 0);
            float timeDiff;
            float duration = this.Duration / this.RepeatCount;
            if (currStep == 0)
            {
                prevBezPoint = GetBezierPoint(currStep);
                currStep += 0.015f;
                currBezPoint = GetBezierPoint(currStep);
                Console.WriteLine($"Initialize: {currStep}: {currBezPoint.X} x {currBezPoint.Y} || {prevBezPoint.X} x {prevBezPoint.Y}");
                timeDiff = (0.015f * duration) % duration;
                prevTime = currentTime;
            }
            else
            {
                if (currStep > 1)
                {
                    return new Vec2Float(0, 0);
                }
                timeDiff = (currentTime - prevTime) % duration;
                
                //Console.WriteLine($"Distance: {currBezPoint.X} x {currBezPoint.Y} || {prevBezPoint.X} x {prevBezPoint.Y} = {distance}");
                
            }

            float angle = (float)Math.Atan2(currBezPoint.Y - prevBezPoint.Y, currBezPoint.X - prevBezPoint.X);
            float distance = (float)Math.Sqrt(Math.Pow(currBezPoint.Y - prevBezPoint.Y, 2) + Math.Pow(currBezPoint.X - prevBezPoint.X, 2));

            float expectedX = (float)(distance * Math.Cos(angle) * timeDiff / (0.01f * duration));
            float expectedY = (float)(distance * Math.Sin(angle) * timeDiff / (0.01f * duration));
            return new Vec2Float(expectedX, expectedY);
        }

        private Vec2Float GetBezierPoint(float step)
        {
            Vec2Float point = new Vec2Float();
            int points = this.Points.Count;
            point.X = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points - 0) * Math.Pow(step, 0) * this.X);
            point.Y = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points - 0) * Math.Pow(step, 0) * this.Y);
            for (int i = 0; i <= points - 1; i++)
            {
                point.X += (float)(GetBinomialCoefficient(points, i + 1) * Math.Pow(1 - step, points - i - 1) * Math.Pow(step, i + 1) * this.Points[i].X);
                point.Y += (float)(GetBinomialCoefficient(points, i + 1) * Math.Pow(1 - step, points - i - 1) * Math.Pow(step, i + 1) * this.Points[i].Y);

            }
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

        public void CheckForUpdate(Player.POINT cursorPos, ref Player.POINT cursorPos2, int currentTime)
        {
            if (currStep > 1)
            {
                return;
            } 
            if (cursorPos.X >= ResolutionUtils.ConvertToScreenXCoord(currBezPoint.X) - 10 && (cursorPos.X <= ResolutionUtils.ConvertToScreenXCoord(currBezPoint.X) + 10)
                && cursorPos.Y >= ResolutionUtils.ConvertToScreenYCoord(currBezPoint.Y) - 10 && cursorPos.Y <= ResolutionUtils.ConvertToScreenYCoord(currBezPoint.Y) + 10)
            {
                cursorPos2.X = cursorPos.X;
                cursorPos2.Y = cursorPos.Y;
                currStep += 0.015f;
                if (currStep > 1)
                    currStep = 1;
                prevBezPoint = currBezPoint;
                currBezPoint = GetBezierPoint(currStep);
                prevTime = currentTime;

                Console.WriteLine($"New Bezier Point: {currStep}: {currBezPoint.X} x {currBezPoint.Y}");
            }
        }
    }
}
