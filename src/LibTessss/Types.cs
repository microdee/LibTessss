using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LibTessDotNet;
using VVVV.Core;
using VVVV.Utils.VMath;

namespace LibTessss
{
    /// <summary>
    /// Encapsulates multiple vertices to form a contour
    /// </summary>
    public class VContour
    {
        /// <summary>
        /// Public array containing vertices
        /// </summary>
        public ContourVertex[] Contour;
        /// <summary>
        /// See LibTessDotNet orientations
        /// </summary>
        public ContourOrientation Orientation = ContourOrientation.Original;

        public ContourVertex this[int i]
        {
            get { return Contour[i]; }
            set { Contour[i] = value; }
        }
    }

    public class NeighborSearchThreadReadyEventArgs : EventArgs
    {
        // sender: VPolygon
        public int ThreadID;
    }
    public class NeighborsFoundEventArgs : EventArgs
    {
        // sender: VPolygon
    }
    /// <summary>
    /// Encapsulates LibTessDotNet tessellator and helper methods
    /// </summary>
    public class VPolygon
    {
        /// <summary>
        /// List of Contours will be used by Tessellate()
        /// </summary>
        public List<VContour> Contours { get; private set; } = new List<VContour>();
        public Tess Tessellator { get; private set; } = new Tess();
        public WindingRule Winding = WindingRule.EvenOdd;
        /// <summary>
        /// ID Assigned by user
        /// </summary>
        public int ID = 0;

        /// <summary>
        /// End result of tessellation is stored here
        /// </summary>
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

        public event EventHandler<NeighborSearchThreadReadyEventArgs> OnNeighborSearchThreadReady;
        public event EventHandler<NeighborsFoundEventArgs> OnNeighborsFound;
        public bool NeighborsFound { get; private set; } = false;
        public bool SearchingNeighbors { get; private set; } = false;
        public bool[] NeighborSearchThreadsReady { get; private set; }

        public void GenerateNeighborsParallel(int threads, double epsilon = 0.00001)
        {
            if(SearchingNeighbors) return;
            SearchingNeighbors = true;
            NeighborSearchThreadsReady = new bool[threads];
            for (int i = 0; i < threads; i++)
            {
                var threadin = new[] {i, threads, epsilon};
                Task.Factory.StartNew(o =>
                {
                    // fetch inputs
                    var indata = (double[]) o;
                    var ii = (int)indata[0];
                    var threadsl = (int)indata[1];
                    var epsl = indata[2];

                    // get sections
                    var tricount = Triangles.Length;
                    var sectionsize = (int) Math.Ceiling((double) tricount/threadsl);
                    var sectionoffs = ii*sectionsize;
                    // for given section
                    for (int j = sectionoffs; j < sectionoffs + sectionsize; j++)
                    {
                        if (j > Triangles.Length - 1) break;
                        var reftri = Triangles[j];
                        // compare every triangle to reference
                        for (int k = 0; k < tricount; k++)
                        {
                            var currtri = Triangles[k];
                            if (currtri.ID == reftri.ID) continue;
                            // for each vertex in reference triangle
                            for (int rtv = 0; rtv < 3; rtv++)
                            {
                                if (reftri.Neighbors[rtv] >= 0) continue;
                                int svc = 0;

                                // compare first opposite reference vertex to current vertices
                                int cvai = -1;
                                var rva = reftri[rtv + 1];
                                for (int ctv = 0; ctv < 3; ctv++)
                                {
                                    if (VMath.Dist(rva, currtri[ctv]) < epsl)
                                    {
                                        cvai = ctv;
                                        svc++;
                                        break;
                                    }
                                }

                                // second opposite reference vertex
                                int cvbi = -1;
                                var rvb = reftri[rtv + 2];
                                for (int ctv = 0; ctv < 3; ctv++)
                                {
                                    if (VMath.Dist(rvb, currtri[ctv]) < epsl)
                                    {
                                        cvbi = ctv;
                                        svc++;
                                        break;
                                    }
                                }

                                // if 2 opposite vertices have a pair with damn close position
                                // chances are high current triangle neighboring the reference
                                // also setting the neighboring for current triangle
                                if (svc >= 2)
                                {
                                    reftri.Neighbors[rtv] = currtri.ID;
                                    for (int ctn = 0; ctn < 3; ctn++)
                                    {
                                        if ((ctn == cvai) || (ctn == cvbi)) continue;
                                        currtri.Neighbors[ctn] = reftri.ID;
                                    }
                                }
                            }
                        }
                    }

                    // tell the others about the result
                    NeighborSearchThreadsReady[ii] = true;
                    OnNeighborSearchThreadReady?.Invoke(this, new NeighborSearchThreadReadyEventArgs {ThreadID = ii});
                    if(NeighborSearchThreadsReady.All(ready => ready))
                    {
                        NeighborsFound = true;
                        SearchingNeighbors = false;
                        OnNeighborsFound?.Invoke(this, new NeighborsFoundEventArgs());
                    }
                }, threadin);
            }
        }
    }

    public class VTriangle
    {
        public Vector2D[] Vertices { get; private set; } = new Vector2D[3];
        public int ID;
        public int[] Neighbors { get; private set; } = new int[3];

        public Vector2D this[int i]
        {
            get { return Vertices[i%3]; }
            set { Vertices[i%3] = value; }
        }

        public VTriangle()
        {
            for (int i = 0; i < 3; i++)
            {
                Neighbors[i] = -1;
            }
        }
    }
}
