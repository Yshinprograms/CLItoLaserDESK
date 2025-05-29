// File: CLItoLaserDESK.LaserDESKAPI/LaserDeskService.cs
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SLLDRemoteControl; // Namespace from SCANLAB DLL

namespace CLItoLaserDESK.LaserDeskAPI {
    public class LaserDeskService : ILaserDeskService {
        private SLLD_Functions _ldRemoteFunc;
        private bool _isConnectedAndInRemoteMode = false;

        // Status flags (consider making these public or part of a shared constants class if needed elsewhere)
        private const uint RM_STATE_RM_MODE = 0x00000100;
        private const uint RM_STATE_JOB_LOAD = 0x00000200;
        private const uint RM_STATE_READY = 0x00000010;
        private const uint RM_STATE_LST_EXEC = 0x00000040;
        private const uint RM_STATE_LST_EXE_ERR = 0x00000080;
        private const uint RM_STATE_CMD_ERR = 0x00000800;
        private const uint RM_STATE_EXEC_DONE = 0x00010000;

        public bool IsConnected => _isConnectedAndInRemoteMode;

        public LaserDeskService() {
            _ldRemoteFunc = new SLLD_Functions();
        }

        public bool Connect(string ipAddressString = "127.0.0.1", int port = 3000) {
            if (_isConnectedAndInRemoteMode) {
                Console.WriteLine("[LaserDeskService] Already connected and in remote mode.");
                return true;
            }

            try {
                IPAddress ipAddr = IPAddress.Parse(ipAddressString);
                byte[] ipBytes = ipAddr.GetAddressBytes(); // Network order (MSB first for IPv4)
                                                           // DLL expects byte[0]=MSB, byte[1], byte[2], byte[3]=LSB for IPv4
                                                           // GetAddressBytes for IPv4 returns exactly this order.

                Console.WriteLine($"[LaserDeskService] Attempting OpenConnection to {ipAddressString}:{port}...");
                // The SLLDRemoteControl.dll OpenConnection does not take ipBytes by ref.
                if (_ldRemoteFunc.OpenConnection(ref ipBytes, port) == 1) {
                    Console.WriteLine("[LaserDeskService] OpenConnection successful. Attempting SwitchRemoteMode(true)...");
                    if (_ldRemoteFunc.SwitchRemoteMode(true) == 1) {
                        _isConnectedAndInRemoteMode = true;
                        Console.WriteLine("[LaserDeskService] Successfully connected and switched to Remote Mode.");
                        return true;
                    } else {
                        Console.Error.WriteLine("[LaserDeskService] Failed to switch to Remote Mode. Closing connection.");
                        _ldRemoteFunc.CloseConnection();
                        _isConnectedAndInRemoteMode = false;
                        return false;
                    }
                } else {
                    Console.Error.WriteLine($"[LaserDeskService] Failed to open connection. Error code: {_ldRemoteFunc.GetTransmissionError()}");
                    _isConnectedAndInRemoteMode = false;
                    return false;
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"[LaserDeskService] Exception during Connect: {ex.Message}");
                _isConnectedAndInRemoteMode = false;
                return false;
            }
        }

        public void Disconnect() {
            if (_isConnectedAndInRemoteMode) {
                Console.WriteLine("[LaserDeskService] Attempting to switch Remote Mode OFF...");
                _ldRemoteFunc.SwitchRemoteMode(false); // Attempt even if it fails
            }
            if (_ldRemoteFunc != null) // Check if _ldRemoteFunc was ever in a state to have an open connection
            {
                // Only call CloseConnection if we expect a connection might have been established at the socket level.
                // The _isConnectedAndInRemoteMode flag is a better guard for remote mode operations.
                // A raw socket connection might exist even if remote mode failed.
                // Check an internal flag or GetState() if a "connected but not remote" state is possible and needs cleanup.
                // For simplicity, if Connect failed early, CloseConnection might not be needed or could error.
                // However, the DLL example always calls CloseConnection.
                Console.WriteLine("[LaserDeskService] Attempting CloseConnection...");
                _ldRemoteFunc.CloseConnection();
            }
            _isConnectedAndInRemoteMode = false;
            Console.WriteLine("[LaserDeskService] Disconnected.");
        }

        public string GetLaserDeskVersion() {
            if (!IsConnected) throw new InvalidOperationException("Not connected to laserDESK.");
            return _ldRemoteFunc.GetVersion();
        }

        public uint GetLaserDeskStatus() {
            // GetState can technically be called if connection is open, even if not in remote mode.
            // However, meaningful status for job operations usually requires remote mode.
            if (_ldRemoteFunc == null) return 0; // Or some error state
            return _ldRemoteFunc.GetState();
        }

        public int GetTransmissionError() {
            if (_ldRemoteFunc == null) return -1; // Indicate service not ready
            return _ldRemoteFunc.GetTransmissionError();
        }

        public bool IsRemoteModeActive() {
            return IsConnected && (GetLaserDeskStatus() & RM_STATE_RM_MODE) == RM_STATE_RM_MODE;
        }

        public bool IsJobLoaded() {
            if (!IsConnected) return false;
            return (GetLaserDeskStatus() & RM_STATE_JOB_LOAD) == RM_STATE_JOB_LOAD;
        }

        public bool IsReadyForExecution() {
            if (!IsConnected) return false;
            uint status = GetLaserDeskStatus();
            bool isReadyFlag = (status & RM_STATE_READY) == RM_STATE_READY;
            bool isNotExecuting = (status & RM_STATE_LST_EXEC) == 0;
            return isReadyFlag && isNotExecuting;
        }

        public bool OpenJob(string jobFilePath, bool saveOld = false) {
            if (!IsConnected) throw new InvalidOperationException("Not connected. Cannot open job.");
            Console.WriteLine($"[LaserDeskService] Opening job: {jobFilePath}");
            int result = _ldRemoteFunc.OpenJob(jobFilePath, saveOld);
            if (result != 1) {
                Console.Error.WriteLine($"[LaserDeskService] Failed to open job '{jobFilePath}'. Result: {result}, Status: 0x{GetLaserDeskStatus():X8}");
                return false;
            }
            Console.WriteLine($"[LaserDeskService] Job '{jobFilePath}' opened successfully.");
            return true;
        }

        public bool ImportDxfLayer(string dxfFilePath, string uid, int optionFlags,
                                   double xPos = 0, double yPos = 0,
                                   double width = 0, double height = 0) {
            if (!IsConnected) throw new InvalidOperationException("Not connected. Cannot import DXF.");
            Console.WriteLine($"[LaserDeskService] Importing DXF: {dxfFilePath}, UID: {uid}");
            int result = _ldRemoteFunc.ImportVectorGraphic(dxfFilePath, xPos, yPos, width, height, uid, optionFlags);
            if (result != 1) {
                Console.Error.WriteLine($"[LaserDeskService] Failed to import DXF '{dxfFilePath}'. Result: {result}, Status: 0x{GetLaserDeskStatus():X8}");
                return false;
            }
            Console.WriteLine($"[LaserDeskService] DXF '{dxfFilePath}' imported successfully.");
            return true;
        }

        public bool SetGlobal3DTransformation(double xOffset, double yOffset, double zOffset,
                                              double m11 = 1.0, double m12 = 0.0,
                                              double m21 = 0.0, double m22 = 1.0) {
            if (!IsConnected) throw new InvalidOperationException("Not connected. Cannot set 3D transformation.");

            // Manual p.176: Set3DTransformation requires Automatic Mode.
            // This is tricky if we are trying to set Z *before* marking in what might be Manual Mode.
            // For now, let's assume the user ensures the mode or this is for future use.
            // A check might be: if (!IsAutomaticModeActive()) { SwitchToAutomaticMode(); ... then switch back if needed }
            // However, the function itself applies to the "whole job".

            Console.WriteLine($"[LaserDeskService] Setting Global 3D Transformation: ZOffset={zOffset}");
            // The Set3DTransformation function has 7 parameters: xshift, yshift, zshift, M11, M12, M21, M22
            int result = _ldRemoteFunc.Set3DTransformation(xOffset, yOffset, zOffset, m11, m12, m21, m22);
            if (result != 1) {
                Console.Error.WriteLine($"[LaserDeskService] Failed to set global 3D transformation. Result: {result}, Status: 0x{GetLaserDeskStatus():X8}");
                return false;
            }
            Console.WriteLine("[LaserDeskService] Global 3D Transformation set successfully.");
            return true;
        }


        public bool StartMarking() {
            if (!IsConnected) throw new InvalidOperationException("Not connected. Cannot start marking.");
            if (!IsReadyForExecution()) {
                Console.Error.WriteLine($"[LaserDeskService] Not ready for execution. Status: 0x{GetLaserDeskStatus():X8}");
                return false;
            }
            Console.WriteLine("[LaserDeskService] Starting marking...");
            int result = _ldRemoteFunc.StartMarking();
            if (result != 1) {
                Console.Error.WriteLine($"[LaserDeskService] Failed to start marking. Result: {result}, Status: 0x{GetLaserDeskStatus():X8}");
                return false;
            }
            Console.WriteLine("[LaserDeskService] Marking command sent.");
            return true;
        }

        public async Task<bool> WaitForMarkingCompletionAsync(int timeoutMilliseconds = 30000) {
            if (!IsConnected) throw new InvalidOperationException("Not connected. Cannot wait for marking completion.");
            Console.WriteLine("[LaserDeskService] Waiting for marking completion...");
            CancellationTokenSource cts = new CancellationTokenSource(timeoutMilliseconds);
            try {
                while (!cts.IsCancellationRequested) {
                    uint currentState = GetLaserDeskStatus();
                    if ((currentState & RM_STATE_LST_EXEC) == 0) // No longer executing
                    {
                        if ((currentState & RM_STATE_LST_EXE_ERR) != 0) {
                            Console.Error.WriteLine("[LaserDeskService] Marking stopped due to list execution error (RM_STATE_LST_EXE_ERR)."); return false;
                        }
                        if ((currentState & RM_STATE_CMD_ERR) != 0) {
                            Console.Error.WriteLine("[LaserDeskService] Marking stopped due to command error (RM_STATE_CMD_ERR)."); return false;
                        }
                        if ((currentState & RM_STATE_EXEC_DONE) != 0) {
                            Console.WriteLine("[LaserDeskService] Marking successfully completed (RM_STATE_EXEC_DONE)."); return true;
                        }
                        Console.WriteLine($"[LaserDeskService] Marking execution stopped. LST_EXEC clear, EXEC_DONE not set. State: 0x{currentState:X8}");
                        return false; // Or indeterminate
                    }
                    await Task.Delay(100, cts.Token); // Poll every 100ms
                }
            } catch (OperationCanceledException) // Catches cancellation from cts timeout
              {
                Console.Error.WriteLine($"[LaserDeskService] Waiting for marking completion timed out after {timeoutMilliseconds}ms.");
                EmergencyStop(); // Safety measure
                return false;
            }
            Console.Error.WriteLine($"[LaserDeskService] Waiting loop exited unexpectedly (timeout)."); // Should be caught by CCE
            return false;
        }

        public void EmergencyStop() {
            if (!IsConnected) {
                Console.WriteLine("[LaserDeskService] Not connected, cannot send EmergencyStop (but consider physical E-Stop).");
                return;
            }
            Console.WriteLine("[LaserDeskService] Sending EmergencyStop command.");
            _ldRemoteFunc.EmergencyStop(); // This function returns int, but often used as void.
        }

        public void Dispose() {
            Disconnect();
            // _ldRemoteFunc might have unmanaged resources if it were a COM object we directly instantiated,
            // but as a .NET class, GC handles it. Disconnect is key.
            GC.SuppressFinalize(this);
        }
    }
}