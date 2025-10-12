namespace SonicHybridUltimate.Engines
{
    public interface IGameEngine
    {
        bool Initialize(string path);
        void Update();
        void Cleanup();
        bool IsRunning { get; }
        string CurrentGame { get; }
    }
}
