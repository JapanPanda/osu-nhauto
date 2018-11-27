﻿using osu.Shared;

namespace osu_nhauto.HitObjects
{
    public abstract class HitObject
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Time { get; private set; }
        public int EndTime { get; protected set; }
        public int StackHeight { get; set; }
        public HitObjectType Type { get; private set; }
        public bool Streamable { get; set; }

        protected HitObject(osu_database_reader.Components.HitObjects.HitObject hollyObj, bool vInvert)
        {
            if (vInvert)
                hollyObj.Y = 384 - hollyObj.Y;

            X = hollyObj.X;
            Y = hollyObj.Y;
            Time = hollyObj.Time;
            Type = hollyObj.Type & (HitObjectType)0b1000_1011;
            StackHeight = 0;
        }
    }
}
