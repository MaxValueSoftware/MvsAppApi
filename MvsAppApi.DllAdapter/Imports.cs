using System;
using System.Runtime.InteropServices;
using System.Text;
using MvsAppApi.Core;
using MvsAppApi.Core.Enums;
using MvsAppApi.Core.Structs;
using MvsAppApi.DllAdapter.Structs;

namespace MvsAppApi.DllAdapter
{
    // API error codes
    // =========================================================================================================================
    public enum ApiErrorCode
    {
        MvsApiResultSuccess = 0,
        MvsApiResultInternalError = -1,      // Something went wrong internally with the DLL or with the Tracker.
        MvsApiResultBadParameter = -2,       // An invalid value was passed into the function.
        MvsApiResultConnectionFailure = -3,  // The DLL was unable to connect to the Tracker's API
        MvsApiResultInsufficientBuffer = -4,  // The buffer passed in is not large enough to contain the data
        MvsApiResultCallbackFailed = -5,  // An application callback returned MVSAPI_FALSE
        MvsApiResultInvalidState = -6,  // A function is being called out of order
        MvsApiResultBadStructSize = -7  // A struct passed in has an unexpected structSize member variable value
    }


    public class Imports
    {
        // internal "wrapper" delegates (there's public versions of these with the same names in IAdapter.cs)

        internal delegate bool ConnectHashCallback(string salt, IntPtr buffer, uint bufferLen);
        internal delegate bool ConnectInfoCallback(string rootDir, string dataDir, string logDir, Restriction[] restrictions, uint restrictionsCount, bool isTrial, string expires, string trackerVersion, string apiVersion);
        internal delegate bool TablesCallback(Table[] tables, uint tablesCount);
        internal delegate bool NoteHandsCallback(string noteId, HandInternal[] noteHands, int handsMax, out int handsCount);
        internal delegate bool GetStatsCallback(StatInternal stat, string type, bool playerPct, bool hudSafe, bool groupBy, bool tableAverageable, int appId, IntPtr userData);
        internal unsafe delegate bool QueryStatsCallback(int callerId, bool errored, int errorCode, string errorMessage, int row, int rowCount, byte** values, byte** pctDetails, int valuesCount, IntPtr userData);
        internal unsafe delegate bool QueryHmqlCallback(int callerId, bool errored, int errorCode, string errorMessage, int row, int rowCount, byte** values, byte** types, int valuesCount, IntPtr userData);
        internal delegate bool QueryPtsqlCallback(int callerId, bool errored, int errorCode, string errorMessage, int row, int rowCount, string[] values, string[] pctDetails, int valuesCount, IntPtr userData);
        internal delegate bool QueryPlayersCallback(int callerId, bool errored, int errorCode, string errorMessage, string playerName, int siteId, bool anonymous, int cashHands, int tourneyHands, int current, int max, IntPtr userData);
        internal delegate bool QueryNotesCallback(int callerId, bool errored, int errorCode, string errorMessage, string player, string color, string notes, int current, int max, IntPtr userData);
        internal delegate bool StatValueCallback(int requestId, string stat, int tableType, int siteId, string player, string filters, int current, int max, StringBuilder buffer, int bufferLen);
        internal unsafe delegate bool GetHandTagsCallback(int callerId, bool errored, int errorCode, string errorMessage, byte **tags, int tagsCount, IntPtr userData);
        internal delegate bool ReplayHandCallback(string hand, int hwnd, Point[] points, int pointsCount);
        internal unsafe delegate bool GetHandsCallback(int callerId, bool errored, int errorCode, string errorMessage, byte** handHistories, int handHistoriesCount, IntPtr userData);
        internal delegate bool GetHandsToFileCallback(int callerId, bool errored, int errorCode, string errorMessage, int handsWritten, IntPtr userData);
        internal delegate bool GetHandsToSharedMemoryCallback(int callerId, bool errored, int errorCode, string errorMessage, int handsWritten, int bytesWritten, IntPtr userData);
        internal delegate bool HandsSelectedCallback(string menuItem, [In] string[] selectedHands, int selectedHandsCount);


        // API DLLs

        private const string X86FileName = "MvsApiApplicationInterface.dll";
        private const string X64FileName = "MvsApiApplicationInterface_x64.dll";

        // Functions

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_initialize")]
        private static extern ApiErrorCode MvsApiInitialize_x86(LogCallback logCallback, QuitCallback quitCallback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_initialize")]
        private static extern ApiErrorCode MvsApiInitialize_x64(LogCallback logCallback, QuitCallback quitCallback);
        internal static ApiErrorCode MvsApiInitialize(LogCallback logCallback, QuitCallback quitCallback)
        {
            return Environment.Is64BitProcess
                ? MvsApiInitialize_x64(logCallback, quitCallback)
                : MvsApiInitialize_x86(logCallback, quitCallback);
        }



        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_shutdown")]
        private static extern ApiErrorCode MvsApiShutdown_x86();

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_shutdown")]
        private static extern ApiErrorCode MvsApiShutdown_x64();
        internal static ApiErrorCode MvsApiShutdown()
        {
            return Environment.Is64BitProcess
                ? MvsApiShutdown_x64()
                : MvsApiShutdown_x86();
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_connect")]
        private static extern ApiErrorCode MvsApiConnect_x86(int tracker, int clientPipes, int serverPipes, string appName, string appVersion, ConnectHashCallback hashCallback, ConnectInfoCallback infoCallback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_connect")]
        private static extern ApiErrorCode MvsApiConnect_x64(int tracker, int clientPipes, int serverPipes, string appName, string appVersion, ConnectHashCallback hashCallback, ConnectInfoCallback infoCallback);
        internal static ApiErrorCode MvsApiConnect(int tracker, int clientPipes, int serverPipes, string appName, string appVersion, ConnectHashCallback hashCallback, ConnectInfoCallback infoCallback)
        {
            return Environment.Is64BitProcess
                ? MvsApiConnect_x64(tracker, clientPipes, serverPipes, appName, appVersion, hashCallback, infoCallback)
                : MvsApiConnect_x86(tracker, clientPipes, serverPipes, appName, appVersion, hashCallback, infoCallback);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_importStarted")]
        private static extern ApiErrorCode MvsApiRegisterCallbackImportStarted_x86(ImportStartedCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_importStarted")]
        private static extern ApiErrorCode MvsApiRegisterCallbackImportStarted_x64(ImportStartedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackImportStarted(ImportStartedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackImportStarted_x64(callback)
                : MvsApiRegisterCallbackImportStarted_x86(callback);
        }
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_importStopped")]
        private static extern ApiErrorCode MvsApiRegisterCallbackImportStopped_x86(ImportStoppedCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_importStopped")]
        private static extern ApiErrorCode MvsApiRegisterCallbackImportStopped_x64(ImportStoppedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackImportStopped(ImportStoppedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackImportStopped_x64(callback)
                : MvsApiRegisterCallbackImportStopped_x86(callback);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_hand")]
        private static extern ApiErrorCode MvsApiRegisterCallbackHand_x86(HandCallback callback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_hand")]
        private static extern ApiErrorCode MvsApiRegisterCallbackHand_x64(HandCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackHand(HandCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackHand_x64(callback)
                : MvsApiRegisterCallbackHand_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_handsSelected")]
        private static extern ApiErrorCode MvsApiRegisterCallbackHandsSelected_x86(HandsSelectedCallback callback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_handsSelected")]
        private static extern ApiErrorCode MvsApiRegisterCallbackHandsSelected_x64(HandsSelectedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackHandsSelected(HandsSelectedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackHandsSelected_x64(callback)
                : MvsApiRegisterCallbackHandsSelected_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_tables")]
        private static extern ApiErrorCode MvsApiRegisterCallbackTables_x86(TablesCallback callback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_tables")]
        private static extern ApiErrorCode MvsApiRegisterCallbackTables_x64(TablesCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackTables(TablesCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackTables_x64(callback)
                : MvsApiRegisterCallbackTables_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_notes")]
        private static extern ApiErrorCode MvsApiRegisterCallbackNotes_x86(NotesCallback callback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_notes")]
        private static extern ApiErrorCode MvsApiRegisterCallbackNotes_x64(NotesCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackNotes(NotesCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackNotes_x64(callback)
                : MvsApiRegisterCallbackNotes_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_tags")]
        private static extern ApiErrorCode MvsApiRegisterCallbackTags_x86(TagsCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_tags")]
        private static extern ApiErrorCode MvsApiRegisterCallbackTags_x64(TagsCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackTags(TagsCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackTags_x64(callback)
                : MvsApiRegisterCallbackTags_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_statValue")]
        private static extern ApiErrorCode MvsApiRegisterCallbackStatValue_x86(StatValueCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_statValue")]
        private static extern ApiErrorCode MvsApiRegisterCallbackStatValue_x64(StatValueCallback callback);

        internal static ApiErrorCode MvsApiRegisterCallbackStatValue(StatValueCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackStatValue_x64(callback)
                : MvsApiRegisterCallbackStatValue_x86(callback);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_statPreview")]
        private static extern ApiErrorCode MvsApiRegisterCallbackStatPreview_x86(StatPreviewCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_statPreview")]
        private static extern ApiErrorCode MvsApiRegisterCallbackStatPreview_x64(StatPreviewCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackStatPreview(StatPreviewCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackStatPreview_x64(callback)
                : MvsApiRegisterCallbackStatPreview_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_callback")]
        private static extern ApiErrorCode MvsApiRegisterCallbackCallback_x86(CallbackCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_callback")]
        private static extern ApiErrorCode MvsApiRegisterCallbackCallback_x64(CallbackCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackCallback(CallbackCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackCallback_x64(callback)
                : MvsApiRegisterCallbackCallback_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_menuSelected")]
        private static extern ApiErrorCode MvsApiRegisterCallbackMenuSelected_x86(MenuSelectedCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_menuSelected")]
        private static extern ApiErrorCode MvsApiRegisterCallbackMenuSelected_x64(MenuSelectedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackMenuSelected(MenuSelectedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackMenuSelected_x64(callback)
                : MvsApiRegisterCallbackMenuSelected_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_noteTabValue")]
        private static extern ApiErrorCode MvsApiRegisterCallbackNoteTabValue_x86(NoteTabValueCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_noteTabValue")]
        private static extern ApiErrorCode MvsApiRegisterCallbackNoteTabValue_x64(NoteTabValueCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackNoteTabValue(NoteTabValueCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackNoteTabValue_x64(callback)
                : MvsApiRegisterCallbackNoteTabValue_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_noteHands")]
        private static extern ApiErrorCode MvsApiRegisterCallbackNoteHands_x86(NoteHandsCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_noteHands")]
        private static extern ApiErrorCode MvsApiRegisterCallbackNoteHands_x64(NoteHandsCallback callback);

        internal static ApiErrorCode MvsApiRegisterCallbackNoteHands(NoteHandsCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackNoteHands_x64(callback)
                : MvsApiRegisterCallbackNoteHands_x86(callback);
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_settingsChanged")]
        private static extern ApiErrorCode MvsApiRegisterCallbackSettingsChanged_x86(SettingsChangedCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_settingsChanged")]
        private static extern ApiErrorCode MvsApiRegisterCallbackSettingsChanged_x64(SettingsChangedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackSettingsChanged(SettingsChangedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackSettingsChanged_x64(callback)
                : MvsApiRegisterCallbackSettingsChanged_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_licenseChanged")]
        private static extern ApiErrorCode MvsApiRegisterCallbackLicenseChanged_x86(LicenseChangedCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_licenseChanged")]
        private static extern ApiErrorCode MvsApiRegisterCallbackLicenseChanged_x64(LicenseChangedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackLicenseChanged(LicenseChangedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackLicenseChanged_x64(callback)
                : MvsApiRegisterCallbackLicenseChanged_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_statsChanged")]
        private static extern ApiErrorCode MvsApiRegisterCallbackStatsChanged_x86(StatsChangedCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_statsChanged")]
        private static extern ApiErrorCode MvsApiRegisterCallbackStatsChanged_x64(StatsChangedCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackStatsChanged(StatsChangedCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackStatsChanged_x64(callback)
                : MvsApiRegisterCallbackStatsChanged_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_sleepBegin")]
        private static extern ApiErrorCode MvsApiRegisterCallbackSleepBegin_x86(SleepBeginCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_sleepBegin")]
        private static extern ApiErrorCode MvsApiRegisterCallbackSleepBegin_x64(SleepBeginCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackSleepBegin(SleepBeginCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackSleepBegin_x64(callback)
                : MvsApiRegisterCallbackSleepBegin_x86(callback);
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_sleepEnd")]
        private static extern ApiErrorCode MvsApiRegisterCallbackSleepEnd_x86(SleepEndCallback callback);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_sleepEnd")]
        private static extern ApiErrorCode MvsApiRegisterCallbackSleepEnd_x64(SleepEndCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackSleepEnd(SleepEndCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackSleepEnd_x64(callback)
                : MvsApiRegisterCallbackSleepEnd_x86(callback);
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_hasUnsavedChanges")]
        private static extern ApiErrorCode MvsApiRegisterCallbackHasUnsavedChanges_x86(HasUnsavedChangesCallback callback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_hasUnsavedChanges")]
        private static extern ApiErrorCode MvsApiRegisterCallbackHasUnsavedChanges_x64(HasUnsavedChangesCallback callback);
        internal static ApiErrorCode MvsApiRegisterCallbackHasUnsavedChanges(HasUnsavedChangesCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterCallbackHasUnsavedChanges_x64(callback)
                : MvsApiRegisterCallbackHasUnsavedChanges_x86(callback);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_replayHand")]
        private static extern ApiErrorCode MvsApiRegisterReplayHand_x86(ReplayHandCallback callback);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerCallback_replayHand")]
        private static extern ApiErrorCode MvsApiRegisterReplayHand_x64(ReplayHandCallback callback);

        internal static ApiErrorCode MvsApiRegisterReplayHand(ReplayHandCallback callback)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterReplayHand_x64(callback)
                : MvsApiRegisterReplayHand_x86(callback);
        }

        // functions
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerHandsMenu")]
        private static extern ApiErrorCode MvsApiRegisterHandsMenu_x86(string[] menuItems, int menuItemsCount, string menuIcon, HandFormat format);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerHandsMenu")]
        private static extern ApiErrorCode MvsApiRegisterHandsMenu_x64(string[] menuItems, int menuItemsCount, string menuIcon, HandFormat format);
        internal static ApiErrorCode MvsApiRegisterHandsMenu(string[] menuItems, string menuIcon, HandFormat format)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterHandsMenu_x64(menuItems, menuItems.Length, menuIcon, format)
                : MvsApiRegisterHandsMenu_x86(menuItems, menuItems.Length, menuIcon, format);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerMenu")]
        private static extern ApiErrorCode MvsApiRegisterMenu_x86(string[] menuItems, int menuItemsCount);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerMenu")]
        private static extern ApiErrorCode MvsApiRegisterMenu_x64(string[] menuItems, int menuItemsCount);
        internal static ApiErrorCode MvsApiRegisterMenu(string[] menuItems)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterMenu_x64(menuItems, menuItems.Length)
                : MvsApiRegisterMenu_x86(menuItems, menuItems.Length);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerNoteTab")]
        private static extern ApiErrorCode MvsApiRegisterNoteTab_x86(string tabName, string tabIcon);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerNoteTab")]
        private static extern ApiErrorCode MvsApiRegisterNoteTab_x64(string tabName, string tabIcon);
        internal static ApiErrorCode MvsApiRegisterNoteTab(string tabName, string tabIcon)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterNoteTab_x64(tabName, tabIcon)
                : MvsApiRegisterNoteTab_x86(tabName, tabIcon);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_noop")]
        private static extern ApiErrorCode MvsApiNoop_x86(bool shouldFail, int waitTime, string extraBytes, out int requestSize);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_noop")]
        private static extern ApiErrorCode MvsApiNoop_x64(bool shouldFail, int waitTime, string extraBytes, out int requestSize);
        internal static ApiErrorCode MvsApiNoop(bool shouldFail, int waitTime, string extraBytes, out int requestSize)
        {
            return Environment.Is64BitProcess
                ? MvsApiNoop_x64(shouldFail, waitTime, extraBytes, out requestSize)
                : MvsApiNoop_x86(shouldFail, waitTime, extraBytes, out requestSize);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_replayHands")]
        private static extern ApiErrorCode MvsApiReplayHands_x86(HandSelectorInternal[] hands, int handsCount);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_replayHands")]
        private static extern ApiErrorCode MvsApiReplayHands_x64(HandSelectorInternal[] hands, int handsCount);
        internal static ApiErrorCode MvsApiReplayHands(HandSelectorInternal [] hands, int handsCount)
        {
            return Environment.Is64BitProcess
                ? MvsApiReplayHands_x64(hands, handsCount)
                : MvsApiReplayHands_x86(hands, handsCount);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestHands")]
        private static extern ApiErrorCode MvsApiRequestHands_x86(out int importStarted);

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestHands")]
        private static extern ApiErrorCode MvsApiRequestHands_x64(out int importStarted);
        internal static ApiErrorCode MvsApiRequestHands(out int importStarted)
        {
            return Environment.Is64BitProcess
                ? MvsApiRequestHands_x64(out importStarted)
                : MvsApiRequestHands_x86(out importStarted);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestNotes")]
        private static extern ApiErrorCode MvsApiRequestNotes_x86();

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestNotes")]
        private static extern ApiErrorCode MvsApiRequestNotes_x64();

        internal static ApiErrorCode MvsApiRequestNotes()
        {
            return Environment.Is64BitProcess
                ? MvsApiRequestNotes_x64()
                : MvsApiRequestNotes_x86();
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestTags")]
        private static extern ApiErrorCode MvsApiRequestTags_x86();

        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestTags")]
        private static extern ApiErrorCode MvsApiRequestTags_x64();
        internal static ApiErrorCode MvsApiRequestTags()
        {
            return Environment.Is64BitProcess
                ? MvsApiRequestTags_x64()
                : MvsApiRequestTags_x86();
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestTables")]
        private static extern ApiErrorCode MvsApiRequestTables_x86();
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_requestTables")]
        private static extern ApiErrorCode MvsApiRequestTables_x64();
        internal static ApiErrorCode MvsApiRequestTables()
        {
            return Environment.Is64BitProcess
                ? MvsApiRequestTables_x64()
                : MvsApiRequestTables_x86();
        }

        //MVSAPI_DLLEXPORT int mvsApi_getStats(int tableType, MVSAPI_BOOL fullDetails, mvsApiCallback_getStats callback, MVSAPI_LPARAM userData);
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getStats")]
        private static extern ApiErrorCode MvsApiGetStats_x86(int tableType, bool fullDetails, GetStatsCallback callbackGetStats, IntPtr userData);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getStats")]
        private static extern ApiErrorCode MvsApiGetStats_x64(int tableType, bool fullDetails, GetStatsCallback callbackGetStats, IntPtr userData);
        internal static ApiErrorCode MvsApiGetStats(int tableType, bool fullDetails, GetStatsCallback callbackGetStats, IntPtr userData)
        {
            return Environment.Is64BitProcess
                ? MvsApiGetStats_x64(tableType, fullDetails, callbackGetStats, userData)
                : MvsApiGetStats_x86(tableType, fullDetails, callbackGetStats, userData);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_importHand")]
        private static extern ApiErrorCode MvsApiImportHand_x86(int siteId, string handHistory);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_importHand")]
        private static extern ApiErrorCode MvsApiImportHand_x64(int siteId, string handHistory);
        internal static ApiErrorCode MvsApiImportHand(int siteId, string handHistory)
        {
            return Environment.Is64BitProcess
                ? MvsApiImportHand_x64(siteId, handHistory)
                : MvsApiImportHand_x86(siteId, handHistory);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_busyStateBegin")]
        private static extern ApiErrorCode MvsApiBusyStateBegin_x86();
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_busyStateBegin")]
        private static extern ApiErrorCode MvsApiBusyStateBegin_x64();
        internal static ApiErrorCode MvsApiBusyStateBegin()
        {
            return Environment.Is64BitProcess
                ? MvsApiBusyStateBegin_x64()
                : MvsApiBusyStateBegin_x86();
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_busyStateEnd")]
        private static extern ApiErrorCode MvsApiBusyStateEnd_x86();
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_busyStateEnd")]
        private static extern ApiErrorCode MvsApiBusyStateEnd_x64();
        internal static ApiErrorCode MvsApiBusyStateEnd()
        {
            return Environment.Is64BitProcess ? MvsApiBusyStateEnd_x64() : MvsApiBusyStateEnd_x86();
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHandTags")]
        private static extern ApiErrorCode MvsApiGetHandTags_x86(int siteId, string handNo, GetHandTagsCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHandTags")]
        private static extern ApiErrorCode MvsApiGetHandTags_x64(int siteId, string handNo, GetHandTagsCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiGetHandTags(int siteId, string handNo, GetHandTagsCallback callback, IntPtr userData, out int callerId)
        {

            return Environment.Is64BitProcess
                ? MvsApiGetHandTags_x64(siteId, handNo, callback, userData, out callerId)
                : MvsApiGetHandTags_x86(siteId, handNo, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_setHandTags")]
        private static extern ApiErrorCode MvsApiSetHandTags_x86(int siteId, string handNo, string tableName, [In] string[] tags, int tagsCount);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_setHandTags")]
        private static extern ApiErrorCode MvsApiSetHandTags_x64(int siteId, string handNo, string tableName, [In] string[] tags, int tagsCount);
        internal static ApiErrorCode MvsApiSetHandTags(int siteId, string handNo, string tableName, [In] string[] tags, int tagsCount)
        {

            return Environment.Is64BitProcess
                ? MvsApiSetHandTags_x64(siteId, handNo, tableName, tags, tagsCount)
                : MvsApiSetHandTags_x86(siteId, handNo, tableName, tags, tagsCount);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHands")]
        private static extern ApiErrorCode MvsApiGetHands_x86(HandInternal[] hands, int handCount, bool includeNative, GetHandsCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHands")]
        private static extern ApiErrorCode MvsApiGetHands_x64(HandInternal[] hands, int handCount, bool includeNative, GetHandsCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiGetHands(HandInternal[] hands, int handCount, bool includeNative, GetHandsCallback callback, IntPtr userData, out int callerId)
        {

            return Environment.Is64BitProcess
                ? MvsApiGetHands_x64(hands, handCount, includeNative, callback, userData, out callerId)
                : MvsApiGetHands_x86(hands, handCount, includeNative, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHandsToFile")]
        private static extern ApiErrorCode MvsApiGetHandsToFile_x86(HandInternal[] hands, int handCount, bool includeNative, string filePath, GetHandsToFileCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHandsToFile")]
        private static extern ApiErrorCode MvsApiGetHandsToFile_x64(HandInternal[] hands, int handCount, bool includeNative, string filePath, GetHandsToFileCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiGetHandsToFile(HandInternal[] hands, int handCount, bool includeNative, string filePath, GetHandsToFileCallback callback, IntPtr userData, out int callerId)
        {

            return Environment.Is64BitProcess
                ? MvsApiGetHandsToFile_x64(hands, handCount, includeNative, filePath, callback, userData, out callerId)
                : MvsApiGetHandsToFile_x86(hands, handCount, includeNative, filePath, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHandsToSharedMemory")]
        private static extern ApiErrorCode MvsApiGetHandsToSharedMemory_x86(HandInternal[] hands, int handCount, bool includeNative, string memoryName, int memorySize, GetHandsToSharedMemoryCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getHandsToSharedMemory")]
        private static extern ApiErrorCode MvsApiGetHandsToSharedMemory_x64(HandInternal[] hands, int handCount, bool includeNative, string memoryName, int memorySize, GetHandsToSharedMemoryCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiGetHandsToSharedMemory(HandInternal[] hands, int handCount, bool includeNative, string memoryName, int memorySize, GetHandsToSharedMemoryCallback callback, IntPtr userData, out int callerId)
        {

            return Environment.Is64BitProcess
                ? MvsApiGetHandsToSharedMemory_x64(hands, handCount, includeNative, memoryName, memorySize, callback, userData, out callerId)
                : MvsApiGetHandsToSharedMemory_x86(hands, handCount, includeNative, memoryName, memorySize, callback, userData, out callerId);
        }
        

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSetting")]
        private static extern ApiErrorCode MvsApiGetSetting_x86(string setting, StringBuilder buffer, int bufferLen);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSetting")]
        private static extern ApiErrorCode MvsApiGetSetting_x64(string setting, StringBuilder buffer, int bufferLen);
        internal static ApiErrorCode MvsApiGetSetting(string setting, StringBuilder buffer, int bufferLen)
        {

            return Environment.Is64BitProcess
                    ? MvsApiGetSetting_x64(setting, buffer, bufferLen)
                    : MvsApiGetSetting_x86(setting, buffer, bufferLen);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSettingActivePlayer")]
        private static extern ApiErrorCode MvsApiGetSettingActivePlayer_x86(ref SettingActivePlayerInternal activePlayer);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSettingActivePlayer")]
        private static extern ApiErrorCode MvsApiGetSettingActivePlayer_x64(ref SettingActivePlayerInternal activePlayer);
        internal static ApiErrorCode MvsApiGetSettingActivePlayer(ref SettingActivePlayerInternal activePlayer)
        {

            return Environment.Is64BitProcess
                ? MvsApiGetSettingActivePlayer_x64(ref activePlayer)
                : MvsApiGetSettingActivePlayer_x86(ref activePlayer);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_cleanupActivePlayer")]
        private static extern ApiErrorCode MvsApiCleanupActivePlayer_x86(ref SettingActivePlayerInternal activePlayer);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_cleanupActivePlayer")]
        private static extern ApiErrorCode MvsApiCleanupActivePlayer_x64(ref SettingActivePlayerInternal activePlayer);
        internal static ApiErrorCode MvsApiCleanupActivePlayer(ref SettingActivePlayerInternal activePlayer)
        {

            return Environment.Is64BitProcess
                ? MvsApiCleanupActivePlayer_x64(ref activePlayer)
                : MvsApiCleanupActivePlayer_x86(ref activePlayer);
        }


        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSettingHudProfiles")]
        private static extern ApiErrorCode MvsApiGetSettingHudProfiles_x86(ref SettingHudProfilesInternal hudProfiles);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSettingHudProfiles")]
        private static extern ApiErrorCode MvsApiGetSettingHudProfiles_x64(ref SettingHudProfilesInternal hudProfiles);
        internal static ApiErrorCode MvsApiGetSettingHudProfiles(ref SettingHudProfilesInternal hudProfiles)
        {

            return Environment.Is64BitProcess
                ? MvsApiGetSettingHudProfiles_x64(ref hudProfiles)
                : MvsApiGetSettingHudProfiles_x86(ref hudProfiles);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_cleanupHudProfiles")]
        private static extern ApiErrorCode MvsApiCleanupHudProfiles_x86(ref SettingHudProfilesInternal activePlayer);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_cleanupHudProfiles")]
        private static extern ApiErrorCode MvsApiCleanupHudProfiles_x64(ref SettingHudProfilesInternal activePlayer);
        internal static ApiErrorCode MvsApiCleanupHudProfiles(ref SettingHudProfilesInternal activePlayer)
        {

            return Environment.Is64BitProcess
                ? MvsApiCleanupHudProfiles_x64(ref activePlayer)
                : MvsApiCleanupHudProfiles_x86(ref activePlayer);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSettingHandTags")]
        private static extern ApiErrorCode MvsApiGetSettingHandTags_x86(ref SettingHandTagsInternal handTags);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_getSettingHandTags")]
        private static extern ApiErrorCode MvsApiGetSettingHandTags_x64(ref SettingHandTagsInternal handTags);
        internal static ApiErrorCode MvsApiGetSettingHandTags(ref SettingHandTagsInternal handTags)
        {
            return Environment.Is64BitProcess
                ? MvsApiGetSettingHandTags_x64(ref handTags)
                : MvsApiGetSettingHandTags_x86(ref handTags);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_cleanupHandTags")]
        private static extern ApiErrorCode MvsApiCleanupHandTags_x86(ref SettingHandTagsInternal handTags);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_cleanupHandTags")]
        private static extern ApiErrorCode MvsApiCleanupHandTags_x64(ref SettingHandTagsInternal handTags);
        internal static ApiErrorCode MvsApiCleanupHandTags(ref SettingHandTagsInternal handTags)
        {
            return Environment.Is64BitProcess
                ? MvsApiCleanupHandTags_x64(ref handTags)
                : MvsApiCleanupHandTags_x86(ref handTags);
        }


        //MVSAPI_DLLEXPORT int mvsApi_messageBox(const char* title, const char* message);
        //MVSAPI_DLLEXPORT int mvsApi_cancelCallback(int callerID);*/



        // These functions get results asynchronously via the API "result_callback" method.  So they return immediately after being called.
        // Once the tracker has completed the processing of the call, the passed in callback function is called with the information from
        // the tracker.

        //MVSAPI_DLLEXPORT int mvsApi_selectStats(int tableType, const char** includedStats, int includedStatsCount, const char** defaultStats, int defaultStatsCount, mvsApiCallback_resultCallbackSelectStats callback, MVSAPI_LPARAM userData, int* callerID);
        // see: https://docs.microsoft.com/en-us/dotnet/framework/interop/marshaling-different-types-of-arrays?redirectedfrom=MSDN
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_selectStats")]
        private static extern ApiErrorCode MvsApiSelectStats_x86(TableType tableType, [In] string[] includedStats, int includedStatsCount, [In] string[] defaultStats, int defaultStatsCount, SelectStatsCallback callback, IntPtr userData, ref int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_selectStats")]
        private static extern ApiErrorCode MvsApiSelectStats_x64(TableType tableType, [In] string[] includedStats, int includedStatsCount, [In] string[] defaultStats, int defaultStatsCount, SelectStatsCallback callback, IntPtr userData, ref int callerId);
        internal static ApiErrorCode MvsApiSelectStats(TableType tableType, [In] string[] includedStats, int includedStatsCount, [In] string[] defaultStats, int defaultStatsCount, SelectStatsCallback callback, IntPtr userData, ref int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiSelectStats_x64(tableType, includedStats, includedStatsCount, defaultStats, defaultStatsCount, callback, userData, ref callerId)
                : MvsApiSelectStats_x86(tableType, includedStats, includedStatsCount, defaultStats, defaultStatsCount, callback, userData, ref callerId);
        }

        //MVSAPI_DLLEXPORT int mvsApi_selectFilters(int tableType, const char* currentFilters, mvsApiCallback_resultCallbackSelectFilters callback, MVSAPI_LPARAM userData, int* callerID);
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_selectFilters")]
        private static extern ApiErrorCode MvsApiSelectFilters_x86(int tableType, [In] string currentFilters, SelectFiltersCallback callback, IntPtr userData, ref int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_selectFilters")]
        private static extern ApiErrorCode MvsApiSelectFilters_x64(int tableType, [In] string currentFilters, SelectFiltersCallback callback, IntPtr userData, ref int callerId);
        internal static ApiErrorCode MvsApiSelectFilters(int tableType, [In] string currentFilters, SelectFiltersCallback callback, IntPtr userData, ref int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiSelectFilters_x64(tableType, currentFilters, callback, userData, ref callerId)
                : MvsApiSelectFilters_x86(tableType, currentFilters, callback, userData, ref callerId);
        }

        //MVSAPI_DLLEXPORT int mvsApi_queryStatsPlayer(int tableType, int idSite, const char* player, const char** stats, int statsCount, const char* filtersJson, mvsApiCallback_resultCallbackQueryStats callback, MVSAPI_LPARAM userData, int* callerID);
        
        //MVSAPI_DLLEXPORT int mvsApi_queryStatsMultiplePlayers(int tableType, int idSite, const char** players, int playersCount, const char** stats, int statsCount, const char* filtersJson, mvsApiCallback_resultCallbackQueryStats callback, MVSAPI_LPARAM userData, int* callerID);
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryStatsMultiplePlayers")]
        private static extern ApiErrorCode MvsApiQueryStatsMultiplePlayers_x86(int tableType, int siteId, string[] players, int playersCount, string[] stats, int statsCount, string filterJson, QueryStatsCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryStatsMultiplePlayers")]
        private static extern ApiErrorCode MvsApiQueryStatsMultiplePlayers_x64(int tableType, int siteId, string[] players, int playersCount, string[] stats, int statsCount, string filterJson, QueryStatsCallback callback, IntPtr userData, out int callerId);

        internal static ApiErrorCode MvsApiQueryStatsMultiplePlayers(int tableType, int siteId, string[] players, int playersCount, string[] stats, int statsCount, string filterJson, QueryStatsCallback callback, IntPtr userData, out int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiQueryStatsMultiplePlayers_x64(tableType, siteId, players, playersCount, stats, statsCount, filterJson, callback, userData, out callerId)
                : MvsApiQueryStatsMultiplePlayers_x86(tableType, siteId, players, playersCount, stats, statsCount, filterJson, callback, userData, out callerId);
        }

        //MVSAPI_DLLEXPORT int mvsApi_queryPTSQL(int tableType, const char** stats, int statsCount, const char* filters, const char** orderByStats, MVSAPI_BOOL orderByDesc, int orderByCount, MVSAPI_BOOL activePlayer, mvsApiCallback_resultCallbackQueryPTSQL callback, MVSAPI_LPARAM userData, int* callerID);

        //MVSAPI_DLLEXPORT int mvsApi_queryPlayers(MvsApi_QueryPlayerFilters* filters, mvsApiCallback_resultCallbackQueryPlayers callback, MVSAPI_LPARAM userData, int* callerID);
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryPlayers")]
        private static extern ApiErrorCode MvsApiQueryPlayers_x86(QueryPlayerFiltersInternal filters, QueryPlayersCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryPlayers")]
        private static extern ApiErrorCode MvsApiQueryPlayers_x64(QueryPlayerFiltersInternal filters, QueryPlayersCallback callback, IntPtr userData, out int callerId);

        internal static ApiErrorCode MvsApiQueryPlayers(QueryPlayerFiltersInternal filters, QueryPlayersCallback callback, IntPtr userData, out int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiQueryPlayers_x64(filters, callback, userData, out callerId)
                : MvsApiQueryPlayers_x86(filters, callback, userData, out callerId);
        }
        
        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_changeHudProfile")]
        private static extern ApiErrorCode MvsApiChangeHudProfile_x86(int siteId, string tableName, string profileName);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_changeHudProfile")]
        private static extern ApiErrorCode MvsApiChangeHudProfile_x64(int siteId, string tableName, string profileName);
        internal static ApiErrorCode MvsApiChangeHudProfile(int siteId, string tableName, string profileName)
        {

            return Environment.Is64BitProcess
                ? MvsApiChangeHudProfile_x64(siteId, tableName, profileName)
                : MvsApiChangeHudProfile_x86(siteId, tableName, profileName);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_importHudProfile")]
        private static extern ApiErrorCode MvsApiImportHudProfile_x86(string fileName, string profileName, int tableType, ImportHudProfileCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_importHudProfile")]
        private static extern ApiErrorCode MvsApiImportHudProfile_x64(string fileName, string profileName, int tableType, ImportHudProfileCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiImportHudProfile(string fileName, string profileName, int tableType, ImportHudProfileCallback callback, IntPtr userData, out int callerId)
        {

            return Environment.Is64BitProcess
                ? MvsApiImportHudProfile_x64(fileName, profileName, tableType, callback, userData, out callerId)
                : MvsApiImportHudProfile_x86(fileName, profileName, tableType, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryHMQL")]
        private static extern ApiErrorCode MvsApiQueryHmql_x86(string query, QueryHmqlCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryHMQL")]
        private static extern ApiErrorCode MvsApiQueryHmql_x64(string query, QueryHmqlCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiQueryHmql(string query, QueryHmqlCallback callback, IntPtr userData, out int callerId)
        {

            return Environment.Is64BitProcess
                ? MvsApiQueryHmql_x64(query, callback, userData, out callerId)
                : MvsApiQueryHmql_x86(query, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryNotesPlayer")]
        private static extern ApiErrorCode MvsApiQueryNotesPlayer_x86(int siteId, string player, QueryNotesCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryNotesPlayer")]
        private static extern ApiErrorCode MvsApiQueryNotesPlayer_x64(int siteId, string player, QueryNotesCallback callback, IntPtr userData, out int callerId);
        
        internal static ApiErrorCode MvsApiQueryNotesPlayer(int siteId, string player, QueryNotesCallback callback, IntPtr userData, out int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiQueryNotesPlayer_x64(siteId, player, callback, userData, out callerId)
                : MvsApiQueryNotesPlayer_x86(siteId, player, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryNotesMultiplePlayers")]
        private static extern ApiErrorCode MvsApiQueryNotesMultiplePlayers_x86(int siteId, string[] player, int playerCount, QueryNotesCallback callback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_queryNotesMultiplePlayers")]
        private static extern ApiErrorCode MvsApiQueryNotesMultiplePlayers_x64(int siteId, string[] player, int playerCount, QueryNotesCallback callback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiQueryNotesMultiplePlayers(int siteId, string[] player, int playerCount, QueryNotesCallback callback, IntPtr userData, out int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiQueryNotesMultiplePlayers_x64(siteId, player, playerCount, callback, userData, out callerId)
                : MvsApiQueryNotesMultiplePlayers_x86(siteId, player, playerCount, callback, userData, out callerId);
        }

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerStats")]
        private static extern ApiErrorCode MvsApiRegisterStats_x86(StatInternal[] stats, int statsCount, RegisterStatsCallback resultCallback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_registerStats")]
        private static extern ApiErrorCode MvsApiRegisterStats_x64(StatInternal[] stats, int statsCount, RegisterStatsCallback resultCallback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiRegisterStats(StatInternal[] stats, int statsCount, RegisterStatsCallback resultCallback, IntPtr userData, out int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiRegisterStats_x64(stats, statsCount, resultCallback, userData, out callerId)
                : MvsApiRegisterStats_x86(stats, statsCount, resultCallback, userData, out callerId);
        }



        //MVSAPI_DLLEXPORT int mvsApi_registerPositionalStats(int tableType, const char** stats, int statsCount, int positionType, int hasPosition, mvsApiCallback_resultCallbackRegisterPositionalStats callback, MVSAPI_LPARAM userData, int* callerID);

        [DllImport(X86FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_removeStats")]
        private static extern ApiErrorCode MvsApiRemoveStats_x86(int idTableType, [In] string[] stats, int statsCount, RemoveStatsCallback resultCallback, IntPtr userData, out int callerId);
        [DllImport(X64FileName, CallingConvention = CallingConvention.StdCall, EntryPoint = "mvsApi_removeStats")]
        private static extern ApiErrorCode MvsApiRemoveStats_x64(int idTableType, [In] string[] stats, int statsCount, RemoveStatsCallback resultCallback, IntPtr userData, out int callerId);
        internal static ApiErrorCode MvsApiRemoveStats(int idTableType, [In] string[] stats, int statsCount, RemoveStatsCallback resultCallback, IntPtr userData, out int callerId)
        {
            return Environment.Is64BitProcess
                ? MvsApiRemoveStats_x64(idTableType, stats, statsCount, resultCallback, userData, out callerId)
                : MvsApiRemoveStats_x86(idTableType, stats, statsCount, resultCallback, userData, out callerId);
        }
    }
}