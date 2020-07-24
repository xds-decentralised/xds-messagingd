using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Features.ItemForwarding.Client.Data
{
    public class MessageRelayRecordRepository
    {
        static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1, 1);

        readonly FStoreRepository<MessageRelayRecord> messageNodeRecords;

        public MessageRelayRecordRepository(FStoreConfig fStoreConfig)
        {
            this.messageNodeRecords = new FStoreRepository<MessageRelayRecord>(new FStoreMono(fStoreConfig), MessageRelayRecordSerializer.Serialize, MessageRelayRecordSerializer.Deserialize);
        }


        public async Task AddMessageNodeRecordAsync(MessageRelayRecord peer)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                var existing = await this.messageNodeRecords.Get(peer.Id);
                if (existing == null)
                {
                    await this.messageNodeRecords.Add(peer);
                }
                   
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<MessageRelayRecord> GetMessageNodeRecordAsync(string id)
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.messageNodeRecords.Get(id);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<uint> GetMessageNodeRecordCountAsync()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.messageNodeRecords.Count();
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task<IReadOnlyList<MessageRelayRecord>> GetAllMessageRelayRecordsAsync()
        {
            await SemaphoreSlim.WaitAsync();
            try
            {
                return await this.messageNodeRecords.GetAll();
            }
            finally
            {
                SemaphoreSlim.Release();
            }

        }

        public async Task UpdatePeerLastError(string peerId, DateTime lastError)
        {
            await SemaphoreSlim.WaitAsync();

            try
            {
                //Peer contact = await this.messageNodeRecords.Get(peerId);
                //contact.LastError = lastError;
                //await this.messageNodeRecords.Update(contact,null);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }

        public async Task UpdatePeerAfterHandshake(MessageRelayRecord messageRelayRecord)
        {
            await SemaphoreSlim.WaitAsync();

            try
            {
                //Peer existingPeer = await this.messageNodeRecords.Get(peer.Id);
                //existingPeer.PeerServices = peer.PeerServices;
                //existingPeer.LastSeen = peer.LastSeen;
                //existingPeer.LastError = DateTime.MaxValue;
                //await this.messageNodeRecords.Update(existingPeer, null);
            }
            finally
            {
                SemaphoreSlim.Release();
            }
        }
    }

}
