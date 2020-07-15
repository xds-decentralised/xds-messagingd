using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XDS.SDK.Cryptography.NoTLS;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.MessagingHost.RequestHandler
{
	public class NoTLSRequestHandler : IRequestHandler
	{
		readonly NOTLSServerRatchet ratchet;
		readonly CommandProcessor commandProcessor;
		readonly ILogger logger;

		public NoTLSRequestHandler(ILoggerFactory loggerFactory, CommandProcessor commandProcessor)
		{
			this.ratchet = new NOTLSServerRatchet();
			this.commandProcessor =commandProcessor;
			this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
		}

		public async Task<byte[]> ProcessRequestAsync(byte[] rawRequest, string clientInformation)
		{
			byte[] response;
			CommandId commandId = CommandId.Zero;

			try
			{
				if (rawRequest == null)
					throw new ArgumentNullException(nameof(rawRequest));
				if (rawRequest.Length == 0)
					throw new ArgumentException(@"Length must not be zero.", nameof(rawRequest));

				var tlsEnvelope = new NOTLSEnvelope(rawRequest);
				var request = this.ratchet.DecryptRequest(tlsEnvelope);

				Command command = request.ParseCommand();
				commandId = command.CommandId;
				response = await this.commandProcessor.ExecuteAuthenticatedRequestAsync(command);
				
			}
			catch (Exception e)
			{
				// Always return a properly formatted response, never null, otherwise the client will simply hang.
				var error = $"{clientInformation}: Command {commandId} threw an Exception: {e.Message}";
				this.logger.LogError(error, nameof(NoTLSRequestHandler));
				response = new RequestCommand(CommandId.ServerException, error).Serialize(CommandHeader.Yes);
			}

			byte[] reply = this.ratchet.EncryptRequest(response).Serialize();
			this.logger.LogInformation($"{clientInformation}: {commandId}, I/O {rawRequest?.Length}/{reply.Length} bytes.");
			return reply;
		}
	}
}
