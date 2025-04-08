
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Functions.Worker;
    using System.IO;
    using LoRaTools.Services;
    using System;
    using LoraDeviceManager;
    using Exceptions;
    using Version;
    using LoRaTools.FunctionBundler;

    public class FunctionBundlerFunction
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ILogger<FunctionBundlerFunction> logger;
        private readonly ITenantValidationStrategy tenantValidationStrategy;

        public FunctionBundlerFunction(
            ILoraDeviceManager loraDeviceManager,
            ILogger<FunctionBundlerFunction> logger,
            ITenantValidationStrategy tenantValidationStrategy)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.logger = logger;
            this.tenantValidationStrategy = tenantValidationStrategy;
        }

        [Function(nameof(FunctionBundler))]
        public async Task<IActionResult> FunctionBundler(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FunctionBundler/{paramDevEUI}")] HttpRequest request,
            string paramDevEUI)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(FunctionBundler));
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(request.Query));
            logger.LogDebug("Path param paramDevEUI: {ParamDevEUI}", paramDevEUI);
            string bodyString;
            using (var reader = new StreamReader(request.Body))
            {
                bodyString = await reader.ReadToEndAsync();
                logger.LogDebug("Post body: {BodyString}", bodyString);
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

            if (!DevEui.TryParse(paramDevEUI, EuiParseOptions.ForbidInvalid, out var devEui))
            {
                var message = "DevEUI is invalid.";
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

            if (string.IsNullOrEmpty(bodyString))
            {
                var message = "Empty body string that should contain serialized FunctionBundlerRequest";
                logger.LogError(message);
                return new BadRequestObjectResult(message);
            }

            var functionBundlerRequest = JsonConvert.DeserializeObject<FunctionBundlerRequest>(bodyString);
            if (functionBundlerRequest == null)
            {
                var message = "Cannot deserialize body into a FunctionBundlerRequest";
                logger.LogError(message);
                return new BadRequestObjectResult(message);
            }

            var functionBundlerResult = await loraDeviceManager.ExecuteFunctionBundlerAsync(devEui, functionBundlerRequest);
            logger.LogDebug("Returning FunctionBundlerResult of {FunctionBundlerResult}", JsonConvert.SerializeObject(functionBundlerResult));
            return new OkObjectResult(functionBundlerResult);
        }
    }
}
