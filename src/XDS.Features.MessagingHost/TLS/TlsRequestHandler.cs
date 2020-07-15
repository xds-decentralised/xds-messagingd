using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.Features.MessagingHost.RequestHandler;
using XDS.SDK.Cryptography.Api.Interfaces;
using XDS.SDK.Cryptography.TLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.MessagingHost.TLS
{
    public class TLSRequestHandler : IRequestHandler
    {
        readonly CommandProcessor commandProcessor;
        readonly TLSServerRatchet tlsServerRatchet;
        readonly ILogger logger;
        public TLSRequestHandler(string nodeId, IXDSSecService visualCrypt2Service, CommandProcessor commandProcessor, ILoggerFactory loggerFactory)
        {
            this.commandProcessor = commandProcessor; 
            this.tlsServerRatchet = new TLSServerRatchet(nodeId, NodeKeys.NodePrivateKey, visualCrypt2Service);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public async Task<byte[]> ProcessRequestAsync(byte[] rawRequest, string clientInformation)
        {
            if (rawRequest == null || rawRequest.Length == 0)
                return null;

            var tlsEnvelope = new TLSEnvelope(rawRequest);
            var request = this.tlsServerRatchet.TLSServerDecryptRequest(tlsEnvelope);

            if (request == null)
            {
                this.logger.LogWarning($"TLSServerRequestHandler: Unknown DynamicPublicKey {tlsEnvelope.DynamicPublicKeyId}, sending {CommandId.LostDynamicKey_Response}.");
                // request == null means we lost the dynamic private key to to the public key the client used.
                // We don't know who the client is, because TLS could not decrypt the message at all.
                // We now need to tell the client it needs to reset its key schedule, i.e. start over
                // using my static public key.
                // We must use our static private key to encrypt this message, so that the client knows the server is talking.
                // We then use the dynamic public key we just received, to calculate a new shared secret that only the unknown
                // client can reproduce for decryption of this message.
                byte[] lostDynamicKeyResponseCommand = this.commandProcessor.ExecuteLostDynamicKey();
                byte[] lostDynamicKeyResponse = this.tlsServerRatchet.TLSServerEncryptRequestAnonymous(lostDynamicKeyResponseCommand, tlsEnvelope.DynamicPublicKey, tlsEnvelope.DynamicPublicKeyId).Serialize();
                return lostDynamicKeyResponse;
            }

            Command command = request.ParseCommand();

            if (!command.CommandId.IsCommandDefined())
                return null;

            byte[] response;
            if (CommandProtocol.IsAuthenticationRequired(command.CommandId))
            {
                if (!request.IsAuthenticated)
                {
                    this.logger.LogWarning($"ServerRequestHandler: Command {command.CommandId} from {request.UserId} requires Auth, which failed. Sending {CommandId.NoSuchUser_Response}");
                    // We may be here, when the server has lost the identity (and client public key for the calling user id), or if the 
                    // user id is locked, revoked etc.
                    byte[] noSuchUserResponseCommand = this.commandProcessor.ExecuteNoSuchUser();
                    byte[] noSuchUserResponse = this.tlsServerRatchet.TLSServerEncryptRequestAnonymous(noSuchUserResponseCommand, tlsEnvelope.DynamicPublicKey, tlsEnvelope.DynamicPublicKeyId).Serialize();
                    return noSuchUserResponse;
                }

                response = await this.commandProcessor.ExecuteAuthenticatedRequestAsync(command);
            }
            else
            {
                response = await this.commandProcessor.ExecutePublishIdentityAsync(command, this.tlsServerRatchet.RefreshTLSUser);
                // when PublishIdentity has returned successfully, we can save the DynamicPublicKey we did not save in TLSDecrpt, because we wanted more logic to run before this
                // Specifically, the IdentityController has called RefreshTLSUser by now, creating the place where this key can be stored.
                if (response != null)
                    this.tlsServerRatchet.SaveIncomingDynamicPublicKey(request.UserId, tlsEnvelope.DynamicPublicKey, tlsEnvelope.DynamicPublicKeyId);
            }
            return response == null ?
                null :
                this.tlsServerRatchet.TLSServerEncryptRequest(response, request.UserId).Serialize();
        }
     
    }
}
