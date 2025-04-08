
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
    using System.Collections.Generic;

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
            string paramDevAddr = request.Query["DevAddr"].ToString(); //this will return "" if it doesn't exist, whereas without it it will return null if it doesn't exist (wtf MS!)
            // OTAA parameters
            string paramDevEui = request.Query["DevEUI"].ToString();
            string paramDevNonce = request.Query["DevNonce"].ToString();
            string paramGatewayId = request.Query["GatewayId"].ToString();

            try
            {
                DevEui? devEui = DevEui.TryParse(paramDevEui, out var someDevEui) ? someDevEui : null;
                DevNonce? devNonce = ushort.TryParse(paramDevNonce, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? new DevNonce(d) : null;
                DevAddr? devAddr = DevAddr.TryParse(paramDevAddr, out var someDevAddr) ? someDevAddr : null;

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

                List<IoTHubDeviceInfo> iotHubDeviceInfos = null;
                if (devEui != null)
                {
                    using var devAddrScope = logger.BeginDeviEuiScope(devEui);
                    if (devNonce == null)
                    {
                        var message = "DevNonce must be set to a valid value if DevEUI is set.";
                        logger.LogError(message);
                        return new BadRequestObjectResult(message);
                    }
                    iotHubDeviceInfos = await loraDeviceManager.GetDeviceList((DevEui) devEui, paramGatewayId, (DevNonce) devNonce);

                }
                else if (devAddr != null)
                {
                    using var devAddrScope = logger.BeginDevAddrScope(devAddr);
                    iotHubDeviceInfos = await loraDeviceManager.GetDeviceList((DevAddr) devAddr);
                }
                else
                {
                    var message = "Either a valid DevEui or DevAddr must be included and both are missing or invalid.";
                    logger.LogError(message);
                    return new BadRequestObjectResult(message);
                }

                // if using tenant, filter out any results that aren't for tenant.
                if (tenantId != null)
                {
                    //get twin for all the results
                    var deviceTwinTasksAndInfo = iotHubDeviceInfos
                        .Select(async r =>
                            (await deviceRegistryManager.GetTwinAsync(r.DevEuiString), r)
                        );

                    (IDeviceTwin twin, IoTHubDeviceInfo info)[] twinInfos = await Task.WhenAll(deviceTwinTasksAndInfo);

                    // only return results where twin tags tenant id matches tenant id.
                    iotHubDeviceInfos = twinInfos
                        .Where(ti => ti.twin.GetTag(TwinPropertiesConstants.TenantIdName) == tenantId)
                        .Select(ti => ti.info)
                        .ToList();
                }

                var bodyStringToSend = JsonConvert.SerializeObject(iotHubDeviceInfos);
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
