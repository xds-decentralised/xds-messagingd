using System;
using System.Diagnostics;
using System.IO;
using Blockcore.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using XDS.Features.MessagingInfrastructure.Blockchain;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Json;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet;

namespace XDS.Features.MessagingInfrastructure.Tools
{
    public sealed class IndexFileHelper
    {
        readonly IJsonSerializer jsonSerializer;
        readonly ILogger logger;
        readonly Network network;

        readonly Stopwatch stopWatchSaving;

        const string AddressIndexFilename = "addressindex.json";
        const string BlockIndexFilename = "blockindex.json";

        readonly string addressIndexFilePath;
        readonly string blockIndexFilePath;

        DateTime lastSaved;

        public IndexFileHelper(IJsonSerializer jsonSerializer, ILoggerFactory loggerFactory, NodeSettings nodeSettings, Network network)
        {
            this.jsonSerializer = jsonSerializer;
            this.logger = loggerFactory.CreateLogger<IndexFileHelper>();
            this.stopWatchSaving = new Stopwatch();
            this.addressIndexFilePath = Path.Combine(nodeSettings.DataDir, AddressIndexFilename);
            this.blockIndexFilePath = Path.Combine(nodeSettings.DataDir, BlockIndexFilename);
            this.network = network;
        }

        public (XDSAddressIndex, XDSBlockIndex) LoadIndexes()
        {
            if (!File.Exists(this.addressIndexFilePath) || !File.Exists(this.blockIndexFilePath))
            {
                DeleteIndexes();
                var result = CreateIndexes();
                SaveIndexes(result.Item1, result.Item2, true);
            }

            try
            {
                byte[] file = File.ReadAllBytes(this.addressIndexFilePath);
                var addressIndex = this.jsonSerializer.Deserialize<XDSAddressIndex>(file);

                file = File.ReadAllBytes(this.blockIndexFilePath);
                var blockIndex = this.jsonSerializer.Deserialize<XDSBlockIndex>(file);

                if(addressIndex.IndexIdentifier != blockIndex.IndexIdentifier)
                    throw new InvalidOperationException("Index identifiers are supposed to match!");

                return (addressIndex, blockIndex);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                DeleteIndexes();
                return LoadIndexes();
            }

        }

        public void SaveIndexes(XDSAddressIndex xdsAddressIndex, XDSBlockIndex xdsBlockIndex, bool force)
        {
            if (DateTime.Now - this.lastSaved < TimeSpan.FromSeconds(60) && force == false)
                return;

            this.lastSaved = DateTime.Now;

            this.stopWatchSaving.Restart();

            var serialized = this.jsonSerializer.Serialize(xdsBlockIndex);
            File.WriteAllBytes(this.blockIndexFilePath, serialized);

            serialized = this.jsonSerializer.Serialize(xdsAddressIndex);
            File.WriteAllBytes(this.addressIndexFilePath, serialized);

            this.logger.LogInformation($"Saved indexes at height {xdsBlockIndex.SyncedHeight} - {serialized.Length} bytes, {this.stopWatchSaving.ElapsedMilliseconds} ms.");
        }

        (XDSAddressIndex, XDSBlockIndex) CreateIndexes()
        {
            var now = DateTimeOffset.UtcNow;
            var identifier = Guid.NewGuid().ToString();

            var addressIndex = new XDSAddressIndex
            {
                IndexIdentifier = identifier,

            };

            var blockIndex = new XDSBlockIndex
            {
                Version = 1,
                IndexIdentifier = identifier,
                CreatedUtc = now.ToUnixTimeSeconds(),
                ModifiedUtc = now.ToUnixTimeSeconds(),
                CheckpointHash = this.network.GenesisHash.ToHash256(),
                SyncedHash = this.network.GenesisHash.ToHash256(),
            };

            return (addressIndex, blockIndex);
        }

        void DeleteIndexes()
        {
            try
            {
                if (File.Exists(this.addressIndexFilePath))
                    File.Delete(this.addressIndexFilePath);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
            try
            {
                if (File.Exists(this.blockIndexFilePath))
                    File.Delete(this.blockIndexFilePath);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
        }
    }
}
