using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MvsAppApi.Core.Enums;
using MvsAppApi.Core.Structs;

namespace MvsAppApi.Core 
{
    public interface IAdapter
    {
        // methods (outbound requests)
        bool Connect(Profile profile, LogCallback log, QuitCallback quit, ConnectHashCallback hash, ConnectInfoCallback info);
        void Disconnect();
        bool BusyStateBegin();
        bool BusyStateEnd();
        bool RegisterMenu(List<string> menuItems);
        bool GetSetting(string activeDatabaseAlias, out object o);
        bool ReplayHands(List<HandSelector> handSelectors);
        bool RequestHands();
        bool RequestTables();
        bool SelectStats(TableType tableType, string[] includedStats, string[] defaultStats, SelectStatsCallback callback);
        bool SelectFilters(string tableType, string statQueryFilters, SelectFiltersCallback callback);
        bool GetHands(IEnumerable<HandIdentifier> handIds, bool includeNative, GetHandsCallback callback);
        bool GetHandsToFile(IEnumerable<HandIdentifier> handIds, bool includeNative, string fileName, GetHandsToFileCallback callback);
        bool GetHandsToSharedMemory(IEnumerable<HandIdentifier> handIds, bool includeNative, string memoryName, int memorySize, GetHandsToSharedMemoryCallback callback);
        bool GetHandTags(int siteId, string handNo, GetHandTagsCallback callback);
        bool GetStats(TableType tableType, bool fullDetails, GetStatsCallback callback, IntPtr userData);
        bool QueryNotes(int siteId, IEnumerable<string> playerNames, QueryNotesCallback callback);
        bool QueryPlayers(int? siteId, string playerName, bool? anon, string gameType, int? minCashHands, int? maxCashHands, int? minTourneyHands, int? maxTourneyHands, List<string> orderByFields, string order, int? limit, int? offset, QueryPlayersCallback callback);
        bool ChangeHudProfile(int siteId, string tableName, string profileName);
        bool RegisterNoteTab(string tabName, string tabIcon);
        bool RegisterHandsMenu(List<string> menuItems, string menuIcon, HandFormat format);
        bool Noop(int wait, bool shouldFail, string extraBytes, out int noopSize);
        bool QueryStats(TableType tableType, int siteId, string[] playersList, string[] statsList, string filters, QueryStatsCallback callback);
        bool RegisterPositionalStats(List<string> stats, string tableType, string hasPosition, string positionType, RegisterPositionalStatsCallback callback);
        bool RegisterStats(List<Stat> stats, RegisterStatsCallback callback);
        bool RemoveStats(List<Stat> stats, RemoveStatsCallback callback);
        bool ImportHand(int importHandSiteId, string encodedHand);
        bool ImportHudProfile(string fileName, string profileFileName, TableType tableType, ImportHudProfileCallback callback);
        bool QueryHmql(string hmqlQueryText, QueryHmqlCallback callback);
        bool QueryPtsql(string tableType, string[] stats, bool activePlayer, bool handQuery, QueryPtsqlCallback callback);

        // misc properties
        bool BreakRequests { get; set; }
        bool DisableUnsavedChangesSupport { get; set; }
        bool SendingBrokenResponses { get; set; }
        bool BreakStatValues { get; set; }
    }

    #region delegates

    // initialization callbacks
    public delegate bool LogCallback(string filename);
    public delegate bool QuitCallback();

    // connection callbacks
    public delegate string ConnectHashCallback(string salt);
    public delegate bool ConnectInfoCallback(string rootDir, string dataDir, string logDir, Restriction[] restrictions, bool isTrial, string expires, bool isSleeping, string email, string trackerVersion, string apiVersion);

    // request callbacks
    public delegate bool ImportStartedCallback(string importType);
    public delegate bool ImportStoppedCallback();
    public delegate bool HandCallback(string hand);
    public delegate bool HandsSelectedCallback(string[] hands, string menuItem);
    public delegate bool TablesCallback(Table[] tables);
    public delegate bool NotesCallback(string player, int siteId, string notes, IntPtr autoNotesCash, int autoNotesCashCount, IntPtr autoNotesTny, int autoNotesTnyCount, string color);
    public delegate bool TagsCallback(int siteId, string handNo, IntPtr tags, int tagsCount);
    public delegate bool StatValueCallback(string stat, int tableType, int siteId, string player, string filters, out string value);
    public delegate bool StatPreviewCallback(string stat, int tableType);
    public delegate bool CallbackCallback(string callback, int windowId, int positionX, int positionY);
    public delegate bool MenuSelectedCallback(string menuItem);
    public delegate bool NoteTabValueCallback(string tabName, string playerName, int siteId, string lastHandNo, StringBuilder jsonBuffer, int jsonBufferLen);
    public delegate bool NoteHandsCallback(string noteId, out HandIdentifier[] hands);
    public delegate bool SettingsChangedCallback(string setting, string newValue);
    public delegate bool LicenseChangedCallback(Restriction[] restrictions, int restrictionsCount, int isTrial, string expires);
    public delegate bool StatsChangedCallback();
    public delegate bool SleepBeginCallback();
    public delegate bool SleepEndCallback();
    public delegate bool HasUnsavedChangesCallback();
    public delegate bool ReplayHandCallback(string hand, int hwnd, Point[] centerPoints);
    public delegate bool NoopCallback(int wait, bool shouldFail);

    // result callbacks
    public delegate bool SelectStatsCallback(int callerId, bool cancelled, string[] selectedStats, IntPtr userData);
    public delegate bool SelectFiltersCallback(int callerId, bool cancelled, string filters, IntPtr userData);
    public delegate bool GetHandsCallback(int callerId, string[] hands, IntPtr userData);
    public delegate bool GetHandsToFileCallback(int callerId, string[] hands, IntPtr userData);
    public delegate bool GetHandsToSharedMemoryCallback(int callerId, string[] hands, IntPtr userData);
    public delegate bool GetHandTagsCallback(int callerId, bool errored, int errorCode, string errorMessage, string[] tags, IntPtr userData);
    public delegate bool GetStatsCallback(BlockingCollection<StatInfo> stats, IntPtr userData);
    public delegate bool QueryNotesCallback(QueryNotesResult result, IntPtr userData);
    public delegate bool QueryPlayersCallback(QueryPlayersResult result, IntPtr userData);
    public delegate bool QueryStatsCallback(QueryStatsResult result, IntPtr userData);
    public delegate bool QueryHmqlCallback(QueryHmqlResult result, IntPtr userData);
    public delegate bool QueryPtsqlCallback(int callerId, bool errored, int errorCode, string errorMessage, StatValue[][] statValues, IntPtr userData);
    public delegate bool ImportHudProfileCallback(int callerId, bool errored, int errorCode, string errorMessage, IntPtr userData);
    public delegate bool RegisterPositionalStatsCallback(int callerId, bool errored, int errorCode, string errorMessage, string[] statNames, IntPtr userData);
    public delegate bool RegisterStatsCallback(int callerId, bool errored, int errorCode, string errorMessage, IntPtr userData);
    public delegate bool RemoveStatsCallback(int callerId, bool errored, int errorCode, string errorMessage, IntPtr userData);

    #endregion delegates
}