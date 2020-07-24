using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Features.MessagingHost.Storage
{
    public interface IMessageNodeRepository
    {
        Task<byte> AnyNews(string recipientId);
	    Task<byte> CheckForResendRequest(XResendRequest resendRequestQuery);

		Task<IReadOnlyList<XMessage>> GetMessages(string myId);
        Task<bool> TryAddIdentity(XIdentity identity, Action<string, byte[]> initTLSUser);
        Task<XIdentity> GetIdentityAsync(string identityId);
        Task<bool> TryAddMessage(XMessage message);
		Task<bool> TryAddResendRequest(XResendRequest resendRequest);

        Task<IReadOnlyList<XIdentity>> GetAllIdentities();

        Task<MessageNodeRepository.RepoStats> GetStatsAsync();

    }
}
