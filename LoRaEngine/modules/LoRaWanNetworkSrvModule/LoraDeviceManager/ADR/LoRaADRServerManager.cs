// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.ADR
{
    using System.Threading.Tasks;
    using LoraDeviceManager.Utils;
    using LoRaTools.ADR;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class LoRaADRServerManager : LoRaADRManagerBase
    {
        private readonly FrameCounter frameCounter;

        public LoRaADRServerManager(ILoRaADRStore loraAdrStore,
                                    ILoRaADRStrategyProvider loraAdrStrategyProvider,
                                    FrameCounter frameCounter,
                                    ILogger<LoRaADRServerManager> logger)
            : base(loraAdrStore, loraAdrStrategyProvider, logger)
        {
            this.frameCounter = frameCounter;
        }

        public override async Task<uint> NextFCntDown(DevEui devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            return await frameCounter.GetNextFCntDownAsync(devEUI, gatewayId, clientFCntUp, clientFCntDown);
        }
    }
}
