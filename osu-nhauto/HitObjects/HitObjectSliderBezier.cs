using osu_database_reader.Components.Beatmaps;
using System.Collections.ObjectModel;

namespace osu_nhauto.HitObjects
{
    public class HitObjectSliderBezier : HitObjectSlider
    {
        public HitObjectSliderBezier(osu_database_reader.Components.HitObjects.HitObjectSlider hollyObj, float sliderVelocity,
            System.Collections.Generic.List<osu_database_reader.Components.Beatmaps.TimingPoint> timingPoints) : base(hollyObj, sliderVelocity, timingPoints)
        {

        }

        public override Vec2Float GetPointAt(int currentTime)
        {
            return new Vec2Float(0, 0);
        }
    }
}
