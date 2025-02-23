
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
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "FunctionBundler/{devEUI}")] HttpRequest req,
            string devEUI)
        {
            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            if (!DevEui.TryParse(devEUI, EuiParseOptions.ForbidInvalid, out var parsedDevEui))
            {
                return new BadRequestObjectResult("Dev EUI is invalid.");
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

            using var deviceScope = logger.BeginDeviceScope(parsedDevEui);

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                return new BadRequestObjectResult("missing body");
            }

            var functionBundlerRequest = JsonConvert.DeserializeObject<FunctionBundlerRequest>(requestBody);
            var result = await loraDeviceManager.ExecuteFunctionBundlerAsync(parsedDevEui, functionBundlerRequest);
            return new OkObjectResult(result);
        }
    }
}
