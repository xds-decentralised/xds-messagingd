using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using XDS.SDK.Cryptography;

namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs
{
    public class Hash256
    {
        public const int Length = 32;

        public static readonly Hash256 Zero = new Hash256(new byte[32]);

        public readonly byte[] Value;

        int hashCode;
        string str;

        public Hash256(byte[] bytes)
        {
            if (bytes == null || bytes.Length != Length)
                throw new ArgumentException($"Expecting a byte[] of lenght {Length}");
            this.Value = bytes;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            return obj is Hash256 hash && hash.Value != null && IsEqual(hash.Value, this.Value);
        }

        public override int GetHashCode()
        {
            if (this.hashCode != 0)
                return this.hashCode;

            for (var i = 0; i < Length / 4; i += 4)
                this.hashCode ^= BitConverter.ToInt32(this.Value, i);
            return this.hashCode;
        }

        public static bool operator ==(Hash256 left, Hash256 right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null))
                return true;

            if (!ReferenceEquals(left, null) && !ReferenceEquals(right, null))
                return IsEqual(left.Value, right.Value);

            return false; // one is null, one not
        }

        public static bool operator !=(Hash256 left, Hash256 right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.Value.ToUtf8Hex());
        }

        static bool IsEqual(byte[] arr1, byte[] arr2)
        {
            Debug.Assert(arr1 != null && arr2 != null);
            Debug.Assert(arr1.Length == Length && arr2.Length == Length); // guaranteed in constructor
            for (var i = 0; i < Length; i++)
                if (arr1[i] != arr2[i])
                    return false;
            return true;
        }
    }
}
