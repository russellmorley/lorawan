// Copyrigh
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.Services;
    using System.IO;
    using Exceptions;
    using Version;

    public class NextFCntDownFunction
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ILogger<NextFCntDownFunction> logger;
        private readonly ITenantValidationStrategy tenantValidationStrategy;

        public NextFCntDownFunction(
            ILoraDeviceManager loraDeviceManager,
            ILogger<NextFCntDownFunction> logger,
            ITenantValidationStrategy tenantValidationStrategy)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.logger = logger;
            this.tenantValidationStrategy = tenantValidationStrategy;
        }

        [Function(nameof(NextFCntDown))]
        public async Task<IActionResult> NextFCntDown(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            string rawDevEui = req.Query["DevEUI"];
            var fCntDown = req.Query["FCntDown"];
            var fCntUp = req.Query["FCntUp"];
            var gatewayId = req.Query["GatewayId"];
            var abpFcntCacheReset = req.Query["ABPFcntCacheReset"];

            if (!DevEui.TryParse(rawDevEui, EuiParseOptions.ForbidInvalid, out var devEui))
            {
                return new BadRequestObjectResult("Dev EUI is invalid.");
            }

            using var deviceScope = logger.BeginDeviceScope(devEui);

            if (!uint.TryParse(fCntUp, out var clientFCntUp))
            {
                throw new ArgumentException("Missing FCntUp");
            }

            if (abpFcntCacheReset != StringValues.Empty)
            {
                await loraDeviceManager.AbpFcntCacheReset(devEui, gatewayId);
                return new OkObjectResult(null);
            }

            // validate input parameters
            if (!uint.TryParse(fCntDown, out var clientFCntDown) ||
                StringValues.IsNullOrEmpty(gatewayId))
            {
                var errorMsg = "Missing FCntDown or GatewayId";
                throw new ArgumentException(errorMsg);
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(devEui.ToString(), req);
            }
            catch (InvalidDataException ide)
            {
                return new BadRequestObjectResult(ide.Message);
            }
            catch (ArgumentException)
            {
                return new NotFoundResult();
            }

            var newFCntDown = await loraDeviceManager.GetNextFCntDownAsync(devEui, gatewayId, clientFCntUp, clientFCntDown);

            return new OkObjectResult(newFCntDown);
        }
    }
}
