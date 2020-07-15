using System;
using System.Threading.Tasks;
using XDS.Features.MessagingHost.Storage;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.MessagingHost.RequestHandler
{
	public class CommandProcessor
	{
		readonly IMessageNodeRepository messageNodeRepository;

		public CommandProcessor(IMessageNodeRepository messageNodeRepository)
		{
			this.messageNodeRepository = messageNodeRepository;
		}

		public async Task<byte[]> ExecuteAuthenticatedRequestAsync(Command command)
		{
			switch (command.CommandId)
			{
				case CommandId.AnyNews:
					{
						string recipientId = command.CommandData.DeserializeStringCore();
						var result = await this.messageNodeRepository.AnyNews(recipientId);
						return new RequestCommand(CommandId.AnyNews_Response, result).Serialize(CommandHeader.Yes);  // only when the client receives it via UDP a header is needed.
					}
				case CommandId.CheckForResendRequest:
					{
						XResendRequest resendRequestQuery = command.CommandData.DeserializeResendRequest();
						byte result = await this.messageNodeRepository.CheckForResendRequest(resendRequestQuery);
						return new RequestCommand(CommandId.CheckForResendRequest_Response, result).Serialize(CommandHeader.Yes);
					}
				case CommandId.DownloadMessages:
					{
						string recipientId = command.CommandData.DeserializeStringCore();
						var messages = await this.messageNodeRepository.GetMessages(recipientId);
						if (messages.Count == 0)
							throw new CommandProtocolException("No message to download, please check with CommandId.AnyNews first."); // connected clients get this error information
						return new RequestCommand(CommandId.DownloadMessage_Response, messages).Serialize(CommandHeader.Yes);
					}
				case CommandId.UploadMessage:
					{
						XMessage message = command.CommandData.DeserializeMessage(); // Deserialize only what's needed, blob-store the rest
						string ack = await this.messageNodeRepository.AddMessage(message);
						return new RequestCommand(CommandId.UploadMessage_Response, ack).Serialize(CommandHeader.Yes);
					}
				case CommandId.UploadResendRequest:
					{
						XResendRequest resendRequest = command.CommandData.DeserializeResendRequest();
						var resendRequestAck = await this.messageNodeRepository.AddResendRequest(resendRequest);
						return new RequestCommand(CommandId.UploadResendRequest_Response, resendRequestAck).Serialize(CommandHeader.Yes);
					}
				case CommandId.GetIdentity:
					{
						var addedContactId = command.CommandData.DeserializeStringCore();
						XIdentity identity = await this.messageNodeRepository.GetIdentityAsync(addedContactId);
						return new RequestCommand(CommandId.GetIdentity_Response, identity).Serialize(CommandHeader.Yes);
					}
				case CommandId.PublishIdentity:
					{
						XIdentity identityBeingPublished = command.CommandData.DeserializeXIdentityCore();
						string result = await this.messageNodeRepository.AddIdentity(identityBeingPublished, null);
						return new RequestCommand(CommandId.PublishIdentity_Response, result).Serialize(CommandHeader.Yes);
					}
				default:
					throw new CommandProtocolException($"Unknown CommandId {command.CommandId}.");
			}
		}

		public byte[] ExecuteLostDynamicKey()
		{
			byte dummy = 0xcc; // it doesn't work with zero size contents
			return new RequestCommand(CommandId.LostDynamicKey_Response, dummy).Serialize(CommandHeader.Yes);
		}

		public byte[] ExecuteNoSuchUser()
		{
			byte dummy = 0xcc; // it doesn't work with zero size contents
			return new RequestCommand(CommandId.NoSuchUser_Response, dummy).Serialize(CommandHeader.Yes);
		}

		public async Task<byte[]> ExecutePublishIdentityAsync(Command command, Action<string, byte[]> initTlsUser)
		{
			if (command.CommandId != CommandId.PublishIdentity)
				throw new InvalidOperationException();

			XIdentity identityBeingPublished = command.CommandData.DeserializeXIdentityCore();
			string result = await this.messageNodeRepository.AddIdentity(identityBeingPublished, initTlsUser);
			return new RequestCommand(CommandId.PublishIdentity_Response, result).Serialize(CommandHeader.Yes);
		}
	}
}
