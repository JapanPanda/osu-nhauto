namespace osu_nhauto.HitObjects
{
    public class HitObjectSpinner : HitObject
    {
        public HitObjectSpinner(osu_database_reader.Components.HitObjects.HitObjectSpinner hollyObj) : base(hollyObj, false)
        {
            EndTime = hollyObj.EndTime;
        }
    }
}
