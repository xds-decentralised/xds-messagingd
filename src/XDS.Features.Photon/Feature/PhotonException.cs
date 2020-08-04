using System;
using System.Net;

namespace XDS.Features.Photon.Feature
{
    public sealed class PhotonException : Exception
    {
        public readonly HttpStatusCode HttpStatusCode;

        public PhotonException(string message) : this(HttpStatusCode.BadRequest, message)
        {
        }

        public PhotonException(HttpStatusCode httpStatusCode, string message, Exception innerException = null) : base(message, innerException)
        {
            this.HttpStatusCode = httpStatusCode;
        }

        public override string ToString()
        {
            return $"Error {this.HttpStatusCode}: {base.ToString()}";
        }
    }
}
