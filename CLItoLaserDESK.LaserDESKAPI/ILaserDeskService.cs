// File: CLItoLaserDESK.LaserDESKAPI/ILaserDeskService.cs
using System.Threading.Tasks;

namespace CLItoLaserDESK.LaserDeskAPI {
    public interface ILaserDeskService : IDisposable // Add IDisposable for cleanup
    {
        // Connection Management
        bool Connect(string ipAddress = "127.0.0.1", int port = 3000);
        void Disconnect();
        bool IsConnected { get; } // Property to check connection status

        // Status & Info
        string GetLaserDeskVersion();
        uint GetLaserDeskStatus();
        bool IsRemoteModeActive();
        bool IsJobLoaded();
        bool IsReadyForExecution(); // e.g., not currently marking
        int GetTransmissionError();

        // Job and Geometry Management
        bool OpenJob(string jobFilePath, bool saveOld = false);
        bool ImportDxfLayer(string dxfFilePath, string uid, int optionFlags,
                            double xPos = 0.0, double yPos = 0.0,
                            double width = 0.0, double height = 0.0);
        // bool SetObjectZPosition(string uid, double zOffset); // We'll use Set3DTransformation for job Z for now

        // For overall Z-shift for the whole job if needed, as Set3DTransformation affects the whole job
        bool SetGlobal3DTransformation(double xOffset, double yOffset, double zOffset,
                                       double m11 = 1.0, double m12 = 0.0,
                                       double m21 = 0.0, double m22 = 1.0);

        // Execution
        bool StartMarking();
        Task<bool> WaitForMarkingCompletionAsync(int timeoutMilliseconds = 30000);
        void EmergencyStop(); // Good to have
    }
}