using Microsoft.Extensions.Logging;
using XDS.SDK.Messaging.CrossTierTypes.Photon;

namespace XDS.Features.MessagingInfrastructure.PhotonServices
{
    public class DefaultPhotonService : IPhotonService
    {
        readonly BlockchainLookup blockchainLookup;
        readonly ILogger logger;

        public DefaultPhotonService(BlockchainLookup blockchainLookup, ILoggerFactory loggerFactory)
        {
            this.blockchainLookup = blockchainLookup;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public (long balance, int height, byte[] hashBlock, PhotonError photonError) GetPhotonBalance(string address, PhotonFlags photonFlags)
        {
            var error = CheckParameters(address, photonFlags);
            if (error > PhotonError.Success)
                return (default, default, default, error);

            return this.blockchainLookup.GetBalanceFromIndex(address, photonFlags, out _);
        }

        public (long balance, int height, byte[] hashBlock, IPhotonOutput[] outputs, PhotonError photonError) GetPhotonOutputs(string address, PhotonFlags photonFlags)
        {
            var error = CheckParameters(address, photonFlags);
            if (error > PhotonError.Success)
                return (default, default, default, default, error);

            return this.blockchainLookup.GetOutputsFromIndex(address, photonFlags);
        }

        PhotonError CheckParameters(string address, PhotonFlags photonFlags)
        {
            if (address == null)
            {
                this.logger.LogError($"Argument must not be null: {nameof(address)}");
                return PhotonError.InvalidArguments;
            }


            if (photonFlags == PhotonFlags.None)
            {
                this.logger.LogError($"Please provide a value other than PhotonFlags.None, because there is no default behavior defined. Argument: {nameof(photonFlags)}");
                return PhotonError.InvalidArguments;
            }

            return PhotonError.Success;
        }
    }


}
