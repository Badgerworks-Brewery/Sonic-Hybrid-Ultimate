namespace SonicHybridUltimate.Core
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message);
        void LogWarning(string message);
    }
}
