using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using XDS.SDK.Cryptography;

namespace XDS.Features.Photon.Model
{
    public class Hash256 : IEquatable<Hash256>
    {
        public const int Length = 32;

        public static readonly Hash256 Zero = new Hash256(new byte[32]);

        public readonly byte[] Value;

        public Hash256(byte[] bytes)
        {
            if (bytes == null || bytes.Length != Length)
                throw new ArgumentException($"Expecting a byte[] of length {Length}");
            this.Value = bytes;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            return obj is Hash256 hash && IsEqual(hash.Value, this.Value);
        }

        public override int GetHashCode()
        {
            return BitConverter.ToInt32(this.Value, 0);
        }

        public static bool operator ==(Hash256 left, Hash256 right)
        {
            if (ReferenceEquals(left, right))
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

        public bool Equals([AllowNull] Hash256 other)
        {
            if (other == null)
                return false;

            return ReferenceEquals(this, other) || IsEqual(this.Value, other.Value);
        }
    }
}
