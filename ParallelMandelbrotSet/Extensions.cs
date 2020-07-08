using System;
using System.Collections.Generic;
using System.Text;

namespace ParallelMandelbrotSet
{
    public static class Extensions
    {
        public static double Map(this double value, double fromSource, double toSource, double fromTarget, double toTarget)
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }
    }
}
