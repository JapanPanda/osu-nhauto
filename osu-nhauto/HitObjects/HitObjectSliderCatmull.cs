using osu_database_reader.Components;
using osu_database_reader.Components.Beatmaps;
using System;
using System.Collections.Generic;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderCatmull : HitObjectSlider
    {
        private const int detail = 50;
        private List<Vec2Float> calculatedPath;
        private List<double> cumulativeLength = new List<double>();

        public HitObjectSliderCatmull(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity,
            List<TimingPoint> timingPoints, bool vInvert) : base(hollyObj, sliderVelocity, timingPoints, vInvert)
        {
            List<Vec2Float> points = new List<Vec2Float>(hollyObj.Points.Count) { new Vec2Float(0, 0) };
            foreach (Vector2 v in hollyObj.Points)
                points.Add(new Vec2Float(v.X - X, v.Y - Y));

            calculatedPath = new List<Vec2Float>((points.Count - 1) * detail * 2);
            for (int i = 0; i < points.Count - 1; i++)
            {
                var v1 = i > 0 ? points[i - 1] : points[i];
                var v2 = points[i];
                var v3 = i < points.Count - 1 ? points[i + 1] : v2.Clone().Multiply(2).Subtract(v1);
                var v4 = i < points.Count - 2 ? points[i + 2] : v3.Clone().Multiply(2).Subtract(v2);

                for (int c = 0; c < detail; c++)
                {
                    calculatedPath.Add(FindPoint(ref v1, ref v2, ref v3, ref v4, (float)c / detail));
                    calculatedPath.Add(FindPoint(ref v1, ref v2, ref v3, ref v4, (float)(c + 1) / detail));
                }
            }

            double l = 0;
            cumulativeLength.Add(l);
            for (int i = 0; i < calculatedPath.Count - 1; ++i)
            {
                Vec2Float diff = calculatedPath[i + 1].Clone().Subtract(calculatedPath[i]);
                double d = diff.Distance(0, 0);
                if (PixelLength - l < d)
                {
                    calculatedPath[i + 1] = calculatedPath[i].Clone().Add(diff.Multiply((float)((PixelLength - l) / d)));
                    calculatedPath.RemoveRange(i + 2, calculatedPath.Count - 2 - i);

                    l = PixelLength;
                    cumulativeLength.Add(l);
                    break;
                }
                l += d;
                cumulativeLength.Add(l);
            }

            if (l < PixelLength && calculatedPath.Count > 1)
            {
                Vec2Float diff = calculatedPath[calculatedPath.Count - 1].Clone().Subtract(calculatedPath[calculatedPath.Count - 2]);
                double d = diff.Distance(0, 0);

                if (d <= 0)
                    return;

                calculatedPath[calculatedPath.Count - 1].Add(diff.Multiply((float)((PixelLength - l) / d)));
                cumulativeLength[calculatedPath.Count - 1] = PixelLength;
            }
        }

        public override Vec2Float GetOffset(int currentTime)
        {
            double d = GetTimeDiff(currentTime) / PathTime * PixelLength;
            return InterpolateVertices(IndexOfDistance(d), d);
        }

        private int IndexOfDistance(double d)
        {
            int i = cumulativeLength.BinarySearch(d);
            if (i < 0)
                i = ~i;
            return i;
        }

        private Vec2Float InterpolateVertices(int i, double d)
        {
            if (i <= 0)
                return calculatedPath[0];
            else if (i >= calculatedPath.Count)
                return calculatedPath[calculatedPath.Count - 1];

            Vec2Float p0 = calculatedPath[i - 1].Clone();
            Vec2Float p1 = calculatedPath[i].Clone();

            double d0 = cumulativeLength[i - 1];
            double d1 = cumulativeLength[i];

            if (d1 - d0 <= Math.Pow(10, -7))
                return p0;

            double w = (d - d0) / (d1 - d0);
            return p0.Add(p1.Subtract(p0).Multiply((float)w));
        }

        private Vec2Float FindPoint(ref Vec2Float vec1, ref Vec2Float vec2, ref Vec2Float vec3, ref Vec2Float vec4, float t)
        {
            float t2 = t * t;
            float t3 = t * t2;

            Vec2Float result;
            result.X = 0.5f * (2f * vec2.X + (-vec1.X + vec3.X) * t + (2f * vec1.X - 5f * vec2.X + 4f * vec3.X - vec4.X) * t2 + (-vec1.X + 3f * vec2.X - 3f * vec3.X + vec4.X) * t3);
            result.Y = 0.5f * (2f * vec2.Y + (-vec1.Y + vec3.Y) * t + (2f * vec1.Y - 5f * vec2.Y + 4f * vec3.Y - vec4.Y) * t2 + (-vec1.Y + 3f * vec2.Y - 3f * vec3.Y + vec4.Y) * t3);

            return result;
        }
    }
}
