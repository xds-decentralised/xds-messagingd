
namespace XDS.Features.MessagingInfrastructure.Addresses
{
    public interface IJsonSerializer
    {
        byte[] Serialize<T>(T obj);

        T Deserialize<T>(byte[] serialized);

        string SerializeToString<T>(T obj);

        T DeserializeFromString<T>(string utf8);
    }
}
