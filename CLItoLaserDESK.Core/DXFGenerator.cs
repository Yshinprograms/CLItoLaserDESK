// File: CLItoLaserDESK.Core\DxfGenerator.cs

using netDxf;
using netDxf.Entities; // Polyline2D, Polyline2DVertex, Line, Vector3 are here
using netDxf.Header;
using netDxf.Tables;
using System;
using System.Collections.Generic;
using System.Globalization;
using CLItoLaserDESK.Core.Models;

namespace CLItoLaserDESK.Core {
    public class DxfGenerator {
        public bool GenerateDxfForLayer(CliLayer layerData, string outputFilePath) {
            if (layerData == null) {
                throw new ArgumentNullException(nameof(layerData));
            }
            if (string.IsNullOrWhiteSpace(outputFilePath)) {
                throw new ArgumentNullException(nameof(outputFilePath));
            }

            DxfDocument dxf = new DxfDocument();

            // string dxfLayerName = $"CLI_Layer_Z{layerData.Height.ToString("F3", CultureInfo.InvariantCulture).Replace('.', '_')}";
            // netDxf.Tables.Layer layerForDxf = new netDxf.Tables.Layer(dxfLayerName) { Color = AciColor.White };
            // dxf.Layers.Add(layerForDxf);

            if (layerData.Loops != null) {
                foreach (CliLoop loopData in layerData.Loops) {
                    if (loopData.Points == null || loopData.Points.Count < 4) {
                        Console.WriteLine($"[DxfGenerator] Skipping empty or invalid loop in layer Z={layerData.Height}. Loop ID: {loopData.Id}");
                        continue;
                    }

                    // Use the new class names
                    List<Polyline2DVertex> vertices = new List<Polyline2DVertex>();
                    for (int i = 0; i < loopData.Points.Count; i += 2) {
                        if (i + 1 < loopData.Points.Count) {
                            float x = loopData.Points[i];
                            float y = loopData.Points[i + 1];
                            vertices.Add(new Polyline2DVertex(x, y)); // Use new class name
                        }
                    }

                    if (vertices.Count > 1) {
                        bool isClosed = false;
                        if (vertices.Count > 2) {
                            Polyline2DVertex first = vertices[0];       // Use new class name
                            Polyline2DVertex last = vertices[vertices.Count - 1]; // Use new class name
                            double tolerance = 1e-6;
                            if (Math.Abs(first.Position.X - last.Position.X) < tolerance &&
                                Math.Abs(first.Position.Y - last.Position.Y) < tolerance) {
                                isClosed = true;
                            }
                        }
                        Polyline2D polyline2D = new Polyline2D(vertices, isClosed); // Use new class name
                        // polyline2D.Layer = layerForDxf;
                        dxf.Entities.Add(polyline2D);
                    }
                }
            }

            if (layerData.Hatches != null) {
                foreach (CliHatches hatchSetData in layerData.Hatches) {
                    if (hatchSetData.Points == null || hatchSetData.Points.Count < 4) {
                        Console.WriteLine($"[DxfGenerator] Skipping empty or invalid hatch set in layer Z={layerData.Height}. Hatch ID: {hatchSetData.Id}");
                        continue;
                    }
                    for (int i = 0; i < hatchSetData.Points.Count; i += 4) {
                        if (i + 3 < hatchSetData.Points.Count) {
                            float sx = hatchSetData.Points[i];
                            float sy = hatchSetData.Points[i + 1];
                            float ex = hatchSetData.Points[i + 2];
                            float ey = hatchSetData.Points[i + 3];
                            Line dxfLine = new Line(new Vector3(sx, sy, 0), new Vector3(ex, ey, 0));
                            // dxfLine.Layer = layerForDxf;
                            dxf.Entities.Add(dxfLine);
                        }
                    }
                }
            }
            // dxf.DrawingVariables.InsUnits = DrawingUnits.Millimeters;

            bool saveSuccess = dxf.Save(outputFilePath);
            if (!saveSuccess) {
                Console.Error.WriteLine($"[DxfGenerator] Warning: netDxf.Save() returned false for '{outputFilePath}'.");
                return false;
            }
            return true;
        }
    }
}