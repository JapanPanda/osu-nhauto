using osu_database_reader.Components.Beatmaps;
using System;
using System.Collections.Generic;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderLinear : HitObjectSlider
    {
        private readonly float xComponent;
        private readonly float yComponent;

        public HitObjectSliderLinear(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity, 
            List<TimingPoint> timingPoints, bool vInvert) : base(hollyObj, sliderVelocity, timingPoints, vInvert)
        {
            float angle = (float)Math.Atan2(Points[0].Y - Y, Points[0].X - X);
            xComponent = (float)Math.Cos(angle);
            yComponent = (float)Math.Sin(angle);
        }

        protected override Vec2Float CalculateOffset(int currentTime)
        {
            float expectedPosition = (float)PixelLength * GetTimeDiff(currentTime) / PathTime;
            return new Vec2Float(expectedPosition * xComponent, expectedPosition * yComponent);
        }
    }
}
