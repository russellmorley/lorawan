// Copyright (c) Compass Point, Inc. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using LoraDeviceManager.Exceptions;
using LoRaTools.FunctionBundler;
using LoRaTools.Services;
using LoRaWan;
using LoRaTools.BasicsStation.Processors;

namespace LoraDeviceManager.Services
{
    public class LoraDeviceManagerServices : LoraDeviceManagerServicesBase
    {
        private readonly ILoraDeviceManager loraDeviceManager;
        private readonly ILogger<LoraDeviceManagerServices> logger;

        public LoraDeviceManagerServices(
            ILoraDeviceManager loraDeviceManager,
            ILogger<LoraDeviceManagerServices> logger,
            Meter meter)
        {
            this.loraDeviceManager = loraDeviceManager;
            this.logger = logger;
        }
        public async override Task<bool> ABPFcntCacheResetAsync(DevEui devEUI, uint fcntUp, string gatewayId)
        {
            await loraDeviceManager.AbpFcntCacheReset(devEUI, gatewayId);
            return true;
        }

        public async override Task<FunctionBundlerResult> ExecuteFunctionBundlerAsync(DevEui devEUI, FunctionBundlerRequest request)
        {
            return await loraDeviceManager.ExecuteFunctionBundlerAsync(devEUI, request);
        }

        public async override Task<string> FetchStationCredentialsAsync(StationEui eui, ConcentratorCredentialType credentialtype, CancellationToken cancellationToken)
        {
            try
            {
                return await loraDeviceManager.GetStationCredentials(eui, credentialtype, cancellationToken);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException
                                           or JsonReaderException
                                           or InvalidCastException
                                           or InvalidOperationException)
            {
                this.logger.LogError(ex, ex.Message);
                return string.Empty;
            }
        }

        public async override Task<HttpContent> FetchStationFirmwareAsync(StationEui eui, CancellationToken token)
        {
            (long length, Stream contentStream) = await loraDeviceManager.GetStationFirmware(eui, token);

            if (contentStream is { } someContent)
            {
                var httpContent = new StreamContent(contentStream);
                contentStream.Position = 0;
                httpContent.Headers.ContentLength = length;
                return httpContent;
            }
            else
            {
                throw new NullReferenceException("Firmware stream content returned by blob storage is null");
            }
        }

        public override Task<string> GetPrimaryKeyByEuiAsync(DevEui eui) =>
            GetPrimaryKeyByEuiAsync(eui.ToString());

        /// <inheritdoc />
        public override Task<string> GetPrimaryKeyByEuiAsync(StationEui eui) =>
            GetPrimaryKeyByEuiAsync(eui.ToString());

        private async Task<string> GetPrimaryKeyByEuiAsync(string euiString)
        {
            var primaryKey =  await this.loraDeviceManager.GetDevicePrimaryKey(euiString);
            if (primaryKey != null)
            {
                logger.LogDebug("Search for {EuiString} found 1 device", euiString);
            }
            else
            {
                logger.LogInformation("Search for {EuiString} found 0 devices", euiString);
            }
            return primaryKey;
        }

        public async override Task<uint> NextFCntDownAsync(DevEui devEUI, uint fcntDown, uint fcntUp, string gatewayId)
        {
            return await loraDeviceManager.GetNextFCntDownAsync(devEUI, gatewayId, fcntDown, fcntUp);
        }

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchAndLockForJoinAsync(string gatewayID, DevEui devEUI, DevNonce devNonce)
            => SearchDevicesAsync(gatewayId: gatewayID, devEui: devEUI, devNonce: devNonce);

        /// <inheritdoc />
        public sealed override Task<SearchDevicesResult> SearchByDevAddrAsync(DevAddr devAddr)
            => SearchDevicesAsync(devAddr: devAddr);

        private async Task<SearchDevicesResult> SearchDevicesAsync(string gatewayId = null, DevAddr? devAddr = null, DevEui? devEui = null, string appEUI = null, DevNonce? devNonce = null)
        {
            try
            {

                var loraDeviceManagerDeviceInfos = await loraDeviceManager.GetDeviceList(devEui, gatewayId, devNonce, devAddr);
                var json = JsonConvert.SerializeObject(loraDeviceManagerDeviceInfos);
                var devices = (List<IoTHubDeviceServiceInfo>)JsonConvert.DeserializeObject(json, typeof(List<IoTHubDeviceServiceInfo>));

                return new SearchDevicesResult(devices);
            }
            catch (DeviceNonceUsedException)
            {
                return new SearchDevicesResult
                {
                    IsDevNonceAlreadyUsed = true,
                };
            }
            catch (JoinRefusedException ex) when (ExceptionFilterUtility.True(() => this.logger.LogDebug("Join refused: {msg}", ex.Message)))
            {
                return new SearchDevicesResult()
                {
                    RefusedMessage = "JoinRefused: " + ex.Message
                };
            }
            catch (ArgumentException ex)
            {

                logger.LogError("{DevAddr} error calling loraDeviceManager.GetDeviceList: {Message}. Details: {Stack}", devAddr, ex.Message, ex);

                // TODO: FBE check if we return null or throw exception
                return new SearchDevicesResult();
            }
        }


        public override Task SendJoinNotificationAsync(DeviceJoinNotification deviceJoinNotification, CancellationToken token)
        {
            loraDeviceManager.AddDevice(deviceJoinNotification.DevAddr, deviceJoinNotification.DevEUI, deviceJoinNotification.GatewayId, deviceJoinNotification.NwkSKeyString);
            return Task.CompletedTask;
        }

        /// <summary>
        /// TODO: set up a local cron job on edge device
        /// </summary>
        /// <returns></returns>
        public Task SyncLoraDevAddrCacheWithRegistry()
        {
            return loraDeviceManager.SyncLoraDevAddrCacheWithRegistry();
        }
    }
}
