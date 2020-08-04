using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blockcore.EventBus;
using Blockcore.EventBus.CoreEvents;
using Blockcore.Interfaces;
using Blockcore.Signals;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;
using XDS.Features.Photon.Addresses;
using XDS.Features.Photon.Balances;
using XDS.Features.Photon.Blockchain;
using XDS.Features.Photon.Model;
using XDS.Features.Photon.Tools;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.Photon
{
    public class BlockchainLookup
    {
        readonly object readWriteLock = new object();

        readonly Stopwatch stopwatchProcessBlock = new Stopwatch();
        readonly Stopwatch stopWatchBalanceQuery = new Stopwatch();

        readonly ILogger logger;
        readonly IBlockStore blockStore;
        readonly INodeLifetime nodeLifetime;
        readonly ISignals signals;

        readonly ChainIndexer chainIndexer;
        readonly AddressIndex addressIndex;
        readonly AddressService addressService;
        readonly Network network;
        readonly IndexFileHelper indexFileHelper;



        bool isStartingUp = true;
        long lastProcessBlockMs;
        SubscriptionToken blockConnectedSubscription;


        public BlockchainLookup(ILoggerFactory loggerFactory, ChainIndexer chainIndexer, IBlockStore blockStore, ISignals signals, IndexFileHelper indexFileHelper, Network network, INodeLifetime nodeLifetime)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.nodeLifetime = nodeLifetime;
            this.signals = signals;
            this.network = network;

            this.indexFileHelper = indexFileHelper;
            AddressHelper.Init(network, loggerFactory);
            IndexBalanceService.Init(network);

            Tools.Extensions.Init(loggerFactory);
            this.addressIndex = indexFileHelper.LoadIndex();
            this.addressService = new AddressService(this.addressIndex, indexFileHelper, loggerFactory);
        }


      

        public int GetAddressCount()
        {
            return this.addressIndex.Entries.Count;
        }



        public long GetNetworkBalance()
        {
            List<string> allAddresses = this.addressIndex.Entries.Select(x => x.Address).ToList();

            long networkBalance = 0;
            foreach (string address in allAddresses)
            {
                // occasionally, ab == null - when does that happen? Investigate!
                IndexAddressBalance ab = IndexBalanceService.GetBalance(this.addressIndex.Entries, GetSyncedHeightAndHash().height, address);
                networkBalance += ab.Confirmed;
            }

            return networkBalance / Constants.SatoshisPerCoin;
        }

        /// <summary>
        /// Check if the wallet tip hash is in the current consensus chain.
        /// </summary>
        bool IsOnBestChain()
        {
            bool isOnBestChain;
            if (this.addressIndex.SyncedHeight == 0 || this.addressIndex.SyncedHash.IsDefaultBlockHash(this.network.GenesisHash.ToBytes()))
            {
                // If the height is 0, we cannot be on the wrong chain. Reset file in case there is something wrong with it.
                ResetMetadata();
                isOnBestChain = true;
                this.logger.LogInformation($"IsOnBestChain: Yes - wallet height is 0.");

            }
            else
            {
                var walletTipHash = new uint256(this.addressIndex.SyncedHash.Value);

                var chainedHeader = this.chainIndexer.GetHeader(walletTipHash);
                isOnBestChain = chainedHeader != null;
                if (isOnBestChain)
                {
                    this.logger.LogInformation($"IsOnBestChain: Yes - because wallet tip hash {walletTipHash} is chainedHeader {chainedHeader.Height}.");
                }
                else
                {
                    this.logger.LogInformation($"IsOnBestChain: No - because wallet tip hash {walletTipHash} is unknown to chainIndexer.");
                }
            }

            return isOnBestChain;
        }

        /// <summary>
        /// Clears and initializes the wallet Metadata file, and sets heights to 0 and the hashes to null,
        /// and saves the Metadata file, effectively updating it to the latest version.
        /// </summary>
        void ResetMetadata()
        {
            this.addressIndex.SyncedHash = this.network.GenesisHash.ToHash256();
            this.addressIndex.SyncedHeight = 0;
            this.addressIndex.CheckpointHash = this.addressIndex.SyncedHash;
            this.addressIndex.CheckpointHeight = 0;
            this.addressIndex.IndexIdentifier = this.addressIndex.IndexIdentifier;
            this.addressIndex.Entries.Clear();
            this.logger.LogInformation($"Resetting blockIndex to initial state, forcing a save.");
            SaveMetadata(this.addressIndex.SyncedHeight, this.addressIndex.SyncedHash, force: true);
        }
        void MoveToBestChain()
        {
            ChainedHeader checkpointHeader = null;
            if (!this.addressIndex.CheckpointHash.IsDefaultBlockHash(this.network.GenesisHash.ToBytes()))
            {
                var header = this.chainIndexer.GetHeader(new uint256(this.addressIndex.CheckpointHash.Value));
                if (header != null && this.addressIndex.CheckpointHeight == header.Height)
                    checkpointHeader = header;  // the checkpoint header is in the correct chain and the the checkpoint height in the wallet is consistent
            }

            if (checkpointHeader != null && this.chainIndexer.Tip.Height - checkpointHeader.Height > this.network.Consensus.MaxReorgLength)  // also check the checkpoint is not newer than it should be
            {
                // we have a valid checkpoint, remove all later blocks
                this.logger.LogWarning($"Rewinding {this.chainIndexer.Tip.Height - checkpointHeader.Height} blocks to checkpoint at height: {checkpointHeader.Height}");
                if (RemoveBlocks(checkpointHeader))
                    return;

            }

            // we do not have a usable checkpoint, sync from start by resetting everything
            this.logger.LogError($"Rewinding to block 0, checkpoint {this.addressIndex.CheckpointHeight}-{this.addressIndex.CheckpointHash} will not be used.");
            ResetMetadata();
        }

        /// <summary>
        /// It is assumed that the argument contains the header of the highest block (inclusive) where the wallet data is
        /// consistent with the right chain.
        /// This method removes all block and the transactions in them of later blocks.
        /// </summary>
        /// <param name="checkpointHeader">ChainedHeader of the checkpoint</param>
        bool RemoveBlocks(ChainedHeader checkpointHeader)
        {
            try
            {
                var entries = this.addressIndex.Entries.ToList();
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    List<IndexUtxo> deleteReceived = new List<IndexUtxo>();
                    foreach (var utxo in entry.Received)
                    {
                        if (utxo.BlockHeight > checkpointHeader.Height)
                            deleteReceived.Add(utxo);
                        else
                        {
                            if (utxo.SpendingHeight > checkpointHeader.Height)
                            {
                                utxo.SpendingTx = null;
                                utxo.SpendingHeight = 0;
                                utxo.SpendingN = 0;
                            }
                        }
                    }

                    foreach (var utxo in deleteReceived)
                    {
                        entry.Received.Remove(utxo);
                    }

                    if (entry.Received.Count == 0)
                        this.addressIndex.Entries.Remove(entry);
                }



                // Update last block synced height
                this.addressIndex.SyncedHeight = checkpointHeader.Height;
                this.addressIndex.SyncedHash = checkpointHeader.HashBlock.ToHash256();

                // force-saving is important here, it will also move the checkpoint backwards
                SaveMetadata(this.addressIndex.SyncedHeight, this.addressIndex.SyncedHash, force: true);
                return true;
            }
            catch (Exception e)
            {
                this.logger.LogError($"{nameof(RemoveBlocks)} failed: {e}");
                return false;
            }
        }

        void CompleteStart()
        {
            this.isStartingUp = false;
            this.logger.LogInformation($"Startup sync completed at height {this.addressIndex.SyncedHeight}, forcing a save.");
            SaveMetadata(this.addressIndex.SyncedHeight, this.addressIndex.SyncedHash, true);
            SubscribeSignals();
        }
        void SubscribeSignals()
        {
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(OnBlockConnected);
        }

        void UnSubscribeSignals()
        {
            if (this.blockConnectedSubscription != null)
                this.signals.Unsubscribe(this.blockConnectedSubscription);
        }


        void OnBlockConnected(BlockConnected blockConnected)
        {
            if (blockConnected.ConnectedBlock.ChainedHeader.Height <= GetSyncedHeightAndHash().height)
            {
                this.logger.LogWarning($"OnBlockConnected is passing block of height {blockConnected.ConnectedBlock.ChainedHeader.Height}, but the index is already at height {GetSyncedHeightAndHash()}. Skipping block!");
                return;
            }

            Sync();
        }

        public (int height, Hash256 hash) GetSyncedHeightAndHash()
        {
            return (this.addressIndex.SyncedHeight, this.addressIndex.SyncedHash);
        }


        void SaveMetadata(int height, Hash256 hashBlock, bool force)
        {
            UpdateLastBlockSyncedAndCheckpoint(height, hashBlock);

            this.indexFileHelper.SaveIndex(this.addressIndex, force);

            void UpdateLastBlockSyncedAndCheckpoint(int currentHeight, Hash256 currentBlockHash)
            {
                this.addressIndex.SyncedHeight = currentHeight;
                this.addressIndex.SyncedHash = currentBlockHash;

                int minCheckpointHeight = (int)this.network.Consensus.MaxReorgLength + 1;

                if (currentHeight > minCheckpointHeight)
                {
                    var checkPoint = this.chainIndexer.GetHeader(currentHeight - minCheckpointHeight);
                    this.addressIndex.CheckpointHash = checkPoint.HashBlock.ToHash256();
                    this.addressIndex.CheckpointHeight = checkPoint.Height;
                }
                else
                {
                    this.addressIndex.CheckpointHash = this.network.GenesisHash.ToHash256();
                    this.addressIndex.CheckpointHeight = 0;
                }
            }
        }

        public void Sync()
        {
            this.logger.LogInformation($"Syncing {(this.isStartingUp ? "(startup)." : "(updating).")}");

            try
            {
                // a) check if the wallet is on the right chain
                if (!IsOnBestChain())
                {
                    MoveToBestChain();
                }

                while (this.chainIndexer.Tip.Height > this.addressIndex.SyncedHeight)
                {
                    // this can take a long time, so watch for cancellation
                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        return;
                    }

                    var nextBlockHeight = this.addressIndex.SyncedHeight + 1;

                    ChainedHeader nextBlockHeader = this.chainIndexer.GetHeader(nextBlockHeight);
                    Block nextBlock = this.blockStore.GetBlock(nextBlockHeader.HashBlock);
                    if (nextBlock == null)
                    {
                        this.logger.LogWarning($"Block with hash {nextBlockHeader.HashBlock}, height {nextBlockHeight} is not in the BlockStore.");
                        Task.Delay(2000).Wait();
                        continue;

                    }

                    lock (this.readWriteLock)
                    {
                        ProcessBlock(nextBlock, nextBlockHeader.Height, nextBlock.GetHash256());
                    }

                }
            }
            catch (Exception e)
            {
                this.logger.LogError($"{nameof(Sync)}: {e.Message}");
            }

            if (this.isStartingUp)
                CompleteStart();
        }

       

      

        void ProcessBlock(Block block, int height, Hash256 hashBlock)
        {
            if (this.addressIndex.SyncedHeight != height - 1 || this.addressIndex.SyncedHash == hashBlock)
            {
                this.isStartingUp = true;
                UnSubscribeSignals();
                MoveToBestChain();
                Sync();
            }

            this.stopwatchProcessBlock.Restart();

            BlockService.AnalyzeBlock(block, height, this.addressService.GetOrCreateAddressInIndex, this.addressService.FindUtxo);

            this.lastProcessBlockMs = this.stopwatchProcessBlock.ElapsedMilliseconds;

            SaveMetadata(height, hashBlock, force: false);
        }

        internal (long balance, int height, byte[] hashBlock, PhotonError photonError) GetBalanceFromIndex(string address, PhotonFlags photonFlags, out IndexAddressBalance balance)
        {
            balance = null;
            var entry = this.addressService.FindAddressInIndex(address);

            if (entry == null)
                return (default, default, default, PhotonError.UnknownAddress);

            if (this.isStartingUp)
                return (default, default, default, PhotonError.ServiceInitializing);

            (int height, Hash256 hash) = GetSyncedHeightAndHash();

            balance = IndexBalanceService.GetBalance(this.addressIndex.Entries, height, address);

            switch (photonFlags)
            {
                case PhotonFlags.Confirmed:
                    return (balance.Confirmed, height, hash.Value, PhotonError.Success);
                case PhotonFlags.Spendable:
                    return (balance.Spendable, height, hash.Value, PhotonError.Success);
                case PhotonFlags.Staking:
                    return (balance.Stakable, height, hash.Value, PhotonError.Success);
                default:
                    return (default, default, default, PhotonError.InvalidArguments);
            }

        }

        internal (long balance, int height, byte[] hashBlock, IPhotonOutput[] outputs, PhotonError photonError) GetOutputsFromIndex(string address, PhotonFlags photonFlags)
        {
            var result = this.GetBalanceFromIndex(address, photonFlags, out var balance);

            if (result.photonError != PhotonError.Success)
                return (default, default, default, default, result.photonError);


            IndexUtxo[] indexUtxos;

            switch (photonFlags)
            {
                case PhotonFlags.Confirmed:
                    // this is basically a history query
                    bool includeSpent = photonFlags.HasFlag(PhotonFlags.IncludeSpentOutputs);
                    return (default, default, default, default, PhotonError.NotImplemented);
                case PhotonFlags.Spendable:
                    indexUtxos = balance.SpendableCoins.Values.ToArray();
                    break;
                case PhotonFlags.Staking:
                    indexUtxos = balance.StakingCoins.Values.ToArray();
                    break;

                default:
                    return (default, default, default, default, PhotonError.InvalidArguments);
            }

            IPhotonOutput[] outputs = new IPhotonOutput[indexUtxos.Length];
            for (var i = 0; i < indexUtxos.Length; i++)
            {
                var utxo = indexUtxos[i];

                outputs[i] = new PhotonOutput
                {
                    SpendingTx = utxo.SpendingTx?.Value,
                    HashTx = utxo.HashTx.Value,
                    SpendingHeight = utxo.SpendingHeight,
                    SpendingN = utxo.SpendingN,
                    BlockHeight = utxo.BlockHeight,
                    Index = utxo.Index,
                    Satoshis = utxo.Satoshis,
                    UtxoType = utxo.UtxoType
                };
            }

            return (result.balance, result.height, result.hashBlock, outputs, PhotonError.Success);
        }

        public void AddComponentStatsAsync(StringBuilder log)
        {
            try
            {
                lock (this.readWriteLock)
                {
                    log.AppendLine();
                    log.AppendLine($"======= XDS Blockchain lookup  =======");
                    log.AppendLine($"Synced Height: {GetSyncedHeightAndHash().height}");
                    log.AppendLine($"Addresses: {GetAddressCount()}");
                    log.AppendLine($"Process Block Cost: {this.lastProcessBlockMs} ms");

                    if (!this.isStartingUp)
                    {
                        this.stopWatchBalanceQuery.Restart();
                        log.AppendLine($"Network Balance: {GetNetworkBalance()}");
                        log.AppendLine($"Money Supply: {GetSyncedHeightAndHash().height * 50}");
                        log.AppendLine($"Balance Query Cost: {this.stopWatchBalanceQuery.ElapsedMilliseconds} ms");
                    }
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
            }
        }
    }
}
