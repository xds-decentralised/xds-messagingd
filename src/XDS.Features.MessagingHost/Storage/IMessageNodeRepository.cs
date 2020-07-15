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
        Task<string> AddIdentity(XIdentity identity, Action<string, byte[]> initTLSUser);
        Task<XIdentity> GetIdentityAsync(string identityId);
        Task<string> AddMessage(XMessage message);
		Task<string> AddResendRequest(XResendRequest resendRequest);
        Task<MessageNodeRepository.RepoStats> GetStatsAsync();

    }
}
