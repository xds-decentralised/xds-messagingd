using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Features.MessagingHost.RequestHandler;
using XDS.SDK.Cryptography.Api.Infrastructure;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Features.MessagingHost.Storage
{

    public sealed class MessageNodeRepository : IMessageNodeRepository
    {
        static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        readonly IAsyncRepository<XIdentity> identitiesRepository;
        readonly IAsyncRepository<XGroup> groupsRepository;
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
            this.groupsRepository = new FStoreRepository<XGroup>(new FStoreMono(fStoreConfig), XGroupExtensions.SerializeXGroup, XGroupExtensions.DeserializeXGroup);
            this.messagesRepository = new FStoreRepository<XMessage>(new FStoreMono(fStoreConfig), XMessageExtensions.SerializeCore, XMessageExtensions.DeserializeMessage);
            this.resendRequestsRepository = new FStoreRepository<XResendRequest>(new FStoreMono(fStoreConfig), XResendRequestExtensions.Serialize, XResendRequestExtensions.DeserializeResendRequest);
            this.statsWatch = new Stopwatch();
        }

        public async Task<bool> TryAddIdentity(XIdentity identity, Action<string, byte[]> initTlsUser)
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
                    this.logger.LogInformation($"Identity {identity.Id} was published.", nameof(MessageNodeRepository));
                    return true;
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

                return false;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Adds a Message and returns true, if the Message was successfully saved
        /// and did not exists before. If the message already existed, it was a no-op and false is returned.
        /// </summary>
        public async Task<bool> TryAddMessage(XMessage message)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var page = message.Id;  // message.Id is the RecipientId, which is the page (folder) where the message is stored
                var filename = NetworkPayloadHash.ComputeAsGuidString(message.SerializedPayload); // be sure not to mutate the message - NetworkPayloadHash must stay the same at sender and recipient
                message.Id = filename;  // FStore uses Id as filename
                var existing = await this.messagesRepository.Get(filename, page);
                if (existing == null)
                {
                    await this.messagesRepository.Add(message, page);

                    this.totalMessagesReceived++; // just stats
                    return true;
                }

                return false;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<bool> TryAddResendRequest(XResendRequest resendRequest)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var existing = await this.resendRequestsRepository.Get(resendRequest.Id);
                if (existing == null)
                {
                    await this.resendRequestsRepository.Add(resendRequest);
                    return true;
                }

                return false;
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
                    throw new CommandProtocolException($"Identity '{identityId}' was not found.");
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
                var maxBatchSize = 2;
                List<XMessage> ret = new List<XMessage>(maxBatchSize);

                uint startIndex = 0;
                var currentBatchSize = 0;
                bool isRetrieving = true;

                while (isRetrieving && currentBatchSize <= maxBatchSize)
                {
                    var messages = await this.messagesRepository.GetRange(startIndex, 1, myId);
                    if (messages.Count == 0)
                    {
                        isRetrieving = false;
                    }
                    else
                    {
                        var message = messages[0];
                        startIndex++; // process another message when we exited the loop.

                        if (message.IsDownloaded) // do not deliver a message twice to the client
                        {
                            continue;
                        }
                        message.IsDownloaded = true; // remember is was downloaded
                        await this.messagesRepository.Update(message, myId);
                        message.Id = myId; // reset Id to the same value with which is was uploaded, so that when the downloader calculates the NetworkPayloadHash its the original value.
                        ret.Add(message);
                        currentBatchSize++;
                    }

                }

                this.totalMessagesDelivered += ret.Count; // just stats

                return ret;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
        public async Task<IReadOnlyList<XIdentity>> GetAllIdentities()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var allIdentities = await this.identitiesRepository.GetAll();
                return allIdentities;
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

        public async Task<bool> TryAddOrUpdateGroup(XGroup xGroup, Action<string, byte[]> initTLSUser)
        {
            await SemaphoreSlim.WaitAsync();

            // todo: verify by signature check that the publisher can publish/update the item
            try
            {
                if (xGroup == null)
                    throw new ArgumentNullException(nameof(XIdentity));
                if (xGroup.Id == null)
                    throw new ArgumentNullException(nameof(XIdentity.Id));
                if (xGroup.PublicKey == null)
                    throw new ArgumentNullException(nameof(XIdentity.PublicIdentityKey));
                if (ChatId.GenerateChatId(xGroup.PublicKey) != xGroup.Id)
                    throw new Exception("Id and public key are unrelated.");

                xGroup.LocalContactState = ContactState.Valid;

                XGroup existing = await this.groupsRepository.Get(xGroup.Id);
                if (existing == null)
                {
                    xGroup.LocalCreatedDate = DateTime.UtcNow;
                    xGroup.LocalModifiedDate = xGroup.LocalCreatedDate;
                    await this.groupsRepository.Add(xGroup);
                    this.logger.LogInformation($"Group {xGroup.Id} was published.", nameof(MessageNodeRepository));
                    return true;
                }
                else
                {
                    if (!ByteArrays.AreAllBytesEqual(xGroup.PublicKey, existing.PublicKey))
                    {
                        throw new Exception($"Different new PublicKey for {xGroup.Id}. Ignoring request!");
                    }
                    existing.LocalModifiedDate = DateTime.UtcNow;
                    await this.groupsRepository.Update(existing);
                }


                if (initTLSUser != null) // TLS
                    initTLSUser(xGroup.Id, xGroup.PublicKey);

                return false;
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<XGroup> GetGroupAsync(string groupId)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var xGroup = await this.groupsRepository.Get(groupId);
                if (xGroup == null)
                    throw new CommandProtocolException($"Group '{groupId}' was not found.");
                return xGroup;
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
