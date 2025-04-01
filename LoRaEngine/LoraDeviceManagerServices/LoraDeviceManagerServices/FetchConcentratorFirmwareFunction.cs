
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
    using LoRaTools;

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
        public async Task<IActionResult> FetchConcentratorFirmware([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest request,
                                                                   CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(FetchConcentratorFirmware));
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

            if (!StationEui.TryParse((string)request.Query["StationEui"], out var stationEui))
            {
                logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            using var deviceScope = logger.BeginStationEuiScope(stationEui);

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(stationEui.ToString(), request);
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

            try
            {
                (long length, Stream contentStream) = await loraDeviceManager.GetStationFirmware(stationEui, cancellationToken);
                logger.LogDebug("Returning content stream of length {Length}", length);
                return new FileStreamWithContentLengthResult(contentStream, "application/octet-stream", length);
            }
            catch (DeviceNotFoundException ex)
            {
                logger.LogDebug(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
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
            catch (RequestFailedException ex) when (ExceptionFilterUtility.True(() => logger.LogError(ex, "Failed to download firmware")))
            {
                return new ObjectResult("Failed to download firmware")
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }
        }
    }
}
