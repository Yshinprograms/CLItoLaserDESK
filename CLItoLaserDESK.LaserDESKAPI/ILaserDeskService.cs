using System.Threading.Tasks; // For async operations later

namespace CLItoLaserDESK.LaserDeskAPI {
    public interface ILaserDeskService {
        bool Connect(string ipAddress = "127.0.0.1", int port = 3000);
        void Disconnect();
        string GetLaserDeskVersion();
        uint GetLaserDeskStatus(); // Status is typically a uint bitmask
        bool IsRemoteModeActive();
        bool IsJobLoaded();
        bool IsReadyForExecution();

        // We will add more methods here later for job loading, importing, marking, etc.
        // bool LoadJob(string jobFilePath);
        // bool ImportDxfLayer(string dxfFilePath, double xPos = 0, double yPos = 0);
        // bool SetLayerZ(double zOffset);
        // bool StartMarking();
        // Task<bool> WaitForMarkingCompletion();
    }
}