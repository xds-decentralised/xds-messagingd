using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace XDS.Features.MessagingInfrastructure.Feature
{
    public sealed class X1RunnerException : Exception
    {
        public readonly HttpStatusCode HttpStatusCode;

        public X1RunnerException(string message) : this(HttpStatusCode.BadRequest, message)
        {
        }

        public X1RunnerException(HttpStatusCode httpStatusCode, string message, Exception innerException = null) : base(message, innerException)
        {
            this.HttpStatusCode = httpStatusCode;
        }

        public override string ToString()
        {
            return $"Error {this.HttpStatusCode}: {base.ToString()}";
        }
    }
}
