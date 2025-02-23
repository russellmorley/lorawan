
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
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req)
        {
            ArgumentNullException.ThrowIfNull(req);

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            // ABP parameters
            string devAddrString = req.Query["DevAddr"];
            // OTAA parameters
            string rawDevEui = req.Query["DevEUI"];
            string rawDevNonce = req.Query["DevNonce"];
            var gatewayId = req.Query["GatewayId"];

            DevEui? devEui = null;
            if (!string.IsNullOrEmpty(rawDevEui))
            {
                if (DevEui.TryParse(rawDevEui, EuiParseOptions.ForbidInvalid, out var parsedDevEui))
                {
                    devEui = parsedDevEui;
                }
                else
                {
                    return new BadRequestObjectResult("Dev EUI is invalid.");
                }
            }

            using var deviceScope = logger.BeginDeviceScope(devEui);

            try
            {
                DevNonce? devNonce = ushort.TryParse(rawDevNonce, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? new DevNonce(d) : null;
                DevAddr? devAddr = DevAddr.TryParse(devAddrString, out var someDevAddr) ? someDevAddr : null;

                using var devAddrScope = logger.BeginDeviceAddressScope(devAddr);

                string tenantId;
                try
                {
                    tenantId = await tenantValidationStrategy.ValidateRequest(req);
                }
                catch (InvalidDataException ide)
                {
                    return new BadRequestObjectResult(ide.Message);
                }
                catch (ArgumentException)
                {
                    return new NotFoundResult();
                }

                var results = await loraDeviceManager.GetDeviceList(devEui, gatewayId, devNonce, devAddr);

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

                var json = JsonConvert.SerializeObject(results);
                return new OkObjectResult(json);
            }
            catch (DeviceNonceUsedException)
            {
                return new BadRequestObjectResult("UsedDevNonce");
            }
            catch (JoinRefusedException ex) when (ExceptionFilterUtility.True(() => logger.LogDebug("Join refused: {msg}", ex.Message)))
            {
                return new BadRequestObjectResult("JoinRefused: " + ex.Message);
            }
            catch (ArgumentException aex)
            {
                return new BadRequestObjectResult(aex.Message);
            }
        }
    }
}
