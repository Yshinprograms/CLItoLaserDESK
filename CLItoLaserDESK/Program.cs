// File: CLItoLaserDESK\Program.cs

using System;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using CLItoLaserDESK.Core;
using CLItoLaserDESK.Core.Models;
using CLItoLaserDESK.LaserDeskAPI;  // To access ILaserDeskService and LaserDeskService

namespace CLItoLaserDESK
{
    internal class Program {
        static async Task Main(string[] args) {
            Console.WriteLine("LaserDESK CLI File Processor");
            Console.WriteLine("----------------------------");

            // --- Configuration ---
            string colainExecutablePath = "colain_parser.exe";
            string cliInputFilePath = "test.cli";
            bool isLongCliFormat = true;
            string dxfOutputDirectory = Path.Combine(AppContext.BaseDirectory, "DXF_Output");
            string jsonOutputDirectory = Path.Combine(AppContext.BaseDirectory, "JSON_Output");

            // LaserDESK Connection Parameters (defaults to 127.0.0.1:3000 in LaserDeskService)
            string laserDeskIpAddress = "127.0.0.1";
            int laserDeskPort = 3000;
            // --- End Configuration ---

            if (!File.Exists(colainExecutablePath)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FATAL ERROR: Rust parser executable not found: '{colainExecutablePath}'");
                Console.WriteLine("Please ensure the path is correct and the Rust parser has been built.");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit."); Console.ReadKey();
                return;
            }
            if (!File.Exists(cliInputFilePath)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FATAL ERROR: CLI input file not found: '{cliInputFilePath}'");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit."); Console.ReadKey();
                return;
            }

            ILaserDeskService laserDeskService = null; // Declare here for use in finally block

            try {
                // --- Step 0: Connect to laserDESK ---
                Console.WriteLine("\n--- Connecting to laserDESK ---");
                laserDeskService = new LaserDeskService(); // Create instance
                if (laserDeskService.Connect(laserDeskIpAddress, laserDeskPort)) {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Successfully connected to laserDESK and entered Remote Mode.");
                    Console.WriteLine($"  LaserDESK Version: {laserDeskService.GetLaserDeskVersion()}");
                    Console.WriteLine($"  LaserDESK Status (hex): 0x{laserDeskService.GetLaserDeskStatus():X8}");
                    Console.WriteLine($"  Is Remote Mode Active: {laserDeskService.IsRemoteModeActive()}");
                    Console.ResetColor();
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed to connect to laserDESK. Please ensure laserDESK is running and configured for remote control.");
                    Console.ResetColor();
                    Console.WriteLine("Press any key to exit."); Console.ReadKey();
                    return; // Exit if connection fails
                }

                // --- Step 1: Parse CLI file using the external Rust parser ---
                CliParserRunner parserRunner = new CliParserRunner(colainExecutablePath);
                Console.WriteLine($"\nAttempting to parse '{Path.GetFileName(cliInputFilePath)}' using '{Path.GetFileName(colainExecutablePath)}'...");

                ParserOutput parserResult = await parserRunner.ParseCliFileAsync(cliInputFilePath, isLongCliFormat);
                ParsedCliFile parsedData = parserResult.ParsedData; // Extract the parsed C# objects
                string rawJson = parserResult.RawJsonOutput;      // Extract the raw JSON string

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nSuccessfully parsed CLI file and deserialized JSON into C# objects!");
                Console.ResetColor();

                if (!Directory.Exists(jsonOutputDirectory)) {
                    Directory.CreateDirectory(jsonOutputDirectory);
                    Console.WriteLine($"Created JSON output directory: {jsonOutputDirectory}");
                }
                string jsonOutputFileName = $"{Path.GetFileNameWithoutExtension(cliInputFilePath)}_parsed.json";
                string fullJsonPath = Path.Combine(jsonOutputDirectory, jsonOutputFileName);
                try {
                    await File.WriteAllTextAsync(fullJsonPath, rawJson); // Asynchronously write the JSON
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"Successfully saved raw JSON output to: {fullJsonPath}");
                    Console.ResetColor();
                } catch (Exception jsonEx) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Could not save raw JSON output to '{fullJsonPath}': {jsonEx.Message}");
                    Console.ResetColor();
                }


                // Display some of the captured data from parsing
                Console.WriteLine("\n--- Header Information (from CLI file) ---");
                Console.WriteLine($"  Binary: {parsedData.Header.Binary}");
                Console.WriteLine($"  Units: {parsedData.Header.Units}");
                Console.WriteLine($"  Version: {parsedData.Header.Version}");
                Console.WriteLine($"  Aligned: {parsedData.Header.Aligned}");
                Console.WriteLine($"  Declared Layers in Header: {parsedData.Header.Layers?.ToString() ?? "N/A"}");

                Console.WriteLine($"\n--- Layer Information (from CLI file) ---");
                Console.WriteLine($"  Total Layers Parsed: {parsedData.Layers.Count}");

                if (parsedData.Layers.Count > 0) {
                    CliLayer sampleLayer = parsedData.Layers[0];
                    Console.WriteLine($"  Sample Layer 0 (Index 0) Height: {sampleLayer.Height}");
                    Console.WriteLine($"    Loops: {sampleLayer.Loops.Count}");
                    Console.WriteLine($"    Hatches: {sampleLayer.Hatches.Count}");

                    if (sampleLayer.Loops.Count > 0 && sampleLayer.Loops[0].Points.Count >= 2) {
                        Console.WriteLine($"    First loop's first point (X,Y): ({sampleLayer.Loops[0].Points[0]}, {sampleLayer.Loops[0].Points[1]})");
                    }
                }

                // --- Step 2: Generate DXF Files per Layer ---
                if (parsedData.Layers.Count > 0) {
                    Console.WriteLine("\n\n--- Generating DXF Files per Layer ---");

                    if (!Directory.Exists(dxfOutputDirectory)) {
                        Directory.CreateDirectory(dxfOutputDirectory);
                        Console.WriteLine($"Created DXF output directory: {dxfOutputDirectory}");
                    }

                    DxfGenerator dxfGenerator = new DxfGenerator();

                    for (int i = 0; i < parsedData.Layers.Count; i++) {
                        CliLayer currentLayer = parsedData.Layers[i];

                        string layerHeightStr = currentLayer.Height.ToString("F3", CultureInfo.InvariantCulture).Replace('.', '_');
                        string dxfFileName = $"Layer_{i:D4}.dxf";
                        string fullDxfPath = Path.Combine(dxfOutputDirectory, dxfFileName);

                        Console.WriteLine($"  Generating DXF for Layer {i} (Height: {currentLayer.Height}). Output: {Path.GetFileName(fullDxfPath)}");

                        try {
                            bool success = dxfGenerator.GenerateDxfForLayer(currentLayer, fullDxfPath);
                            if (success) {
                                Console.ForegroundColor = ConsoleColor.DarkGreen;
                                Console.WriteLine($"    Successfully generated: {Path.GetFileName(fullDxfPath)}");
                                Console.ResetColor();

                            } else {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"    Warning: DXF generation for {Path.GetFileName(fullDxfPath)} reported issues (Save returned false).");
                                Console.ResetColor();
                            }
                        } catch (Exception dxfEx) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Error.WriteLine($"    ERROR generating DXF for Layer {i} (File: {Path.GetFileName(fullDxfPath)}): {dxfEx.Message}");
                            Console.ResetColor();
                        }
                    }
                    Console.WriteLine($"\nDXF generation process complete. Files intended for: {dxfOutputDirectory}");
                } else {
                    Console.WriteLine("\nNo layers found in parsed data to generate DXF files for.");
                }
            } catch (FileNotFoundException fnfEx) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR (File Not Found): {fnfEx.Message}");
                Console.ResetColor();
            } catch (Exception ex) // Catch-all for other exceptions from parsing or laserDESK connection
              {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nAN UNEXPECTED ERROR OCCURRED:");
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            } finally {
                if (laserDeskService != null && laserDeskService.IsRemoteModeActive()) // Check if it was successfully connected
                {
                    Console.WriteLine("\n--- Disconnecting from laserDESK ---");
                    laserDeskService.Disconnect();
                } else if (laserDeskService != null) { // Was instantiated but maybe not fully connected/in remote mode
                    Console.WriteLine("\n--- Ensuring laserDESK disconnection (if partially connected) ---");
                    laserDeskService.Disconnect(); // Attempt disconnect anyway
                }
                Console.WriteLine("\nApplication finished. Press any key to exit.");
                Console.ReadKey();
            }
        }
    }
}