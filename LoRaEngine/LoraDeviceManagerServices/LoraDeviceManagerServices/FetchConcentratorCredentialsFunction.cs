
namespace LoraDeviceManagerServices.LoraDeviceManagerServices
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;
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
        public async Task<IActionResult> FetchConcentratorCredentials([HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
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

            return await RunFetchConcentratorCredentials(req, cancellationToken);
        }

        internal async Task<IActionResult> RunFetchConcentratorCredentials(HttpRequest req, CancellationToken cancellationToken)
        {
            if (!StationEui.TryParse((string)req.Query["StationEui"], out var stationEui))
            {
                logger.LogError("StationEui missing in request or invalid");
                return new BadRequestObjectResult("StationEui missing in request or invalid");
            }

            var credentialTypeQueryString = req.Query["CredentialType"];
            if (StringValues.IsNullOrEmpty(credentialTypeQueryString))
            {
                logger.LogError("CredentialType missing in request");
                return new BadRequestObjectResult("CredentialType missing in request");
            }
            if (!Enum.TryParse<ConcentratorCredentialType>(credentialTypeQueryString.ToString(), out var credentialType))
            {
                logger.LogError("Could not parse '{QueryString}' to a ConcentratorCredentialType.", credentialTypeQueryString.ToString());
                return new BadRequestObjectResult($"Could not parse desired concentrator credential type '{credentialTypeQueryString}'.");
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
                var credentials = await loraDeviceManager.GetStationCredentials(stationEui, credentialType, cancellationToken);
                if (string.IsNullOrWhiteSpace(credentials))
                {
                    logger.LogInformation($"Searching for {stationEui} returned 0 devices");
                    return new NotFoundResult();
                }
                return new OkObjectResult(credentials);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException
                                          or JsonReaderException
                                          or InvalidCastException
                                          or InvalidOperationException)
            {
                logger.LogError(ex, ex.Message);
                return new ObjectResult(ex.Message)
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                };
            }
        }
    }
}
