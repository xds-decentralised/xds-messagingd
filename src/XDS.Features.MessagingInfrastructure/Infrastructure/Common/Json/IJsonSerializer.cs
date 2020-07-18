
namespace XDS.Features.MessagingInfrastructure.Infrastructure.Common.Json
{
    public interface IJsonSerializer
    {
        byte[] Serialize<T>(T obj);

        T Deserialize<T>(byte[] serialized);

        string SerializeToString<T>(T obj);

        T DeserializeFromString<T>(string utf8);
    }
}
