using osu_database_reader;
using osu_database_reader.Components;
using osu_database_reader.Components.Beatmaps;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace osu_nhauto.HitObjects
{
    public abstract class HitObjectSlider : HitObject
    {
        public double PixelLength { get; protected set; }
        public int RepeatCount { get; private set; }
        public List<Vector2> Points;
        public CurveType Curve;
        public int Duration { get; set; }
        public float PathTime { get; private set; }
        public int TreatAsCircle { get; set; }

        public int? timeStop;
        public float timeStopFactor = 0.925f;

        public HitObjectSlider(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity, 
            List<TimingPoint> timingPoints, bool vInvert) : base(hollyObj, vInvert)
        {
            if (vInvert)
                for (int i = 0; i < hollyObj.Points.Count; ++i)
                    hollyObj.Points[i] = new Vector2(hollyObj.Points[i].X, 384 - hollyObj.Points[i].Y);

            PixelLength = hollyObj.Length;
            RepeatCount = hollyObj.RepeatCount;
            Points = hollyObj.Points;
            Curve = hollyObj.CurveType;
            Duration = CalculateSliderDuration(sliderVelocity, timingPoints);
            EndTime = Time + Duration;
            PathTime = Duration / RepeatCount;
        }

        private int CalculateSliderDuration(float sliderVelocity, List<TimingPoint> timingPoints)
        {
            int start = 0, end = timingPoints.Count - 1, mid;
            do
            {
                mid = (start + end) / 2;
                if (Time > timingPoints[mid].Time)
                    start = mid + 1;
                else if (Time < timingPoints[mid].Time)
                    end = mid - 1;
                else
                    break;
            } while (start <= end);
            if (Time < timingPoints[mid].Time)
                --mid;

            mid = Math.Max(0, mid);
            double msPerQuarter = timingPoints[mid].MsPerQuarter >= 0 ? timingPoints[mid].MsPerQuarter : 1000;
            double speedVelocity = timingPoints[mid].MsPerQuarter < 0 ? - 100 / timingPoints[mid].MsPerQuarter : 1;
            for (int i = mid + 1; i < timingPoints.Count; ++i)
            {
                if (timingPoints[i].Time != timingPoints[mid].Time)
                    break;

                double msPerQuarterI = timingPoints[i].MsPerQuarter;
                if (msPerQuarterI < 0)
                    speedVelocity = -100 / timingPoints[i].MsPerQuarter;
            }
            for (int i = mid; i >= 0; --i)
            {
                double msPerQuarterI = timingPoints[i].MsPerQuarter;
                if (msPerQuarterI > 0)
                {
                    msPerQuarter = msPerQuarterI;
                    break;
                }
            }
            speedVelocity = Math.Max(0.1, speedVelocity);
            speedVelocity = Math.Min(10, speedVelocity);
            return (int)Math.Ceiling(PixelLength * RepeatCount / (100 * sliderVelocity * speedVelocity / msPerQuarter));
        }

        protected float GetTimeDiff(int currentTime)
        {
            // circular >> linear motion
            if (timeStop.HasValue && currentTime > timeStop.Value)
                currentTime = timeStop.Value + (int)((currentTime - timeStop.Value) * timeStopFactor);
            if (currentTime <= Time)
                return 0;

            int period = currentTime - Time;
            if (TreatAsCircle == 1)
            {
                buzzRestFactor = Math.Max(buzzRestFactor - 0.01f, 2.5f);
                return Math.Min(period, PathTime / buzzRestFactor);
            }
            else if (TreatAsCircle == 2)
                return 0;

            float timeDiff = period % PathTime;
            int repeatNumber = (int)(period / PathTime);

            if (repeatNumber >= RepeatCount)
                return RepeatCount % 2 == 1 ? PathTime : 0;

            if (repeatNumber % 2 == 1)
                timeDiff = PathTime - timeDiff;

            return timeDiff;
        }

        public Vec2Float GetPosition(int currentTime) => GetRelativePosition(currentTime).Add(X, Y);

        public Vec2Float GetRelativePosition(int currentTime) => CalculateOffset(currentTime);

        protected abstract Vec2Float CalculateOffset(int currentTime);

        private float buzzRestFactor = 3.0f;
    }
}
