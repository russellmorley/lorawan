// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraDeviceManager.Utils
{

    /// <summary>
    /// Http utilities.
    /// </summary>
    public static class HttpStatusCodeUtilities
    {
        /// <summary>
        /// Checks if the http status code indicates success.
        /// </summary>
        public static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;
    }
}
