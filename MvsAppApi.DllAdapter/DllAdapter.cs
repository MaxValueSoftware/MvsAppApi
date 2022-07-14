using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MvsAppApi.Core;
using MvsAppApi.Core.Enums;
using MvsAppApi.Core.Structs;
using MvsAppApi.DllAdapter.Structs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MvsAppApi.DllAdapter
{
    // see https://stackoverflow.com/questions/42443175/marshal-const-char
    public unsafe static class StringMarshaller
    {
        public static string[] Marshal(byte** nativeStrings, int stringCount, LogCallback _log)
        {
            _log($@"Marshal: {stringCount} strings");
            var strings = new string[stringCount];
            for (var x = 0; x < stringCount; ++x)
            {
                if (nativeStrings[x] == null)
                {
                    _log($@"Marshal: nativeStrings[x] == null, skipping");
                    continue;
                }

                var length = GetStringLength(nativeStrings[x]);
                //_log($@"Marshal: GetStringLength(nativeStrings[x]) == {length}");
                strings[x] = length == 0
                    ? string.Empty
                    : Encoding.UTF8.GetString(nativeStrings[x], length);
                //_log($@"Marshal: marshalled to {strings[x]}");
            }

            return strings;
        }

        private static int GetStringLength(byte* nativeString)
        {
            var length = 0;

            while (*nativeString != '\0')
            {
                ++length;
                ++nativeString;
            }

            return length;
        }
    }

    public class DllAdapter : IAdapter
    {
        private Profile _profile;
        // initial callbacks
        private LogCallback _log;
        private QuitCallback _quit;
        // connection callbacks
        private ConnectHashCallback _connectHash;
        private ConnectInfoCallback _connectInfo;
        private Imports.ReplayHandCallback _replayHandCallbackKeepAlive;
        private Imports.NoteHandsCallback _noteHandsCallbackKeepAlive;
        private Imports.StatValueCallback _statValueCallbackKeepAlive;
        private Imports.HandsSelectedCallback _handsSelectedCallbackKeepAlive;
        private Imports.TablesCallback _tablesCallbackKeepAlive;
        
        private const string RequestLabel = "Request: ";
        private TimeSpan _totalClientDuration;
        private long _totalClientResponses;        

        public bool SendingBrokenResponses { get; set; }
        public bool BreakStatValues { get; set; }

        // todo: BreakRequests, DisableUnsavedChangesSupport (support these for DLL?  conditionally disable in demo?  or remove from demo completely?)
        public bool BreakRequests { get; set; }
        public bool DisableUnsavedChangesSupport { get; set; }
        

        public bool Connect(Profile profile, LogCallback log, QuitCallback quit, ConnectHashCallback hash, ConnectInfoCallback info)
        {
            _profile = profile;

            // these are the mandatory callbacks so not in profile  and need to be kept alive after registering to avoid garbage collection
            _log = log;
            _quit = quit;
            _connectHash = hash;
            _connectInfo = info;

            // note, these are internal callbacks and need to be kept alive after registering to avoid garbage collection
            _replayHandCallbackKeepAlive = ReplayHandCallback;
            _noteHandsCallbackKeepAlive = NoteHandsCallback;
            _statValueCallbackKeepAlive = StatValueCallback;
            _handsSelectedCallbackKeepAlive = HandsSelectedCallback;
            _tablesCallbackKeepAlive = TablesCallback;
            
            var res = Imports.MvsApiInitialize(_log, _quit);
            if (res == ApiErrorCode.MvsApiResultSuccess)
                res = Imports.MvsApiConnect((int)_profile.Tracker, _profile.MaxOutbound, _profile.MaxInbound, _profile.AppName,
                    _profile.AppVersion, ConnectHashCallback, ConnectInfoCallback);

            // register callbacks (inbound requests)
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.ImportStartedCallback != null) 
                res = Imports.MvsApiRegisterCallbackImportStarted(profile.ImportStartedCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.ImportStoppedCallback != null)
                res = Imports.MvsApiRegisterCallbackImportStopped(profile.ImportStoppedCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.HandCallback != null)
                res = Imports.MvsApiRegisterCallbackHand(profile.HandCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && _handsSelectedCallbackKeepAlive != null)
                res = Imports.MvsApiRegisterCallbackHandsSelected(_handsSelectedCallbackKeepAlive);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.TablesCallback != null)
                res = Imports.MvsApiRegisterCallbackTables(_tablesCallbackKeepAlive);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.NotesCallback != null)
                res = Imports.MvsApiRegisterCallbackNotes(profile.NotesCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.TagsCallback != null)
                res = Imports.MvsApiRegisterCallbackTags(profile.TagsCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.StatValueCallback != null)
                res = Imports.MvsApiRegisterCallbackStatValue(_statValueCallbackKeepAlive);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.StatPreviewCallback != null)
                res = Imports.MvsApiRegisterCallbackStatPreview(profile.StatPreviewCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.CallbackCallback != null)
                res = Imports.MvsApiRegisterCallbackCallback(profile.CallbackCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.MenuSelectedCallback != null)
                res = Imports.MvsApiRegisterCallbackMenuSelected(profile.MenuSelectedCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.NoteTabValueCallback != null)
                res = Imports.MvsApiRegisterCallbackNoteTabValue(profile.NoteTabValueCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.NoteHandsCallback != null)
                res = Imports.MvsApiRegisterCallbackNoteHands(_noteHandsCallbackKeepAlive);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.SettingsChangedCallback != null)
                res = Imports.MvsApiRegisterCallbackSettingsChanged(profile.SettingsChangedCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.LicenseChangedCallback != null)
                res = Imports.MvsApiRegisterCallbackLicenseChanged(profile.LicenseChangedCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.StatsChangedCallback != null)
                res = Imports.MvsApiRegisterCallbackStatsChanged(profile.StatsChangedCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.SleepBeginCallback != null)
                res = Imports.MvsApiRegisterCallbackSleepBegin(profile.SleepBeginCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.SleepEndCallback != null)
                res = Imports.MvsApiRegisterCallbackSleepEnd(profile.SleepEndCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.HasUnsavedChangesCallback != null)
                res = Imports.MvsApiRegisterCallbackHasUnsavedChanges(profile.HasUnsavedChangesCallback);
            if (res == ApiErrorCode.MvsApiResultSuccess && profile.ReplayHandCallback != null)
                res = Imports.MvsApiRegisterReplayHand(_replayHandCallbackKeepAlive);

            if (res != ApiErrorCode.MvsApiResultSuccess)
            {
                Log("Error registering callbacks");
            }

            return res == ApiErrorCode.MvsApiResultSuccess;
        }

        // native methods (wrapper)
        private bool ConnectHashCallback(string salt, IntPtr buffer, uint bufferLen)
        {
            var hash = _connectHash(salt);
            // unmarshal it, then return OK
            byte[] bytes = Encoding.ASCII.GetBytes(hash);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            return true;
        }

        private bool ConnectInfoCallback(string rootDir, string dataDir, string logDir, Restriction[] restrictions, uint restrictionsCount, bool isTrial, string expires, string trackerVersion, string apiVersion)
        {
            // todo: ConnectInfoCallback (needs dll fix; missing is_sleeping, and email)
            var isSleeping = false;
            var email = "";
            
            var success = false;
            if (_connectInfo != null)
                success = _connectInfo(rootDir, dataDir, logDir, restrictions, isTrial, expires, isSleeping, email, trackerVersion, apiVersion);

            return success;
        }

        unsafe bool QueryHmqlCallback(int callerId, bool errored, int errorCode, string errorMessage, int rowIndex, int rowCount, byte** valuesArr, byte** typesArr, int valuesCount, IntPtr userData)
        {
            var success = false;

            var values = StringMarshaller.Marshal(valuesArr, valuesCount, _log);
            var types = StringMarshaller.Marshal(typesArr, valuesCount, _log);
            var hmqlValues = new List<HmqlValue>();
            for (int x = 0; x < values.Length; x++)
            {
                var hmqlValue = new HmqlValue
                {
                    Value = values[x],
                    Type = types[x]
                };
                hmqlValues.Add(hmqlValue);
            }
            
            _queryHmqlResult.CallerId = callerId;
            _queryHmqlResult.Errored = errored;
            _queryHmqlResult.ErrorCode = errorCode;
            _queryHmqlResult.ErrorMessage = errorMessage;
            _queryHmqlResult.Values.Add(hmqlValues.ToArray());

            if (_queryHmqlCallback != null && rowIndex == 0)
                success = _queryHmqlCallback(_queryHmqlResult, userData);

            if (rowIndex == rowCount - 1)
                _queryHmqlResult.Values.CompleteAdding();
            
            return success;
        }

        unsafe bool QueryPtsqlCallback(int callerId, bool errored, int errorCode, string errorMessage, int row, int rowCount, byte** valuesArr, byte** pctDetailsArr, int valuesCount, IntPtr userData)
        {
            string[] values;
            string[] pctDetails;
            values = StringMarshaller.Marshal(valuesArr, valuesCount, _log);
            pctDetails = StringMarshaller.Marshal(pctDetailsArr, valuesCount, _log);
            _queryPtsqlResult.CallerId = callerId;
            _queryPtsqlResult.Errored = errored;
            _queryPtsqlResult.ErrorCode = errorCode;
            _queryPtsqlResult.ErrorMessage = errorMessage;
            var statValueList = new List<StatValue>();
            for (var x = 0; x < valuesCount; x++)
            {
                var statValue = new StatValue
                {
                    Value = values[x],
                    PctDetail = pctDetails[x]
                };
                statValueList.Add(statValue);
            }

            bool success = false;
            if (_queryPtsqlCallback != null && row == 0)
                success = _queryPtsqlCallback(_queryPtsqlResult, userData);

            _queryPtsqlResult.PlayerStatValues.Add(statValueList.ToArray());
            if (row == rowCount - 1)
                _queryPtsqlResult.PlayerStatValues.CompleteAdding();
            return success;
        }

        private unsafe bool GetHandsCallback(int callerId, bool errored, int errorCode, string errorMessage, byte** hands, int handsCount, IntPtr userData)
        {
            var handsArray = StringMarshaller.Marshal(hands, handsCount, _log);
            var result = _getHandsCallback(callerId, handsArray, userData);
            return result;
        }

        private string SplitHandsDelimeter => "\n\n";

        internal class HandsToSharedMemoryInfo
        {
            internal MemoryMappedFile MemoryMappedFile;
            internal MemoryMappedViewStream MemoryMappedViewStream;
        }

        private HandsToSharedMemoryInfo _mmvs;


        private string _getHandsToFileFilename;
        private bool GetHandsToFileCallback(int callerId, bool errored, int errorCode, string errorMessage, int handsWritten, IntPtr userData)
        {
            var contents = File.ReadAllBytes(_getHandsToFileFilename);
            var text = Encoding.UTF8.GetString(contents);
            var hands = text.Split(new[] { SplitHandsDelimeter }, StringSplitOptions.None);
            var result = _getHandsToFileCallback(callerId, hands, userData);
            return result;
        }

        private bool GetHandsToSharedMemoryCallback(int callerId, bool errored, int errorCode, string errorMessage, int handsWritten, int bytesWritten, IntPtr userData)
        {
            var binaryReader = new BinaryReader(_mmvs.MemoryMappedViewStream);
            var contents = binaryReader.ReadBytes(bytesWritten);
            _mmvs.MemoryMappedViewStream.Dispose();
            _mmvs.MemoryMappedFile.Dispose();
            var text = Encoding.UTF8.GetString(contents);
            var handsArray = text.Split(new[] { SplitHandsDelimeter }, StringSplitOptions.None);

            var hands = handsArray.Select(ha => ha.ToString()).ToArray();
            var result = _getHandsToSharedMemoryCallback(callerId, hands, userData);
            return result;
        }


        private unsafe bool GetHandTagsCallback(int callerId, bool errored, int errorCode, string errorMessage, byte** tags, int tagsCount, IntPtr userData)
        {
            var tagsArray = StringMarshaller.Marshal(tags, tagsCount, _log);
            var result = _getHandTagsCallback(callerId, errored, errorCode, errorMessage, tagsArray, userData);
            return result;
        }

        private bool QueryPlayersCallback(int callerId, bool errored, int errorCode, string errorMessage, string playerName, int siteId, bool anonymous, int cashHands, int tourneyHands, int current, int max, IntPtr userData)
        {
            if (current == 0)
                _log("first QueryPlayersCallback");
            var success = false;
            var player = new PlayerData
            {
                Anon = anonymous,
                Name = playerName,
                SiteId = siteId,
                CashHands = cashHands,
                TournamentHands = tourneyHands
            };

            if (_queryPlayersResult != null)
            {
                _queryPlayersResult.CallerId = callerId;
                _queryPlayersResult.Errored = errored;
                _queryPlayersResult.ErrorCode = errorCode;
                _queryPlayersResult.ErrorMessage = errorMessage;
                _queryPlayersResult.Players.Add(player);

                if (current == 0)
                    success = _queryPlayersCallback(_queryPlayersResult, userData);

                if (current == max - 1)
                {
                    _log("last QueryPlayersCallback");
                    _queryPlayersResult.Players.CompleteAdding();
                }
            }

            return success;
        }

        private bool NoteHandsCallback(string noteId, HandInternal[] dllNoteHands, int handsMax, out int handsCount)
        {
            var res = false;
            HandIdentifier[] noteHands = null;
            if (_profile.NoteHandsCallback != null)
                res = _profile.NoteHandsCallback(noteId, out noteHands);
            handsCount = 0;
            if (res)
            {
                for (var x = 0; x < handsMax && x < noteHands.Length; x++)
                {
                    dllNoteHands[x].SiteId = noteHands[x].SiteId;
                    dllNoteHands[x].HandNo = noteHands[x].HandNo;
                    dllNoteHands[x].StructSize = Marshal.SizeOf(dllNoteHands[x]);
                }
            }
            return res;
        }

        private bool ReplayHandCallback(string hand, int hwnd, Point[] centerPoints, int pointsCount)
        {
            return _profile.ReplayHandCallback(hand, hwnd, centerPoints);
        }

        private bool TablesCallback(Table[] tables, uint tablesCount)
        {
            return _profile.TablesCallback(tables);
        }

        private bool StatValueCallback(int requestId, string stat, int tableType, int siteId, string player, string filters, int current, int max, StringBuilder buffer, int bufferLen)
        {
            if (current == 0)
                _log("first StatValueCallback");

            var success = _profile.StatValueCallback(stat, tableType, siteId, player, filters, out var value);
            buffer.Append(value);
            
            if (current == max - 1)
                _log("last StatValueCallback");

            return success;
        }

        private bool HandsSelectedCallback(string menuItem, string [] selectedHands, int selectedHandsCount)
        {
            return _profile.HandsSelectedCallback(selectedHands, menuItem);
        }

        public void Disconnect()
        {
            _log("Disconnect: calling Imports.MvsApiShutdown()");
            Imports.MvsApiShutdown();
            _log("Disconnect: past Imports.MvsApiShutdown()");
        }

        public bool BusyStateBegin()
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiBusyStateBegin();
            AddClientText(RequestLabel + "BusyStateBegin");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == 0;
        }

        public bool BusyStateEnd()
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiBusyStateEnd();
            AddClientText(RequestLabel + "BusyStateEnd");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == 0;
        }

        public bool RegisterMenu(List<string> menuItems)
        {
            // register the menu
            var start = DateTime.Now;

            var result = Imports.MvsApiRegisterMenu(menuItems.ToArray());
            AddClientText(RequestLabel + "RegisterMenu");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private const int MaxSettingSize = 1000;
        public bool GetSetting(string settingName, out object value)
        {
            value = null;
            var start = DateTime.Now;
            ApiErrorCode result;
            switch (settingName)
            {
                case "active_player":
                    var settingActivePlayer = new SettingActivePlayerInternal();
                    settingActivePlayer.StructSize = Marshal.SizeOf(settingActivePlayer);
                    result = Imports.MvsApiGetSettingActivePlayer(ref settingActivePlayer);
                    if (result == ApiErrorCode.MvsApiResultSuccess)
                    {
                        var currentPlayerInfo = new CurrentPlayerInfo
                        {
                            SiteId = settingActivePlayer.SiteId.ToString(),
                            PlayerName = settingActivePlayer.PlayerName
                        };
                        value = currentPlayerInfo;
                        result = Imports.MvsApiCleanupActivePlayer(ref settingActivePlayer);
                    }
                    break;
                case "available_hud_profiles":
                    var settingHudProfiles = new SettingHudProfilesInternal();
                    settingHudProfiles.StructSize = Marshal.SizeOf(settingHudProfiles);
                    result = Imports.MvsApiGetSettingHudProfiles(ref settingHudProfiles);
                    if (result == ApiErrorCode.MvsApiResultSuccess)
                    {
                        unsafe
                        {
                            value = StringMarshaller.Marshal(settingHudProfiles.Profiles, (int)settingHudProfiles.ProfilesCount, _log);
                            result = Imports.MvsApiCleanupHudProfiles(ref settingHudProfiles);
                        }
                    }
                    break;
                case "hand_tags":
                    var handTags = new SettingHandTagsInternal();
                    handTags.StructSize = Marshal.SizeOf(handTags);
                    result = Imports.MvsApiGetSettingHandTags(ref handTags);
                    if (result == ApiErrorCode.MvsApiResultSuccess)
                    {
                        var handTagsList = new List<SettingHandTag>();
                        var tags = handTags.Tags;
                        for (var i = 0; i < handTags.TagsCount; i++)
                        {
                            var ms = (SettingHandTag)Marshal.PtrToStructure(tags, typeof(SettingHandTag));
                            handTagsList.Add(ms);
                            tags += Marshal.SizeOf(ms);
                        }
                        value = handTagsList.ToArray();
                        result = Imports.MvsApiCleanupHandTags(ref handTags);
                    }
                    break;
                default:
                    var buffer = new StringBuilder(MaxSettingSize);
                    result = Imports.MvsApiGetSetting(settingName, buffer, MaxSettingSize);
                    if (result == ApiErrorCode.MvsApiResultSuccess) 
                        value = buffer.ToString();
                    break;
            }

            AddClientText(RequestLabel + "GetSetting");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool ReplayHands(List<HandSelector> handSelectors)
        {
            var start = DateTime.Now;
            var dllHandSelectors = new List<HandSelectorInternal>();
            foreach (var hs in handSelectors)
            {
                var dllHandSelector = new HandSelectorInternal
                {
                    Action = hs.Action, 
                    Street = hs.Street, 
                    SiteId = hs.SiteId,
                    HandNo = hs.HandNo
                };
                dllHandSelector.StructSize = Marshal.SizeOf(dllHandSelector);
                dllHandSelectors.Add(dllHandSelector);
            }
            var result = Imports.MvsApiReplayHands(dllHandSelectors.ToArray(), dllHandSelectors.Count);
            AddClientText(RequestLabel + "ReplayHands");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool RequestHands()
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiRequestHands(out var importStarted);
            AddClientText(RequestLabel + "RequestHands" + ", importStarted=" + importStarted);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool RequestTables()
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiRequestTables();
            AddClientText(RequestLabel + "RequestTables");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private static SelectStatsCallback _selectStatsCallback;
        private static Imports.SelectStatsCallback _selectStatsCallbackKeepAlive;

        public bool SelectStats(TableType tableType, string[] includedStats, string[] defaultStats, SelectStatsCallback callback)
        {
            _selectStatsCallback = callback;
            _selectStatsCallbackKeepAlive = SelectStatsCallback;
            var userData = new IntPtr();
            int callerId = 0;
            var start = DateTime.Now;
            ApiErrorCode result;
            unsafe
            {
                result = Imports.MvsApiSelectStats(tableType, includedStats, includedStats.Length, defaultStats, defaultStats.Length, _selectStatsCallbackKeepAlive, userData, ref callerId);
            }
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + "SelectStats");
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private bool SelectStatsCallback(int callerId, bool cancelled, IntPtr selectedStats, int selectedStatsCount, IntPtr userData)
        {
            string[] selectedStatsArray;
            unsafe
            {
                selectedStatsArray = StringMarshaller.Marshal((byte **) selectedStats, selectedStatsCount, _log);
            }
            var result = _selectStatsCallback(callerId, cancelled, selectedStatsArray, userData);
            return result;
        }

        public bool SelectFilters(string tableType, string statQueryFilters, SelectFiltersCallback callback)
        {
            var userData = new IntPtr();
            int callerId = 0;
            var start = DateTime.Now;
            var tableTypeInt = tableType == "cash" ? 1 : 2;
            AddClientText(RequestLabel + "SelectFilters");
            var result = Imports.MvsApiSelectFilters(tableTypeInt, statQueryFilters, callback, userData, ref callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private HandInternal[] HandIdsInternalFromHandIdentifiers(IEnumerable<HandIdentifier> handIds)
        {
            var handIdsInternal = new List<HandInternal>();
            foreach (var hand in handIds)
            {
                var handInternal = new HandInternal
                {
                    HandNo = hand.HandNo,
                    SiteId = hand.SiteId
                };
                handInternal.StructSize = Marshal.SizeOf(handInternal);
                handIdsInternal.Add(handInternal);
            }

            return handIdsInternal.ToArray();
        }

        private static GetHandsCallback _getHandsCallback;
        public bool GetHands(IEnumerable<HandIdentifier> handIds, bool includeNative, GetHandsCallback callback)
        {
            unsafe
            {
                _getHandsCallback = callback;
                var start = DateTime.Now;
                AddClientText(RequestLabel + "GetHands");
                var handIdsInternal = HandIdsInternalFromHandIdentifiers(handIds);
                var includeNative2 = includeNative;
                var result = Imports.MvsApiGetHands(handIdsInternal, handIdsInternal.Length, includeNative2, GetHandsCallback, IntPtr.Zero, out var callerId);
                var dateDiff = DateTime.Now - start;
                AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
                return result == ApiErrorCode.MvsApiResultSuccess;
            }
        }

        private static GetHandsToFileCallback _getHandsToFileCallback;
        public bool GetHandsToFile(IEnumerable<HandIdentifier> handIds, bool includeNative, string fileName, GetHandsToFileCallback callback)
        {
            _getHandsToFileCallback = callback;
            _getHandsToFileFilename = fileName;
            var start = DateTime.Now;
            AddClientText(RequestLabel + "GetHandsToFile");
            var handIdsInternal = HandIdsInternalFromHandIdentifiers(handIds);
            var includeNative2 = includeNative;
            var result = Imports.MvsApiGetHandsToFile(handIdsInternal, handIdsInternal.Length, includeNative2, fileName, GetHandsToFileCallback, IntPtr.Zero, out var callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private static GetHandsToSharedMemoryCallback _getHandsToSharedMemoryCallback;
        public bool GetHandsToSharedMemory(IEnumerable<HandIdentifier> handIds, bool includeNative, string memoryName, int memorySize, GetHandsToSharedMemoryCallback callback)
        {
            _getHandsToSharedMemoryCallback = callback;
            var handsToSharedMemoryInfo = new HandsToSharedMemoryInfo();
            handsToSharedMemoryInfo.MemoryMappedFile = MemoryMappedFile.CreateOrOpen(memoryName, memorySize);
            handsToSharedMemoryInfo.MemoryMappedViewStream = handsToSharedMemoryInfo.MemoryMappedFile.CreateViewStream();
            // todo: fix this hack!
            _mmvs = handsToSharedMemoryInfo; 
            
            var start = DateTime.Now;
            AddClientText(RequestLabel + "GetHandsToSharedMemory");
            var handIdsInternal = HandIdsInternalFromHandIdentifiers(handIds);
            var includeNative2 = includeNative;
            var result = Imports.MvsApiGetHandsToSharedMemory(handIdsInternal, handIdsInternal.Length, includeNative2, memoryName, memorySize, GetHandsToSharedMemoryCallback, IntPtr.Zero, out var callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }


        private static GetHandTagsCallback _getHandTagsCallback;

        public bool GetHandTags(int siteId, string handNo, GetHandTagsCallback callback)
        {
            unsafe
            {
                _getHandTagsCallback = callback;
                var start = DateTime.Now;
                AddClientText($"{RequestLabel}GetHandTags({siteId},{handNo})");
                var result = Imports.MvsApiGetHandTags(siteId, handNo, GetHandTagsCallback, IntPtr.Zero, out int callerId);
                var dateDiff = DateTime.Now - start;
                AddClientText(ClientResponseLine(dateDiff, $"result={result}, callerId={callerId}"));
                return result == ApiErrorCode.MvsApiResultSuccess;
            }
        }

        private BlockingCollection<StatInfo> _statInfos;
        public bool GetStats(TableType tableType, bool fullDetails, GetStatsCallback callback, IntPtr userData)
        {
            var start = DateTime.Now;
            AddClientText($"{RequestLabel}GetStats({tableType},{fullDetails})");
            _statInfos = new BlockingCollection<StatInfo>();
            _log("call MvsApiGetStats, gathering stats");
            var result = Imports.MvsApiGetStats((int) tableType, fullDetails, GetStatsCallback, IntPtr.Zero);
            _statInfos.CompleteAdding();
            _log($@"gathering stats, done ({_statInfos.Count} stats)");
            _log("invoke GetStats callback");
            callback?.Invoke(_statInfos, userData);
            _log("invoked GetStats callback");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private bool GetStatsCallback(StatInternal stat, string type, bool playerPct, bool hudSafe, bool groupBy, bool tableAverageable, int appId, IntPtr userData)
        {
            var statInfo = new StatInfo
            {
                Stat = stat.Name,
                Desc = stat.Description,
                Value = stat.Value,
                Format = stat.Format,
                Type = type,
                // Categories = stat.Categories,
                PlayerPct = playerPct,
                HudSafe = hudSafe,
                GroupBy = groupBy,
                AppId = appId,
                // TableAverageable = tableAverageable,
                // Flags = stat.Flags,
                // etc
            };
            _statInfos.TryAdd(statInfo);
            return true;
        }

        private QueryNotesCallback _queryNotesCallback;
        private Imports.QueryNotesCallback _queryNotesCallbackKeepAlive;
        private QueryNotesResult _queryNotesResult;
        
        public bool QueryNotes(int siteId, IEnumerable<string> playerNames, QueryNotesCallback callback)
        {
            _queryNotesCallback = callback;
            _queryNotesCallbackKeepAlive = QueryNotesCallback;
            _queryNotesResult = new QueryNotesResult { PlayerNotes = new BlockingCollection<PlayerNote>() };
            var start = DateTime.Now;
            var playerNamesArray = playerNames.ToArray();
            AddClientText($"{RequestLabel}QueryNotes for players:{string.Join(",", playerNamesArray)}, siteId:{siteId},");

            var result = Imports.MvsApiQueryNotesMultiplePlayers(siteId, playerNamesArray, playerNamesArray.Length, QueryNotesCallback, IntPtr.Zero, out var callerId);
            
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private bool QueryNotesCallback(int callerId, bool errored, int errorCode, string errorMessage, string player, string color, string notes, int current, int max, IntPtr userData)
        {
            if (current == 0)
                _log("first QueryNotesCallback");
            var success = false;
            var playerObj = new PlayerNote
            {
                Player = player,
                Color = color,
                Note = notes,
            };

            if (_queryNotesResult != null)
            {
                _queryNotesResult.CallerId = callerId;
                _queryNotesResult.Errored = errored;
                _queryNotesResult.ErrorCode = errorCode;
                _queryNotesResult.ErrorMessage = errorMessage;
                _queryNotesResult.PlayerNotes.Add(playerObj);

                if (current == 0)
                    success = _queryNotesCallback(_queryNotesResult, userData);

                if (current == max - 1)
                {
                    _log("last QueryNotesCallback");
                    _queryNotesResult.PlayerNotes.CompleteAdding();
                }
            }

            return success;
        }

        private QueryPlayersCallback _queryPlayersCallback;
        private Imports.QueryPlayersCallback _queryPlayersCallbackKeepAlive;
        private QueryPlayersResult _queryPlayersResult;

        public bool QueryPlayers(int? siteId, string playerName, bool? anon, string gameType, int? minCashHands, int? maxCashHands,
            int? minTourneyHands, int? maxTourneyHands, List<string> orderByFields, string order, int? limit, int? offset, QueryPlayersCallback callback)
        {
            unsafe
            {
                _queryPlayersCallback = callback;
                _queryPlayersCallbackKeepAlive = QueryPlayersCallback;
                _queryPlayersResult = new QueryPlayersResult { Players = new BlockingCollection<PlayerData>() };

                // register the menu
                var start = DateTime.Now;
                var orderByDesc = order == "DESC";
                var orderByFieldIds = new List<int>();
                foreach (var field in orderByFields)
                {
                    var fieldId = 0;
                    switch (field)
                    {
                        case "name":
                            fieldId = 1;
                            break;
                        case "site_id":
                            fieldId = 2;
                            break;
                        case "anon":
                            fieldId = 3;
                            break;
                        case "c_hands":
                            fieldId = 4;
                            break;
                        case "t_hands":
                            fieldId = 5;
                            break;
                    }
                    orderByFieldIds.Add(fieldId);
                }

                var queryPlayersFilters = new QueryPlayerFiltersInternal
                {
                    SiteId = siteId ?? 0,
                    HandsMin = 0, // minCashHands ?? 0,
                    HandsMax = 0, // maxCashHands ?? 0,
                    CashHandsMin = minCashHands ?? 0,
                    CashHandsMax = maxCashHands ?? 0,
                    TourneyHandsMin = minTourneyHands ?? 0,
                    TourneyHandsMax = maxTourneyHands ?? 0,
                    Anon = anon == null ? 0 : anon == true ? 1 : 2,
                    GameType = gameType == null ? 0 : gameType == "cash" ? 1 : 2,
                    LimitTo = limit ?? 0,
                    Offset = offset ?? 0,
                    OrderByDesc = orderByDesc,
                    PlayerName = playerName
                };

                if (orderByFieldIds != null)
                {
                    var len = orderByFieldIds.Count > 5 ? 5 : orderByFieldIds.Count;
                    for (var x = 0; x < len; x++)
                        queryPlayersFilters.OrderBy[x] = orderByFieldIds[x];
                }

                queryPlayersFilters.StructSize = Marshal.SizeOf(queryPlayersFilters);
                
                AddClientText(RequestLabel + "QueryPlayers");

                var result = Imports.MvsApiQueryPlayers(queryPlayersFilters, _queryPlayersCallbackKeepAlive, IntPtr.Zero, out var callerId);
                var dateDiff = DateTime.Now - start;
                AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
                return result == ApiErrorCode.MvsApiResultSuccess;
            }
        }

        public bool RegisterNoteTab(string tabName, string tabIcon)
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiRegisterNoteTab(tabName, tabIcon);
            AddClientText(RequestLabel + "RegisterNoteTab");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool RegisterHandsMenu(List<string> menuItems, string menuIcon, HandFormat format)
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiRegisterHandsMenu(menuItems.ToArray(), menuIcon, format);
            AddClientText(RequestLabel + "RegisterHandsMenu");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool Noop(int wait, bool shouldFail, string extraBytes, out int noopSize)
        {
            var start = DateTime.Now;
            var result = Imports.MvsApiNoop(shouldFail, wait, extraBytes, out noopSize);
            AddClientText(RequestLabel + "Noop");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private Imports.QueryStatsCallback _queryStatsCallbackKeepAlive;
        private QueryStatsResult _queryStatsResult;
        private QueryStatsCallback _queryStatsCallback;

        public unsafe bool QueryStats(TableType tableType, int siteId, string[] players, string[] stats, string statQueryFilters, QueryStatsCallback callback)
        {
            var start = DateTime.Now;
            _queryStatsCallback = callback;
            _queryStatsCallbackKeepAlive = QueryStatsMultiplePlayersCallback;
            _queryStatsResult = new QueryStatsResult { PlayerStatValues = new BlockingCollection<StatValue[]>() };
            ApiErrorCode result;
            result = Imports.MvsApiQueryStatsMultiplePlayers((int)tableType, siteId, players, players.Length, stats, stats.Length, statQueryFilters, _queryStatsCallbackKeepAlive, IntPtr.Zero, out int callerId);
            AddClientText(RequestLabel + "QueryStats");
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private unsafe bool QueryStatsMultiplePlayersCallback(int callerId, bool errored, int errorCode, string errorMessage, int row, int rowCount, byte** valuesArr, byte** pctDetailsArr, int valuesCount, IntPtr userData)
        {
            string[] values;
            string[] pctDetails;
            values = StringMarshaller.Marshal(valuesArr, valuesCount, _log);
            pctDetails = StringMarshaller.Marshal(pctDetailsArr, valuesCount, _log);
            _queryStatsResult.CallerId = callerId;
            _queryStatsResult.Errored = errored;
            _queryStatsResult.ErrorCode = errorCode;
            _queryStatsResult.ErrorMessage = errorMessage;
            var statValueList = new List<StatValue>();
            for (var x = 0; x < valuesCount; x++)
            {
                var statValue = new StatValue
                {
                    Value = values[x],
                    PctDetail = pctDetails[x]
                };
                statValueList.Add(statValue);
            }

            bool success = false;
            if (_queryStatsCallback != null && row == 0)
                success = _queryStatsCallback(_queryStatsResult, userData);
            
            _queryStatsResult.PlayerStatValues.Add(statValueList.ToArray());
            if (row == rowCount - 1)
                _queryStatsResult.PlayerStatValues.CompleteAdding();
            return success;
        }

        public bool RegisterPositionalStats(TableType tableType, List<string> stats, PositionType positionType, HasPosition hasPosition, RegisterPositionalStatsCallback callback)
        {
            var start = DateTime.Now;
            AddClientText(RequestLabel + "RegisterPositionalStats");
            
            var result = Imports.MvsApiRegisterPositionalStats(tableType, stats.ToArray(), stats.Count, positionType, hasPosition, callback, IntPtr.Zero, out int callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool RegisterStats(List<Stat> stats, RegisterStatsCallback callback)
        {
            var start = DateTime.Now;
            AddClientText(RequestLabel + "RegisterStats");
            var statInternals = new List<StatInternal>();
            foreach (var stat in stats)
            {
                TableType tableType;
                switch (stat.TableType.ToLower())
                {
                    case "cash":
                        tableType = TableType.Cash;
                        break;
                    case "both":
                        tableType = TableType.Both;
                        break;
                    default:
                        tableType = TableType.Tournament;
                        break;
                }

                var statInternal = new StatInternal
                {
                    Name = stat.Name,
                    TableType = (int) tableType,
                    Value = stat.Value,
                    Description = stat.Description,
                    DetailedDescription = stat.Detail,
                    //Categories = stat.Categories,
                    //CategoriesCount = stat.Categories?.Length ?? 0,
                    //Flags = stat.Flags,
                    //FlagsCount = stat.Flags?.Length ?? 0,
                    Format = stat.Format,
                    Title = stat.Title,
                    Width = stat.Width
                };
                statInternal.StructSize = Marshal.SizeOf(statInternal);
                statInternals.Add(statInternal);
            }

            var result = Imports.MvsApiRegisterStats(statInternals.ToArray(), statInternals.Count, callback, IntPtr.Zero, out int callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool RemoveStats(List<Stat> stats, RemoveStatsCallback callback)
        {
            var tableTypeDict = new Dictionary<TableType, List<string>>();
            foreach (var stat in stats)
            {
                TableType tableType;
                switch (stat.TableType.ToLower())
                {
                    case "cash":
                        tableType = TableType.Cash;
                        break;
                    case "both":
                        tableType = TableType.Both;
                        break;
                    default:
                        tableType = TableType.Tournament;
                        break;
                }

                if (!tableTypeDict.TryGetValue(tableType, out var statNameList))
                {
                    statNameList = new List<string>();
                    tableTypeDict[tableType] = statNameList;
                }

                statNameList.Add(stat.Name);
            }

            var success = true;
            AddClientText(RequestLabel + "RemoveStats");
            foreach (var tuple in tableTypeDict)
            {
                var start = DateTime.Now;
                var tableType = tuple.Key;
                var statList = tuple.Value;
                var result = Imports.MvsApiRemoveStats((int)tableType, statList.ToArray(), statList.Count, callback, IntPtr.Zero, out int callerId);
                if (result != ApiErrorCode.MvsApiResultSuccess)
                    success = false;
                var dateDiff = DateTime.Now - start;
                AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            }

            return success;
        }

        public bool ImportHand(int siteId, string hand)
        {
            var start = DateTime.Now;
            AddClientText(RequestLabel + "ImportHand");
            var result = Imports.MvsApiImportHand(siteId, hand);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool ChangeHudProfile(int siteId, string tableName, string profileName)
        {
            var start = DateTime.Now;
            AddClientText(RequestLabel + "ChangeHudProfile");
            var result = Imports.MvsApiChangeHudProfile(siteId, tableName, profileName);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        public bool ImportHudProfile(string fileName, string profileName, TableType tableType, ImportHudProfileCallback callback)
        {
            var start = DateTime.Now;
            AddClientText(RequestLabel + "ImportHudProfile");
            int callerId;
            var result = Imports.MvsApiImportHudProfile(fileName, profileName, (int) tableType, callback, IntPtr.Zero, out callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private Imports.QueryHmqlCallback _queryHmqlCallbackKeepAlive;
        private QueryHmqlCallback _queryHmqlCallback;
        private QueryHmqlResult _queryHmqlResult;
        
        public unsafe bool QueryHmql(string hmqlQueryText, QueryHmqlCallback callback)
        {
            _queryHmqlCallback = callback;
            _queryHmqlCallbackKeepAlive = QueryHmqlCallback;
            _queryHmqlResult = new QueryHmqlResult { Values = new BlockingCollection<HmqlValue[]>() };

            var start = DateTime.Now;
            AddClientText(RequestLabel + "QueryHmql");
            var result = Imports.MvsApiQueryHmql(hmqlQueryText, _queryHmqlCallbackKeepAlive, IntPtr.Zero, out var callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private Imports.QueryPtsqlCallback _queryPtsqlCallbackKeepAlive;
        private QueryPtsqlCallback _queryPtsqlCallback;
        private QueryStatsResult _queryPtsqlResult;
        public unsafe bool QueryPtsql(TableType tableType, string[] stats, string filters, string[] orderByStats, bool orderByDesc, bool ptsqlQueryActivePlayer, bool ptsqlQueryHandQuery, QueryPtsqlCallback callback)
        {
            _queryPtsqlCallback = callback;
            _queryPtsqlCallbackKeepAlive = QueryPtsqlCallback;
            _queryPtsqlResult = new QueryStatsResult { PlayerStatValues = new BlockingCollection<StatValue[]>() };

            var start = DateTime.Now;
            AddClientText($@"{RequestLabel} QueryPtsql(tableType:{tableType}, activePlayer:{ptsqlQueryActivePlayer}, handQuery:{ptsqlQueryHandQuery}");
            var result = Imports.MvsApiQueryPtsql(tableType, stats, stats.Length, filters, orderByStats, orderByDesc, orderByStats.Length, ptsqlQueryActivePlayer, ptsqlQueryHandQuery, _queryPtsqlCallbackKeepAlive, IntPtr.Zero, out var callerId);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result == ApiErrorCode.MvsApiResultSuccess;
        }

        private void Log(string msg)
        {
            _log(msg);
        }

        private void AddServerText(string text)
        {
            var msg = "Server: " + text;
            Log(msg);
        }

        private void AddClientText(string text)
        {
            var msg = "Client: " + text;
            Log(msg);
        }

        private string ClientResponseLine(TimeSpan span, string response)
        {
            _totalClientResponses++;
            _totalClientDuration += span;
            ClientStatus = @"Tracker's avg. response time: " + _totalClientDuration.TotalMilliseconds / _totalClientResponses + @"ms.";
            return $"Response({span.TotalMilliseconds}ms): {response}";
        }

        public string ClientStatus { get; set; }
    }
}