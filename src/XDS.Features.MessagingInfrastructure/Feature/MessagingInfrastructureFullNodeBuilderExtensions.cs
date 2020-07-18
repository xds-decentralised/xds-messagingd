using System.IO;
using Blockcore.Builder;
using Blockcore.Configuration.Logging;
using Microsoft.Extensions.DependencyInjection;
using XDS.Features.MessagingInfrastructure.Infrastructure.Common.Json;
using XDS.Features.MessagingInfrastructure.Tools;

namespace XDS.Features.MessagingInfrastructure.Feature
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class MessagingInfrastructureFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseMessagingInfrastructure(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MessagingInfrastructureFeature>(nameof(MessagingInfrastructureFeature));
           
            

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MessagingInfrastructureFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<BlockchainLookup>();
                        services.AddSingleton<ItemPropagation>();
                        services.AddSingleton<IndexFileHelper>();
                        services.AddSingleton<IJsonSerializer, X1WalletFileJsonSerializer>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}