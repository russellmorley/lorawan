// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.FunctionBundler
{
    using LoRaTools.CacheStore;
    using Newtonsoft.Json;
    using System;

    /// <summary>
    /// Defines a preferred gateway result.
    /// </summary>
    public class PreferredGatewayResult
    {
        public uint RequestFcntUp { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public uint? CurrentFcntUp { get; set; }

        public string PreferredGatewayID { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether there was a conflict in the preferred gateway resolution.
        /// </summary>
        /// <remarks>
        /// A conflict happens if a request to resolve the preferred gateway is received with a fcntUp older than the current resolved one.
        /// Causes are the calling gateway took too long to call the function while another device requests have been addressed by other gateways.
        /// </remarks>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool Conflict { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Indicates if the preferred gateway resolution was executed successfully.
        /// </summary>
        public bool IsSuccessful() => !Conflict && string.IsNullOrEmpty(ErrorMessage);

        public PreferredGatewayResult()
        {
        }

        public PreferredGatewayResult(uint fcntUp, LoRaDevicePreferredGateway preferredGateway)
        {
            if (preferredGateway is null) throw new ArgumentNullException(nameof(preferredGateway));

            RequestFcntUp = fcntUp;
            CurrentFcntUp = preferredGateway.FcntUp;
            PreferredGatewayID = preferredGateway.GatewayID;
            Conflict = fcntUp != preferredGateway.FcntUp;
        }

        public PreferredGatewayResult(uint fcntUp, string errorMessage)
        {
            RequestFcntUp = fcntUp;
            ErrorMessage = errorMessage;
        }
    }
}
