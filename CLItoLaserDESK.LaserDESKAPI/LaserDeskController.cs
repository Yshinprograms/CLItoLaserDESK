// using SLLDRemoteControl; // Assuming the DLL is referenced and this namespace is available
// using System.Diagnostics; // For Stopwatch
// using System.Threading;   // For Thread.Sleep

public class LaserDeskController {
    private SLLDRemoteControl.SLLD_Functions LDRemoteFunc;
    private const string IP_ADDRESS = "127.0.0.1"; // Or actual IP if laserDESK is remote
    private const int PORT = 3000; // Default, confirm from laserDESK settings

    // --- Flags from laserDESK Manual p.204 (RM_STATE_...) ---
    private const uint RM_STATE_LST_EXEC = 0x00000040; // Job is in execution
    private const uint RM_STATE_EXEC_DONE = 0x00010000; // Set, when no job is running

    public LaserDeskController() {
        LDRemoteFunc = new SLLDRemoteControl.SLLD_Functions();
    }

    public void RunSingleDxfTrace() {
        Console.WriteLine("Attempting to connect to laserDESK...");
        byte[] ipBytes = System.Net.IPAddress.Parse(IP_ADDRESS).GetAddressBytes();
        // Note: OpenConnection expects MSB first for IP, but GetAddressBytes is usually fine for IPv4.
        // If 127.0.0.1 fails, and you are sure it's correct, try:
        // byte[] ipBytes = new byte[] { 127, 0, 0, 1 };

        if (LDRemoteFunc.OpenConnection(ref ipBytes, PORT) == 0) {
            Console.WriteLine($"Failed to open connection to laserDESK at {IP_ADDRESS}:{PORT}. Error: {LDRemoteFunc.GetTransmissionError()}");
            return;
        }
        Console.WriteLine("Connected.");

        if (LDRemoteFunc.SwitchRemoteMode(true) == 0) {
            Console.WriteLine("Failed to switch to Remote Mode.");
            LDRemoteFunc.CloseConnection();
            return;
        }
        Console.WriteLine("Switched to Remote Mode.");
        Console.WriteLine($"laserDESK Version: {LDRemoteFunc.GetVersion()}");
        Console.WriteLine($"Initial State: {LDRemoteFunc.GetStateAsInt()}");

        // STEP 1: Open a minimal, pre-configured job (IMPORTANT for field correction etc.)
        string templateJobPath = "C:\\ProgramData\\Scanlab\\SLLaserDesk\\HardwareConfiguration.sld";
        Console.WriteLine($"Opening template job: {templateJobPath}");
        if (LDRemoteFunc.OpenJob(templateJobPath, false) == 0) // false = don't save old job
        {
            Console.WriteLine("Failed to open template job. Ensure path is correct and laserDESK can access it.");
            // Consider if you want to proceed or stop. For robustness, stopping is better.
            LDRemoteFunc.SwitchRemoteMode(false);
            LDRemoteFunc.CloseConnection();
            return;
        }
        Console.WriteLine("Template job opened successfully.");
        Console.WriteLine($"State after opening job: {LDRemoteFunc.GetStateAsInt()}");


        // STEP 2: Import the DXF layer
        string dxfFilePath = "C:\\Users\\pin20\\Downloads\\SIMTech_Internship\\CLItoLaserDESK\\CLItoLaserDESK\\CLItoLaserDESK\\bin\\Debug\\net8.0\\DXF_Output\\Layer_0000.dxf";
        string importUID = "MySquareLayer"; // We assign a UID

        // optionFlags: enValues1to1 (0x10) - use absolute coords from DXF.
        // Potentially add enArea (0x02) if polylines need to be areas to be markable.
        // int optionFlags = 0x10 | 0x02; // enValues1to1 and enArea
        int optionFlags = 0x10; // Just enValues1to1 to start simple

        Console.WriteLine($"Importing DXF: {dxfFilePath} with UID: {importUID}");
        if (LDRemoteFunc.ImportVectorGraphic(dxfFilePath, 0.0, 0.0, 0.0, 0.0, importUID, optionFlags) == 0) {
            Console.WriteLine("Failed to import vector graphic. Assuming laserDESK places it at its native Z (0.0 for this test).");
            // Error handling
            LDRemoteFunc.SwitchRemoteMode(false);
            LDRemoteFunc.CloseConnection();
            return;
        }
        Console.WriteLine("DXF Imported.");
        Console.WriteLine($"State after import: {LDRemoteFunc.GetStateAsInt()}");

        // STEP 3: Set Z-Position -- OMITTED for Z=0 test.
        // We are relying on the DXF's Z-coordinates and the enValues1to1 flag.

        // STEP 4: Apply Marking Parameters (Skipping explicit SetMarkingParameter for now)
        // We are relying on laserDESK assigning default parameters inherited from the job,
        // as suggested by your GUI screenshots.

        // --- MANUAL VERIFICATION PAUSE (Optional but Recommended) ---
        Console.WriteLine("Pausing for manual verification in laserDESK GUI. Check if object is loaded correctly.");
        Console.WriteLine("Press Enter to proceed to marking...");
        Console.ReadLine();
        // Check laserDESK now: Is "MySquareLayer" there? Correct size/pos? Parameters look like your screenshot?

        // STEP 5: Trigger Marking
        Console.WriteLine("Attempting to start marking. ENSURE ALL SAFETY PRECAUTIONS ARE IN PLACE!");
        // You might want another Console.ReadLine() here for a final human "go" signal.
        if (LDRemoteFunc.StartMarking() == 0) {
            Console.WriteLine("Failed to start marking. Check laser/RTC status. Is laser on? Interlocks okay?");
            uint currentState = LDRemoteFunc.GetState();
            Console.WriteLine($"Current laserDESK State: {currentState} (Refer to manual p.152-157 for bits)");
            // Check for specific error bits like RM_STATE_LAS_ERR, RM_STATE_CMD_ERR etc.
            LDRemoteFunc.SwitchRemoteMode(false);
            LDRemoteFunc.CloseConnection();
            return;
        }
        Console.WriteLine("Marking command sent. Waiting for completion...");
        Console.WriteLine($"State after sending StartMarking: {LDRemoteFunc.GetStateAsInt()}");


        // STEP 6: Wait for Completion
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        bool markingCompletedSuccessfully = false;
        while (sw.ElapsedMilliseconds < 30000) // 30-second timeout
        {
            uint currentState = LDRemoteFunc.GetState();
            if ((currentState & RM_STATE_LST_EXEC) == 0) // If no longer executing
            {
                // Check if it finished *without* critical errors that might also clear LST_EXEC
                if ((currentState & 0x00000080) != 0) // RM_STATE_LST_EXE_ERR (Bit 7)
                {
                    Console.WriteLine("Marking stopped due to execution error (RM_STATE_LST_EXE_ERR).");
                } else if ((currentState & 0x00000800) != 0) // RM_STATE_CMD_ERR (Bit 11)
                {
                    Console.WriteLine("Marking stopped due to command error (RM_STATE_CMD_ERR).");
                } else if ((currentState & RM_STATE_EXEC_DONE) != 0) // Proper completion
                  {
                    Console.WriteLine("Marking execution successfully completed (RM_STATE_EXEC_DONE is set).");
                    markingCompletedSuccessfully = true;
                } else {
                    Console.WriteLine($"Marking execution stopped. RM_STATE_LST_EXEC is clear, RM_STATE_EXEC_DONE is NOT set. State: {currentState}");
                }
                break;
            }
            System.Threading.Thread.Sleep(200); // Poll every 200ms
        }
        sw.Stop();

        if (!markingCompletedSuccessfully && sw.ElapsedMilliseconds >= 30000) {
            Console.WriteLine("Marking timed out after 30 seconds. Sending EmergencyStop.");
            LDRemoteFunc.EmergencyStop(); // Important safety measure
        }
        Console.WriteLine($"Final State: {LDRemoteFunc.GetStateAsInt()}");


        // STEP 7: Cleanup
        Console.WriteLine("Switching off Remote Mode.");
        LDRemoteFunc.SwitchRemoteMode(false);
        Console.WriteLine("Closing connection.");
        LDRemoteFunc.CloseConnection();
        Console.WriteLine("Program finished.");
    }
}