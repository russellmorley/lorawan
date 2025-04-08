
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
    using Newtonsoft.Json;
    //using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;
    // see https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger?tabs=python-v2%2Cisolated-process%2Cnodejs-v4%2Cfunctionsv2&pivots=programming-language-csharp#payload


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
        public async Task<IActionResult> DeviceJoinNotification(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "devicejoinnotification")] HttpRequest request
            // , [FromBody] DeviceJoinNotification joinNotification
            // this version of stupid functions uses Microsoft.AspNetCore.Mvc.Formatters.SystemTextJsonInputFormatter, which uses JsonSerializer,
            // which is not compatible with Newtonsoft attributes, and thus the DeviceJoinNotification class is not compatible.
            // Rather than porting the entire app from Newtonsoft, I elected to just deserialize the object myself using Newtonsoft. 
            // -- RM 3/23/25
            )
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(DeviceJoinNotification));
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(request.Query));
            string bodyString;
            using (var reader = new StreamReader(request.Body))
            {
                bodyString = await reader.ReadToEndAsync();
                logger.LogDebug("Post body: {BodyString}", bodyString); 
            }
            if (string.IsNullOrEmpty(bodyString))
            {
                var message = "Empty body string that should contain serialized DeviceJoinNotification";
                logger.LogError(message);
                return new BadRequestObjectResult(message);
            }
            var deviceJoinNotification = JsonConvert.DeserializeObject<DeviceJoinNotification>(bodyString);
            if (deviceJoinNotification == null)
            {
                var message = "Cannot deserialize body into a DeviceJoinNotification";
                logger.LogError(message);
                return new BadRequestObjectResult(message);
            }
            if (deviceJoinNotification.DevEUI == null)
            {
                var message = "deviceJoinNotification.DevEUI is null";
                logger.LogError(message);
                return new BadRequestObjectResult(message);
            }

            using var deviceScope = logger.BeginDeviEuiScope(deviceJoinNotification.DevEUI);

            try
            {
                VersionValidator.Validate(request);
            }
            catch (IncompatibleVersionException ex)
            {
                logger.LogError(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(deviceJoinNotification.DevEUI.ToString(), request);
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

            loraDeviceManager.AddDevice(deviceJoinNotification.DevAddr, deviceJoinNotification.DevEUI, deviceJoinNotification.GatewayId, deviceJoinNotification.NwkSKeyString);
            logger.LogDebug("Returning OkResult");
            return new OkResult();
        }
    }
}
