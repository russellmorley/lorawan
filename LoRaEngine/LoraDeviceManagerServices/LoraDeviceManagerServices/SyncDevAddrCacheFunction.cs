
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.ADR;

    public class SyncDevAddrCacheFunction
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ILoggerFactory loggerFactory;

        public SyncDevAddrCacheFunction(ILoraDeviceManager loraDeviceManager, ILoggerFactory loggerFactory)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.loggerFactory = loggerFactory;
        }

        [Function(nameof(SyncDevAddrCache))]
        public async Task SyncDevAddrCache([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            var logger = loggerFactory.CreateLogger<SyncDevAddrCacheFunction>();
            if (myTimer is null) throw new ArgumentNullException(nameof(myTimer));

            logger.LogDebug($"{(myTimer.IsPastDue ? "The timer is past due" : "The timer is on schedule")}, Function last ran at {myTimer.ScheduleStatus.Last} Function next scheduled run at {myTimer.ScheduleStatus.Next})");

            await loraDeviceManager.SyncLoraDevAddrCacheWithRegistry();

            logger.LogDebug("SyncDevAddrCache ran successfully");
        }
    }
}
