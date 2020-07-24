using XDS.Features.ItemForwarding.Client.Data;
using XDS.SDK.Messaging.CrossTierTypes;
using XDS.SDK.Messaging.CrossTierTypes.FStore;

namespace XDS.Features.MessagingHost.Storage
{
	public static class FStoreInitializer
    {
		public static void InitFStore(FStoreConfig fStoreConfig)
        {
            var fStore = new FStoreMono(fStoreConfig);

            var identitiesTable = new FSTable(nameof(XIdentity), IdMode.UserGenerated); // Id is necessary to retrieve an item
            if (!fStore.TableExists(identitiesTable, null))
                fStore.CreateTable(identitiesTable);
            FStoreTables.TableConfig[typeof(XIdentity)] = identitiesTable;

            var messagesTable = new FSTable(nameof(XMessage), IdMode.UserGenerated, true, false); // e.g. /tbl_XMessage/1234567890/ac59f6f8-6e93-e185-d01a-94220b30d216
			if (!fStore.TableExists(messagesTable, null))                
                fStore.CreateTable(messagesTable);
            FStoreTables.TableConfig[typeof(XMessage)] = messagesTable;

	        var resendRequestsTable = new FSTable(nameof(XResendRequest), IdMode.UserGenerated, false, false); // e.g. /tbl_XMessage/1234567890/ac59f6f8-6e93-e185-d01a-94220b30d216
	        if (!fStore.TableExists(resendRequestsTable, null))
		        fStore.CreateTable(resendRequestsTable);
	        FStoreTables.TableConfig[typeof(XResendRequest)] = resendRequestsTable;

            var messageRelayRecordsTable = new FSTable(nameof(MessageRelayRecord), IdMode.UserGenerated); // Id is necessary to retrieve an item
            if (!fStore.TableExists(messageRelayRecordsTable, null))
                fStore.CreateTable(messageRelayRecordsTable);
            FStoreTables.TableConfig[typeof(MessageRelayRecord)] = messageRelayRecordsTable;
        }
    }
}
