using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.Configuration;
using Blockcore.Consensus;
using Blockcore.EventBus;
using Blockcore.EventBus.CoreEvents;
using Blockcore.Interfaces;
using Blockcore.Signals;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;
using XDS.Features.MessagingInfrastructure.Addresses;
using XDS.Features.MessagingInfrastructure.Balances;
using XDS.Features.MessagingInfrastructure.Blockchain;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.DTOs;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Wallet;
using XDS.Features.MessagingInfrastructure.Tools;

namespace XDS.Features.MessagingInfrastructure
{
    public class BlockchainLookup
    {
        readonly SemaphoreSlim WalletSemaphore = new SemaphoreSlim(1, 1);
        readonly ILogger logger;
        readonly ChainIndexer chainIndexer;
        readonly IBlockStore blockStore;

        readonly XDSAddressIndex addressIndex;
        readonly XDSBlockIndex blockIndex;
        readonly AddressService addressService;
        IInitialBlockDownloadState initialBlockDownloadState;
        Network network;
        INodeLifetime nodeLifetime;
        IndexFileHelper indexFileHelper;
        NodeSettings nodeSettings;
        ISignals signals;
        public bool IsStartingUp = true;
        private SubscriptionToken blockConnectedSubscription;
        private SubscriptionToken transactionReceivedSubscription;


        public BlockchainLookup(ILoggerFactory loggerFactory, ChainIndexer chainIndexer, IBlockStore blockStore, ISignals signals, IndexFileHelper indexFileHelper, IInitialBlockDownloadState initialBlockDownloadState, Network network, INodeLifetime nodeLifetime, NodeSettings nodeSettings)
        {
            this.network = network;
            this.indexFileHelper = indexFileHelper;

            (this.addressIndex, this.blockIndex) = indexFileHelper.LoadIndexes();


            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;

            AddressHelper.Init(network, loggerFactory);
            BalanceService.Init(network);
            IndexBalanceService.Init(network);

            Tools.Extensions.Init(loggerFactory);

            this.addressService = new AddressService(this.addressIndex, indexFileHelper, loggerFactory);
            this.initialBlockDownloadState = initialBlockDownloadState;

            this.nodeLifetime = nodeLifetime;

            this.nodeSettings = nodeSettings;
            this.signals = signals;

            Task.Run(() =>
            {
                Task.Delay(5000).Wait();
                while (IsIBD())
                {
                    Task.Delay(2000).Wait();
                }

                while (this.chainIndexer.Tip == null || this.chainIndexer.Tip.HashBlock == null)
                {
                    Task.Delay(2000).Wait();
                }
                SyncWallet();
            });
        }

        public int GetAddressCount()
        {
            return this.addressIndex.Entries.Count;
        }

        bool IsIBD()
        {
            try
            {
                return this.initialBlockDownloadState.IsInitialBlockDownload();
            }
            catch (Exception e)
            {
                this.logger.LogWarning(
                    $"Error in {nameof(this.initialBlockDownloadState.IsInitialBlockDownload)}: {e.Message}");
                return true;
            }
        }

        public long GetNetworkBalance()
        {
            List<string> allAddresses = this.addressIndex.Entries.Select(x => x.Address).ToList();

            long networkBalance = 0;
            foreach (string address in allAddresses)
            {
                IndexAddressBalance ab = IndexBalanceService.GetBalance(this.addressIndex.Entries, this.GetSyncedHeight(), address);
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
            if (this.blockIndex.SyncedHeight == 0 || this.blockIndex.SyncedHash.IsDefaultBlockHash(this.network.GenesisHash.ToBytes()))
            {
                // If the height is 0, we cannot be on the wrong chain. Reset file in case there is something wrong with it.
                ResetMetadata();
                isOnBestChain = true;
                this.logger.LogInformation($"IsOnBestChain: Yes - wallet height is 0.");

            }
            else
            {
                var walletTipHash = new uint256(this.blockIndex.SyncedHash.Value);

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
            this.blockIndex.SyncedHash = this.network.GenesisHash.ToHash256();
            this.blockIndex.SyncedHeight = 0;
            this.blockIndex.CheckpointHash = this.blockIndex.SyncedHash;
            this.blockIndex.CheckpointHeight = 0;
            this.blockIndex.IndexIdentifier = this.addressIndex.IndexIdentifier;
            this.blockIndex.Blocks = new System.Collections.Concurrent.ConcurrentDictionary<int, BlockMetadata>();
            this.logger.LogInformation($"Resetting blockIndex to initial state, forcing a save.");
            SaveMetadata(this.blockIndex.SyncedHeight, this.blockIndex.SyncedHash, force: true);
        }
        void MoveToBestChain()
        {
            ChainedHeader checkpointHeader = null;
            if (!this.blockIndex.CheckpointHash.IsDefaultBlockHash(this.network.GenesisHash.ToBytes()))
            {
                var header = this.chainIndexer.GetHeader(new uint256(this.blockIndex.CheckpointHash.Value));
                if (header != null && this.blockIndex.CheckpointHeight == header.Height)
                    checkpointHeader = header;  // the checkpoint header is in the correct chain and the the checkpoint height in the wallet is consistent
            }

            if (checkpointHeader != null && this.chainIndexer.Tip.Height - checkpointHeader.Height > this.network.Consensus.MaxReorgLength)  // also check the checkpoint is not newer than it should be
            {
                // we have a valid checkpoint, remove all later blocks
                RemoveBlocks(checkpointHeader);
            }
            else
            {
                // we do not have a usable checkpoint, sync from start by resetting everything
                ResetMetadata();
            }
        }

        /// <summary>
        /// It is assumed that the argument contains the header of the highest block (inclusive) where the wallet data is
        /// consistent with the right chain.
        /// This method removes all block and the transactions in them of later blocks.
        /// </summary>
        /// <param name="checkpointHeader">ChainedHeader of the checkpoint</param>
        void RemoveBlocks(ChainedHeader checkpointHeader)
        {
            var blocksAfterCheckpoint = this.blockIndex.Blocks.Keys.Where(x => x > checkpointHeader.Height).ToArray();
            foreach (var height in blocksAfterCheckpoint)
                this.blockIndex.Blocks.TryRemove(height, out var _);
            this.logger.LogInformation(
                $"Removed {blocksAfterCheckpoint.Length} after checkpoint height {checkpointHeader.Height}, forcing a save.");

            // Update last block synced height
            this.blockIndex.SyncedHeight = checkpointHeader.Height;
            this.blockIndex.SyncedHash = checkpointHeader.HashBlock.ToHash256();
            this.blockIndex.CheckpointHeight = checkpointHeader.Height;
            this.blockIndex.CheckpointHash = checkpointHeader.HashBlock.ToHash256();
            SaveMetadata(this.blockIndex.SyncedHeight, this.blockIndex.SyncedHash, force: true);
        }

        void CompleteStart()
        {
            this.IsStartingUp = false;
            this.logger.LogInformation($"Startup sync completed at height {this.blockIndex.SyncedHeight}, forcing a save.");
            SaveMetadata(this.blockIndex.SyncedHeight, this.blockIndex.SyncedHash, true);
            SubscribeSignals();
        }
        void SubscribeSignals()
        {
            //this.nodeServices.BroadcasterManager.TransactionStateChanged += OnTransactionStateChanged;
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(OnBlockConnected);
            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(OnTransactionReceived);
        }
        void UnSubscribeSignals()
        {
            //this.nodeServices.BroadcasterManager.TransactionStateChanged -= OnTransactionStateChanged;

            if (this.transactionReceivedSubscription != null)
                this.signals.Unsubscribe(this.transactionReceivedSubscription);

            if (this.blockConnectedSubscription != null)
                this.signals.Unsubscribe(this.blockConnectedSubscription);
        }


        void OnBlockConnected(BlockConnected blockConnected)
        {
            if (blockConnected.ConnectedBlock.ChainedHeader.Height <= GetSyncedHeight())
            {
                this.logger.LogWarning($"OnBlockConnected is passing block of height {blockConnected.ConnectedBlock.ChainedHeader.Height}, but the wallet is already at height {GetSyncedHeight()}. Skipping block!");
                return;
            }

            SyncWallet();
        }

        public int GetSyncedHeight()
        {
            return this.blockIndex.SyncedHeight;
        }

        protected Hash256 GetSyncedHash()
        {
            return this.blockIndex.SyncedHash;
        }

        void OnTransactionReceived(TransactionReceived transactionReceived)
        {
            try
            {
                this.WalletSemaphore.Wait();

                ReceiveTransactionFromMemoryPool(transactionReceived.ReceivedTransaction);
            }
            finally
            {
                this.WalletSemaphore.Release();
            }
        }
        void ReceiveTransactionFromMemoryPool(Transaction transaction)
        {
            var walletTransaction = BlockService.AnalyzeTransaction(transaction, int.MaxValue, new BlockMetadata(), this.blockIndex.Blocks.Values, GetOrAddAnyonesAddress);
            if (walletTransaction != null)
            {
                //var entry = MemoryPoolService.CreateMemoryPoolEntry(walletTransaction, null);
                //this.blockIndex.MemoryPool.Entries.Add(entry);
            }
            SaveMetadata(this.blockIndex.SyncedHeight, this.blockIndex.SyncedHash, true);
        }

        void SaveMetadata(int height, Hash256 hashBlock, bool force)
        {
            UpdateLastBlockSyncedAndCheckpoint(height, hashBlock);
            this.indexFileHelper.SaveIndexes(this.addressIndex, this.blockIndex, force);

            void UpdateLastBlockSyncedAndCheckpoint(int height, Hash256 hashBlock)
            {
                this.blockIndex.SyncedHeight = height;
                this.blockIndex.SyncedHash = hashBlock;

                const int minCheckpointHeight = 125;
                if (height > minCheckpointHeight)
                {
                    var checkPoint = this.chainIndexer.GetHeader(height - minCheckpointHeight);
                    this.blockIndex.CheckpointHash = checkPoint.HashBlock.ToHash256();
                    this.blockIndex.CheckpointHeight = checkPoint.Height;
                }
                else
                {
                    this.blockIndex.CheckpointHash = this.network.GenesisHash.ToHash256();
                    this.blockIndex.CheckpointHeight = 0;
                }
            }
        }

        protected void SyncWallet()
        {
            this.logger.LogInformation($"Wallet is syncing {(this.IsStartingUp ? "(startup)." : "(updating).")}");
            try
            {
                // a) check if the wallet is on the right chain
                if (!IsOnBestChain())
                {
                    MoveToBestChain();
                }

                while (this.chainIndexer.Tip.Height > this.blockIndex.SyncedHeight)
                {
                    // this can take a long time, so watch for cancellation
                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        return;
                    }

                    var nextBlockHeight = this.blockIndex.SyncedHeight + 1;

                    ChainedHeader nextBlockHeader = this.chainIndexer.GetHeader(nextBlockHeight);
                    Block nextBlock = this.blockStore.GetBlock(nextBlockHeader.HashBlock);
                    if (nextBlock == null)
                    {
                        this.logger.LogWarning($"Block with hash {nextBlockHeader.HashBlock}, height {nextBlockHeight} is not in the BlockStore.");
                        Task.Delay(2000).Wait();
                        continue;

                    }

                    lock (this.lockObj)
                    {
                        ProcessBlock(nextBlock, nextBlockHeader.Height, nextBlock.GetHash256());
                        var networkBalance = GetNetworkBalance();
                        var expectedBalance = nextBlockHeader.Height * 50;
                        if (networkBalance != expectedBalance)
                            ;
                    }
                    
                }
            }
            catch (Exception e)
            {
                this.logger.LogError($"{nameof(SyncWallet)}: {e.Message}");
            }

            if (this.IsStartingUp)
                CompleteStart();
        }

        public object lockObj = new object();

        public long ProcessBlockMS { get; private set; }
        Stopwatch stopwatchProcessBlock = new Stopwatch();

        void ProcessBlock(Block block, int height, Hash256 hashBlock)
        {
            this.stopwatchProcessBlock.Restart();

            var walletBlock = BlockService.AnalyzeBlock(block, height, this.blockIndex.Blocks.Values, GetOrAddAnyonesAddress);
            
            
            if (!this.blockIndex.Blocks.TryAdd(height, walletBlock))
            {
                this.IsStartingUp = true;
                UnSubscribeSignals();
                MoveToBestChain();
                SyncWallet();
            }

            BlockAddedToWallet(height, walletBlock);

            this.ProcessBlockMS = this.stopwatchProcessBlock.ElapsedMilliseconds;

           

            SaveMetadata(height, hashBlock, force: true);
        }

        ISegWitAddress GetOrAddAnyonesAddress(string bech32Address, int blockHeight)
        {
            return this.addressService.GetOrAddAnyonesAddress(bech32Address, blockHeight);
        }

        void BlockAddedToWallet(int height, BlockMetadata blockMetadata)
        {
            if (!this.IsStartingUp)
                this.logger.LogDebug(
                    $"Block {height} added {blockMetadata.Transactions.Count} transactions with {blockMetadata.Transactions.Sum(x => x.ValueAdded) / Constants.SatoshisPerCoin} coins to the wallet.");
        }
    }
}
