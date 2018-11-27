﻿using osu_database_reader.Components.Beatmaps;
using System;
using System.Collections.Generic;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderPerfect : HitObjectSlider
    {
        private readonly Vec2Float circleCenter;
        private readonly float circleRadius;
        private readonly float startAngle;
        private readonly float endAngle;

        public HitObjectSliderPerfect(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity, 
            List<TimingPoint> timingPoints, bool vInvert) : base(hollyObj, sliderVelocity, timingPoints, vInvert)
        {
            const float TWO_PI = 2 * (float)Math.PI;

            Vec2Float midpt1 = new Vec2Float((X + Points[0].X) / 2f, (Y + Points[0].Y) / 2f);
            Vec2Float midpt2 = new Vec2Float((Points[0].X + Points[1].X) / 2f, (Points[0].Y + Points[1].Y) / 2f);
            Vec2Float norml1 = new Vec2Float(Points[0].X - X, Points[0].Y - Y).Normal();
            Vec2Float norml2 = new Vec2Float(Points[1].X - Points[0].X, Points[1].Y - Points[0].Y).Normal();
            try
            {
                circleCenter = Vec2Float.Intersect(midpt1, norml1, midpt2, norml2);
            }
            catch (Exception)
            {
                throw new Exception();
            }

            float midAngle = (float)Math.Atan2(Points[0].Y - circleCenter.Y, Points[0].X - circleCenter.X);
            startAngle = (float)Math.Atan2(Y - circleCenter.Y, X - circleCenter.X);
            endAngle = (float)Math.Atan2(Points[1].Y - circleCenter.Y, Points[1].X - circleCenter.X);

            bool isInside(float a, float b, float c) => (b > a && b < c) || (b < a && b > c);
            if (!isInside(startAngle, midAngle, endAngle))
            {
                if (Math.Abs(startAngle + TWO_PI - endAngle) < TWO_PI && isInside(startAngle + TWO_PI, midAngle, endAngle))
                    startAngle += TWO_PI;
                else if (Math.Abs(startAngle - (endAngle + TWO_PI)) < TWO_PI && isInside(startAngle, midAngle, endAngle + TWO_PI))
                    endAngle += TWO_PI;
                else if (Math.Abs(startAngle - TWO_PI - endAngle) < TWO_PI && isInside(startAngle - TWO_PI, midAngle, endAngle))
                    startAngle -= TWO_PI;
                else if (Math.Abs(startAngle - (endAngle - TWO_PI)) < TWO_PI && isInside(startAngle, midAngle, endAngle - TWO_PI))
                    endAngle -= TWO_PI;
            }

            circleRadius = (float)Math.Sqrt(Math.Pow(X - circleCenter.X, 2) + Math.Pow(Y - circleCenter.Y, 2));
            float arcAngle = (float)PixelLength / circleRadius;
            endAngle = endAngle > startAngle ? startAngle + arcAngle : startAngle - arcAngle;
            }

        protected override Vec2Float CalculateOffset(int currentTime)
        {
            float currAngle = startAngle + (endAngle - startAngle) * GetTimeDiff(currentTime) / PathTime;
            return new Vec2Float(circleCenter.X - X + circleRadius * (float)Math.Cos(currAngle), circleCenter.Y - Y + circleRadius * (float)Math.Sin(currAngle));
        }
    }
}
