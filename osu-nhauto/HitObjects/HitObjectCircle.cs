namespace osu_nhauto.HitObjects
{
    public class HitObjectCircle : HitObject
    {
        public HitObjectCircle(osu_database_reader.Components.HitObjects.HitObjectCircle hollyObj, bool vInvert) : base(hollyObj, vInvert)
        {
            EndTime = hollyObj.Time;
        }

        public HitObjectCircle(HitObjectSliderPerfect nhautoSliderPerfect, bool vInvert) : base(new osu_database_reader.Components.HitObjects.HitObjectCircle(), vInvert)
        {
            X = nhautoSliderPerfect.X;
            Y = nhautoSliderPerfect.Y;
            Time = nhautoSliderPerfect.Time;
            EndTime = nhautoSliderPerfect.EndTime;
            Type = osu.Shared.HitObjectType.Normal;
        }
    }
}
