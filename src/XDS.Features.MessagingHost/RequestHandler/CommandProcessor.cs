using System;
using System.Threading.Tasks;
using XDS.Features.ItemForwarding.Client;
using XDS.Features.MessagingHost.Storage;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.MessagingHost.RequestHandler
{
    public class CommandProcessor
    {
        readonly IMessageNodeRepository messageNodeRepository;
        readonly IPhotonService photonService;
        readonly ItemForwarding.ItemForwarding itemForwarding;

        public CommandProcessor(IMessageNodeRepository messageNodeRepository, IPhotonService photonService, ItemForwarding.ItemForwarding itemForwarding, MessageRelayConnectionFactory messageRelayConnectionFactory)
        {
            this.messageNodeRepository = messageNodeRepository;
            this.photonService = photonService;
            this.itemForwarding = itemForwarding;
            messageRelayConnectionFactory.GetAllIdentities = messageNodeRepository.GetAllIdentities;
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
                        var isNewAndWasAdded = await this.messageNodeRepository.TryAddMessage(message);
                        string ack = $"{message.DynamicPublicKeyId};{message.DynamicPublicKeyId}";

                        // We only forward messages when we received them the first time. Otherwise, we'll endlessly 
                        // forward the message, when we receive them back from other nodes, forward them, receive them back and so on.
                        if (isNewAndWasAdded)
                            this.itemForwarding.PushAndForget(command);

                        return new RequestCommand(CommandId.UploadMessage_Response, ack).Serialize(CommandHeader.Yes);
                    }
                case CommandId.UploadResendRequest:
                    {
                        XResendRequest resendRequest = command.CommandData.DeserializeResendRequest();
                        var isNewAndWasAdded = await this.messageNodeRepository.TryAddResendRequest(resendRequest);

                        // We only forward identities when we received them the first time. Otherwise, we'll endlessly 
                        // forward the message, when we receive them back from other nodes, forward them, receive them back and so on.
                        if (isNewAndWasAdded)
                            this.itemForwarding.PushAndForget(command);

                        return new RequestCommand(CommandId.UploadResendRequest_Response, resendRequest.Id).Serialize(CommandHeader.Yes);
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
                        var isNewAndWasAdded = await this.messageNodeRepository.TryAddIdentity(identityBeingPublished, null);

                        // We only forward identities when we received them the first time. Otherwise, we'll endlessly 
                        // forward the message, when we receive them back from other nodes, forward them, receive them back and so on.
                        if (isNewAndWasAdded)
                            this.itemForwarding.PushAndForget(command);
                       
                        return new RequestCommand(CommandId.PublishIdentity_Response, identityBeingPublished.Id).Serialize(CommandHeader.Yes);
                    }
                case CommandId.GetGroup:
                {
                    var groupId = command.CommandData.DeserializeStringCore();
                    XGroup xGroup = await this.messageNodeRepository.GetGroupAsync(groupId);
                    return new RequestCommand(CommandId.GetGroup_Response, xGroup).Serialize(CommandHeader.Yes);
                }
                case CommandId.PublishGroup:
                {
                    XGroup xGroup = command.CommandData.DeserializeXGroup();
                    var success = await this.messageNodeRepository.TryAddOrUpdateGroup(xGroup, null);

                    // We only forward items when we received them the first time. Otherwise, we'll endlessly 
                    // forward the item, when we receive them back from other nodes, forward them, receive them back and so on.
                    if (success)
                        this.itemForwarding.PushAndForget(command);

                    return new RequestCommand(CommandId.PublishIdentity_Response, xGroup.Id).Serialize(CommandHeader.Yes);
                }
                case CommandId.PhotonBalance:
                    {
                        string address_options = command.CommandData.DeserializeStringCore();
                        var parts = address_options.Split(";");
                        if (parts.Length != 2)
                            throw new CommandProtocolException($"{nameof(CommandId.PhotonBalance)} requires [address;PhotonFlags].");
                        string address = parts[0];
                        PhotonFlags photonFlags = (PhotonFlags)int.Parse(parts[1]);
                        ValueTuple<long, int, byte[], int> balance = ((long, int, byte[], int))this.photonService.GetPhotonBalance(address, photonFlags);
                        return new RequestCommand(CommandId.PhotonBalance_Response, balance).Serialize(CommandHeader.Yes);
                    }
                case CommandId.PhotonOutputs:
                    {
                        string address_options = command.CommandData.DeserializeStringCore();
                        var parts = address_options.Split(";");
                        if (parts.Length != 2)
                            throw new CommandProtocolException($"{nameof(CommandId.PhotonBalance)} requires [address;PhotonFlags].");
                        string address = parts[0];
                        PhotonFlags photonFlags = (PhotonFlags)int.Parse(parts[1]);
                        ValueTuple<long, int, byte[], IPhotonOutput[], int> balance = ((long, int, byte[], IPhotonOutput[], int))this.photonService.GetPhotonOutputs(address, photonFlags);
                        return new RequestCommand(CommandId.PhotonOutputs_Response, balance).Serialize(CommandHeader.Yes);
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
            bool isNewAndWasAdded = await this.messageNodeRepository.TryAddIdentity(identityBeingPublished, initTlsUser);
           if(isNewAndWasAdded)
               this.itemForwarding.PushAndForget(command);
            return new RequestCommand(CommandId.PublishIdentity_Response, identityBeingPublished.Id).Serialize(CommandHeader.Yes);
        }
    }
}
