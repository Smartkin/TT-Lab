using System.Collections.Generic;
using Twinsanity.TwinsanityInterchange.Common;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout
{
    public interface ITwinPath : ITwinItem
    {
        /// <summary>
        /// The control points of the path's uniform cubic B-spline
        /// </summary>
        List<Vector4> PointList { get; set; }
        /// <summary>
        /// Every segment's arc length from the start of the path (<see cref="TwinPathParameters"/>)
        /// </summary>
        List<System.Single> ArcLengths { get; set; }
        /// <summary>
        /// 1 over the steps every segment takes
        /// </summary>
        List<System.Single> InverseSteps { get; set; }
    }
}
