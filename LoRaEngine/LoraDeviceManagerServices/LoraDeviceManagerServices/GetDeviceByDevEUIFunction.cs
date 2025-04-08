
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan;
    using LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.Services;
    using System.IO;
    using Exceptions;
    using Version;
    using Newtonsoft.Json;

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
        public async Task<IActionResult> GetDeviceByDevEUI([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(GetDeviceByDevEUI));
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
            if (!DevEui.TryParse(paramDevEui, out var devEui))
            {
                var message = "DevEUI is missing or invalid.";
                logger.LogError(message);
                return new BadRequestObjectResult(message);
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

            using var deviceScope = logger.BeginDeviEuiScope(devEui);

            var primaryKey = await loraDeviceManager.GetDevicePrimaryKey(paramDevEui);
            if (primaryKey == null)
            {
                logger.LogDebug("Search for {ParamDevEui} found 0 devices", paramDevEui);
                return new NotFoundResult();
            }

            logger.LogDebug("Search for {ParamDevEui} found 1 device. Returning its primaryKey {PrimaryKey}", paramDevEui, primaryKey);
            return new OkObjectResult(new
            {
                DevEUI = paramDevEui,
                PrimaryKey = primaryKey
            });
        }
    }
}
