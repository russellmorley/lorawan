
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.Services;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Exceptions;
    using Version;

    public class FetchConcentratorFirmwareFunction
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ITenantValidationStrategy tenantValidationStrategy;
        private readonly ILogger<FetchConcentratorFirmwareFunction> logger;

        public FetchConcentratorFirmwareFunction(
            ILoraDeviceManager loraDeviceManager,
            ITenantValidationStrategy tenantValidationStrategy,
            ILogger<FetchConcentratorFirmwareFunction> logger)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.tenantValidationStrategy = tenantValidationStrategy;
            this.logger = logger;
        }

        [Function(nameof(FetchConcentratorFirmware))]
        public async Task<IActionResult> FetchConcentratorFirmware([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
                                                                   CancellationToken cancellationToken)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                logger.LogError(ex, "Invalid version");
                return new BadRequestObjectResult(ex.Message);
            }

            if (!StationEui.TryParse((string)req.Query["StationEui"], out var stationEui))
            {
                logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(stationEui.ToString(), req);
            }
            catch (InvalidDataException ide)
            {
                return new BadRequestObjectResult(ide.Message);
            }
            catch (ArgumentException)
            {
                return new NotFoundResult();
            }

            try
            {
                (long length, Stream contentStream) = await loraDeviceManager.GetStationFirmware(stationEui, cancellationToken);

                return new FileStreamWithContentLengthResult(contentStream, "application/octet-stream", length);
            }
            catch (DeviceNotFoundException)
            {
                return new NotFoundResult();
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException or JsonReaderException or NullReferenceException)
            {
                var message = $"Failed to parse firmware upgrade url from the 'cups' desired property.";
                logger.LogError(ex, message);
                return new ObjectResult(message)
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }
            catch (RequestFailedException ex) when (ExceptionFilterUtility.True(() => logger.LogError(ex, "Failed to download firmware from storage.")))
            {
                return new ObjectResult("Failed to download firmware")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }
    }
}
