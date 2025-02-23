
namespace LoraDeviceManagerServices.LoraDeviceDownstreamService
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Functions.Worker;
    using LoRaTools.ChannelPublisher;
    using LoRaTools.EdgeDeviceGetter;
    using LoRaTools.ServiceClient;
    using LoraDeviceManager.Utils;
    using Exceptions;
    using Version;

    public sealed class ClearNetworkServerCacheFunction
    {
        private readonly IEdgeDeviceGetter edgeDeviceGetter;
        private readonly IServiceClient serviceClient;
        private readonly IChannelPublisher channelPublisher;
        private readonly ILogger<ClearNetworkServerCacheFunction> logger;

        public ClearNetworkServerCacheFunction(IEdgeDeviceGetter edgeDeviceGetter,
                             IServiceClient serviceClient,
                             IChannelPublisher channelPublisher,
                             ILogger<ClearNetworkServerCacheFunction> logger)
        {
            this.edgeDeviceGetter = edgeDeviceGetter;
            this.serviceClient = serviceClient;
            this.channelPublisher = channelPublisher;
            this.logger = logger;
        }

        [Function(nameof(ClearNetworkServerCache))]
        public async Task<IActionResult> ClearNetworkServerCache([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req, CancellationToken cancellationToken)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            try
            {
                VersionValidator.Validate(req);
            }
            catch (IncompatibleVersionException ex)
            {
                return new BadRequestObjectResult(ex.Message);
            }

            await ClearLnsCacheInternalAsync(cancellationToken);

            return new AcceptedResult();
        }

        internal async Task ClearLnsCacheInternalAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Clearing device cache for all edge and Pub/Sub channel based Network Servers.");
            // Edge device discovery for invoking direct methods
            var edgeDevices = await edgeDeviceGetter.ListEdgeDevicesAsync(cancellationToken);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Invoking clear cache direct method for following devices: {deviceList}", string.Join(',', edgeDevices));
            }
            var tasks = edgeDevices.Select(e => InvokeClearViaDirectMethodAsync(e, cancellationToken)).ToArray();
            // Publishing a single message for all cloud based LNSes
            await PublishClearMessageAsync();
            await Task.WhenAll(tasks);
        }

        internal async Task PublishClearMessageAsync()
        {
            try
            {
                await channelPublisher.PublishAsync(LoraDeviceManagerConstants.ClearCacheMethodName, new LnsRemoteCall(RemoteCallKind.ClearCache, null));
                logger.LogInformation("Cache clear message published on Pub/Sub channel to inform all network servers.");
            }
            catch (NotImplementedException)
            {
                logger.LogDebug("Cache clear message not published on Pub/Sub channel because method isn't implemented.");
            }
        }

        internal async Task InvokeClearViaDirectMethodAsync(string lnsId, CancellationToken cancellationToken)
        {
            //Reason why the yield is needed is to avoid any potential "synchronous" code that might fail the publishing of a message on the pub/sub channel
            await Task.Yield();
            var res = await serviceClient.InvokeDeviceMethodAsync(lnsId,
                Constants.NetworkServerModuleId,
               new CloudToDeviceMethod(LoraDeviceManagerConstants.ClearCacheMethodName),
               cancellationToken);
            if (HttpStatusCodeUtilities.IsSuccessStatusCode(res.Status))
            {
                logger.LogInformation("Cache cleared for {gatewayID} via direct method", lnsId);
            }
            else
            {
                throw new InvalidOperationException($"Direct method call to {lnsId} failed with {res.Status}. Response: {res.GetPayloadAsJson()}");
            }
        }
    }
}
