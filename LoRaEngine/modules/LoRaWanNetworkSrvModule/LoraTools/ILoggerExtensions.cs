// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    public static class ILoggerExtensions
    {
        public const string DevEUIKey = "DevEUI";
        public const string DeviceAddressKey = "DevAddr";
        public const string StationEuiKey = "StationEUI";

        public static IDisposable BeginDeviEuiScope(this ILogger logger, DevEui? devEui) =>
            devEui is { } someDevEui
            ? logger?.BeginScope(new Dictionary<string, object> { [DevEUIKey] = someDevEui.ToString() })
            : NoopDisposable.Instance;

        public static IDisposable BeginDevAddrScope(this ILogger logger, DevAddr? devAddr) =>
            devAddr is { } someDevAddr ? logger?.BeginDevAddrStringScope(someDevAddr.ToString()) : NoopDisposable.Instance;

        public static IDisposable BeginDevAddrStringScope(this ILogger logger, string devAddrString) =>
            logger?.BeginScope(new Dictionary<string, object> { [DeviceAddressKey] = devAddrString });

        public static IDisposable BeginStationEuiScope(this ILogger logger, StationEui stationEui) =>
            logger?.BeginScope(new Dictionary<string, object> { [StationEuiKey] = stationEui.ToString() });
    }
}
