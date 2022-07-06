using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using MvsAppApi.Core.Enums;
using MvsAppApi.Core.Structs;
using Newtonsoft.Json;

namespace MvsAppApi.JsonAdapter
{
    public class PipeStream
    {
        private readonly string _appName;
        private string _apiVersion;

        protected PipeStream(string appName, string pipeName, string apiVersion)
        {
            _appName = appName;
            _apiVersion = apiVersion;

            Tracker = pipeName.StartsWith("pt4") ? Tracker.PT4 : Tracker.HM3;

            NamedPipeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            NamedPipeStream.Connect();
        }

        public Tracker Tracker { get; }

        public bool BreakRequests { private get; set; }

        public NamedPipeClientStream NamedPipeStream { get; private set; }

        public string PriorRequest { get; private set; }

        public string Invoke(string command)
        {
            if (BreakRequests)
            {
                dynamic request = JsonConvert.DeserializeObject(command);
                var json = JsonConvert.SerializeObject(request);
                Dictionary<string, object> dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (dictionary != null && dictionary.ContainsKey("params"))
                {
                    dictionary.Remove("params");
                    command = JsonConvert.SerializeObject(dictionary);
                }
            }

            PriorRequest = command;
            var stream = new StreamString(NamedPipeStream);
            stream.WriteString(command);
            var text = stream.ReadString();
            return text;
        }

        internal static bool IsSuccess(dynamic response)
        {
            return response != null && response["error"] == null;
        }

        public bool Register(int id, string appVersion, out string responseStr, out string salt, out string trackerVersion, out string apiVersion)
        {
            responseStr = Invoke(RegisterCommand(id,appVersion, _apiVersion));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            var result = response != null ? response["result"] : null;
            salt = result != null ? result["salt"].ToString() : "";
            trackerVersion = result != null ? result["tracker_version"].ToString() : "";
            _apiVersion = apiVersion = result != null ? result["api_version"].ToString() : "";
            return IsSuccess(response);
        }

        public bool Verify(int requestId, string hash, out string responseStr, bool isServer, out dynamic response)
        {
            responseStr = Invoke(VerifyCommand(requestId, hash, isServer));
            response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RegisterMenu(int requestId, List<string> menuItems, out string responseStr)
        {
            var menuItemsString = string.Join(",", menuItems.Select(mi => "\"" + mi + "\""));
            responseStr = Invoke(RegisterMenuCommand(requestId, menuItemsString));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RegisterNoteTab(int requestId, string tabName, string tabIcon, out string responseStr)
        {
            responseStr = Invoke(RegisterNoteTabCommand(requestId, tabName, tabIcon));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RegisterHandsMenu(int requestId, List<string> menuItems, string menuIcon, HandFormat format, out string responseStr)
        {
            var menuItemsString = menuItems != null && menuItems.Count > 0
                ? string.Join(",", menuItems.Select(mi => "\"" + mi + "\""))
                : "";
            responseStr = Invoke(RegisterHandsMenuCommand(requestId, menuItemsString, menuIcon, format == HandFormat.Xml ? "xml" : "json"));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }


        public bool Noop(int requestId, int wait, bool shouldFail, string extraBytes, out int noopSize, out string responseStr)
        {
            var cmd = NoopCommand(requestId, wait, shouldFail, extraBytes);
            noopSize = cmd.Length;
            responseStr = Invoke(cmd);
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RequestHands(int requestId, out string responseStr)
        {
            responseStr = Invoke(RequestHandsCommand(requestId));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RequestTables(int requestId, out string responseStr)
        {
            responseStr = Invoke(RequestTablesCommand(requestId));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool GetStats(int requestId, string tableTypeName, bool fullDetails, out string responseStr)
        {
            responseStr = Invoke(GetStatsCommand(requestId, tableTypeName, fullDetails));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RegisterStats(int requestId, List<Stat> stats, out string responseStr)
        {
            responseStr = Invoke(RegisterStatsCommand(requestId,stats));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RemoveStats(int requestId, List<Stat> stats, out string responseStr)
        {
            responseStr = Invoke(RemoveStatsCommand(requestId, stats));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool RegisterPositionalStats(int requestId, List<string> stats, string tableType, string hasPosition, string positionType, out string responseStr)
        {
            responseStr = Invoke(RegisterPositionalStatsCommand(requestId, stats, tableType, hasPosition, positionType));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool SelectStats(int requestId, string tableType, string[] includedStats, string[] defaultStats, out string responseStr)
        {
            responseStr = Invoke(SelectStatsCommand(requestId, tableType, includedStats, defaultStats));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool SelectFilters(int requestId, string tableType, string filter, out string responseStr)
        {
            responseStr = Invoke(SelectFiltersCommand(requestId, tableType, filter));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool GetHands(int requestId, IEnumerable<HandIdentifier> handIds, bool includeNative, out string responseStr)
        {
            responseStr = Invoke(GetHandsCommand(requestId, handIds, includeNative));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool GetHandsToSharedMemory(int requestId, IEnumerable<HandIdentifier> handIds, string memoryName, long memorySize, bool includeNative, out string responseStr)
        {
            responseStr = Invoke(GetHandsToSharedMemoryCommand(requestId, handIds, memoryName, memorySize, includeNative));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool GetHandsToFile(int requestId, IEnumerable<HandIdentifier> handIds, string fileName, bool includeNative, out string responseStr)
        {
            responseStr = Invoke(GetHandsToFileCommand(requestId, handIds, fileName, includeNative));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool GetHandTags(int requestId, int siteId, string gameNumber, out string responseStr)
        {
            responseStr = Invoke(GetHandTagsCommand(requestId, siteId, gameNumber));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool ImportHand(int requestId, int siteId, string encodedHand, out string responseStr)
        {
            responseStr = Invoke(ImportHandCommand(requestId, siteId, encodedHand));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool ChangeHudProfile(int requestId, int siteId, string tableName, string profileName, out string responseStr)
        {
            responseStr = Invoke(ChangeHudProfileCommand(requestId, siteId, tableName, profileName));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool ImportHudProfile(int requestId, string fileName, string profileName, TableType tableType, out string responseStr)
        {
            var tableTypeStr = tableType == TableType.Cash ? "cash" : "tournament";
            responseStr = Invoke(ImportHudProfileCommand(requestId, fileName, profileName, tableTypeStr));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool ReplayHands(int requestId, IEnumerable<HandSelector> handSelectors, out string responseStr)
        {
            responseStr = Invoke(ReplayHandsCommand(requestId, handSelectors));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }
        
        public bool GetSetting(int requestId, string settingName, out object value, out string responseStr)
        {
            responseStr = Invoke(GetSettingCommand(requestId, settingName));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            value = response != null ? response["result"] : null;
            return IsSuccess(response);
        }
        
        public bool BusyStateBegin(int requestId, out string responseStr)
        {
            responseStr = Invoke(BusyStateBeginCommand(requestId));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool BusyStateEnd(int requestId, out string responseStr)
        {
            responseStr = Invoke(BusyStateEndCommand(requestId));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool QueryPlayers(int requestId, int? siteId, string playerName, bool? anonymous, string gameType, int? minCashHands, int? maxCashHands, int? minTournamentHands, int? maxTournamentHands, List<string> orderByFields, string order, int? limit, int? offset, out string responseStr)
        {
            responseStr = Invoke(QueryPlayersCommand(requestId, siteId, playerName, anonymous, gameType, minCashHands, maxCashHands, minTournamentHands, maxTournamentHands, orderByFields, order, limit, offset));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool QueryNotes(int requestId, int siteId, IEnumerable<string> playerNames, out string responseStr)
        {
            responseStr = Invoke(QueryNotesCommand(requestId, siteId, playerNames));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool HudNotes(int requestId, int siteId, string player, int windowId, int posX, int posY, out string responseStr)
        {
            responseStr = Invoke(HudNotesCommand(requestId, siteId, player, windowId, posX, posY));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool QueryStats(int requestId, TableType tableType, int siteId, string[] playersList, string [] statsList, string filters, out string responseStr)
        {
            var tableTypeStr = tableType == TableType.Cash ? "cash" : "tournament";
            responseStr = Invoke(QueryStatsCommand(requestId, siteId, tableTypeStr, playersList, statsList, filters));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool QueryHmql(int requestId, string queryText, out string responseStr)
        {
            responseStr = Invoke(QueryHmqlCommand(requestId, queryText));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool QueryPtsql(int requestId, string tableType, IEnumerable stats, bool activePlayer, bool handQuery, out string responseStr)
        {
            responseStr = Invoke(QueryPtsqlCommand(requestId, tableType, stats, activePlayer, handQuery));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }

        public bool CancelCallback(int requestId, int cancelRequestId, out string responseStr)
        {
            responseStr = Invoke(CancelCallbackCommand(requestId, cancelRequestId));
            dynamic response = JsonConvert.DeserializeObject(responseStr);
            return IsSuccess(response);
        }
        
        protected string RegisterCommand(int requestId, string version, string requestedApiVersion)
        {
            return "{ \"id\": " + requestId + ", \"method\": \"register\", \"params\": " + "{ \"name\": \"" + _appName + "\", \"version\":\"" + version + "\", \"requested_api_version\":\"" + requestedApiVersion + "\" } }";
        }

        protected string VerifyCommand(int requestId, string hash, bool isServer = false)
        {

            return "{\"id\": " + requestId + ", \"method\": \"verify\", \"params\": { \"hash\": \"" + hash + "\", \"mode\":\"" + (isServer ? "server" : "client") + "\"" + "} }";
        }

        protected string RegisterMenuCommand(int requestId, string menuItems)
        {
            return "{\"id\": " + requestId + ", \"method\": \"register_menu\", \"params\": { \"menu_items\": [" + menuItems + "] } }";
        }

        public string RegisterNoteTabCommand(int requestId, string tabName, string tabIcon)
        {
            return "{\"id\": " + requestId + ", \"method\": \"register_note_tab\", \"params\": { \"tab_name\": \"" + tabName + "\", \"tab_icon\": \"" + tabIcon + "\" } }";
        }

        public string RegisterHandsMenuCommand(int requestId, string menuItems, string menuIcon, string format)
        {
            return "{\"id\": " + requestId + ", \"method\": \"register_hands_menu\", \"params\": { \"menu_items\": [" + menuItems + "], \"menu_icon\": \"" + menuIcon + "\", \"format\": \"" + format + "\" } }";
        }

        public string NoopCommand(int requestId, int wait, bool shouldFail, string extraBytes = null)
        {
            var cmd = "{\"id\": " + requestId + ", \"method\": \"noop\", \"params\": { \"wait\": \"" + wait + "\", \"should_fail\": \"" + shouldFail + "\"";
            if (!string.IsNullOrEmpty(extraBytes)) 
                cmd += ", \"extra_bytes\": \"" + extraBytes + "\"";
            cmd += " } }";
            return cmd;
        }

        public string RequestHandsCommand(int requestId)
        {
            return "{\"id\": " + requestId + ", \"method\": \"request_hands\" }";
        }

        public string RequestTablesCommand(int requestId)
        {
            return "{\"id\": " + requestId + ", \"method\": \"request_tables\" }";
        }

        protected string GetStatsCommand(int requestId, string tableType, bool fullDetails)
        {
            return "{\"id\": " + requestId + ",\"method\":\"get_stats\",\"params\":{\"" + "table_type" + "\":\"" + tableType + "\",\"full_details\":" + JsonConvert.ToString(fullDetails) + "}}";
        }

        protected string RegisterStatsCommand(int requestId, List<Stat> stats)
        {
            var serializedStats = JsonConvert.SerializeObject(stats);
            return "{\"id\": " + requestId + ",\"method\":\"register_stats\",\"params\":{\"stats\":" + serializedStats + "}}";
        }

        protected string RemoveStatsCommand(int requestId, List<Stat> stats)
        {
            var serializedStats = JsonConvert.SerializeObject(stats);
            return "{\"id\": " + requestId + ",\"method\":\"remove_stats\",\"params\":{\"stats\":" + serializedStats + "}}";
        }

        protected string RegisterPositionalStatsCommand(int requestId, List<string> stats, string tableType, string hasPosition, string positionType)
        {
            var serializedStats = JsonConvert.SerializeObject(stats);
            return "{\"id\": " + requestId + ",\"method\":\"register_positional_stats\",\"params\":{\"stats\":" + serializedStats +
                   ", \"table_type\":" + JsonConvert.ToString(tableType) +
                   (!string.IsNullOrEmpty(hasPosition) ? ", \"has_position\":" + JsonConvert.ToString(hasPosition) : "") +
                   (!string.IsNullOrEmpty(positionType) ? ", \"position_type\":" + JsonConvert.ToString(positionType) : "") + "}}";
        }

        protected string SelectStatsCommand(int requestId, string tableType, string [] includedStats, string[] defaultStats)
        {
            var serializedIncludedStats = JsonConvert.SerializeObject(includedStats);
            var serializedDefaultStats = JsonConvert.SerializeObject(defaultStats);
            return "{\"id\": " + requestId + 
                   ",\"method\":\"select_stats\",\"params\":{\"included_stats\":" + serializedIncludedStats + 
                   ", \"default_stats\":" + serializedDefaultStats + 
                   ", \"table_type\":" + JsonConvert.ToString(tableType) + "}}";
        }

        protected string SelectFiltersCommand(int requestId, string tableType, string filters)
        {
            return "{\"id\": " + requestId + 
                   ",\"method\":\"select_filters\",\"params\":{\"filters\":" + filters +
                   ",\"table_type\":" + EscapeJsonStringProperty(tableType) +
                   "}}";
        }

        private string GetHandIdsFragment(IEnumerable<HandIdentifier> handIds)
        {
            var result = "[";
            var first = true;
            foreach (var item in handIds)
            {
                if (!first)
                    result += ",";
                first = false;
                result += "{ \"site_id\": " + item.SiteId + "," + "\"hand_no\": " + "\"" + item.HandNo + "\"" + "}";
            }
            result += "]";
            return result;
        }

        protected string GetHandsCommand(int requestId, IEnumerable<HandIdentifier> handIds, bool includeNative)
        {
            return "{\"id\": " + requestId + ",\"method\":\"get_hands\",\"params\":{\"hands\":" + GetHandIdsFragment(handIds) + 
                ",\"include_native\":" + (includeNative ? "true" : "false") + "}}";
        }

        protected string GetHandsToSharedMemoryCommand(int requestId, IEnumerable<HandIdentifier> handIds, string memoryName, long memorySize, bool includeNative)
        {
            return "{\"id\": " + requestId + ",\"method\":\"get_hands_to_shared_memory\",\"params\":{\"hands\":" + GetHandIdsFragment(handIds) +
                ",\"memory_name\": \"" + memoryName + "\"" +
                ",\"memory_size\":" + memorySize +
                ",\"include_native\":" + (includeNative ? "true" : "false") + "}}";
        }

        protected string GetHandsToFileCommand(int requestId, IEnumerable<HandIdentifier> handIds, string fileName, bool includeNative)
        {
            return "{\"id\": " + requestId + ",\"method\":\"get_hands_to_file\",\"params\":{\"hands\":" + GetHandIdsFragment(handIds) +
                ",\"file_name\": \"" + fileName.Replace(@"\",@"\\") + "\"" +
                ",\"include_native\":" + (includeNative ? "true" : "false") + "}}";
        }

        protected string GetHandTagsCommand(int requestId, int siteId, string handNo)
        {
            return "{\"id\": " + requestId + ",\"method\":\"get_hand_tags\",\"params\":{\"site_id\":" + siteId +
                   ",\"hand_no\": \"" + handNo + "\"" + "}}";
        }
        
        protected string ImportHandCommand(int requestId, int siteId, string encodedHand)
        {
            return "{\"id\": " + requestId + ",\"method\":\"import_hand\",\"params\":{\"hand_history\":\"" + encodedHand + "\", \"site_id\":" + siteId + "}}";
        }

        private string GetHandSelectorsFragment(IEnumerable<HandSelector> handSelectors)
        {
            var result = "[";
            var first = true;
            foreach (var item in handSelectors)
            {
                if (!first)
                    result += ",";
                first = false;
                result += "{ \"site_id\": " + item.SiteId + "," +
                            "\"hand_no\": " + item.HandNo + "," +
                            "\"street\": " + item.Street + "," +
                            "\"action\": " + item.Action +
                            "}";
            }
            result += "]";
            return result;
        }

        protected string ChangeHudProfileCommand(int requestId, int siteId, string table, string profileName)
        {
            return "{\"id\": " + requestId + ",\"method\":\"change_hud_profile\",\"params\":{\"table\":\"" + table + "\", \"site_id\":" + siteId + ", \"profile_name\":\"" + profileName + "\"}}";
        }

        private string EscapeJsonStringProperty(string property)
        {
            return JsonConvert.ToString(property);
        }

        protected string ImportHudProfileCommand(int requestId, string fileName, string profileName, string tableType)
        {
            return "{\"id\": " + requestId + 
                   ",\"method\":\"import_hud_profile\",\"params\":{\"table_type\":" + EscapeJsonStringProperty(tableType) + 
                   ", \"file_name\":" + EscapeJsonStringProperty(fileName) + 
                   ", \"profile_name\":" + EscapeJsonStringProperty(profileName) + "}}";
        }

        protected string ReplayHandsCommand(int requestId, IEnumerable<HandSelector> handSelectors, int index = 0, int street = 0, int action = 0)
        {
            return "{\"id\": " + requestId + ",\"method\":\"replay_hands\",\"params\":{\"hand_selectors\":" + GetHandSelectorsFragment(handSelectors) + "}}";
        }
        
        protected string GetSettingCommand(int requestId, string settingName)
        {
            return "{\"id\": " + requestId + ",\"method\":\"get_setting\",\"params\":{\"name\":" + "\"" + settingName + "\"" + "}}";
        }

        protected string BusyStateBeginCommand(int requestId)
        {
            return "{\"id\": " + requestId + ",\"method\":\"busy_state_begin\"}";
        }

        protected string BusyStateEndCommand(int requestId)
        {
            return "{\"id\": " + requestId + ",\"method\":\"busy_state_end\"}";
        }

        protected string QueryPlayersCommand(int requestId, int? siteId, string playerName, bool? anonymous, string gameType, int? minCashHands, int? maxCashHands, int? minTourneyHands, int? maxTourneyHands, List<string> orderByFields, string order, int? limit, int? offset)
        {
            var queryPlayersParams = new Dictionary<string, object>();
            if (siteId != null)
                queryPlayersParams.Add("site_id", siteId);
            if (!string.IsNullOrEmpty(playerName))
                queryPlayersParams.Add("name", playerName);
            if (anonymous != null)
                queryPlayersParams.Add("anonymous", anonymous);
            if (!string.IsNullOrEmpty(gameType))
                queryPlayersParams.Add("game_type", gameType);

            var cashRange = new Dictionary<string, object>();
            if (minCashHands != null)
                cashRange.Add("min", minCashHands);
            if (maxCashHands != null)
                cashRange.Add("max", maxCashHands);
            if (cashRange.Count > 0)
                queryPlayersParams.Add("cash_hands", cashRange);

            var tournamentRange = new Dictionary<string, object>();
            if (minTourneyHands != null)
                tournamentRange.Add("min", minTourneyHands);
            if (maxTourneyHands != null)
                tournamentRange.Add("max", maxTourneyHands);
            if (tournamentRange.Count > 0)
                queryPlayersParams.Add("tournament_hands", tournamentRange);

            if (limit != null)
                queryPlayersParams.Add("limit", limit);
            if (offset != null)
                queryPlayersParams.Add("offset", offset);

            var orderBy = new Dictionary<string, object> {{"order", order }, {"fields", orderByFields } };
            if (orderBy.Count > 0)
                queryPlayersParams.Add("order_by", orderBy);

            var qp = JsonConvert.SerializeObject(queryPlayersParams);

            return "{\"id\": " + requestId + ",\"method\":\"query_players\",\"params\":" + qp + "}";
        }
        // some other commands ...

        //const string command = "{\"id\":4,\"method\":\"message_box\",\"params\":{\"title\":\"HelloWorld\",\"message\":\"Hello World!\"}}";

        //const string command = "{\"id\":4,\"method\":\"get_tracker_hwnd\"}";

        private string QueryNotesCommand(int requestId, int siteId, IEnumerable<string> playerNames)
        {
            var serializedPlayers = JsonConvert.SerializeObject(playerNames);
            return "{\"id\": " + requestId + ",\"method\":\"query_notes\",\"params\":{\"site_id\":" + siteId +
                   ",\"players\": " + serializedPlayers + "}}";
        }

        private string HudNotesCommand(int requestId, int siteId, string player, int tableWindow, int posX, int posY)
        {
            var serializedPlayers = JsonConvert.SerializeObject(player);
            return "{\"id\": " + requestId + ",\"method\":\"hud_notes\",\"params\":{\"site_id\":" + siteId +
                   ",\"player\": " + serializedPlayers +
                   ",\"table_window\": " + tableWindow +
                   ",\"position\": " + $"\"{posX},{posY}\"" + "}}";
        }

        private string QueryStatsCommand(int requestId, int siteId, string tableType, string[] playersList, string [] statsList, string filters)
        {
            var serializedPlayersList = JsonConvert.SerializeObject(playersList);
            var serializedStatsList = JsonConvert.SerializeObject(statsList);
            var query = "{\"id\": " + requestId + ",\"method\":\"query_stats\",\"params\":" +
                        "{\"table_type\":\"" + tableType + "\"," +
                        "\"timeout\":15000," +
                        "\"players\":" + serializedPlayersList + "," +
                        "\"stats\":" + serializedStatsList + "," +
                        "\"site_id\":" + siteId;
            if (!string.IsNullOrEmpty(filters))
                query += "," + "\"filters\":" + filters;
            query += "}}";
            return query;
        }

        private string QueryHmqlCommand(int requestId, string queryText)
        {
            return "{\"id\": " + requestId + ",\"method\":\"query_hmql\",\"params\":{\"query\":" + "\"" + queryText + "\"" + "}}";
        }

        private string QueryPtsqlCommand(int requestId, string tableType, IEnumerable stats, bool activePlayer, bool handQuery)
        {
            return "{\"id\": " + requestId + 
                   ",\"method\":\"query_ptsql\",\"params\":{\"table_type\":" + JsonConvert.ToString(tableType) +
                   ",\"stats\":" + JsonConvert.SerializeObject(stats) +
                   ",\"active_player\":" + JsonConvert.ToString(activePlayer) +
                   ",\"hand_query\":" + JsonConvert.ToString(handQuery) +
                   "}}";
        }

        private string CancelCallbackCommand(int requestId, int cancelRequestId)
        {
            return "{\"id\": " + requestId + ",\"method\":\"cancel_callback\",\"params\":" + "{\"caller_id\":" + cancelRequestId + "}}"; 
        }


        ~PipeStream()
        {
            NamedPipeStream.Dispose();
            NamedPipeStream.Close();
            NamedPipeStream = null;
        }
        
        public static PipeStream Create(bool isHm3, string apiVersion, string appName)
        {
            return new PipeStream(appName, isHm3 ? "hm3_api" : "pt4_api", apiVersion);
        }
    }
}