
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using LoraDeviceManager;
    using Exceptions;
    using Version;
    using LoRaTools;
    using LoRaTools.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Extensions.Logging;
    using System;
    using System.IO;
    using System.Threading.Tasks;

    internal class DeviceJoinNotificationFunction
    {

        private readonly ILogger<DeviceJoinNotificationFunction> logger;
        private readonly ITenantValidationStrategy tenantValidationStrategy;
        private readonly ILoraDeviceManager loraDeviceManager;

        public DeviceJoinNotificationFunction(
            ILoraDeviceManager loraDeviceManager,
            ILogger<DeviceJoinNotificationFunction> logger,
            ITenantValidationStrategy tenantValidationStrategy)
        {

            this.logger = logger;
            this.tenantValidationStrategy = tenantValidationStrategy;
            this.loraDeviceManager = loraDeviceManager;
        }

        [Function(nameof(DeviceJoinNotification))]
        public async Task<IActionResult> DeviceJoinNotification([HttpTrigger(AuthorizationLevel.Function, "post", Route = "devicejoinnotification")] DeviceJoinNotification joinNotification,
                                 HttpRequest req)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(joinNotification.DevEUI.ToString(), req);
            }
            catch (InvalidDataException ide)
            {
                return new BadRequestObjectResult(ide.Message);
            }
            catch (ArgumentException)
            {
                return new NotFoundResult();
            }

            using var deviceScope = logger.BeginDeviceScope(joinNotification.DevEUI);

            loraDeviceManager.AddDevice(joinNotification.DevAddr, joinNotification.DevEUI, joinNotification.GatewayId, joinNotification.NwkSKeyString);
            return new OkResult();
        }
    }
}
