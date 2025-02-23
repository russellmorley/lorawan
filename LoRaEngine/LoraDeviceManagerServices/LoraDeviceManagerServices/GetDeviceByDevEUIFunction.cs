
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.Services;
    using System.IO;
    using Exceptions;
    using Version;

    public class GetDeviceByDevEUIFunction
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ITenantValidationStrategy tenantValidationStrategy;
        private readonly ILogger<GetDeviceByDevEUIFunction> logger;

        public GetDeviceByDevEUIFunction(
            ILoraDeviceManager loraDeviceManager,
            ITenantValidationStrategy tenantValidationStrategy,
            ILogger<GetDeviceByDevEUIFunction> logger)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.tenantValidationStrategy = tenantValidationStrategy;
            this.logger = logger;
        }

        [Function(nameof(GetDeviceByDevEUI))]
        public async Task<IActionResult> GetDeviceByDevEUI([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            ArgumentNullException.ThrowIfNull(req);

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                logger.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            string devEui = req.Query["DevEUI"];
            if (!DevEui.TryParse(devEui, out var parsedDevEui))
            {
                return new BadRequestObjectResult("DevEUI missing or invalid.");
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(parsedDevEui.ToString(), req);
            }
            catch (InvalidDataException ide)
            {
                return new BadRequestObjectResult(ide.Message);
            }
            catch (ArgumentException)
            {
                return new NotFoundResult();
            }

            var primaryKey = await loraDeviceManager.GetDevicePrimaryKey(devEui);
            if (primaryKey == null)
            {
                logger.LogInformation($"Search for {devEui} found 0 devices");
                return new NotFoundResult();
            }

            logger.LogDebug($"Search for {devEui} found 1 device");
            return new OkObjectResult(new
            {
                DevEUI = devEui,
                PrimaryKey = primaryKey
            });
        }
    }
}
