﻿#region Usings

using Emgu.CV.Util;

#endregion

namespace DartboardRecognition.Services
{
    public class DartContour
    {
        public VectorOfPoint ContourPoints { get; }
        public double Arc { get; }

        public DartContour(VectorOfPoint points, double arc)
        {
            ContourPoints = new VectorOfPoint();
            ContourPoints.Push(points);
            Arc = arc;
        }
    }
}