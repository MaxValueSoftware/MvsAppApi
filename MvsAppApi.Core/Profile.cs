using MvsAppApi.Core.Enums;

namespace MvsAppApi.Core
{
    public class Profile
    {
        public Tracker Tracker { get; set; }
        public int MaxOutbound { get; set; }
        public int MaxInbound { get; set; }
        public string AppName { get; set; }
        public string AppVersion { get; set; }

        // todo: missing from dll ...
        public string AppId { get; set; }
        public string ApiVersion { get; set; }

        // request callbacks
        public HandCallback HandCallback { get; set; }
        public HandsSelectedCallback HandsSelectedCallback { get; set; }
        public MenuSelectedCallback MenuSelectedCallback { get; set; }
        public TablesCallback TablesCallback { get; set; }
        public ImportStartedCallback ImportStartedCallback { get; set; }
        public ImportStoppedCallback ImportStoppedCallback { get; set; }
        public NotesCallback NotesCallback { get; set; }
        public TagsCallback TagsCallback { get; set; }
        public StatValueCallback StatValueCallback { get; set; }
        public StatPreviewCallback StatPreviewCallback { get; set; }
        public CallbackCallback CallbackCallback { get; set; }
        public NoteTabValueCallback NoteTabValueCallback { get; set; }
        public NoteHandsCallback NoteHandsCallback { get; set; }
        public SettingsChangedCallback SettingsChangedCallback { get; set; }
        public LicenseChangedCallback LicenseChangedCallback { get; set; }
        public StatsChangedCallback StatsChangedCallback { get; set; }
        public SleepBeginCallback SleepBeginCallback { get; set; }
        public SleepEndCallback SleepEndCallback { get; set; }
        public HasUnsavedChangesCallback HasUnsavedChangesCallback { get; set; }
        public ReplayHandCallback ReplayHandCallback { get; set; }
        public NoopCallback NoopCallback { get; set; }
    }
}