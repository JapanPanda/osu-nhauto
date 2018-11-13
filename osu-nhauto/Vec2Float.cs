using System;

namespace osu_nhauto
{
    public struct Vec2Float
    {
        public float X;
        public float Y;

        public Vec2Float(float f, float f1)
        {
            X = f;
            Y = f1;
        }

        public Vec2Float Add(float f, float f1)
        {
            X += f;
            Y += f1;
            return this;
        }

        public Vec2Float Add(Vec2Float v) => Add(v.X, v.Y);

        public Vec2Float Subtract(float f, float f1)
        {
            X -= f;
            Y -= f1;
            return this;
        }

        public Vec2Float Subtract(Vec2Float v) => Subtract(v.X, v.Y);

        public Vec2Float Normal()
        {
            float tempX = X;
            X = Y;
            Y = -tempX;
            return this;
        }

        public float Distance(float x, float y) => (float)Math.Sqrt(Math.Pow(X - x, 2) + Math.Pow(Y - y, 2));

        public float Distance(Vec2Float v) => Distance(v.X, v.Y);

        public Vec2Float Clone() => new Vec2Float(X, Y);

        public static Vec2Float Intersect(Vec2Float a, Vec2Float da, Vec2Float b, Vec2Float db)
        {
            float det = db.X * da.Y - db.Y * da.X;
            if (det == 0)
                throw new Exception("Vectors are parallel.");

            float u = ((b.Y - a.Y) * da.X + (a.X - b.X) * da.Y) / det;
            return b.Clone().Add(db.X * u, db.Y * u);
        }
    }
}