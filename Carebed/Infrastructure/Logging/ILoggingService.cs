using System.Threading.Tasks;

namespace Carebed.Infrastructure.Logging
{
    internal interface ILoggingService
    {
        void Log(LogMessage message);
        Task StartAsync();
        Task StopAsync();
    }
}