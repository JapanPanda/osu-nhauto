using osu.Shared;

namespace osu_nhauto.HitObjects
{
    public abstract class HitObject
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Time { get; private set; }
        public int EndTime { get; protected set; }
        public int StackHeight { get; protected set; }
        public HitObjectType Type { get; private set; }

        protected HitObject(osu_database_reader.Components.HitObjects.HitObject hollyObj)
        {
            X = hollyObj.X;
            Y = hollyObj.Y;
            Time = hollyObj.Time;
            Type = hollyObj.Type & (HitObjectType)0b1000_1011;
            StackHeight = 0;
        }
    }
}
