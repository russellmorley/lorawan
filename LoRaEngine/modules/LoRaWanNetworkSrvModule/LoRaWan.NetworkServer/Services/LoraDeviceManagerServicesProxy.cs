// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.BasicsStation.Processors;
    using LoRaTools.FunctionBundler;
    using LoRaTools.Services;
    using LoRaWan;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public sealed class LoraDeviceManagerServicesProxy : LoraDeviceManagerServicesBase
    {
        private const string PrimaryKeyPropertyName = "PrimaryKey";
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<LoraDeviceManagerServicesProxy> logger;
        private readonly ITenantValidationStrategy tenantValidationStrategy;
        private readonly Counter<int> deviceLoadRequests;

        public LoraDeviceManagerServicesProxy(NetworkServerConfiguration configuration,
                                    IHttpClientFactory httpClientFactory,
                                    ILogger<LoraDeviceManagerServicesProxy> logger,
                                    ITenantValidationStrategy tenantValidationStrategy,
                                    Meter meter)
        {
            if (meter is null) throw new ArgumentNullException(nameof(meter));

            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
            this.tenantValidationStrategy = tenantValidationStrategy;
            deviceLoadRequests = meter.CreateCounter<int>(MetricRegistry.DeviceLoadRequests);

            if (configuration is null) throw new ArgumentNullException(nameof(configuration));
            AuthCode = configuration.DeviceManagerServicesCode;
            URL = configuration.DeviceManagerServicesUrl;
            TenantId = configuration.TenantId;
            TenantKey = configuration.TenantKey;
            CallingGatewayId = configuration.GatewayID;
        }

        public override async Task<uint> NextFCntDownAsync(DevEui devEUI, uint fcntDown, uint fcntUp, string gatewayId)
        {
            var relativePath = "NextFCntDown";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);
 
            using var client = CreateClient();
 
            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["DevEUI"] = devEUI.ToString(),
                ["FCntDown"] = fcntDown.ToString(CultureInfo.InvariantCulture),
                ["FCntUp"] = fcntUp.ToString(CultureInfo.InvariantCulture),
                ["GatewayId"] = gatewayId,
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                return 0;
            }

            var receivedBodyString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Body string received: {BodyString}", receivedBodyString);

            if (ushort.TryParse(receivedBodyString, out var newFCntDown))
                return newFCntDown;

            return 0;
        }

        public override async Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(DevEui devEUI, FunctionBundlerRequest request)
        {
            var relativePath = $"FunctionBundler/{devEUI}";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            using var client = CreateClient();

            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var sendBodyString = JsonConvert.SerializeObject(request);
            logger.LogDebug("Body string to send: {BodyString}", sendBodyString);

            using var content = PreparePostContent(sendBodyString);
            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            var receivedBodyString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Body string received: {BodyString}", receivedBodyString);

            return JsonConvert.DeserializeObject<FunctionBundlerResult>(receivedBodyString);
        }

        public override async Task<bool> ABPFcntCacheResetAsync(DevEui devEUI, uint fcntUp, string gatewayId)
        {
            var relativePath = "NextFCntDown";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["DevEUI"] = devEUI.ToString(),
                ["ABPFcntCacheReset"] = "true",
                ["FCntUp"] = fcntUp.ToString(CultureInfo.InvariantCulture),
                ["GatewayId"] = gatewayId,
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));
            
            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                return false;
            }
            logger.LogDebug("Received success status code");
            return true;
        }

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, DevEui devEUI, DevNonce devNonce)
            => SearchDevicesAsync(gatewayID: gatewayID, devEui: devEUI, devNonce: devNonce);

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchByDevAddrAsync(DevAddr devAddr)
            => SearchDevicesAsync(devAddr: devAddr);

        /// <summary>
        /// Helper method that calls the API GetDevice method.
        /// </summary>
        private async Task<SearchDevicesResult> SearchDevicesAsync(string gatewayID = null, DevAddr? devAddr = null, DevEui? devEui = null, string appEUI = null, DevNonce? devNonce = null)
        {
            var relativePath = "GetDevice";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            deviceLoadRequests?.Add(1);

            using var client = CreateClient();


            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["GateWayId"] = gatewayID,
                ["DevAddr"] = devAddr?.ToString(),
                ["DevEUI"] = devEui?.ToString(),
                ["AppEUI"] = appEUI,
                ["DevNonce"] = devNonce?.ToString(),
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));


            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var receivedBodyStringBadRequest = await response.Content.ReadAsStringAsync();
                    logger.LogDebug("Status code BadRequest body string received: {BodyString}", receivedBodyStringBadRequest);

                    if (string.Equals(receivedBodyStringBadRequest, "UsedDevNonce", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SearchDevicesResult
                        {
                            IsDevNonceAlreadyUsed = true,
                        };
                    }

                    if (receivedBodyStringBadRequest != null && receivedBodyStringBadRequest.StartsWith("JoinRefused", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SearchDevicesResult()
                        {
                            RefusedMessage = receivedBodyStringBadRequest
                        };
                    }
                }

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }

            var receivedBodyString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Body string received: {BodyString}", receivedBodyString);
            var devices = (List<IoTHubDeviceServiceInfo>)JsonConvert.DeserializeObject(receivedBodyString, typeof(List<IoTHubDeviceServiceInfo>));
            return new SearchDevicesResult(devices);
        }

        /// <inheritdoc />
        public override Task<string> GetPrimaryKeyByEuiAsync(DevEui eui) =>
            GetPrimaryKeyByEuiAsync(eui.ToString());

        /// <inheritdoc />
        public override Task<string> GetPrimaryKeyByEuiAsync(StationEui eui) =>
            GetPrimaryKeyByEuiAsync(eui.ToString());

        private async Task<string> GetPrimaryKeyByEuiAsync(string eui)
        {
            var relativePath = "GetDeviceByDevEUI";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            deviceLoadRequests?.Add(1);

            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>
            {
                ["code"] = AuthCode,
                ["DevEUI"] = eui
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogDebug("Calling {RelativePath} resulted in StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                    return default;
                }

                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                return default;
            }

            var receivedBodyString = await response.Content.ReadAsStringAsync();
            logger.LogDebug("Body string received: {BodyString}", receivedBodyString);

            return receivedBodyString is { Length: > 0 } json
                   && JsonDocument.Parse(json).RootElement is { ValueKind: JsonValueKind.Object } root
                   && root.EnumerateObject()
                          .FirstOrDefault(p => PrimaryKeyPropertyName.Equals(p.Name, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.String } property
                   ? property.Value.GetString()
                   : null;
        }

        public override async Task<string> FetchStationCredentialsAsync(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken token)
        {
            var relativePath = "FetchConcentratorCredentials";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["StationEui"] = eui.ToString(),
                ["CredentialType"] = credentialtype.ToString()
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var response = await client.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is not System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                }
                logger.LogDebug("Calling {RelativePath} resulted in StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                return string.Empty;
            }

            var receivedBodyString = await response.Content.ReadAsStringAsync(token);
            logger.LogDebug("Body string received: {BodyString}", receivedBodyString);

            return receivedBodyString;
        }

        public override async Task<HttpContent> FetchStationFirmwareAsync(StationEui eui, CancellationToken token)
        {
            var relativePath = "FetchConcentratorFirmware";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["StationEui"] = eui.ToString()
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
                throw new InvalidOperationException(string.Format("Error calling {0}. StatusCode {1}, Reason: {2}", relativePath, response.StatusCode,response.ReasonPhrase));
            }

            logger.LogDebug("Binary body content received");
            return response.Content;
        }

        public override async Task SendJoinNotificationAsync(DeviceJoinNotification deviceJoinNotification, CancellationToken token)
        {
            var relativePath = "DeviceJoinNotification";

            using var scope = logger.BeginScope("{RelativePath}: ", relativePath);

            using var client = CreateClient();

            var queryParameters = new Dictionary<string, string>
            {
                ["code"] = AuthCode
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(relativePath, queryParameters);
            logger.LogDebug("QueryParams: {Query}", JsonConvert.SerializeObject(url.Query));

            var sendBodyString = JsonConvert.SerializeObject(deviceJoinNotification);
            logger.LogDebug("Body string to send: {BodyString}", sendBodyString);

            using var content = PreparePostContent(sendBodyString);
            using var response = await client.PostAsync(url, content, token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error calling {RelativePath}. StatusCode {StatusCode}, Reason: {ReasonPhrase}", relativePath, response.StatusCode, response.ReasonPhrase);
            }
        }

        private HttpClient CreateClient() => httpClientFactory.CreateClient(LoRaApiHttpClient.Name);
        internal Uri GetFullUri(string relativePath)
        {
            // If base URL does not end with a slash, the relative path component is discarded.
            // https://docs.microsoft.com/en-us/dotnet/api/system.uri.-ctor?view=net-5.0#System_Uri__ctor_System_Uri_System_String_
            var baseUrl = URL.OriginalString.EndsWith('/') ? URL : new Uri($"{URL.OriginalString}/");
            return new Uri(baseUrl, relativePath);
        }

        internal Uri BuildUri(string relativePath, IDictionary<string, string> queryParameters)
        {
            var baseUrl = GetFullUri(relativePath);

            var queryParameterSb = new StringBuilder(relativePath);
            queryParameterSb = queryParameters
                .Where(qp => !string.IsNullOrEmpty(qp.Value))
                .Select((qp, i) => $"{(i == 0 ? "?" : "&")}{qp.Key}={qp.Value}")
                .Aggregate(queryParameterSb, (sb, qp) => sb.Append(qp));

            return new Uri(baseUrl, queryParameterSb.ToString());
        }
        protected static ByteArrayContent PreparePostContent(string requestBody)
        {
            var buffer = Encoding.UTF8.GetBytes(requestBody);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return byteContent;
        }
    }
}
