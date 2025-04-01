
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
    using LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Newtonsoft.Json;
    using Microsoft.Azure.Functions.Worker;
    using LoraDeviceManager;
    using LoRaTools.Services;
    using Exceptions;
    using Version;
    using LoRaTools.BasicsStation.Processors;

    public class FetchConcentratorCredentialsFunction
    {
        private readonly ILogger<FetchConcentratorCredentialsFunction> logger;
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ITenantValidationStrategy tenantValidationStrategy;

        public FetchConcentratorCredentialsFunction(
            ILoraDeviceManager loraDeviceManager,
            ITenantValidationStrategy tenantValidationStrategy,
            ILogger<FetchConcentratorCredentialsFunction> logger)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.tenantValidationStrategy = tenantValidationStrategy;
            this.logger = logger;
        }

        [Function(nameof(FetchConcentratorCredentials))]
        public async Task<IActionResult> FetchConcentratorCredentials([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest request,
                                                                      CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            using var scope = logger.BeginScope("{RelativePath}: ", nameof(FetchConcentratorCredentials));
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

            return await RunFetchConcentratorCredentials(request, cancellationToken);
        }

        internal async Task<IActionResult> RunFetchConcentratorCredentials(HttpRequest req, CancellationToken cancellationToken)
        {
            if (!StationEui.TryParse((string)req.Query["StationEui"], out var stationEui))
            {
                logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            using var deviceScope = logger.BeginStationEuiScope(stationEui);

            var credentialTypeQueryString = req.Query["CredentialType"];
            if (StringValues.IsNullOrEmpty(credentialTypeQueryString))
            {
                logger.LogError("CredentialType missing in request");
                return new BadRequestObjectResult("CredentialType missing in request");
            }
            if (!Enum.TryParse<ConcentratorCredentialType>(credentialTypeQueryString.ToString(), out var credentialType))
            {
                logger.LogError("Could not parse '{CredentialTypeQueryString}' to a ConcentratorCredentialType.", credentialTypeQueryString.ToString());
                return new BadRequestObjectResult($"Could not parse '{credentialTypeQueryString}' to a ConcentratorCredentialType.");
            }

            try
            {
                await tenantValidationStrategy.ValidateRequestAndEui(stationEui.ToString(), req);
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
                var credentials = await loraDeviceManager.GetStationCredentials(stationEui, credentialType, cancellationToken);
                if (string.IsNullOrWhiteSpace(credentials))
                {
                    logger.LogInformation("Searching for {StationEui} returned 0 devices", stationEui);
                    return new NotFoundResult();
                }
                logger.LogDebug("Returning credentials {Credentials}", credentials);
                return new OkObjectResult(credentials);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException
                                          or JsonReaderException
                                          or InvalidCastException
                                          or InvalidOperationException)
            {
                logger.LogError(ex, "{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
                return new ObjectResult(ex.Message)
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }
        }
    }
}
