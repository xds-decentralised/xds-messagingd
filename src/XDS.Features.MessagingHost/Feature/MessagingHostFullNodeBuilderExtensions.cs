using System.IO;
using Blockcore.Builder;
using Blockcore.Configuration.Logging;
using Microsoft.Extensions.DependencyInjection;
using XDS.Features.ItemForwarding.Client.Data;
using XDS.Features.MessagingHost.RequestHandler;
using XDS.Features.MessagingHost.Servers;
using XDS.Features.MessagingHost.Storage;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Features.MessagingHost.Feature
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class MessagingHostFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseMessagingHost(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MessagingHostFeature>(nameof(MessagingHostFeature));
           
            var fStoreConfig = new FStoreConfig
            {
                DefaultStoreName = "FStore",
                StoreLocation = new DirectoryInfo(Path.Combine(fullNodeBuilder.NodeSettings.DataDir, "messaging")),
                Initializer = FStoreInitializer.InitFStore
            };

            FStoreInitializer.InitFStore(fStoreConfig); 

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MessagingHostFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton(fStoreConfig);
                        services.AddSingleton<IMessageNodeRepository, MessageNodeRepository>();
                        services.AddSingleton<MessageRelayRecordRepository>();
                        services.AddTransient<CommandProcessor>();
                        services.AddSingleton<IRequestHandler, NoTLSRequestHandler>();
                        services.AddTransient<TcpAsyncServer>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}