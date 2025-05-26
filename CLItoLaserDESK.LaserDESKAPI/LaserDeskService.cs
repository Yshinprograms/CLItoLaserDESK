using System;
using System.Net; // For IPAddress
using SLLDRemoteControl; // This is the namespace from the SCANLAB DLL

namespace CLItoLaserDESK.LaserDeskAPI {
    public class LaserDeskService : ILaserDeskService {
        private SLLD_Functions _ldRemoteFunc; // Instance of the laserDESK functions class
        private bool _isConnected = false;

        // Define status flag constants based on manual page 204
        // Or check if SLLDRemoteControl.TelegramBuilder (from manual example p.205) is available
        // If TelegramBuilder is available and public: use SLLDRemoteControl.TelegramBuilder.RM_STATE_...
        // Otherwise, define them:
        public const uint RM_STATE_NO_INIT = 0x00000000;
        public const uint RM_STATE_WND_OPEN = 0x00000001;    // laserDESK runs
        public const uint RM_STATE_RTC_INIT = 0x00000002;    // RTC board is initialized
        public const uint RM_STATE_LAS_INIT = 0x00000004;    // Laser system is initialized
        public const uint RM_STATE_MOT_INIT = 0x00000008;    // All external controls are initialized
        public const uint RM_STATE_ALL_INIT = 0x0000000F;    // All hardware components are initialized
        public const uint RM_STATE_READY = 0x00000010;       // RTC Command thread runs
        public const uint RM_STATE_AUTOMODE = 0x00000020;    // Automatic Mode on
        public const uint RM_STATE_LST_EXEC = 0x00000040;    // Job is in execution
        public const uint RM_STATE_LST_EXE_ERR = 0x00000080; // Execution error
        public const uint RM_STATE_RM_MODE = 0x00000100;     // Remote Mode is on
        public const uint RM_STATE_JOB_LOAD = 0x00000200;    // Job is loaded
        public const uint RM_STATE_EXEC_DONE = 0x00010000;   // Set, when no job is running

        public LaserDeskService() {
            _ldRemoteFunc = new SLLD_Functions();
        }

        public bool Connect(string ipAddressString = "127.0.0.1", int port = 3000) {
            if (_isConnected) {
                Console.WriteLine("[LaserDeskService] Already connected.");
                return true;
            }

            try {
                // Convert IP string to byte array as required by OpenConnection
                // The manual (p.165) shows Most Significant Byte first for IP.
                // However, IPAddress.Parse().GetAddressBytes() returns in network order (big-endian), which is usually correct.
                // Let's assume the manual's example cadr[0]=127 is correct for that API.
                IPAddress ipAddr = IPAddress.Parse(ipAddressString);
                byte[] ipBytes = ipAddr.GetAddressBytes();
                // If GetAddressBytes isn't MSB first as Scanlab expects (e.g. if they want 127,0,0,1 literally)
                // byte[] cadr = { 127, 0, 0, 1 }; // For 127.0.0.1

                Console.WriteLine($"[LaserDeskService] Attempting to connect to laserDESK at {ipAddressString}:{port}...");
                int result = _ldRemoteFunc.OpenConnection(ref ipBytes, port); // Or use 'cadr' if direct byte array is needed

                if (result == 1) // 1 = success (manual p.165)
                {
                    Console.WriteLine("[LaserDeskService] OpenConnection successful. Switching to Remote Mode...");
                    int remoteModeResult = _ldRemoteFunc.SwitchRemoteMode(true); // true to switch ON
                    if (remoteModeResult == 1) // 1 = success (manual p.203)
                    {
                        _isConnected = true;
                        Console.WriteLine("[LaserDeskService] Switched to Remote Mode successfully.");
                        // Verify by checking status
                        if (IsRemoteModeActive()) {
                            Console.WriteLine("[LaserDeskService] Confirmed: Remote Mode is active.");
                        } else {
                            Console.WriteLine("[LaserDeskService] Warning: Remote Mode switch reported success, but status doesn't confirm.");
                        }
                        return true;
                    } else {
                        Console.Error.WriteLine($"[LaserDeskService] Failed to switch to Remote Mode. Result code: {remoteModeResult}");
                        _ldRemoteFunc.CloseConnection(); // Clean up connection attempt
                        return false;
                    }
                } else {
                    Console.Error.WriteLine($"[LaserDeskService] Failed to open connection. Result code: {result}");
                    return false;
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"[LaserDeskService] Exception during Connect: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        public void Disconnect() {
            if (!_isConnected) {
                Console.WriteLine("[LaserDeskService] Not connected, no need to disconnect.");
                return;
            }
            try {
                Console.WriteLine("[LaserDeskService] Switching Remote Mode OFF...");
                int remoteModeResult = _ldRemoteFunc.SwitchRemoteMode(false); // false to switch OFF
                if (remoteModeResult != 1) {
                    Console.Error.WriteLine($"[LaserDeskService] Warning: Failed to switch Remote Mode OFF cleanly. Result code: {remoteModeResult}");
                } else {
                    Console.WriteLine("[LaserDeskService] Remote Mode switched OFF.");
                }

                Console.WriteLine("[LaserDeskService] Closing connection...");
                int result = _ldRemoteFunc.CloseConnection();
                if (result != 1) // 0 = error (manual p.137)
                {
                    Console.Error.WriteLine($"[LaserDeskService] Warning: CloseConnection did not return success. Result code: {result}");
                } else {
                    Console.WriteLine("[LaserDeskService] Connection closed.");
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"[LaserDeskService] Exception during Disconnect: {ex.Message}");
            } finally {
                _isConnected = false;
            }
        }

        public string GetLaserDeskVersion() {
            if (!_isConnected) return "Not connected";
            try {
                return _ldRemoteFunc.GetVersion();
            } catch (Exception ex) { return $"Error getting version: {ex.Message}"; }
        }

        public uint GetLaserDeskStatus() {
            // GetState can be called even if not in remote mode, but connection must be open
            // if (!_isConnected && !_isInRemoteMode) return RM_STATE_NO_INIT; // Or throw exception
            try {
                return _ldRemoteFunc.GetState();
            } catch (Exception) { return RM_STATE_NO_INIT; /* Or handle error more robustly */ }
        }

        public bool IsRemoteModeActive() {
            return _isConnected && (GetLaserDeskStatus() & RM_STATE_RM_MODE) == RM_STATE_RM_MODE;
        }

        public bool IsJobLoaded() {
            return _isConnected && (GetLaserDeskStatus() & RM_STATE_JOB_LOAD) == RM_STATE_JOB_LOAD;
        }

        public bool IsReadyForExecution() {
            // READY implies ALL_INIT and RTC Command thread runs. Also not in execution.
            uint status = GetLaserDeskStatus();
            bool isReady = (status & RM_STATE_READY) == RM_STATE_READY;
            bool isNotExecuting = (status & RM_STATE_LST_EXEC) == 0;
            return _isConnected && isReady && isNotExecuting;
        }
    }
}