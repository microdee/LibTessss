using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibTessDotNet;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;

namespace LibTessss.Nodes
{
    [PluginInfo(
        Name = "Contours",
        Category = "LibTessss",
        Author = "microdee",
        Help = "Prepare contours for LibTessss",
        Tags = "Triangulate, Polygons",
        AutoEvaluate = true
    )]
    public class LibTessssContourNode : IPluginEvaluate
    {
        [Input("Points ")]
        public ISpread<ISpread<Vector2D>> FIn;

        [Input("Orientation", DefaultEnumEntry = "Original")]
        public ISpread<ContourOrientation> FOrientation;

        [Output("Output")]
        public ISpread<VContour> FOut;

        public void Evaluate(int SpreadMax)
        {
            if (FIn.IsChanged)
            {
                FOut.SliceCount = FIn.SliceCount;
                for (int i = 0; i < FIn.SliceCount; i++)
                {
                    var cnt = new VContour {
                        Contour = new ContourVertex[FIn[i].SliceCount],
                        Orientation = FOrientation[i]
                    };
                    for (int j = 0; j < FIn[i].SliceCount; j++)
                    {
                        cnt[j] = new ContourVertex {
                            Position = new Vec3 {
                                X = (float) FIn[i][j].x,
                                Y = (float) FIn[i][j].y
                            }
                        };
                    }
                    FOut[i] = cnt;
                }
            }
        }
    }

    [PluginInfo(
        Name = "LibTessss",
        Category = "2d",
        Author = "microdee",
        Help = "Triangulate using LibTessDotNet",
        Tags = "Triangulate, Polygons"
    )]
    public class LibTessssNode : IPluginEvaluate
    {
        [Input("Contours", AutoValidate = false)]
        public ISpread<ISpread<VContour>> FContours;

        [Input("Winding Rule", DefaultEnumEntry = "EvenOdd", AutoValidate = false)]
        public ISpread<WindingRule> FWinding;

        [Input("Search Neighbors")]
        public ISpread<bool> FSearchNeighbors;
        [Input("Neighbor Generating Thread Count", DefaultValue = 4)]
        public ISpread<int> FSearchNeighborsThreads;
        [Input("Tessellate", IsBang = true)]
        public ISpread<bool> FTess;

        [Output("Polygons")]
        public ISpread<VPolygon> FPolygons;
        [Output("Vertices")]
        public ISpread<ISpread<Vector2D>> FOut;
        [Output("Neighbor On Opposite Edge")]
        public ISpread<ISpread<int>> FNeighbor;

        EventWaitHandle WaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        public void Evaluate(int SpreadMax)
        {
            if (FTess.IsChanged)
            {
                FContours.Sync();
                FWinding.Sync();
                FOut.SliceCount = FContours.SliceCount;
                FPolygons.SliceCount = FContours.SliceCount;
                FNeighbor.SliceCount = FContours.SliceCount;
                for (int i = 0; i < FContours.SliceCount; i++)
                {
                    if (FTess[i])
                    {
                        if (FPolygons[i] != null)
                        {
                            if(FPolygons[i].SearchingNeighbors) continue;
                        }
                        FPolygons[i] = new VPolygon
                        {
                            Winding = FWinding[i],
                            ID = i
                        };
                        for (int j = 0; j < FContours[i].SliceCount; j++)
                        {
                            if(FContours[i][j] != null)
                                FPolygons[i].Contours.Add(FContours[i][j]);
                        }
                        FPolygons[i].Tessellate();
                        FOut[i].SliceCount = FPolygons[i].Triangles.Length * 3;
                        for (int j = 0; j < FPolygons[i].Triangles.Length; j++)
                        {
                            FOut[i][j * 3 + 0] = FPolygons[i][j][0];
                            FOut[i][j * 3 + 1] = FPolygons[i][j][1];
                            FOut[i][j * 3 + 2] = FPolygons[i][j][2];
                        }
                        if (FSearchNeighbors[i])
                        {
                            FPolygons[i].OnNeighborsFound += (sender, args) =>
                            {
                                var polygon = (VPolygon)sender;
                                FNeighbor[polygon.ID].SliceCount = FOut[polygon.ID].SliceCount;
                                for (int j = 0; j < polygon.Triangles.Length; j++)
                                {
                                    FNeighbor[polygon.ID][j * 3 + 0] = polygon[j].Neighbors[0];
                                    FNeighbor[polygon.ID][j * 3 + 1] = polygon[j].Neighbors[1];
                                    FNeighbor[polygon.ID][j * 3 + 2] = polygon[j].Neighbors[2];
                                }
                                WaitHandle.Set();
                            };
                            FPolygons[i].GenerateNeighborsParallel(FSearchNeighborsThreads[i]);
                            WaitHandle.WaitOne();
                        }
                        else
                        {

                            FNeighbor[i].SliceCount = 0;
                        }
                    }
                }
            }
        }
    }
}
