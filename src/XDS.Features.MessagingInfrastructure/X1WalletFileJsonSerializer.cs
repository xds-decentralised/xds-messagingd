using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NBitcoin;
using Utf8Json;
using Utf8Json.Resolvers;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Json;
using XDS.SDK.Cryptography;

namespace XDS.Features.MessagingInfrastructure
{
    public sealed class X1WalletFileJsonSerializer : IJsonSerializer
    {
        public T Deserialize<T>(byte[] serialized)
        {
            return JsonSerializer.Deserialize<T>(serialized, OrdinalResolver.Instance);
        }

        public T DeserializeFromString<T>(string utf8)
        {
            var stringBytes = Encoding.UTF8.GetBytes(utf8);
            return Deserialize<T>(stringBytes);
        }

        public byte[] Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize((object)obj, OrdinalResolver.Instance);
        }

        public string SerializeToString<T>(T obj)
        {
            var bytes = Serialize(obj);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public sealed class OrdinalResolver : IJsonFormatterResolver
    {
        public static readonly IJsonFormatterResolver Instance = new OrdinalResolver();

        static readonly IJsonFormatter[] Formatters = { new Uint256Formatter(), new Hash256Formatter() };

        static readonly IJsonFormatterResolver[] Resolvers =
        {
            EnumResolver.Default,
            StandardResolver.ExcludeNull,
        };

        OrdinalResolver()
        {
        }

        public IJsonFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.Formatter;
        }

        static class FormatterCache<T>
        {
            public static readonly IJsonFormatter<T> Formatter;

            static FormatterCache()
            {
                foreach (var item in Formatters)
                {
                    foreach (var implInterface in item.GetType().GetTypeInfo().ImplementedInterfaces)
                    {
                        var ti = implInterface.GetTypeInfo();
                        if (ti.IsGenericType && ti.GenericTypeArguments[0] == typeof(T))
                        {
                            Formatter = (IJsonFormatter<T>)item;
                            return;
                        }
                    }
                }

                foreach (var item in Resolvers)
                {
                    var f = item.GetFormatter<T>();
                    if (f != null)
                    {
                        Formatter = f;
                        return;
                    }
                }
            }
        }
    }

    public sealed class Uint256Formatter : IJsonFormatter<uint256>
    {
        public uint256 Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            if (reader.ReadIsNull())
                return null;

            // if target type is primitive, you can also use reader.Read***.
            var path = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, formatterResolver);
            return uint256.Parse(path);
        }

        public void Serialize(ref JsonWriter writer, uint256 value, IJsonFormatterResolver formatterResolver)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // if target type is primitive, you can also use writer.Write***.
            formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, value.ToString(), formatterResolver);
        }
    }

    public sealed class Hash256Formatter : IJsonFormatter<Hash256>
    {
        static bool fast = true; // ~ 2x faster
        public Hash256 Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            if (reader.ReadIsNull())
                return null;

            if (fast)
            {
                var bytesRead = reader.ReadNextBlockSegment();
                var byteArray = new byte[bytesRead.Count - 2];
                Buffer.BlockCopy(bytesRead.Array, bytesRead.Offset + 1, byteArray, 0, bytesRead.Count - 2);

                return new Hash256(byteArray.FromUtf8Hex());
            }

            var hex = formatterResolver.GetFormatterWithVerify<string>().Deserialize(ref reader, formatterResolver);
            var bytes = hex.FromHexString().Reverse().ToArray();
            return new Hash256(bytes);
        }

        public void Serialize(ref JsonWriter writer, Hash256 hash256, IJsonFormatterResolver formatterResolver)
        {
            if (hash256 == null)
            {
                writer.WriteNull();
                return;
            }

            if (fast)
            {
                var hex2 = hash256.Value.ToUtf8Hex();

                writer.WriteQuotation();
                for (var i = 0; i < hex2.Length; i++)
                {
                    var b = hex2[i];
                    writer.WriteRaw(b);
                }
                writer.WriteQuotation();
            }
            else
            {
                var hex = hash256.Value.Reverse().ToArray().ToHexString();
                formatterResolver.GetFormatterWithVerify<string>().Serialize(ref writer, hex, formatterResolver);
            }




        }
    }
}
