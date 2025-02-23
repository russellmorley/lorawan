// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.Exceptions
{
    using System;

    public class DeviceNonceUsedException : Exception
    {
        public DeviceNonceUsedException(string message) : base(message)
        {
        }

        public DeviceNonceUsedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public DeviceNonceUsedException()
        {
        }
    }
}
