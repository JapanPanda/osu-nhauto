using osu_database_reader.Components;
using osu_database_reader.Components.Beatmaps;
using System;
using System.Collections.Generic;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderBezier : HitObjectSlider
    {
        private List<Vec2Float> calculatedPath = new List<Vec2Float>();
        private List<double> cumulativeLength = new List<double>();

        private Vec2Float[] subdivisionBuffer1;
        private Vec2Float[] subdivisionBuffer2;

        private Vec2Float? linearApproximation = null;

        private const float bezier_tolerance = 0.0625f;
        private int count;

        public float maxDistFromHead = 0;

        public bool TreatAsLinear { get => linearApproximation.HasValue; }

        public HitObjectSliderBezier(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity, 
            List<TimingPoint> timingPoints, bool vInvert) : base(hollyObj, sliderVelocity, timingPoints, vInvert)
        {
            List<Vec2Float> points = new List<Vec2Float>(hollyObj.Points.Count) { new Vec2Float(0, 0) };
            foreach (Vector2 v in hollyObj.Points)
                points.Add(new Vec2Float(v.X - X, v.Y - Y));

            int finalIndex = points.Count - 1;
            List<Vec2Float> subpath = new List<Vec2Float>();
            for (int i = 0; i <= finalIndex; ++i)
            {
                subpath.Add(points[i]);
                if (i == finalIndex || (points[i].X == points[i + 1].X && points[i].Y == points[i + 1].Y))
                {
                    List<Vec2Float> calculatedSubpath = ApproximateBezier(subpath);
                    foreach (Vec2Float v in calculatedSubpath)
                        calculatedPath.Add(v);
                    subpath.Clear();
                }
            }

            CalculateLength();
            if (calculatedPath.Count == 0)
                return;
            
            for (int i = 1; i < PathTime; i += 8)
            {
                Vec2Float nextOffset = CalculateOffset(Time + i);
                maxDistFromHead = Math.Max(maxDistFromHead, nextOffset.Length());
            }

            Vec2Float endPt = CalculateOffset(Time + (int)PathTime);
            float lineLength = endPt.Length();
            if (PixelLength - lineLength <= 35)
            {
                float angle = (float)Math.Atan2(endPt.Y, endPt.X);
                linearApproximation = new Vec2Float((float)Math.Cos(angle), (float)Math.Sin(angle));
                PixelLength = lineLength;
            }
        }

        protected override Vec2Float CalculateOffset(int currentTime)
        {
            if (TreatAsLinear)
            {
                float expectedPosition = (float)PixelLength * GetTimeDiff(currentTime) / PathTime;
                return new Vec2Float(expectedPosition * linearApproximation.Value.X, expectedPosition * linearApproximation.Value.Y);
            }
            double d = GetTimeDiff(currentTime) / PathTime * PixelLength;
            return InterpolateVertices(IndexOfDistance(d), d);
        }

        private List<Vec2Float> ApproximateBezier(List<Vec2Float> points)
        {
            List<Vec2Float> output = new List<Vec2Float>();
            count = points.Count;

            subdivisionBuffer1 = new Vec2Float[count];
            subdivisionBuffer2 = new Vec2Float[count * 2 - 1];

            Stack<Vec2Float[]> toFlatten = new Stack<Vec2Float[]>();
            Stack<Vec2Float[]> freeBuffers = new Stack<Vec2Float[]>();

            toFlatten.Push(points.ToArray());

            Vec2Float[] leftChild = subdivisionBuffer2;

            while (toFlatten.Count > 0)
            {
                Vec2Float[] parent = toFlatten.Pop();
                if (IsFlatEnough(parent))
                {
                    Approximate(parent, output);
                    freeBuffers.Push(parent);
                    continue;
                }

                Vec2Float[] rightChild = freeBuffers.Count > 0 ? freeBuffers.Pop() : new Vec2Float[count];
                Subdivide(parent, leftChild, rightChild);

                for (int i = 0; i < count; ++i)
                    parent[i] = leftChild[i];

                toFlatten.Push(rightChild);
                toFlatten.Push(parent);
            }
            output.Add(points[count - 1]);
            return output;
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

        private bool IsFlatEnough(Vec2Float[] controlPoints)
        {
            for (int i = 1; i < controlPoints.Length - 1; i++)
            {
                Vec2Float a = new Vec2Float(controlPoints[i - 1].X - 2 * controlPoints[i].X + controlPoints[i + 1].X,
                    controlPoints[i - 1].Y - 2 * controlPoints[i].Y + controlPoints[i + 1].Y);

                if (a.X * a.X + a.Y * a.Y > bezier_tolerance * bezier_tolerance * 4)
                    return false;
            }
            return true;
        }

        private void Subdivide(Vec2Float[] controlPoints, Vec2Float[] l, Vec2Float[] r)
        {
            Vec2Float[] midpoints = subdivisionBuffer1;

            for (int i = 0; i < count; ++i)
                midpoints[i] = controlPoints[i];

            for (int i = 0; i < count; i++)
            {
                l[i] = midpoints[0];
                r[count - i - 1] = midpoints[count - i - 1];

                for (int j = 0; j < count - i - 1; j++)
                {
                    Vec2Float a = new Vec2Float((midpoints[j].X + midpoints[j + 1].X) / 2f,
                        (midpoints[j].Y + midpoints[j + 1].Y) / 2f);
                    midpoints[j] = a;
                }
            }
        }

        private void Approximate(Vec2Float[] controlPoints, List<Vec2Float> output)
        {
            Vec2Float[] l = subdivisionBuffer2;
            Vec2Float[] r = subdivisionBuffer1;

            Subdivide(controlPoints, l, r);

            for (int i = 0; i < count - 1; ++i)
                l[count + i] = r[i + 1];

            output.Add(controlPoints[0]);
            for (int i = 1; i < count - 1; ++i)
            {
                int index = 2 * i;
                Vec2Float a = new Vec2Float((l[index - 1].X + 2 * l[index].X + l[index + 1].X) * 0.25f,
                    (l[index - 1].Y + 2 * l[index].Y + l[index + 1].Y) * 0.25f);
                output.Add(a);
            }
        }

        private void CalculateLength()
        {
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
    }
}
