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
            logger.LogDebug("syncing FCntDown for multigateway");

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

            var url = BuildUri("NextFCntDown", queryParameters);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"error calling the NextFCntDown function, check the function log. {response.ReasonPhrase}");
                return 0;
            }

            var fcntDownString = await response.Content.ReadAsStringAsync();

            if (ushort.TryParse(fcntDownString, out var newFCntDown))
                return newFCntDown;

            return 0;
        }

        public override async Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(DevEui devEUI, FunctionBundlerRequest request)
        {
            using var client = CreateClient();

            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri($"FunctionBundler/{devEUI}", queryParameters);

            var requestBody = JsonConvert.SerializeObject(request);

            using var content = PreparePostContent(requestBody);
            using var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"error calling the bundling function, check the function log. {response.ReasonPhrase}");
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<FunctionBundlerResult>(payload);
        }

        public override async Task<bool> ABPFcntCacheResetAsync(DevEui devEUI, uint fcntUp, string gatewayId)
        {
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

            var url = BuildUri("NextFCntDown", queryParameters);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"error calling the NextFCntDown function, check the function log, {response.ReasonPhrase}");
                return false;
            }

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

            var url = BuildUri("GetDevice", queryParameters);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var badReqResult = await response.Content.ReadAsStringAsync();

                    if (string.Equals(badReqResult, "UsedDevNonce", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SearchDevicesResult
                        {
                            IsDevNonceAlreadyUsed = true,
                        };
                    }

                    if (badReqResult != null && badReqResult.StartsWith("JoinRefused", StringComparison.OrdinalIgnoreCase))
                    {
                        return new SearchDevicesResult()
                        {
                            RefusedMessage = badReqResult
                        };
                    }
                }

                logger.LogError($"{devAddr} error calling get device function api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log");

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }

            var result = await response.Content.ReadAsStringAsync();
            var devices = (List<IoTHubDeviceServiceInfo>)JsonConvert.DeserializeObject(result, typeof(List<IoTHubDeviceServiceInfo>));
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
            deviceLoadRequests?.Add(1);

            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>
            {
                ["code"] = AuthCode,
                ["DevEUI"] = eui
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri("GetDeviceByDevEUI", queryParameters);
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return default;
                }

                logger.LogError($"error calling get device/station by EUI api: {response.ReasonPhrase}, status: {response.StatusCode}, check the azure function log");

                return default;
            }

            return await response.Content.ReadAsStringAsync() is { Length: > 0 } json
                   && JsonDocument.Parse(json).RootElement is { ValueKind: JsonValueKind.Object } root
                   && root.EnumerateObject()
                          .FirstOrDefault(p => PrimaryKeyPropertyName.Equals(p.Name, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.String } property
                   ? property.Value.GetString()
                   : null;
        }

        public override async Task<string> FetchStationCredentialsAsync(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken token)
        {
            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["StationEui"] = eui.ToString(),
                ["CredentialType"] = credentialtype.ToString()
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri("FetchConcentratorCredentials", queryParameters);

            var response = await client.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is not System.Net.HttpStatusCode.NotFound)
                {
                    logger.LogError($"error calling fetch station credentials api: {response.ReasonPhrase}, status: {response.StatusCode}, content: {response.Content}, check the azure function log");
                }

                return string.Empty;
            }

            return await response.Content.ReadAsStringAsync(token);
        }

        public override async Task<HttpContent> FetchStationFirmwareAsync(StationEui eui, CancellationToken token)
        {
            using var client = CreateClient();
            var queryParameters = new Dictionary<string, string>()
            {
                ["code"] = AuthCode,
                ["StationEui"] = eui.ToString()
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri("FetchConcentratorFirmware", queryParameters);

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"error calling fetch station firmware api: {response.ReasonPhrase}, status: {response.StatusCode}, content {response.Content}, check the azure function log");
            }

            return response.Content;
        }

        public override async Task SendJoinNotificationAsync(DeviceJoinNotification deviceJoinNotification, CancellationToken token)
        {
            using var client = CreateClient();
            const string FunctionName = "DeviceJoinNotification";
            var queryParameters = new Dictionary<string, string>
            {
                ["code"] = AuthCode
            };
            queryParameters = tenantValidationStrategy.AddQueryParameters(queryParameters, CallingGatewayId, TenantId, TenantKey, DateTime.Now.AddMinutes(10));

            var url = BuildUri(FunctionName, queryParameters);

            var requestBody = JsonConvert.SerializeObject(deviceJoinNotification);

            using var content = PreparePostContent(requestBody);
            using var response = await client.PostAsync(url, content, token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"error calling the {FunctionName} function, check the function log. {response.ReasonPhrase}");
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
