using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        
        [Input("Tessellate", IsBang = true)]
        public ISpread<bool> FTess;

        [Output("Output")]
        public ISpread<ISpread<Vector2D>> FOut;

        public void Evaluate(int SpreadMax)
        {
            if (FTess.IsChanged)
            {
                FContours.Sync();
                FWinding.Sync();
                FOut.SliceCount = FContours.SliceCount;
                for (int i = 0; i < FContours.SliceCount; i++)
                {
                    if (FTess[i])
                    {
                        var polygon = new VPolygon {
                            Winding = FWinding[i]
                        };
                        for (int j = 0; j < FContours[i].SliceCount; j++)
                        {
                            if(FContours[i][j] != null)
                                polygon.Contours.Add(FContours[i][j]);
                        }
                        polygon.Tessellate();
                        FOut[i].SliceCount = polygon.Triangles.Length * 3;
                        for (int j = 0; j < polygon.Triangles.Length; j++)
                        {
                            FOut[i][j * 3 + 0] = polygon[j][0];
                            FOut[i][j * 3 + 1] = polygon[j][1];
                            FOut[i][j * 3 + 2] = polygon[j][2];
                        }
                    }
                }
            }
        }
    }
}
