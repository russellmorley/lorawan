
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.Services;
    using System.Linq;
    using System.IO;
    using LoraDeviceManager.Cache;
    using LoraDeviceManager.Exceptions;
    using Exceptions;
    using Version;
    public class GetDeviceFunction
    {
        private readonly ILogger<GetDeviceFunction> logger;
        private readonly ITenantValidationStrategy tenantValidationStrategy;
        private readonly IDeviceRegistryManager deviceRegistryManager;
        private readonly ILoraDeviceManager loraDeviceManager;

        public GetDeviceFunction(
            ILoraDeviceManager loraDeviceManager,
            ILogger<GetDeviceFunction> logger,
            ITenantValidationStrategy tenantValidationStrategy,
            IDeviceRegistryManager deviceRegistryManager)
        {
            this.logger = logger;
            this.tenantValidationStrategy = tenantValidationStrategy;
            this.deviceRegistryManager = deviceRegistryManager;
            this.loraDeviceManager = loraDeviceManager;
        }

        /// <summary>
        /// Entry point function for getting devices.
        /// </summary>
        [Function(nameof(GetDevice))]
        public async Task<IActionResult> GetDevice(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(GetDevice));
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

            // ABP parameters
            string paramDevAddr = request.Query["DevAddr"];
            // OTAA parameters
            string paramDevEui = request.Query["DevEUI"];
            string paramDevNonce = request.Query["DevNonce"];
            var paramGatewayId = request.Query["GatewayId"];

            if (!DevEui.TryParse(paramDevEui, EuiParseOptions.ForbidInvalid, out var devEui))
            {
                return new BadRequestObjectResult("DevEUI is missing invalid.");
            }

            using var deviceScope = logger.BeginDeviEuiScope(devEui);

            try
            {
                DevNonce? devNonce = ushort.TryParse(paramDevNonce, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? new DevNonce(d) : null;
                DevAddr? devAddr = DevAddr.TryParse(paramDevAddr, out var someDevAddr) ? someDevAddr : null;

                using var devAddrScope = logger.BeginDevAddrScope(devAddr);

                string tenantId;
                try
                {
                    tenantId = await tenantValidationStrategy.ValidateRequest(request);
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

                var results = await loraDeviceManager.GetDeviceList(devEui, paramGatewayId, devNonce, devAddr);

                // if using tenant, filter out any results that aren't for tenant.
                if (tenantId != null)
                {
                    //get twin for all the results
                    var deviceTwinTasksAndInfo = results
                        .Select(async r =>
                            (await deviceRegistryManager.GetTwinAsync(r.DevEuiString), r)
                        );

                    (IDeviceTwin twin, IoTHubDeviceInfo info)[] twinInfos = await Task.WhenAll(deviceTwinTasksAndInfo);

                    // only return results where twin tags tenant id matches tenant id.
                    results = twinInfos
                        .Where(ti => ti.twin.GetTag(TwinPropertiesConstants.TenantIdName) == tenantId)
                        .Select(ti => ti.info)
                        .ToList();
                }

                var bodyStringToSend = JsonConvert.SerializeObject(results);
                logger.LogDebug("Returning json-serialized IoTHubDeviceInfos: {BodyStringToSend}", bodyStringToSend);
                return new OkObjectResult(bodyStringToSend);
            }
            catch (DeviceNonceUsedException ex)
            {
                logger.LogError(ex, "Used device Nonce: {ex}", ex.Message);
                return new BadRequestObjectResult("UsedDevNonce");
            }
            catch (JoinRefusedException ex) when (ExceptionFilterUtility.True(() => logger.LogDebug("Join refused: {msg}", ex.Message)))
            {
                logger.LogError(ex, "Join refused {ex}", ex.Message);
                return new BadRequestObjectResult("JoinRefused: " + ex.Message);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
        }
    }
}
