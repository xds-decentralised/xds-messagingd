using System;
using System.Diagnostics;
using System.IO;
using Blockcore.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using XDS.Features.Photon.Addresses;
using XDS.Features.Photon.Model;

namespace XDS.Features.Photon.Tools
{
    public sealed class IndexFileHelper
    {
        readonly IJsonSerializer jsonSerializer;
        readonly ILogger logger;
        readonly Network network;

        readonly Stopwatch stopWatchSaving;

        const string AddressIndexFilename = "addressindex.json";

        readonly string addressIndexFilePath;

        DateTime lastSaved;

        public IndexFileHelper(IJsonSerializer jsonSerializer, ILoggerFactory loggerFactory, NodeSettings nodeSettings, Network network)
        {
            this.jsonSerializer = jsonSerializer;
            this.logger = loggerFactory.CreateLogger<IndexFileHelper>();
            this.stopWatchSaving = new Stopwatch();
            this.addressIndexFilePath = Path.Combine(nodeSettings.DataDir, AddressIndexFilename);
            this.network = network;
        }

        public AddressIndex LoadIndex()
        {
            if (!File.Exists(this.addressIndexFilePath))
            {
                DeleteIndex();
                var result = CreateIndex();
                SaveIndex(result, true);
            }

            try
            {
                byte[] file = File.ReadAllBytes(this.addressIndexFilePath);
                var addressIndex = this.jsonSerializer.Deserialize<AddressIndex>(file);

                return addressIndex;
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                DeleteIndex();
                return LoadIndex();
            }

        }

        public void SaveIndex(AddressIndex addressIndex, bool force)
        {
            if (DateTime.Now - this.lastSaved < TimeSpan.FromSeconds(60) && force == false)
                return;

            this.lastSaved = DateTime.Now;

            this.stopWatchSaving.Restart();

            var serialized = this.jsonSerializer.Serialize(addressIndex);
            File.WriteAllBytes(this.addressIndexFilePath, serialized);

            this.logger.LogInformation($"Saved indexe at height {addressIndex.SyncedHeight} - {serialized.Length} bytes, {this.stopWatchSaving.ElapsedMilliseconds} ms.");
        }

        AddressIndex CreateIndex()
        {
            var now = DateTimeOffset.UtcNow;
            var identifier = Guid.NewGuid().ToString();

            var addressIndex = new AddressIndex
            {
                Version = 1,
                IndexIdentifier = identifier,
                CreatedUtc = now.ToUnixTimeSeconds(),
                ModifiedUtc = now.ToUnixTimeSeconds(),
                CheckpointHash = this.network.GenesisHash.ToHash256(),
                SyncedHash = this.network.GenesisHash.ToHash256(),

            };


            return addressIndex;
        }

        void DeleteIndex()
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
        }
    }
}
