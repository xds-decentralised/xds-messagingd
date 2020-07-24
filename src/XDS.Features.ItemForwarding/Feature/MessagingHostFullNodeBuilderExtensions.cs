using Blockcore.Builder;
using Blockcore.Configuration.Logging;
using Microsoft.Extensions.DependencyInjection;
using XDS.Features.ItemForwarding.Client;
using XDS.Features.ItemForwarding.Client.Data;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Features.ItemForwarding.Feature
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class ItemForwardingFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseItemForwarding(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ItemForwardingFeature>(nameof(ItemForwardingFeature));

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ItemForwardingFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<ItemForwarding>();
                        services.AddSingleton<MessageRelayConnectionFactory>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}