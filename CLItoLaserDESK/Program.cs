// File: CLItoLaserDESK\Program.cs

using System;
using System.IO;
using System.Threading.Tasks;
using System.Globalization; // For CultureInfo in DXF filename formatting
using CLItoLaserDESK.Core;    // To access CliParserRunner
using CLItoLaserDESK.Core.Models; // To access ParsedCliFile and other models

namespace CLItoLaserDESK // Your main console application's namespace
{
    internal class Program {
        static async Task Main(string[] args) {
            Console.WriteLine("LaserDESK CLI File Processor");
            Console.WriteLine("----------------------------");

            // --- Configuration ---
            string colainExecutablePath = "colain_parser.exe";
            string cliInputFilePath = "complex.cli";
            bool isLongCliFormat = true;
            string dxfOutputDirectory = Path.Combine(AppContext.BaseDirectory, "DXF_Output");
            string jsonOutputDirectory = Path.Combine(AppContext.BaseDirectory, "JSON_Output"); // Output JSONs
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

            try {
                // --- Step 1: Parse CLI file using the external Rust parser ---
                CliParserRunner parserRunner = new CliParserRunner(colainExecutablePath);
                Console.WriteLine($"\nAttempting to parse '{Path.GetFileName(cliInputFilePath)}' using '{Path.GetFileName(colainExecutablePath)}'...");

                // MODIFICATION 3: Receive ParserOutput
                ParserOutput parserResult = await parserRunner.ParseCliFileAsync(cliInputFilePath, isLongCliFormat);
                ParsedCliFile parsedData = parserResult.ParsedData; // Extract the parsed C# objects
                string rawJson = parserResult.RawJsonOutput;      // Extract the raw JSON string

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nSuccessfully parsed CLI file and deserialized JSON into C# objects!");
                Console.ResetColor();

                // MODIFICATION 4: Ensure JSON output directory exists and save the raw JSON
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
                Console.WriteLine("\n--- Header Information ---");
                Console.WriteLine($"  Binary: {parsedData.Header.Binary}");
                Console.WriteLine($"  Units: {parsedData.Header.Units}");
                Console.WriteLine($"  Version: {parsedData.Header.Version}");
                Console.WriteLine($"  Aligned: {parsedData.Header.Aligned}");
                Console.WriteLine($"  Declared Layers in Header: {parsedData.Header.Layers?.ToString() ?? "N/A"}");

                Console.WriteLine($"\n--- Layer Information ---");
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
                    // JSON directory creation moved up

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
                                Console.WriteLine($"    Warning: DXF generation for {Path.GetFileName(fullDxfPath)} might have had issues (Save returned false).");
                                Console.ResetColor();
                            }
                        } catch (Exception dxfEx) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Error.WriteLine($"    ERROR generating DXF for Layer {i} (File: {Path.GetFileName(fullDxfPath)}): {dxfEx.Message}");
                            Console.ResetColor();
                        }
                    }
                    Console.WriteLine($"\nDXF generation process complete. Files intended to be saved in: {dxfOutputDirectory}");
                } else {
                    Console.WriteLine("\nNo layers found in parsed data to generate DXF files for.");
                }
            } catch (FileNotFoundException fnfEx) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR (File Not Found): {fnfEx.Message}");
                Console.ResetColor();
            } catch (Exception ex) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nAN UNEXPECTED ERROR OCCURRED:");
                Console.WriteLine(ex.ToString());
                Console.ResetColor();
            }

            Console.WriteLine("\nApplication finished. Press any key to exit.");
            Console.ReadKey();
        }
    }
}