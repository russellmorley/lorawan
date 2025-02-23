// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraDeviceManagerServices
{
    using System;
    using System.Text;
    using System.Threading;
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using LoraDeviceManager;
    using LoraDeviceManager.Utils;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class GetDeviceListTest : FunctionTestBase
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";

        [Fact]
        public async System.Threading.Tasks.Task DeviceGetter_OTAA_Join()
        {
            var devEui = TestEui.GenerateDevEui();
            var gatewayId = NewUniqueEUI64();
            var cacheStore = new LoRaInMemoryDeviceStore();
            var loraDeviceManager = new LoraDeviceManagerImpl(InitRegistryManager(devEui), cacheStore, null, null, null,NullLogger<LoraDeviceManagerImpl>.Instance, new FrameCounter(cacheStore));
            var items = await loraDeviceManager.GetDeviceList(devEui, gatewayId, new DevNonce(0xABCD), null);

            Assert.Single(items);
            Assert.Equal(devEui, items[0].DevEUI);
        }

        private static IDeviceRegistryManager InitRegistryManager(DevEui devEui)
        {
            var mockRegistryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            mockRegistryManager
                .Setup(x => x.GetDevicePrimaryKeyAsync(It.Is(devEui.ToString(), StringComparer.Ordinal)))
                .ReturnsAsync((string _) => primaryKey);

            mockRegistryManager
                .Setup(x => x.GetLoRaDeviceTwinAsync(It.Is(devEui.ToString(), StringComparer.Ordinal), It.IsAny<CancellationToken?>()))
                .ReturnsAsync((string deviceId, CancellationToken _) => new IoTHubLoRaDeviceTwin (new Twin(deviceId)));

            return mockRegistryManager.Object;
        }
    }
}
