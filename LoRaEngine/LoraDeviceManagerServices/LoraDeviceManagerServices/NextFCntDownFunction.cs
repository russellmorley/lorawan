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
    using Newtonsoft.Json;

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
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(NextFCntDown));
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(request.Query));
            using (var reader = new StreamReader(request.Body))
            {
                logger.LogDebug("Post body: {Body}", await reader.ReadToEndAsync());
            }

            try
            {
                VersionValidator.Validate(request);
            }
            catch (IncompatibleVersionException ex)
            {
                logger.LogError(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }

            string paramDevEui = request.Query["DevEUI"];
            var paramFCntDown = request.Query["FCntDown"];
            var paramFCntUp = request.Query["FCntUp"];
            var paramGatewayId = request.Query["GatewayId"];
            var paramAbpFcntCacheReset = request.Query["ABPFcntCacheReset"];

            if (!DevEui.TryParse(paramDevEui, EuiParseOptions.ForbidInvalid, out var devEui))
            {
                return new BadRequestObjectResult("DevEUI is missing or invalid.");
            }

            using var deviceScope = logger.BeginDeviEuiScope(devEui);

            if (!uint.TryParse(paramFCntUp, out var fCntUp))
            {
                throw new ArgumentException("FCntUp param is missing or invalid");
            }

            if (paramAbpFcntCacheReset != StringValues.Empty)
            {
                await loraDeviceManager.AbpFcntCacheReset(devEui, paramGatewayId);
                return new OkObjectResult(null);
            }

            // validate input parameters
            if (!uint.TryParse(paramFCntDown, out var fCntDown) ||
                StringValues.IsNullOrEmpty(paramGatewayId))
            {
                return new BadRequestObjectResult("FCntDown or GatewayId params missing or invalid");
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(devEui.ToString(), request);
            }
            catch (InvalidDataException ex)
            {
                logger.LogError(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return new NotFoundResult();
            }

            var newFCntDown = await loraDeviceManager.GetNextFCntDownAsync(devEui, paramGatewayId, fCntUp, fCntDown);
            logger.LogDebug("Returning new fCntDown of {NewFCntDown}", newFCntDown.ToString());
            return new OkObjectResult(newFCntDown);
        }
    }
}
