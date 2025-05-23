using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CLItoLaserDESK.Core.Models {
    public class PolylineData 
    {
        public List<Point2D> Vertices { get; set; } = new List<Point2D>();
        public bool IsClosed { get; set; } // From CLI: whether it forms a closed loop
        // public string LayerName { get; set; } // Optional: if CLI has layer names per polyline
        // public int ColorIndex { get; set; } // Optional: if CLI has color info
    }
}
