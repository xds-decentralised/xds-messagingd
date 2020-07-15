using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Features.MessagingHost.Storage
{

    public sealed class MessageNodeRepository : IMessageNodeRepository
    {
        static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        readonly IAsyncRepository<XIdentity> identitiesRepository;
        readonly IAsyncRepository<XMessage> messagesRepository;
        readonly IAsyncRepository<XResendRequest> resendRequestsRepository;
        readonly ILogger logger;
        readonly Stopwatch statsWatch;

        int totalMessagesReceived;
        int totalMessagesDelivered;

        public MessageNodeRepository(ILoggerFactory loggerFactory, FStoreConfig fStoreConfig)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.identitiesRepository = new FStoreRepository<XIdentity>(new FStoreMono(fStoreConfig), XIdentityExtensions.SerializeXIdentity, XIdentityExtensions.DeserializeXIdentityCore);
            this.messagesRepository = new FStoreRepository<XMessage>(new FStoreMono(fStoreConfig), XMessageExtensions.SerializeCore, XMessageExtensions.DeserializeMessage);
            this.resendRequestsRepository = new FStoreRepository<XResendRequest>(new FStoreMono(fStoreConfig), XResendRequestExtensions.Serialize, XResendRequestExtensions.DeserializeResendRequest);
            this.statsWatch = new Stopwatch();
        }

        public async Task<string> AddIdentity(XIdentity identity, Action<string, byte[]> initTlsUser)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                if (identity == null)
                    throw new ArgumentNullException(nameof(XIdentity));
                if (identity.Id == null)
                    throw new ArgumentNullException(nameof(XIdentity.Id));
                if (identity.PublicIdentityKey == null)
                    throw new ArgumentNullException(nameof(XIdentity.PublicIdentityKey));
                if (ChatId.GenerateChatId(identity.PublicIdentityKey) != identity.Id)
                    throw new Exception("Id and public key are unrelated.");
                identity.ContactState = ContactState.Valid;

                XIdentity existing = await this.identitiesRepository.Get(identity.Id);
                if (existing == null)
                {
                    await this.identitiesRepository.Add(identity);

                }
                else
                {
                    if (!ByteArrays.AreAllBytesEqual(identity.PublicIdentityKey, existing.PublicIdentityKey))
                    {
                        throw new Exception($"Different new PublicKey for {identity.Id}. Ignoring request!");
                    }
                    existing.LastSeenUTC = DateTime.UtcNow;
                    await this.identitiesRepository.Update(existing);
                }


                if (initTlsUser != null) // TLS
                    initTlsUser(identity.Id, identity.PublicIdentityKey);

                this.logger.LogInformation($"Identity {identity.Id} was published.", nameof(MessageNodeRepository));
                return identity.Id;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<string> AddMessage(XMessage message)
        {
            if (message == null || message.Id == null)
                return null;

            await SemaphoreSlim.WaitAsync();
            try
            {
                var page = message.Id;  // message.Id is the RecipientId, which is the page (folder) where the message is stored
                var filename = NetworkPayloadHash.ComputeAsGuidString(message.SerializedPayload); // be sure not to mutate the message - NetworkPayloadHash must stay the same at sender and recipient
                message.Id = filename;  // FStore uses Id as filename
                await this.messagesRepository.Add(message, page);

                this.totalMessagesReceived++; // just stats

                return $"{message.DynamicPublicKeyId};{message.DynamicPublicKeyId}";
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<string> AddResendRequest(XResendRequest resendRequest)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                await this.resendRequestsRepository.Add(resendRequest);
                return $"{resendRequest.Id}";
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<byte> AnyNews(string recipientId)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {

                var count = await this.messagesRepository.Count(recipientId);
                return count > 0
                    ? (byte)1
                    : (byte)0;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<byte> CheckForResendRequest(XResendRequest resendRequestQuery)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                // 1.) Check if the message is still stored here
                XMessage message = await this.messagesRepository.Get(resendRequestQuery.Id, resendRequestQuery.RecipientId);
                XResendRequest resendRequest = await this.resendRequestsRepository.Get(resendRequestQuery.Id);

                // Since the client is checking for a resend request because he still sees 'XDSNetwork',
                // this means normally that the receiver has downloaded the message, has not send a resend request (yet),
                // and the sender also has not got a receipt yet (if he had, he would not be checking here).
                // When 0 is returned, the client sets the state to SendMessageState.Untracable and keeps checking till a timeout is reached.
                if (message == null && resendRequest == null)
                    return 0;

                // Ultimately it will probably be the superior solution _not_ to delete messages after one download, especially in the decentralised scenario. Deleting would need
                // cryptographic authentication by the receiver (less privacy) and the event would need to be propagated across the network. Rather, the messages could simply expire.
                // That would also enable the use of more than one device for one chat id (the message is not gone if the other device pulls it first) and in the group case
                // we also cannot delete message until the last member has got it.
                if (message != null && resendRequest != null)
                    throw new InvalidOperationException("Weird! If the message is still there, there should be no ResendRequest, or didn't we delete the message when downloading it?");

                // The message is still there, waiting to be downloaded.
                // Querying client will keep SendMessageState.OnServer and keep checking.
                if (message != null)
                    return 1;   

                return 2;       // There is no message but a ResendRequest fwas found. Yes, resend required. Client will resend and change state to SendMessageState.Resent. Will not check or resend any more.

            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<XIdentity> GetIdentityAsync(string identityId)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var foundIndentity = await this.identitiesRepository.Get(identityId);
                if (foundIndentity == null)
                    return new XIdentity { Id = identityId, ContactState = ContactState.NonExistent };
                return foundIndentity;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<IReadOnlyList<XMessage>> GetMessages(string myId)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var messages = await this.messagesRepository.GetRange(0, 3, myId);

                foreach (var message in messages)
                {
                    await this.messagesRepository.Delete(message.Id, myId);
                    message.Id = myId; // reset Id to the same value with which is was uploaded, so that when the downloader calculates the NetworkPayloadHash its the original value.
                }

                this.totalMessagesDelivered += messages.Count; // just stats

                return messages.ToList(); // serializer expects a List<XMessage>
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<RepoStats> GetStatsAsync()
        {
            await SemaphoreSlim.WaitAsync();
            this.statsWatch.Restart();

            try
            {
                IReadOnlyList<XIdentity> identities = await this.identitiesRepository.GetAll();
                uint messagesCount = 0;

                foreach (var identity in identities)
                {
                    messagesCount += await this.messagesRepository.Count(identity.Id);
                }

                return new RepoStats
                {
                    IdentitiesCount = (uint)identities.Count,
                    MessagesCount = messagesCount,
                    ResendRequestsCount = await this.resendRequestsRepository.Count(),
                    TotalMessagesReceived = this.totalMessagesReceived,
                    TotalMessagesDelivered = this.totalMessagesDelivered,
                    Time = this.statsWatch.ElapsedMilliseconds
                };
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public class RepoStats
        {
            public uint IdentitiesCount;
            public uint MessagesCount;
            public uint ResendRequestsCount;
            public int TotalMessagesReceived;
            public int TotalMessagesDelivered;
            public long Time;
        }

    }
}
