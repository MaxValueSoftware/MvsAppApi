using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MvsAppApi.Core;
using MvsAppApi.Core.Enums;
using MvsAppApi.Core.Structs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MvsAppApi.JsonAdapter
{
    public class JsonAdapter : IAdapter
    {
        private Profile _profile;
        // initial callbacks
        private LogCallback _log;
        private QuitCallback _quit;
        // connection callbacks
        private ConnectHashCallback _connectHash;
        private ConnectInfoCallback _connectInfo;
        

        private PipeStream _outbound;
        private int _outboundRequestId;
        private List<PipeStream> _inbound;
        private List<Thread> _serverLoopThread;

        private TimeSpan _totalClientDuration;
        private long _totalClientResponses;
        private TimeSpan _totalServerDuration;
        private long _totalServerResponses;
        private bool _breakRequests;

        private string _getHandsToFileFilename;
        private HandsToSharedMemoryInfo _mmvs;

        private bool _isConnected;


        private QueryPlayersCallback _queryPlayersCallback;
        private SelectStatsCallback _selectStatsCallback;
        private SelectFiltersCallback _selectFiltersCallback;
        private GetHandsCallback _getHandsCallback;
        private GetHandsToFileCallback _getHandsToFileCallback;
        private GetHandTagsCallback _getHandTagsCallback;
        private RegisterPositionalStatsCallback _registerPositionalStatsCallback;
        private RegisterStatsCallback _registerStatsCallback;
        private RemoveStatsCallback _removeStatsCallback;
        private GetStatsCallback _getStatsCallback;
        private GetHandsToSharedMemoryCallback _getHandsToSharedMemoryCallback;
        private QueryHmqlCallback _queryHmqlCallback;
        private QueryPtsqlCallback _queryPtsqlCallback;
        private QueryStatsCallback _queryStatsCallback;
        private QueryNotesCallback _queryNotesCallback;
        private ImportHudProfileCallback _importHudProfileCallback;


        private const string RequestLabel = "Request: ";
        
        public bool SendingBrokenResponses { get; set; }
        public string ServerStatus { get; set; }

        public bool Connect(Profile profile, LogCallback log, QuitCallback quit, ConnectHashCallback hash, ConnectInfoCallback info)
        {
            _profile = profile;
            _log = log;
            _quit = quit;
            _connectHash = hash;
            _connectInfo = info;
            var maxInbound = profile.MaxInbound;
            var tracker = profile.Tracker;
            var appName = profile.AppName;
            var apiVersion = profile.ApiVersion;
            var appId = profile.AppId;
            _inbound = new List<PipeStream>(maxInbound);
            _serverLoopThread = new List<Thread>(maxInbound);

            string salt, trackerVersion;
            dynamic response;
            
            // create/register/verify inbound (server) pipes

            var anyInboundRegErrors = false;
            //isSleeping = false;
            
            for (var x = 0; x < maxInbound; x++)
            {
                _inbound.Add(PipeStream.Create(tracker == Tracker.HM3, apiVersion, appName));
                if (!Register(_inbound[x], out salt, out trackerVersion, out apiVersion) || !Verify(_inbound[x], salt, true, out response))
                    anyInboundRegErrors = true;
            }

            if (anyInboundRegErrors)
            {
                _log("Error registering inbound pipes.");
                return false;
            }

            // create/register the outbound pipe

            _outbound = PipeStream.Create(tracker == Tracker.HM3, apiVersion, appName);

            if (!Register(_outbound, out salt, out trackerVersion, out apiVersion) || !Verify(_outbound, salt, false, out response))
            {
                _log("Error registering outbound pipe.");
                return false;
            }

            // todo: deserialize the verify response directly to a more appropriate object for the callback
            // get data from response ...
            dynamic result = null;
            if (response != null)
                result = response["result"];
            var dataDir = result != null ? result.Value<string>("data_directory") : "";
            var rootDir = result != null ? result.Value<string>("root_directory") : "";
            var logDir = result != null ? result.Value<string>("log_directory") : "";
            var restrictionsObj = result != null ? result["restrictions"] : null;
            var isTrial = result != null ? result.Value<bool>("trial") : false;
            var expires = result != null ? result.Value<string>("expires") : "";
            var email = result != null ? result.Value<string>("email") : false;
            var isSleeping = result != null ? result.Value<bool>("is_sleeping") : false;
            
            var restrictions = new List<Restriction>();
            if (restrictionsObj is JArray)
            {
                foreach (var restriction in restrictionsObj)
                {
                    restrictions.Add(new Restriction
                    {
                        Name = restriction.Value<string>("name"),
                        Type = restriction.Value<string>("type"),
                        Units = restriction.Value<string>("units"),
                        Value = restriction.Value<string>("value"),
                    });
                }
            }
            if (_connectInfo != null)
                _connectInfo(rootDir, dataDir, logDir, restrictions.ToArray(), isTrial, expires, isSleeping, email, trackerVersion, apiVersion);

            _isConnected = true;
            // start inbound threads
            for (var i = 0; i < maxInbound; i++)
            {
                var thread = new Thread(ServerLoop);
                _serverLoopThread.Add(thread);
                thread.Start(i);
            }

            return true;
        }

        public void Disconnect()
        {
            // stop the server loop
            _isConnected = false;

            var maxWaitCount = 50; // wait 5 secs max then disconnect if necessary (temp: workaround for app and HM3 server not closing)
            var waitCount = 0;

            while (waitCount < maxWaitCount && _serverLoopThread != null && _serverLoopThread.Any(slt => slt.IsAlive))
            {
                Thread.Sleep(20); // wait for servers to close
                waitCount++;
            }

            for (var i = 0; i < _profile.MaxInbound; i++)
            {
                var inbound = _inbound[i];
                if (inbound == null)
                    continue;
                if (inbound.NamedPipeStream.IsConnected)
                    inbound.NamedPipeStream.Close();
                inbound.NamedPipeStream.Dispose();
                _inbound[i] = null;
            }

            if (_outbound == null)
                return;
            if (_outbound.NamedPipeStream.IsConnected)
                _outbound.NamedPipeStream.Close();
            _outbound.NamedPipeStream.Dispose();
        }

        public bool BusyStateBegin()
        {
            // register the menu
            var start = DateTime.Now;
            var result = _outbound.BusyStateBegin(++_outboundRequestId, out var responseStr);
            AddClientText(RequestLabel + _outbound.PriorRequest);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool BusyStateEnd()
        {
            // register the menu
            var start = DateTime.Now;
            var result = _outbound.BusyStateEnd(++_outboundRequestId, out var responseStr);
            AddClientText(RequestLabel + _outbound.PriorRequest);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool RegisterMenu(List<string> menuItems)
        {
            // register the menu
            var start = DateTime.Now;
            var result = _outbound.RegisterMenu(++_outboundRequestId, menuItems, out var responseStr);
            AddClientText(RequestLabel + _outbound.PriorRequest);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool GetSetting(string settingName, out object value)
        {
            // register the menu
            var start = DateTime.Now;
            var result = _outbound.GetSetting(++_outboundRequestId, settingName, out value, out var responseStr);
            switch (settingName)
            {
                case "active_player":
                    var str = value.ToString();
                    value = JsonConvert.DeserializeObject<CurrentPlayerInfo>(str);
                    break;
                case "available_hud_profiles":
                    str = value.ToString();
                    value = JsonConvert.DeserializeObject<string[]>(str);
                    break;
                case "hand_tags":
                    str = value.ToString();
                    value = JsonConvert.DeserializeObject<SettingHandTag[]>(str);
                    break;
            }
            AddClientText(RequestLabel + _outbound.PriorRequest);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool ReplayHands(List<HandSelector> handSelectors)
        {
            var start = DateTime.Now;
            var result = _outbound.ReplayHands(++_outboundRequestId, handSelectors, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + "ReplayHands");
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool RequestHands()
        {
            var start = DateTime.Now;
            var result = _outbound.RequestHands(++_outboundRequestId, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + "RequestHands");
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool RequestTables()
        {
            var start = DateTime.Now;
            var result = _outbound.RequestTables(++_outboundRequestId, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return result;
        }

        public bool SelectStats(TableType tableType, string[] includedStats, string[] defaultStats, SelectStatsCallback callback)
        {
            _selectStatsCallback = callback;
            
            var start = DateTime.Now;
            var tableTypeStr = tableType == TableType.Cash ? "cash" : "tournament";
            var success = _outbound.SelectStats(++_outboundRequestId, tableTypeStr, includedStats, defaultStats, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool SelectFilters(string tableType, string statQueryFilters, SelectFiltersCallback callback)
        {
            _selectFiltersCallback = callback;
            
            var start = DateTime.Now;
            var success = _outbound.SelectFilters(++_outboundRequestId, tableType, statQueryFilters, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool GetHands(IEnumerable<HandIdentifier> handIds, bool includeNative, GetHandsCallback callback)
        {
            _getHandsCallback = callback;
            
            var start = DateTime.Now;
            var success = _outbound.GetHands(++_outboundRequestId, handIds, includeNative, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + "GetHands");
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            
            return success;
        }

        public bool GetHandsToFile(IEnumerable<HandIdentifier> handIds, bool includeNative, string fileName, GetHandsToFileCallback callback)
        {
            _getHandsToFileCallback = callback;
            _getHandsToFileFilename = fileName;

            var start = DateTime.Now;
            var success = _outbound.GetHandsToFile(++_outboundRequestId, handIds, fileName, includeNative, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + "GetHandsToFile");
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        private string SplitHandsDelimeter => "\n\n";

        internal class HandsToSharedMemoryInfo
        {
            internal MemoryMappedFile MemoryMappedFile;
            internal MemoryMappedViewStream MemoryMappedViewStream;
        }

        public bool GetHandsToSharedMemory(IEnumerable<HandIdentifier> handIds, bool includeNative, string memoryName, int memorySize, GetHandsToSharedMemoryCallback callback)
        {
            _getHandsToSharedMemoryCallback = callback;

            var handsToSharedMemoryInfo = new HandsToSharedMemoryInfo();
            handsToSharedMemoryInfo.MemoryMappedFile = MemoryMappedFile.CreateOrOpen(memoryName, memorySize);
            handsToSharedMemoryInfo.MemoryMappedViewStream = handsToSharedMemoryInfo.MemoryMappedFile.CreateViewStream();

            // todo: fix this hack!
            _mmvs = handsToSharedMemoryInfo;

            var start = DateTime.Now;
            var success = _outbound.GetHandsToSharedMemory(++_outboundRequestId, handIds, memoryName, memorySize, includeNative, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + "GetHandsToSharedMemory");
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }
        
        public bool GetHandTags(int siteId, string handNo, GetHandTagsCallback callback)
        {
            _getHandTagsCallback = callback;

            var success = _outbound.GetHandTags(++_outboundRequestId, siteId, handNo, out var responseStr);
            return success;
        }

        private BlockingCollection<StatInfo> _statInfos;
        public bool GetStats(TableType tableType, bool fullDetails, GetStatsCallback callback, IntPtr userData)
        {
            var start = DateTime.Now;
            _getStatsCallback = callback;
            _statInfos = new BlockingCollection<StatInfo>();
            var tableTypeStr = tableType == TableType.Cash ? "cash" : "tournament";
            var success = _outbound.GetStats(++_outboundRequestId, tableTypeStr, fullDetails, out var responseStr);
            if (success)
            {
                _log("invoke GetStats callback");
                _getStatsCallback?.Invoke(_statInfos, userData);
                _log("invoked GetStats callback");
                Task.Run(() =>
                {
                    _log("deserialize response");
                    var getStatsResponse = JsonConvert.DeserializeObject<Request>(responseStr);
                    if (getStatsResponse != null && getStatsResponse.Error == null && getStatsResponse.Result != null)
                        if (getStatsResponse.Result is JArray stats)
                        {
                            _log("gathering stats");
                            var getStatsResults = stats.ToObject<ObservableCollection<StatInfo>>();
                            foreach (var stat in getStatsResults)
                                _statInfos.TryAdd(stat);
                            _statInfos.CompleteAdding();
                            _log("gathering stats, done");
                        }
                });
            }

            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));

            return success;
        }

        
        private void QueryPlayersResponseHandler(JArray players, int callerId)
        {
            var result = new QueryPlayersResult
            {
                CallerId = callerId,
                UserData = IntPtr.Zero,
                ErrorCode = 0,
                ErrorMessage = "",
                Players = new BlockingCollection<PlayerData>()
            }; 
            
            foreach (var player in players)
            {
                result.Players.Add(new PlayerData
                {
                    Name = player["name"].Value<string>(),
                    SiteId = player["site_id"].Value<int>(),
                    Anon = player["anon"].Value<bool>(),
                    CashHands = player["c_hands"].Value<int>(),
                    TournamentHands = player["t_hands"].Value<int>()
                });
            }
            result.Players.CompleteAdding();

            _queryPlayersCallback(result, IntPtr.Zero);
        }

        public bool QueryNotes(int siteId, IEnumerable<string> playerNames, QueryNotesCallback callback)
        {
            _queryNotesCallback = callback;
            var start = DateTime.Now;
            var success = _outbound.QueryNotes(++_outboundRequestId, siteId, playerNames, out var responseStr);

            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        private void QueryNotesResponseHandler(JArray playerNotes, int callerId)
        {
            var result = new QueryNotesResult
            {
                CallerId = callerId,
                UserData = IntPtr.Zero,
                ErrorCode = 0,
                ErrorMessage = "",
                PlayerNotes = new BlockingCollection<PlayerNote>()
            };

            foreach (var playerNote in playerNotes)
            {
                result.PlayerNotes.Add(new PlayerNote
                {
                    Player = playerNote["player"].Value<string>(),
                    Color = playerNote["color"].Value<string>(),
                    Note = Base64Decode(playerNote["note"].Value<string>())
                });
            }
            result.PlayerNotes.CompleteAdding();

            _queryNotesCallback(result, IntPtr.Zero);
        }

        public bool QueryPlayers(int? siteId, string playerName, bool? anon, string gameType, int? minCashHands, int? maxCashHands,
            int? minTourneyHands, int? maxTourneyHands, List<string> orderByFields, string order, int? limit, int? offset, QueryPlayersCallback callback)
        {
            _queryPlayersCallback = callback;

            var start = DateTime.Now;
            var success = _outbound.QueryPlayers(++_outboundRequestId, siteId, playerName, anon, gameType, minCashHands, maxCashHands, minTourneyHands, maxTourneyHands, orderByFields, order, limit, offset, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool ChangeHudProfile(int siteId, string tableName, string hudProfile)
        {
            var start = DateTime.Now;
            var success = _outbound.ChangeHudProfile(++_outboundRequestId, siteId, tableName, hudProfile, out var responseStr);
            AddClientText(RequestLabel + _outbound.PriorRequest);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            if (success)
                AddClientText("Table Hud Changed: " + tableName + " [" + siteId + "]");
            return success;
        }

        public bool RegisterNoteTab(string tabName, string tabIcon)
        {
            var start = DateTime.Now;
            var success = _outbound.RegisterNoteTab(++_outboundRequestId, tabName, tabIcon, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool RegisterHandsMenu(List<string> menuItems, string menuIcon, HandFormat format)
        {
            var start = DateTime.Now;
            var success = _outbound.RegisterHandsMenu(++_outboundRequestId, menuItems, menuIcon, format, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool Noop(int wait, bool shouldFail, string extraBytes, out int noopSize)
        {
            var start = DateTime.Now;
            var success = _outbound.Noop(++_outboundRequestId, wait, shouldFail, extraBytes, out noopSize, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool QueryStats(TableType tableType, int siteId, string[] statQueryPlayersList, string[] statQueryStatsList,
            string statQueryFilters, QueryStatsCallback doQueryStatsCallback)
        {
            _queryStatsCallback = doQueryStatsCallback;
            // todo: convey through user data
            StatQueryStats = statQueryStatsList;
            StatQueryPlayers = statQueryPlayersList;

            var start = DateTime.Now;
            var success = _outbound.QueryStats(++_outboundRequestId, tableType, siteId, statQueryPlayersList, statQueryStatsList, statQueryFilters, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool RegisterPositionalStats(TableType tableType, List<string> stats, PositionType positionType, HasPosition hasPosition, RegisterPositionalStatsCallback callback)
        {
            _registerPositionalStatsCallback = callback;

            var start = DateTime.Now;
            var success = _outbound.RegisterPositionalStats(++_outboundRequestId, tableType, stats, positionType, hasPosition, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool RegisterStats(List<Stat> stats, RegisterStatsCallback callback)
        {
            _registerStatsCallback = callback;
            var start = DateTime.Now;            
            var success = _outbound.RegisterStats(++_outboundRequestId, stats, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool RemoveStats(List<Stat> stats, RemoveStatsCallback callback)
        {
            _removeStatsCallback = callback;
            var start = DateTime.Now;
            var success = _outbound.RemoveStats(++_outboundRequestId, stats, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool ImportHand(int importHandSiteId, string hand)
        {
            var start = DateTime.Now;
            AddClientText(RequestLabel + "ImportHand");
            var encodedHand = Base64Encode(hand);
            var result = _outbound.ImportHand(++_outboundRequestId, importHandSiteId, encodedHand, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(ClientResponseLine(dateDiff, $"result={result}"));
            return result;
        }

        public bool BreakRequests
        {
            get => _breakRequests;
            set
            {
                _outbound.BreakRequests = value;
                _breakRequests = value;
            }
        }

        
        // todo: temp, should be conveyed through userData
        public string [] StatQueryPlayers { get; set; }
        public string [] StatQueryStats { get; set; }
        public string[] PtsqlQueryStats { get; set; }


        private void QueryStatsResponseHandler(JArray results, int callerId)
        {
            // todo: callback errors
            var result = new QueryStatsResult
            {
                CallerId = callerId,
                UserData = IntPtr.Zero,
                ErrorCode = 0,
                ErrorMessage = "",
                PlayerStatValues = new BlockingCollection<StatValue[]>()
            };
            
            var stats = StatQueryStats;
            for (var index = 0; index < stats.Length; index++)
                stats[index] = stats[index].Trim();

            var players = StatQueryPlayers;
            for (var index = 0; index < players.Length; index++)
                players[index] = players[index].Trim();

            var playersCount = 0;
            foreach (var playerResults in results)
            {
                if (!(playerResults is JArray))
                    continue;
                
                var statCount = 0;
                var statValues = new List<StatValue>();
                foreach (var playerStatResult in (JArray)playerResults)
                {
                    if (statCount >= playerResults.Count()) break;
                    statValues.Add(new StatValue
                    {
                        Value = playerStatResult["v"].ToString(),
                        PctDetail = playerStatResult["%"]?.ToString()
                    });
                    statCount++;
                }

                result.PlayerStatValues.TryAdd(statValues.ToArray());
                playersCount++;
            }
            _queryStatsCallback(result, IntPtr.Zero);
        }


        private void QueryPlayersCallback(Request request, int callerId, JArray result, DateTime start)
        {
            if (result != null)
                QueryPlayersResponseHandler(result, callerId);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void QueryNotesCallback(Request request, int callerId, JArray result, DateTime start)
        {
            if (result != null)
                QueryNotesResponseHandler(result, callerId);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void QueryStatsCallback(Request request, int callerId, JArray result, DateTime start)
        {
            if (result != null)
                QueryStatsResponseHandler(result, callerId);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void QueryHmqlResponseHandler(JArray rows, int callerId)
        {
            var result = new QueryHmqlResult
            {
                CallerId = callerId,
                UserData = IntPtr.Zero,
                ErrorCode = 0,
                ErrorMessage = "",
                Values = new BlockingCollection<HmqlValue[]>()
            };
            var types = new List<string>();
            for (var x = 0; x < rows.Count; x++)
            {
                if (x == 0)
                {
                    // first row is data types
                    foreach (var col in rows[0])
                        types.Add(col.ToString());
                    continue;
                }

                var rowData = new List<HmqlValue>();
                var row = rows[x];
                var y = 0;
                foreach (var col in row)
                {
                    var type = types[y];
                    var hmqlValue = new HmqlValue
                    {
                        Value = col.ToString(),
                        Type = type
                    };
                    y++;
                    rowData.Add(hmqlValue);
                }
                result.Values.Add(rowData.ToArray());
            }

            _queryHmqlCallback(result, IntPtr.Zero);
        }
        
        private void QueryHmqlCallback(Request request, int callerId, JArray result, DateTime start)
        {
            if (result != null)
                QueryHmqlResponseHandler(result, callerId);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void QueryPtsqlResponseHandler(JArray rows, int callerId)
        {
            var stats = PtsqlQueryStats;
            for (var index = 0; index < stats.Length; index++)
                stats[index] = stats[index].Trim();

            var players = StatQueryPlayers;
            for (var index = 0; index < players.Length; index++)
                players[index] = players[index].Trim();

            var playerStatValues = new List<StatValue[]>();
            var playersCount = 0;
            foreach (var playerResults in rows)
            {
                if (!(playerResults is JArray))
                    continue;

                var statValues = new List<StatValue>();
                var statCount = 0;
                foreach (var playerStatResult in (JArray)playerResults)
                {
                    if (statCount >= playerResults.Count()) break;
                    statValues.Add(new StatValue
                    {
                        Value = playerStatResult["v"].ToString(),
                        PctDetail = playerStatResult["%"].ToString()
                    });
                    statCount++;
                }

                playerStatValues.Add(statValues.ToArray());

                // todo: callback errors
                _queryPtsqlCallback(callerId, false, 0, "", playerStatValues.ToArray(), IntPtr.Zero);
                playersCount++;
            }
        }

        
        private void QueryPtsqlCallback(Request request, int callerId, JArray result, DateTime start)
        {
            if (result != null)
                QueryPtsqlResponseHandler(result, callerId);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void GetHandTagsCallback(Request request, int callerId, JArray result, DateTime start)
        {
            if (result != null)
            {
                var handsTagsList = new List<string>();
                foreach (var handTag in result)
                {
                    if (handTag.Type == JTokenType.Object)
                        handsTagsList.Add(handTag["tag"].ToString());
                    else
                        handsTagsList.Add(handTag.ToString());
                }

                if (_getHandTagsCallback != null)
                    _getHandTagsCallback(callerId, false, 0, "", handsTagsList.ToArray(), IntPtr.Zero);
            }

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void RegisterPositionalStatsCallback(int callerId, JArray results)
        {
            var values = new List<string>();
                foreach (var statName in results)
                {
                    values.Add(statName.ToString());
                    
                }
                var valuesArray = values.ToArray();

            _registerPositionalStatsCallback(callerId, false, 0, "", valuesArray, IntPtr.Zero);
        }

        private void RegisterPositionalStatsCallback(Request request, JArray result, DateTime start)
        {
            if (result != null)
                RegisterPositionalStatsCallback(request.Id, result);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }



        private bool Register(PipeStream stream, out string salt, out string trackerVersion, out string apiVersion)
        {
            var start = DateTime.Now;
            var success = stream.Register(++_outboundRequestId, _profile.AppVersion, out var responseStr, out salt, out trackerVersion, out apiVersion);
            _log(RequestLabel + stream.PriorRequest);
            var dateDiff = DateTime.Now - start;
            _log(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        private bool Verify(PipeStream stream, string salt, bool isServer, out dynamic response)
        {
            var hash = _connectHash(salt);
            var start = DateTime.Now;
            var success = stream.Verify(++_outboundRequestId, hash, out var responseStr, isServer, out response);
            _log(RequestLabel + stream.PriorRequest);
            var dateDiff = DateTime.Now - start;
            _log(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        
        private void ServerLoop(object state)
        {
            var index = (int)state;
            while (_isConnected)
            {
                if (!_inbound[index].NamedPipeStream.IsConnected) return;
                Process(_inbound[index].NamedPipeStream, index);
            }
        }

        private void Process(System.IO.Pipes.PipeStream pipeStream, int index)
        {
            // Parse request
            var streamString = new StreamString(pipeStream);
            var requestText = streamString.ReadString();
            if (requestText.EndsWith(Environment.NewLine))
            {
                requestText = requestText.Substring(0, requestText.Length - 2);
                AddServerText("Sanitized request: removed CR+LF");
            }
            // not sure why yet but getting empty/null requests and also 1 byte requests with "\uFEFF" char
            if (requestText.EndsWith("\uFEFF") && requestText.Length == 1)
            {
                AddServerText("Skipping bad request: its a BOM");
                return;
            }
            if (string.IsNullOrEmpty(requestText.Trim()))
            {
                AddServerText("Skipping bad request: null or empty");
                return;
            }
            var len = requestText.Length;
            if (len < 5)
            {
                var ch = requestText[len - 1];
                var chInt = Convert.ToInt32(ch);
                var charHexOutput = $"{chInt:X}";
                AddServerText($"Skipping bad request: not null and < 5 bytes (val={requestText},len={len},lastch={charHexOutput})");
                return;
            }
            ProcessRequest(requestText, index);
        }

        private void ProcessRequest(string requestText, int index)
        {
            AddServerText($"Request (p{index}): {requestText}");

            var start = DateTime.Now;
            Request request = null;
            try
            {
                request = JsonConvert.DeserializeObject<Request>(requestText);
            }
            catch (Exception e)
            {
                AddServerText("Exception: " + e);
            }

            if (request == null)
                return;

            request.Index = index;

            if (!string.IsNullOrEmpty(request.Method))
            {
                switch (request.Method)
                {
                    case "menu_selected":
                        MenuSelectedCommand(request, start);
                        break;
                    case "result_callback":
                        ResultCallbackCommand(request, start);
                        return;
                    case "note_tab_value":
                        NoteTabValueCommand(request, start);
                        break;
                    case "note_hands":
                        NoteHandsCommand(request, start);
                        break;
                    case "hands":
                        HandsCommand(request, start);
                        break;
                    case "hands_selected":
                        HandsSelectedCommand(request, start);
                        break;
                    case "tables":
                        TablesCommand(request, start);
                        break;
                    case "settings_changed":
                        SettingsChangedCommand(request, start);
                        return;
                    case "stats_changed":
                        StatsChangedCommand(request, start);
                        return;
                    case "replay_hand":
                        ReplayHandCommand(request, start);
                        return;
                    case "noop":
                        NoopCommand(request, start);
                        return;
                    case "has_unsaved_changes":
                        if (!DisableUnsavedChangesSupport)
                            HasUnsavedChangesCommand(request, start);
                        else
                            MethodNotFoundError(start, request);
                        break;
                    case "import_started":
                        ImportStartedCommand(request, start);
                        break;
                    case "import_stopped":
                        ImportStoppedCommand(request, start);
                        break;
                    case "stat_value":
                        StatValueCommand(request, start);
                        break;
                    case "sleep_begin":
                        SleepBeginCommand(request, start);
                        break;
                    case "sleep_end":
                        SleepEndCommand(request, start);
                        break;
                    case "quit":
                        QuitCommand(request, start);
                        break;
                    default:
                        MethodNotFoundError(start, request);
                        break;
                }
            }
            else
            {
                const string msg = "null or empty method name: ";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
            }
        }

        public bool DisableUnsavedChangesSupport { get; set; }

        public bool ImportHudProfile(string fileName, string profileName, TableType tableType, ImportHudProfileCallback callback)
        {
            _importHudProfileCallback = callback;
            var start = DateTime.Now;
            var success = _outbound.ImportHudProfile(++_outboundRequestId, fileName, profileName, tableType, out var responseStr);

            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));

            return success;
        }

        public bool QueryHmql(string hmqlQueryText, QueryHmqlCallback doQueryHmqlCallback)
        {
            _queryHmqlCallback = doQueryHmqlCallback;
            var start = DateTime.Now;
            var success =_outbound.QueryHmql(++_outboundRequestId, hmqlQueryText, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        public bool QueryPtsql(string ptsqlQueryTableType, string[] stats, bool ptsqlQueryActivePlayer, bool ptsqlQueryHandQuery, QueryPtsqlCallback doQueryPtsqlCallback)
        {
            _queryPtsqlCallback = doQueryPtsqlCallback;
            PtsqlQueryStats = stats;

            var start = DateTime.Now;
            var success =  _outbound.QueryPtsql(++_outboundRequestId, ptsqlQueryTableType, stats, ptsqlQueryActivePlayer, ptsqlQueryHandQuery, out var responseStr);
            var dateDiff = DateTime.Now - start;
            AddClientText(RequestLabel + _outbound.PriorRequest);
            AddClientText(ClientResponseLine(dateDiff, responseStr));
            return success;
        }

        private void ImportHudProfileCallback(Request request, int callerId, DateTime start)
        {
            // todo: callback errors
            _importHudProfileCallback(callerId, false, 0, "", IntPtr.Zero);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        
        private void RegisterStatsCallback(Request request, int callerId, DateTime start)
        {
            _registerStatsCallback(callerId, false, 0, "", IntPtr.Zero);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void RemoveStatsCallback(Request request, int callerId, DateTime start)
        {
            _removeStatsCallback(callerId, false, 0, "", IntPtr.Zero);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        
        private void MenuSelectedCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("menu_item", out var menuItem);
            if (!(menuItem is string))
            {
                const string msg = "menu_selected request missing menu item.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            if (_profile.MenuSelectedCallback != null)
                _profile.MenuSelectedCallback(menuItem.ToString());
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void NoteTabValueCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("tab_name", out var tabName);
            if (!(tabName is string))
            {
                const string msg = "invalid or missing tab_name parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            request.Params.TryGetValue("player_name", out var playerName);
            if (!(playerName is string))
            {
                const string msg = "invalid or missing missing player_name parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            request.Params.TryGetValue("site_id", out var ptSiteId);
            if (ptSiteId == null || !int.TryParse(ptSiteId.ToString(), out var ptSiteIdInt))
            {
                const string msg = "invalid or missing site_id parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            request.Params.TryGetValue("last_hand_no", out var lastHandNo);
            if (lastHandNo == null)
            {
                const string msg = "invalid or missing site_id parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            // todo: is this correct?  if so, how big should it be?
            var sb = new StringBuilder(10000);
            if (_profile.NoteTabValueCallback != null)
                _profile.NoteTabValueCallback(tabName.ToString(), playerName.ToString(), ptSiteIdInt, lastHandNo.ToString(), sb, sb.Length);

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, JsonConvert.DeserializeObject(sb.ToString()));
        }

        private string HandsCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("hand", out var hand);
            if (!(hand is string))
            {
                const string msg = "invalid or missing hand parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return "";
            }

            var decoded = Base64Decode(hand.ToString());
            if (_profile.HandCallback != null)
                _profile.HandCallback(decoded);

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
            return decoded;
        }

        private void HandsSelectedCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("hands", out var hands);
            var array = hands as JArray;
            if (array == null)
            {
                const string msg = "invalid or missing hands parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            var menuItem = "";
            if (request.Params.TryGetValue("menu_item", out var menuItemObj))
            {
                if (!(menuItemObj is string))
                {
                    const string msg = "invalid menu_item parameter.";
                    SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                    return;
                }

                menuItem = (string) menuItemObj;
            }
            
            var handsList = new List<string>();
            foreach (var item in array)
            {
                var xmlHandText = Base64Decode(item.ToString());
                handsList.Add(xmlHandText);
                
            }
            if (_profile.HandsSelectedCallback != null)
                _profile.HandsSelectedCallback(handsList.ToArray(), menuItem);

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void NoteHandsCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("note_id", out var noteId);
            if (!(noteId is string))
            {
                const string msg = "invalid or missing noteId parameter.";
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg);
                return;
            }

            var handIds = Array.Empty<HandIdentifier>();
            if (_profile.NoteHandsCallback != null)
                _profile.NoteHandsCallback(noteId.ToString(), out handIds);
            
            var result = new StringBuilder();
            result.Append("[");
            var first = true;
            foreach (var hand in handIds)
            {
                var siteId = hand.SiteId;
                var handNo = hand.HandNo;
                if (!first)
                    result.Append(",");
                result.Append("{\"site_id\": " + siteId + ", \"hand_no\": \"" + handNo + "\"}");
                first = false;
            }
            result.Append("]");
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, JsonConvert.DeserializeObject(result.ToString()));
        }


        private void ImportStartedCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("import_type", out var importType);
            if (importType == null)
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "missing or invalid import_type");
                return;
            }
            if (_profile.ImportStartedCallback(importType.ToString()))
                SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, "OK");
            else
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "callback returned false");
        }

        private void ImportStoppedCommand(Request request, DateTime start)
        {
            _profile.ImportStoppedCallback();
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, "OK");
        }

        private void TablesCommand(Request request, DateTime start)
        {
            var tableList = new List<Table>();
            request.Params.TryGetValue("tables", out var tables);
            var result = new StringBuilder();
            result.Append("Tables received: ");
            if (tables is JArray array)
            {
                foreach (var table in array)
                {
                    var siteId = table.Value<int>("site_id");
                    var tableName = table.Value<string>("table");
                    var isTourney = table.Value<bool>("is_tourney");
                    var hudShowing = table.Value<bool>("hud_showing");
                    var hudProfile = table.Value<string>("profile_name");
                    result.Append(Environment.NewLine + "Table: " + tableName + "; Site Id: " + siteId + "; Is Tourney: " + isTourney + "; Hud Showing: " + hudShowing + "; Hud Profile: " + hudProfile);

                    tableList.Add
                    (
                        new Table
                        {
                            SiteId = siteId,
                            TableName = tableName,
                            IsTourney = isTourney,
                            HudShowing = hudShowing,
                            ProfileName = hudProfile
                        }
                    );
                }
                _profile.TablesCallback(tableList.ToArray());
            }

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void StatsChangedCommand(Request request, DateTime start)
        {
            _profile.StatsChangedCallback();
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void ReplayHandCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("hand", out var hand);
            request.Params.TryGetValue("hwnd", out var hwnd);
            request.Params.TryGetValue("center_points", out var centerPointsObj);
            if (hand != null && hwnd != null && centerPointsObj != null)
            {
                var centerPoints = new List<Point>();
                if (centerPointsObj is JArray centerPointsArray)
                {
                    foreach (var item in centerPointsArray)
                    {
                        var coords = item.ToString().Split(',');
                        if (coords.Length == 2)
                        {
                            var point = new Point
                            {
                                X = Convert.ToDouble(coords[0]),
                                Y = Convert.ToDouble(coords[1])
                            };
                            centerPoints.Add(point);
                        }
                    }
                }
                var decodedHand = Base64Decode(hand.ToString());
                _profile.ReplayHandCallback(hand.ToString(), Convert.ToInt32(hwnd), centerPoints.ToArray());
            }

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }


        private void NoopCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("wait", out var wait);
            request.Params.TryGetValue("should_fail", out var shouldFail);
            var success = false;
            if (_profile.NoopCallback != null)
                success = _profile.NoopCallback(Convert.ToInt32(wait), Convert.ToBoolean(shouldFail));

            if (success)
                SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
            else
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "fail");
        }

        private void SettingsChangedCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("setting", out var setting);
            request.Params.TryGetValue("new_value", out var newValue);
            if (setting != null && newValue != null)
                _profile.SettingsChangedCallback(setting.ToString(), newValue.ToString());

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void SleepBeginCommand(Request request, DateTime start)
        {
            _profile.SleepBeginCallback();
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, "OK");
        }

        private void SleepEndCommand(Request request, DateTime start)
        {
            _profile.SleepEndCallback();
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, "OK");
        }


        private void QuitCommand(Request request, DateTime start)
        {
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
            _quit();
        }

        private void StatValueCommand(Request request, DateTime start)
        {
            if (!request.Params.TryGetValue("player", out var playerNameObj) || !(playerNameObj is string))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "can't find player");
                return;
            }
            if (!request.Params.TryGetValue("site_id", out var siteIdObj))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "missing site_id property");
                return;
            }
            if (!request.Params.TryGetValue("table_type", out var tableTypeObj))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "missing table_type property");
                return;
            }

            if (!int.TryParse(siteIdObj.ToString(), out var siteId))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid site_id property");
                return;
            }
            
            // todo: stat_value -> single stat queries (i.e. v1.0 or 1.1 without the call to register for multiple stats)
            /*if (request.Params.TryGetValue("stat", out var statObj))
            {
                var statValue = _profile.StatValueCallback(statObj.ToString(), playerNameObj.ToString());
                SendSuccessResponse(_inbound[request.Index].PipeStream, request, start, statValue);
            }
            else 
            */
            if (!request.Params.TryGetValue("stats", out var statsObj))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "missing stat or stats property");
            }

            var statsList = statsObj as JArray;
            if (statsList == null)
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid stats parameter (expected an array)");
                return;
            }

            var statValues = new List<string>();
            var playerName = playerNameObj.ToString();
            var tableType = tableTypeObj.ToString() == "cash" ? 1 : 2;
            var filters = ""; // todo: add filters
            var success = true;
            foreach (var item in statsList)
            {
                var res = _profile.StatValueCallback(item.ToString(), tableType, siteId, playerName, filters, out var value);
                statValues.Add(value);
                if (!res)
                    success = false;
            }

            object statValueResult = statValues.ToArray();
            if (BreakStatValues)
                statValueResult = "-";
            if (success)
                SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, statValueResult);
            else
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "stat_value callback error");
        }

        public bool BreakStatValues { get; set; }

        private void HasUnsavedChangesCommand(Request request, DateTime start)
        {
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start, _profile.HasUnsavedChangesCallback());
        }

        private void ResultCallbackCommand(Request request, DateTime start)
        {
            request.Params.TryGetValue("error", out var error);
            request.Params.TryGetValue("result", out var result);
            request.Params.TryGetValue("caller_id", out var callerIdObj);
            request.Params.TryGetValue("caller_method", out var callerMethod);
            if (callerIdObj == null || !int.TryParse(callerIdObj.ToString(), out var callerId))
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid callback - missing caller_id");
            else if (callerMethod == null)
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid callback - missing caller_method");
            else if (error == null && result == null)
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid callback - missing error or result");
            else if (error != null)
                SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start); // its valid to get an error here, just acknowledge it
            else
            {
                if (result is JToken callbackResult)
                {
                    switch (callerMethod)
                    {
                        case "select_stats":
                            SelectStatsCallback(request, callerId, callbackResult, start);
                            break;
                        case "select_filters":
                            SelectFiltersCallback(request, callerId, callbackResult, start);
                            break;
                        case "get_hands":
                            GetHandsCallback(request, callerId, callbackResult, start);
                            break;
                        case "get_hands_to_file":
                            GetHandsToFileCallback(request, callerId, callbackResult, start);
                            break;
                        case "get_hands_to_shared_memory":
                            GetHandsToSharedMemoryCallback(request, callerId, callbackResult, start);
                            break;
                        case "get_hand_tags":
                            GetHandTagsCallback(request, callerId, (JArray)callbackResult, start);
                            break;
                        case "query_players":
                            QueryPlayersCallback(request, callerId, (JArray)callbackResult, start);
                            break;
                        case "query_notes":
                            QueryNotesCallback(request, callerId, (JArray)callbackResult, start);
                            break;
                        case "query_hmql":
                            QueryHmqlCallback(request, callerId, (JArray)callbackResult, start);
                            break;
                        case "query_ptsql":
                            QueryPtsqlCallback(request, callerId, (JArray)callbackResult, start);
                            break;
                        case "query_stats":
                            QueryStatsCallback(request, callerId, (JArray)callbackResult, start);
                            break;
                        case "register_positional_stats":
                            RegisterPositionalStatsCallback(request, (JArray)callbackResult, start);
                            break;
                        default:
                            SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid callback - unrecognized caller_method");
                            break;
                    }
                }
                else if (result is long && callerMethod.Equals("get_hands_to_file"))
                    GetHandsToFileCallback(request, callerId, (long) result, start);
                else if (result is string)
                {
                    switch (callerMethod)
                    {
                        case "import_hud_profile":
                            ImportHudProfileCallback(request, callerId, start);
                            break;
                        case "register_stats":
                            RegisterStatsCallback(request, callerId, start);
                            break;
                        case "remove_stats":
                            RemoveStatsCallback(request, callerId, start);
                            break;
                    }
                }
            }
        }

        private void SelectStatsCallback(Request request, int callerId, JToken result, DateTime start)
        {
            var cancelledStr = result.Value<string>("cancelled");
            var cancelled = false;
            if (cancelledStr != null && !bool.TryParse(cancelledStr, out cancelled))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid cancelled parameter", ErrorCode.InvalidParams);
                return;
            }

            var selectedStats = result.Value<JArray>("selected_stats");
            if (selectedStats == null && !cancelled)
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "missing selected_stats parameter", ErrorCode.InvalidParams);
                return;
            }

            if (_selectStatsCallback != null)
            {
                var stats = selectedStats != null ? Enumerable.ToArray<string>(selectedStats.Select(e => e.ToString())) : Array.Empty<string>();
                _selectStatsCallback(callerId, Convert.ToBoolean(cancelled), stats, IntPtr.Zero);
            }

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void SelectFiltersCallback(Request request, int callerId, JToken result, DateTime start)
        {
            var cancelledStr = result.Value<string>("cancelled");
            var cancelled = false;
            if (cancelledStr != null && !bool.TryParse(cancelledStr, out cancelled))
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "invalid cancelled parameter", ErrorCode.InvalidParams);
                return;
            }

            var filtersObj = result.Value<JToken>("filters");
            if (filtersObj == null && !cancelled)
            {
                SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, "missing filters parameter", ErrorCode.InvalidParams);
                return;
            }


            if (_selectFiltersCallback != null)
            {
                var filters = JsonConvert.SerializeObject(filtersObj);
                _selectFiltersCallback(callerId, cancelled, filters, IntPtr.Zero);
            }
            
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void GetHandsCallback(Request request, int callerId, JToken result, DateTime start)
        {
            if (result is JArray handsArray)
            {
                var hands = Enumerable.ToArray<string>(handsArray.Select(ha => ha.ToString()));
                _getHandsCallback(callerId, hands, IntPtr.Zero);
            }

            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }

        private void GetHandsToFileCallback(Request request, int callerId, JToken result, DateTime start)
        {
            var contents = File.ReadAllBytes(_getHandsToFileFilename);
            var text = Encoding.UTF8.GetString(contents);
            var hands = text.Split(new[] { SplitHandsDelimeter }, StringSplitOptions.None);
            _getHandsToFileCallback(callerId, hands, IntPtr.Zero);
        }

        private void GetHandsToSharedMemoryCallback(Request request, int callerId, JToken result, DateTime start)
        {
            var handsWritten = Convert.ToInt32(((JValue)result["hands_written"]).Value);
            var bytesWritten = Convert.ToInt32(((JValue)result["bytes_written"]).Value);
            var binaryReader = new BinaryReader(_mmvs.MemoryMappedViewStream);
            var contents = binaryReader.ReadBytes(bytesWritten);
            _mmvs.MemoryMappedViewStream.Dispose();
            _mmvs.MemoryMappedFile.Dispose();
            var text = Encoding.UTF8.GetString(contents);
            var handsArray = text.Split(new[] { SplitHandsDelimeter }, StringSplitOptions.None);
            
            var hands = handsArray.Select(ha => ha.ToString()).ToArray();
            _getHandsToSharedMemoryCallback(callerId, hands, IntPtr.Zero);
            SendSuccessResponse(_inbound[request.Index].NamedPipeStream, request, start);
        }
        
        // Miscellaneous

        public static string Base64Encode(string plainText)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        private static string Base64Decode(string encodedText)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedText));
        }

        private string SerializeResponse(Response response)
        {
            var responseText = JsonConvert.SerializeObject(response);

            // for testing HMT-5403 by removing the trailing "}" char to break the JSON (tracker should treat broken JSON for responses the same as an error)
            if (SendingBrokenResponses)
                responseText = responseText.Substring(0, responseText.Length - 1);

            return responseText;
        }

        private string ServerResponseLine(TimeSpan span, string response)
        {
            _totalServerResponses++;
            _totalServerDuration += span;
            ServerStatus = @"App's average response time: " + _totalServerDuration.TotalMilliseconds / _totalServerResponses + @"ms.";
            return $"Response({span.TotalMilliseconds}ms): {response}";
        }

        private string ClientResponseLine(TimeSpan span, string response)
        {
            _totalClientResponses++;
            _totalClientDuration += span;
            ClientStatus = @" Tracker's avg. response time: " + _totalClientDuration.TotalMilliseconds / _totalClientResponses + @"ms";
            return $"Response({span.TotalMilliseconds}ms): {response}";
        }

        public string ClientStatus { get; set; }


        internal void SendErrorResponse(System.IO.Pipes.PipeStream pipeStream, Request requestData, DateTime start, string message, ErrorCode code = ErrorCode.GeneralFailure)
        {
            var response = new Response
            {
                Id = requestData.Id,
                Error = new Error { ErrorCode = code, Message = message }
            };
            var responseText = SerializeResponse(response);
            var pipeWriter = new StreamWriter(pipeStream);
            pipeWriter.Write(responseText);
            pipeWriter.Flush();
            var dateDiff = DateTime.Now - start;
            AddServerText(ServerResponseLine(dateDiff, responseText));
        }

        internal void SendSuccessResponse(System.IO.Pipes.PipeStream pipeStream, Request requestData, DateTime start, object result = null)
        {
            var response = new Response
            {
                Id = requestData.Id,
                Result = result ?? "OK"  // send "OK" instead of null, or "", or omitting result entirely
            };

            var responseText = SerializeResponse(response);
            // make sure to turn off emitting of UTF identifier
            var encoding = new UTF8Encoding(false);
            var pipeWriter = new StreamWriter(pipeStream, encoding, responseText.Length + 1);
            pipeWriter.Write(responseText);
            pipeWriter.Flush();
            var dateDiff = DateTime.Now - start;
            AddServerText(ServerResponseLine(dateDiff, responseText));
        }

        private void MethodNotFoundError(DateTime start, Request request)
        {
            var msg = "invalid method name: " + request.Method;
            AddServerText(msg);
            SendErrorResponse(_inbound[request.Index].NamedPipeStream, request, start, msg, ErrorCode.MethodNotFound);
        }

        private void AddServerText(string text)
        {
            var msg = "Server: " + text;
            _log(msg);
        }

        private void AddClientText(string text)
        {
            var msg = "Client: " + text;
            _log(msg);
        }
    }
}