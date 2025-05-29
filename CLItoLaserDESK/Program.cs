// File: CLItoLaserDESK\Program.cs
using System;
using System.IO;
using System.Threading.Tasks;
using CLItoLaserDESK.LaserDeskAPI; // Import the API namespace

namespace CLItoLaserDESK {
    internal class Program {
        // Configuration (can be loaded from a file or appsettings.json later)
        // Keep your existing config for colain_parser, CLI input, DXF/JSON output directories.
        static string laserDeskIpAddress = "127.0.0.1";
        static int laserDeskPort = 3000;
        static string templateJobPath = @"C:\\ProgramData\\Scanlab\\SLLaserDesk\\HardwareConfiguration.sld"; // IMPORTANT: Update this path
        static string dxfToMarkPath = @"C:\\Users\\pin20\\Downloads\\SIMTech_Internship\\CLItoLaserDESK\\CLItoLaserDESK\\CLItoLaserDESK\\bin\\Debug\\net8.0\\DXF_Output\\Layer_0000.dxf";       // IMPORTANT: Update this path

        static async Task Main(string[] args) {
            Console.WriteLine("LaserDESK Automation Program (Refactored)");
            Console.WriteLine("---------------------------------------");

            // Validate paths (optional but good practice)
            if (!File.Exists(templateJobPath)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FATAL ERROR: Template job file not found: '{templateJobPath}'");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit."); Console.ReadKey(); return;
            }
            if (!File.Exists(dxfToMarkPath)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FATAL ERROR: DXF file to mark not found: '{dxfToMarkPath}'");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit."); Console.ReadKey(); return;
            }


            ILaserDeskService laserService = new LaserDeskService();

            try {
                // --- Connect to laserDESK ---
                Console.WriteLine("\nAttempting to connect to laserDESK...");
                if (!laserService.Connect(laserDeskIpAddress, laserDeskPort)) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed to connect to laserDESK. Ensure it's running and configured.");
                    Console.ResetColor();
                    return;
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Successfully connected to laserDESK.");
                Console.WriteLine($"  Version: {laserService.GetLaserDeskVersion()}");
                Console.WriteLine($"  Status: 0x{laserService.GetLaserDeskStatus():X8}");
                Console.WriteLine($"  Remote Mode Active: {laserService.IsRemoteModeActive()}");
                Console.ResetColor();

                // --- Open Template Job ---
                Console.WriteLine($"\nOpening template job: {Path.GetFileName(templateJobPath)}");
                if (!laserService.OpenJob(templateJobPath)) // saveOld defaults to false
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed to open template job.");
                    Console.ResetColor();
                    return;
                }
                Console.WriteLine("Template job opened.");
                Console.WriteLine($"  Status after job open: 0x{laserService.GetLaserDeskStatus():X8}");


                // --- Import DXF Layer ---
                string importUID = "MyFirstTrace";
                int optionFlags = 0x10; // enValues1to1 (use absolute coords from DXF)
                Console.WriteLine($"\nImporting DXF: {Path.GetFileName(dxfToMarkPath)} with UID: {importUID}");
                if (!laserService.ImportDxfLayer(dxfToMarkPath, importUID, optionFlags)) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed to import DXF layer.");
                    Console.ResetColor();
                    return;
                }
                Console.WriteLine("DXF layer imported.");
                Console.WriteLine($"  Status after import: 0x{laserService.GetLaserDeskStatus():X8}");

                // For Z=0 test, we are not calling SetGlobal3DTransformation explicitly,
                // relying on DXF Z-coordinates and the default machine Z setup.

                // --- Optional: Manual Verification Pause ---
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nPAUSE: Check laserDESK GUI. Is the DXF loaded correctly?");
                Console.WriteLine("Ensure all safety precautions are met (eyewear, enclosure, test material).");
                Console.WriteLine("Press Enter to START MARKING or Ctrl+C to abort.");
                Console.ResetColor();
                Console.ReadLine();

                // --- Start Marking ---
                Console.WriteLine("\nAttempting to start marking...");
                if (!laserService.StartMarking()) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Failed to start marking process. Check laser/RTC status.");
                    Console.Error.WriteLine($"  LaserDESK Status: 0x{laserService.GetLaserDeskStatus():X8}");
                    Console.ResetColor();
                    return;
                }
                Console.WriteLine("Marking command sent. Waiting for completion...");

                // --- Wait for Marking Completion ---
                bool markingSuccess = await laserService.WaitForMarkingCompletionAsync(30000); // 30-second timeout

                if (markingSuccess) {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Marking completed successfully!");
                    Console.ResetColor();
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("Marking failed or timed out.");
                    Console.ResetColor();
                }
                Console.WriteLine($"  Final Status: 0x{laserService.GetLaserDeskStatus():X8}");

            } catch (InvalidOperationException ioEx) // e.g., trying an operation when not connected
              {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Operation Error: {ioEx.Message}");
                Console.ResetColor();
            } catch (Exception ex) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"An unexpected error occurred: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
            } finally {
                Console.WriteLine("\nDisconnecting from laserDESK...");
                laserService.Dispose(); // This will call Disconnect()
                Console.WriteLine("Application finished. Press any key to exit.");
                Console.ReadKey();
            }
        }
    }
}