using System;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderLinear : HitObjectSlider
    {
        private readonly float xComponent;
        private readonly float yComponent;

        public HitObjectSliderLinear(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity,
            System.Collections.Generic.List<osu_database_reader.Components.Beatmaps.TimingPoint> timingPoints, bool vInvert) : base(hollyObj, sliderVelocity, timingPoints, vInvert)
        {
            int lastIndex = Points.Count - 1;
            float angle = (float)Math.Atan2(Points[lastIndex].Y - Y, Points[lastIndex].X - X);
            xComponent = (float)Math.Cos(angle);
            yComponent = (float)Math.Sin(angle);
        }

        public override Vec2Float GetOffset(int currentTime)
        {
            float expectedPosition = (float)PixelLength * GetTimeDiff(currentTime) / PathTime;
            return new Vec2Float(expectedPosition * xComponent, expectedPosition * yComponent);
        }
    }
}
