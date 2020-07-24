using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.BlockchainIntegration;

namespace XDS.Features.ItemForwarding.Client.Data
{
	public static class MessageRelayRecordSerializer
	{
		public static byte[] Serialize(MessageRelayRecord messageRelayRecord)
		{
			byte[] serialized = PocoSerializer.Begin()
				.Append(messageRelayRecord.Id)
				.Append(messageRelayRecord.LastSeenUtc)
                .Append(messageRelayRecord.LastErrorUtc)
                .Append(messageRelayRecord.ErrorScore)
				.Finish();
			return serialized;
		}

		public static MessageRelayRecord Deserialize(byte[] serializedMessageNodeRecord)
		{
			if (serializedMessageNodeRecord == null)
				return null;

			var ser = PocoSerializer.GetDeserializer(serializedMessageNodeRecord);

            var messageNodeRecord = new MessageRelayRecord
            {
                Id = ser.MakeString(0),
                LastSeenUtc = ser.MakeDateTime(1),
                LastErrorUtc = ser.MakeDateTime(2),
                ErrorScore = ser.MakeInt32(3)
            };

            var (ipAddress, port) = messageNodeRecord.Id.ToAddress();
			messageNodeRecord.IpAddress = ipAddress;
			messageNodeRecord.MessagingPort = port;

			return messageNodeRecord;
		}

        
    }
}
