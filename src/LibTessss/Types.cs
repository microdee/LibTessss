using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LibTessDotNet;
using VVVV.Utils.VMath;

namespace LibTessss
{
    public class VContour
    {
        public ContourVertex[] Contour;
        public ContourOrientation Orientation = ContourOrientation.Original;

        public ContourVertex this[int i]
        {
            get { return Contour[i]; }
            set { Contour[i] = value; }
        }
    }
    public class VPolygon
    {
        public List<VContour> Contours { get; private set; } = new List<VContour>();
        public Tess Tessellator { get; private set; } = new Tess();
        public WindingRule Winding = WindingRule.EvenOdd;

        public VTriangle[] Triangles { get; private set; }

        public void Tessellate()
        {
            Tessellator.NoEmptyPolygons = true;
            foreach (var c in Contours)
            {
                Tessellator.AddContour(c.Contour, c.Orientation);
            }
            Tessellator.Tessellate(Winding, ElementType.Polygons, 3);
            Triangles = new VTriangle[Tessellator.ElementCount];
            for (int i = 0; i < Tessellator.ElementCount; i++)
            {
                var v0 = Tessellator.Vertices[Tessellator.Elements[i * 3]];
                var v1 = Tessellator.Vertices[Tessellator.Elements[i * 3 + 1]];
                var v2 = Tessellator.Vertices[Tessellator.Elements[i * 3 + 2]];

                Triangles[i] = new VTriangle
                {
                    ID = i,
                    [0] = new Vector2D(v0.Position.X, v0.Position.Y),
                    [1] = new Vector2D(v1.Position.X, v1.Position.Y),
                    [2] = new Vector2D(v2.Position.X, v2.Position.Y)
                };
            }
        }
        public VTriangle this[int i] => Triangles[i];
    }

    public class VTriangle
    {
        public Vector2D[] Vertices { get; private set; } = new Vector2D[3];
        public int ID;

        public Vector2D this[int i]
        {
            get { return Vertices[i%3]; }
            set { Vertices[i%3] = value; }
        }
    }
}
