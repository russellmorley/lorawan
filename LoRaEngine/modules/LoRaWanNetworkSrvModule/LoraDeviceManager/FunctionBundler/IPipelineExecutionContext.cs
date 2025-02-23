// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.FunctionBundler
{
    using LoRaTools.FunctionBundler;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public interface IPipelineExecutionContext
    {
        DevEui DevEUI { get; }

        FunctionBundlerRequest Request { get; }

        FunctionBundlerResult Result { get; }

        ILogger Logger { get; }
    }
}
