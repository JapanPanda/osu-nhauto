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

        public override Vec2Float GetOffset(int currentTime)
        {
            Vec2Float point = GetBezierPoint(GetTimeDiff(currentTime) / PathTime);
            //Console.WriteLine($"{point.X}, {point.Y}");
            return new Vec2Float(point.X - X, point.Y - Y);
        }

        private Vec2Float GetBezierPoint(float step)
        {
            Vec2Float point = new Vec2Float(0, 0);
            int points = this.Points.Count;
            point.X = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points) * Math.Pow(step, 0) * this.X);
            point.Y = (float)(GetBinomialCoefficient(points, 0) * Math.Pow(1 - step, points) * Math.Pow(step, 0) * this.Y);
            for (int i = 1; i <= points; i++)
            {
                point.X += (float)(GetBinomialCoefficient(points, i) * Math.Pow(1 - step, points - i) * Math.Pow(step, i) * this.Points[i - 1].X);
                point.Y += (float)(GetBinomialCoefficient(points, i) * Math.Pow(1 - step, points - i) * Math.Pow(step, i) * this.Points[i - 1].Y);

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
                res *= n - i;
                res /= i + 1;
            }
            return res;
        }
    }
}
