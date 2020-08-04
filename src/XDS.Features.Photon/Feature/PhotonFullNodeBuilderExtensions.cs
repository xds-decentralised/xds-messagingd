using Blockcore.Builder;
using Blockcore.Configuration.Logging;
using Microsoft.Extensions.DependencyInjection;
using XDS.Features.Photon.Addresses;
using XDS.Features.Photon.PhotonServices;
using XDS.Features.Photon.Tools;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.Photon.Feature
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class PhotonFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UsePhoton(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<PhotonFeature>(nameof(PhotonFeature));
           
            

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PhotonFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<BlockchainLookup>();
                        services.AddSingleton<IndexFileHelper>();
                        services.AddSingleton<IJsonSerializer, IndexJsonSerializer>();
                        services.AddSingleton<IPhotonService, DefaultPhotonService>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}