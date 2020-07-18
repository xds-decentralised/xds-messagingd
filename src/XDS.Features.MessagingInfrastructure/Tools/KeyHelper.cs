using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XDS.Features.MessagingInfrastructure.Feature;
using XDS.SDK.Cryptography;

namespace XDS.Features.MessagingInfrastructure.Tools
{
    static class KeyHelper
    {
        public static void CheckBytes(byte[] bytes, int expectedLength)
        {
            if (bytes == null || bytes.Length != expectedLength || bytes.All(b => b == bytes[0]))
            {
                var display = bytes == null ? "null" : bytes.ToHexString();
                var message =
                    $"Suspicious byte array '{display}', it does not look like a cryptographic key or hash, please investigate. Expected lenght was {expectedLength}.";
                throw new X1RunnerException(System.Net.HttpStatusCode.BadRequest, message);
            }
        }
    }
}
