using osu_database_reader;
using osu_database_reader.Components;
using osu_database_reader.Components.Beatmaps;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace osu_nhauto.HitObjects
{
    public abstract class HitObjectSlider : HitObject
    {
        public double PixelLength { get; private set; }
        public int RepeatCount { get; private set; }
        public ReadOnlyCollection<Vector2> Points { get; private set; }
        public CurveType Curve;
        public int Duration { get; private set; }
        public float PathTime { get; private set; }

        public HitObjectSlider(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity,
            List<TimingPoint> timingPoints) : base(hollyObj)
        {
            PixelLength = hollyObj.Length;
            RepeatCount = hollyObj.RepeatCount;
            Points = hollyObj.Points.AsReadOnly();
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

            mid = System.Math.Max(0, mid);
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
            return (int)System.Math.Ceiling(PixelLength * RepeatCount / (100 * sliderVelocity * speedVelocity / msPerQuarter));
        }

        protected float GetTimeDiff(int currentTime)
        {
            int period = currentTime - Time;
            float timeDiff = period % PathTime;
            int repeatNumber = (int)(period / PathTime);
            if (repeatNumber % 2 == 1)
                timeDiff = PathTime - timeDiff;
            return timeDiff;
        }

        public Vec2Float GetPosition(int currentTime) => GetOffset(currentTime).Clone().Add(X, Y);

        public abstract Vec2Float GetOffset(int currentTime);
    }
}
