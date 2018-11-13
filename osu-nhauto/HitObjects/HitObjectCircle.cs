namespace osu_nhauto.HitObjects
{
    public class HitObjectCircle : HitObject
    {
        public HitObjectCircle(osu_database_reader.Components.HitObjects.HitObjectCircle hollyObj) : base(hollyObj)
        {
            EndTime = hollyObj.Time;
        }
    }
}
