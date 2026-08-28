using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public class LauncherForm : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    private const int EM_SETCUEBANNER = 0x1501;

    private void SetCue(TextBox tb, string example)
    {
        SendMessage(tb.Handle, EM_SETCUEBANNER, IntPtr.Zero, example);
    }

    private ToolTip toolTip;

    private Label lblStatus;
    private Button btnRefresh;

    private TabControl tabControl;
    private TabPage tabBasic, tabAdvanced, tabWindow, tabCamera;

    // Basic
    private GroupBox grpMode;
    private RadioButton rbNormal, rbFullscreen, rbBorderless, rbReadOnly;

    private GroupBox grpConnection;
    private RadioButton rbUsb, rbWifi;
    private Button btnConnectWifi, btnLowLatencyPreset;

    private GroupBox grpOptions;
    private CheckBox cbShowTouches, cbStayAwake, cbRecord;

    // Advanced - Video
    private CheckBox cbNoVideo, cbNoVideoPlayback;
    private TextBox txtMaxSize, txtBitrate, txtMaxFps, txtCrop;
    private ComboBox cmbVideoCodec, cmbVideoSource, cmbDisplayOrientation;

    // Advanced - Audio
    private CheckBox cbNoAudio, cbNoAudioPlayback, cbAudioDup;
    private ComboBox cmbAudioSource, cmbAudioCodec;
    private TextBox txtAudioBitrate;

    // Advanced - Control
    private CheckBox cbOtg, cbTurnScreenOff, cbKeepActive, cbPowerOffOnClose, cbNoPowerOn;
    private ComboBox cmbKeyboard, cmbMouse, cmbGamepad;

    // Advanced - Other
    private TextBox txtTimeLimit;
    private ComboBox cmbVerbosity;
    private CheckBox cbPrintFps, cbNoClipboardSync, cbKillAdbOnClose;

    // Window and capture
    private TextBox txtWindowX, txtWindowY, txtWindowWidth, txtWindowHeight, txtWindowTitle;
    private CheckBox cbNoWindow;
    private ComboBox cmbRecordFormat, cmbRecordOrientation;
    private TextBox txtStartApp, txtNewDisplay, txtDisplayId;

    // Camera
    private TextBox txtCameraId, txtCameraSize, txtCameraFps, txtCameraAr;
    private ComboBox cmbCameraFacing;
    private CheckBox cbCameraHighSpeed, cbCameraTorch;
    private TextBox txtCameraZoom;

    private Button btnClearAdvanced;
    private Label lblExtra;
    private TextBox txtExtra;

    private Button btnStart, btnStop, btnRestartAdb, btnStopAdb, btnRecordings;

    private ComboBox cmbTools;
    private Button btnRunTool;

    private Label lblLog;
    private TextBox txtLog;

    // The adb server is a single machine-wide daemon: it may be in use by Android Studio,
    // another terminal or another window of this app. It is only stopped on exit if this
    // session started it, so the system is left as it was found.
    private bool adbWasAlreadyRunning;

    public LauncherForm()
    {
        Text = "Speculum";

        // The icon is embedded in the .exe with /win32icon (see README). Extracting it
        // from there avoids duplicating it as a resource and guarantees that the title
        // bar, the taskbar and Explorer all show exactly the same one. If it fails,
        // WinForms falls back to its default and nothing breaks.
        try { this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
        ClientSize = new Size(620, 780);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(640, 680);
        StartPosition = FormStartPosition.CenterScreen;

        toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ReshowDelay = 100, ShowAlways = true };

        lblStatus = new Label { Text = "Checking status...", Location = new Point(10, 12), Size = new Size(440, 20) };
        Controls.Add(lblStatus);

        btnRefresh = new Button { Text = "Refresh status", Location = new Point(470, 8), Size = new Size(140, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnRefresh.Click += (s, e) => RefreshStatus();
        Controls.Add(btnRefresh);

        tabControl = new TabControl { Location = new Point(10, 40), Size = new Size(600, 380), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        tabBasic = new TabPage("Basic");
        tabAdvanced = new TabPage("Advanced");
        tabWindow = new TabPage("Window and capture");
        tabCamera = new TabPage("Camera");
        tabControl.TabPages.Add(tabBasic);
        tabControl.TabPages.Add(tabAdvanced);
        tabControl.TabPages.Add(tabWindow);
        tabControl.TabPages.Add(tabCamera);
        Controls.Add(tabControl);

        BuildBasicTab();
        BuildAdvancedTab();
        BuildWindowTab();
        BuildCameraTab();

        int y = 430;
        btnStart = new Button { Text = "Start scrcpy", Location = new Point(10, y), Size = new Size(120, 32) };
        btnStart.Click += (s, e) => Launch();
        Controls.Add(btnStart);

        btnStop = new Button { Text = "Stop scrcpy", Location = new Point(140, y), Size = new Size(120, 32) };
        btnStop.Click += (s, e) => Stop();
        Controls.Add(btnStop);

        btnRecordings = new Button { Text = "Recordings", Location = new Point(270, y), Size = new Size(120, 32) };
        btnRecordings.Click += (s, e) => OpenRecordingsFolder();
        Controls.Add(btnRecordings);

        y += 38;
        btnRestartAdb = new Button { Text = "Restart ADB", Location = new Point(10, y), Size = new Size(120, 32) };
        btnRestartAdb.Click += (s, e) => RestartAdb();
        Controls.Add(btnRestartAdb);

        btnStopAdb = new Button { Text = "Stop ADB", Location = new Point(140, y), Size = new Size(120, 32) };
        btnStopAdb.Click += (s, e) => StopAdb();
        Controls.Add(btnStopAdb);

        y += 42;
        cmbTools = new ComboBox { Location = new Point(10, y + 1), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTools.Items.AddRange(new object[] {
            "scrcpy version",
            "List encoders",
            "List cameras",
            "List camera sizes",
            "List displays",
            "List installed apps"
        });
        cmbTools.SelectedIndex = 0;
        Controls.Add(cmbTools);

        btnRunTool = new Button { Text = "Run", Location = new Point(400, y), Size = new Size(100, 25) };
        btnRunTool.Click += (s, e) => RunTool();
        Controls.Add(btnRunTool);

        y += 32;
        lblLog = new Label { Text = "Log:", Location = new Point(10, y), AutoSize = true };
        Controls.Add(lblLog);

        y += 20;
        txtLog = new TextBox
        {
            Location = new Point(10, y),
            Size = new Size(600, ClientSize.Height - y - 10),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        Controls.Add(txtLog);

        Load += (s, e) =>
        {
            // Before RefreshStatus(), which queries adb and would therefore start it.
            adbWasAlreadyRunning = Process.GetProcessesByName("adb").Length > 0;
            MigrateLegacyRecordingsFolder();
            RefreshStatus();
        };

        FormClosing += (s, e) => StopAdbIfWeStartedIt();
    }

    private void BuildBasicTab()
    {
        grpMode = new GroupBox { Text = "Launch mode", Location = new Point(6, 6), Size = new Size(285, 150) };
        rbNormal = new RadioButton { Text = "Normal", Location = new Point(10, 22), Checked = true, AutoSize = true };
        rbFullscreen = new RadioButton { Text = "Fullscreen", Location = new Point(10, 48), AutoSize = true };
        rbBorderless = new RadioButton { Text = "Borderless (for OBS)", Location = new Point(10, 74), AutoSize = true };
        rbReadOnly = new RadioButton { Text = "Read-only (no control)", Location = new Point(10, 100), AutoSize = true };
        grpMode.Controls.Add(rbNormal);
        grpMode.Controls.Add(rbFullscreen);
        grpMode.Controls.Add(rbBorderless);
        grpMode.Controls.Add(rbReadOnly);
        tabBasic.Controls.Add(grpMode);

        grpConnection = new GroupBox { Text = "Connection", Location = new Point(300, 6), Size = new Size(285, 150) };
        rbUsb = new RadioButton { Text = "USB", Location = new Point(10, 22), Checked = true, AutoSize = true };
        rbWifi = new RadioButton { Text = "WiFi", Location = new Point(10, 48), AutoSize = true };
        btnConnectWifi = new Button { Text = "Pair over WiFi (uses USB)", Location = new Point(10, 76), Size = new Size(260, 26) };
        btnConnectWifi.Click += (s, e) => ConnectWifi();
        btnLowLatencyPreset = new Button { Text = "Preset: low-latency WiFi", Location = new Point(10, 106), Size = new Size(260, 26) };
        btnLowLatencyPreset.Click += (s, e) => ApplyLowLatencyPreset();
        grpConnection.Controls.Add(rbUsb);
        grpConnection.Controls.Add(rbWifi);
        grpConnection.Controls.Add(btnConnectWifi);
        grpConnection.Controls.Add(btnLowLatencyPreset);
        tabBasic.Controls.Add(grpConnection);

        grpOptions = new GroupBox { Text = "Options", Location = new Point(6, 162), Size = new Size(579, 70) };
        cbShowTouches = new CheckBox { Text = "Show touches", Location = new Point(10, 28), AutoSize = true };
        cbStayAwake = new CheckBox { Text = "Keep screen awake", Location = new Point(160, 28), AutoSize = true };
        cbRecord = new CheckBox { Text = "Record this session", Location = new Point(360, 28), AutoSize = true };
        grpOptions.Controls.Add(cbShowTouches);
        grpOptions.Controls.Add(cbStayAwake);
        grpOptions.Controls.Add(cbRecord);
        tabBasic.Controls.Add(grpOptions);
    }

    private ComboBox NewCombo(string[] values, Point loc, int width)
    {
        var c = new ComboBox { Location = loc, Size = new Size(width, 21), DropDownStyle = ComboBoxStyle.DropDownList };
        c.Items.Add("(default)");
        c.Items.AddRange(values);
        c.SelectedIndex = 0;
        return c;
    }

    private string ComboValue(ComboBox c)
    {
        if (c.SelectedIndex <= 0) return "";
        return c.SelectedItem.ToString();
    }

    private void BuildWindowTab()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabWindow.Controls.Add(pnl);

        int y = 6;

        // --- Window ---
        var grpWindow = new GroupBox { Text = "scrcpy window", Location = new Point(6, y), Size = new Size(560, 115) };

        var lblWx = new Label { Text = "Position X:", Location = new Point(10, 26), AutoSize = true };
        txtWindowX = new TextBox { Location = new Point(85, 23), Size = new Size(60, 20) };
        var lblWy = new Label { Text = "Y:", Location = new Point(160, 26), AutoSize = true };
        txtWindowY = new TextBox { Location = new Point(180, 23), Size = new Size(60, 20) };
        var lblWw = new Label { Text = "Width:", Location = new Point(260, 26), AutoSize = true };
        txtWindowWidth = new TextBox { Location = new Point(305, 23), Size = new Size(60, 20) };
        var lblWh = new Label { Text = "Height:", Location = new Point(380, 26), AutoSize = true };
        txtWindowHeight = new TextBox { Location = new Point(428, 23), Size = new Size(60, 20) };
        SetCue(txtWindowX, "e.g. 0"); SetCue(txtWindowY, "e.g. 0");
        SetCue(txtWindowWidth, "e.g. 1280"); SetCue(txtWindowHeight, "e.g. 720");
        string posHelp = "Position and size of the window when it opens, in pixels. Handy to keep it "
                       + "always in the same place and capture it in OBS without moving it every time.";
        toolTip.SetToolTip(txtWindowX, posHelp); toolTip.SetToolTip(txtWindowY, posHelp);
        toolTip.SetToolTip(txtWindowWidth, posHelp); toolTip.SetToolTip(txtWindowHeight, posHelp);
        grpWindow.Controls.Add(lblWx); grpWindow.Controls.Add(txtWindowX);
        grpWindow.Controls.Add(lblWy); grpWindow.Controls.Add(txtWindowY);
        grpWindow.Controls.Add(lblWw); grpWindow.Controls.Add(txtWindowWidth);
        grpWindow.Controls.Add(lblWh); grpWindow.Controls.Add(txtWindowHeight);

        var lblWt = new Label { Text = "Title:", Location = new Point(10, 58), AutoSize = true };
        txtWindowTitle = new TextBox { Location = new Point(85, 55), Size = new Size(240, 20) };
        SetCue(txtWindowTitle, "e.g. Phone (OBS)");
        toolTip.SetToolTip(txtWindowTitle, "Text of the title bar. Helps to tell the window apart "
            + "when several are open, and to select it by title in OBS.");
        grpWindow.Controls.Add(lblWt); grpWindow.Controls.Add(txtWindowTitle);

        cbNoWindow = new CheckBox { Text = "No window (--no-window)", Location = new Point(10, 85), AutoSize = true };
        toolTip.SetToolTip(cbNoWindow, "Opens no window at all. Only makes sense together with recording "
            + "or with OTG control: otherwise you will see nothing.");
        grpWindow.Controls.Add(cbNoWindow);

        pnl.Controls.Add(grpWindow);
        y += grpWindow.Height + 8;

        // --- Recording ---
        var grpRec = new GroupBox { Text = "Recording", Location = new Point(6, y), Size = new Size(560, 80) };
        var lblRf = new Label { Text = "Format:", Location = new Point(10, 30), AutoSize = true };
        cmbRecordFormat = NewCombo(new[] { "mp4", "mkv", "m4a", "mka", "opus", "aac", "flac", "wav" }, new Point(75, 27), 100);
        toolTip.SetToolTip(cmbRecordFormat, "Format of the recorded file. By default it is inferred from the "
            + "file extension (.mp4). The audio-only ones (m4a, opus, flac, wav) require video to be disabled.");
        var lblRo = new Label { Text = "Orientation:", Location = new Point(210, 30), AutoSize = true };
        cmbRecordOrientation = NewCombo(new[] { "0", "90", "180", "270", "flip0", "flip90", "flip180", "flip270" }, new Point(295, 27), 120);
        toolTip.SetToolTip(cmbRecordOrientation, "Rotation applied to the recorded file. It does not affect what you "
            + "see on screen, only the recording.");
        var lblRnote = new Label { Text = "These apply to 'Record this session', on the Basic tab.", Location = new Point(10, 56), AutoSize = true, ForeColor = SystemColors.GrayText };
        grpRec.Controls.Add(lblRf); grpRec.Controls.Add(cmbRecordFormat);
        grpRec.Controls.Add(lblRo); grpRec.Controls.Add(cmbRecordOrientation);
        grpRec.Controls.Add(lblRnote);
        pnl.Controls.Add(grpRec);
        y += grpRec.Height + 8;

        // --- Apps and displays ---
        var grpApps = new GroupBox { Text = "Apps and displays", Location = new Point(6, y), Size = new Size(560, 115) };

        var lblSa = new Label { Text = "Start app:", Location = new Point(10, 26), AutoSize = true };
        txtStartApp = new TextBox { Location = new Point(95, 23), Size = new Size(230, 20) };
        SetCue(txtStartApp, "e.g. org.videolan.vlc");
        toolTip.SetToolTip(txtStartApp, "Launches that app on the phone when connecting. Takes the package name. "
            + "With a leading '?' it searches by name (e.g. ?VLC). Use 'List installed apps' to see them.");
        grpApps.Controls.Add(lblSa); grpApps.Controls.Add(txtStartApp);

        var lblNd = new Label { Text = "New display:", Location = new Point(10, 56), AutoSize = true };
        txtNewDisplay = new TextBox { Location = new Point(110, 53), Size = new Size(130, 20) };
        SetCue(txtNewDisplay, "e.g. 1920x1080");
        toolTip.SetToolTip(txtNewDisplay, "Creates a new virtual display on the phone instead of mirroring the real one. "
            + "Format: width x height, optionally /dpi (e.g. 1920x1080/240). Empty = default size.");
        var lblDi = new Label { Text = "or existing display (id):", Location = new Point(260, 56), AutoSize = true };
        txtDisplayId = new TextBox { Location = new Point(410, 53), Size = new Size(60, 20) };
        SetCue(txtDisplayId, "e.g. 0");
        toolTip.SetToolTip(txtDisplayId, "Mirrors a specific display of the phone. Use 'List displays' to see "
            + "the available ids. Cannot be combined with 'New display'.");
        grpApps.Controls.Add(lblNd); grpApps.Controls.Add(txtNewDisplay);
        grpApps.Controls.Add(lblDi); grpApps.Controls.Add(txtDisplayId);

        var lblAnote = new Label { Text = "'New display' and 'existing display' are mutually exclusive: use only one.", Location = new Point(10, 85), AutoSize = true, ForeColor = SystemColors.GrayText };
        grpApps.Controls.Add(lblAnote);

        pnl.Controls.Add(grpApps);
    }

    private void BuildCameraTab()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabCamera.Controls.Add(pnl);

        var lblNotice = new Label
        {
            Text = "Filling in any field on this tab switches the video source to 'camera' automatically.",
            Location = new Point(8, 8),
            Size = new Size(560, 18),
            ForeColor = SystemColors.GrayText
        };
        pnl.Controls.Add(lblNotice);

        var grp = new GroupBox { Text = "Phone camera", Location = new Point(6, 30), Size = new Size(560, 150) };

        var lblId = new Label { Text = "Camera id:", Location = new Point(10, 26), AutoSize = true };
        txtCameraId = new TextBox { Location = new Point(100, 23), Size = new Size(60, 20) };
        SetCue(txtCameraId, "e.g. 0");
        toolTip.SetToolTip(txtCameraId, "Id of the camera to use. 'List cameras' shows the available ones. "
            + "If set, the 'Facing' field is not needed.");
        var lblFacing = new Label { Text = "Facing:", Location = new Point(190, 26), AutoSize = true };
        cmbCameraFacing = NewCombo(new[] { "front", "back", "external" }, new Point(240, 23), 110);
        toolTip.SetToolTip(cmbCameraFacing, "Picks the camera by its position instead of by id: front, back "
            + "or external. Cannot be combined with 'Camera id'.");
        grp.Controls.Add(lblId); grp.Controls.Add(txtCameraId);
        grp.Controls.Add(lblFacing); grp.Controls.Add(cmbCameraFacing);

        var lblSize = new Label { Text = "Size:", Location = new Point(10, 58), AutoSize = true };
        txtCameraSize = new TextBox { Location = new Point(100, 55), Size = new Size(110, 20) };
        SetCue(txtCameraSize, "e.g. 1920x1080");
        toolTip.SetToolTip(txtCameraSize, "Capture resolution. Use 'List camera sizes' in the tools "
            + "dropdown to see which ones your phone supports.");
        var lblFps = new Label { Text = "FPS:", Location = new Point(230, 58), AutoSize = true };
        txtCameraFps = new TextBox { Location = new Point(270, 55), Size = new Size(60, 20) };
        SetCue(txtCameraFps, "e.g. 30");
        var lblAr = new Label { Text = "Aspect ratio:", Location = new Point(350, 58), AutoSize = true };
        txtCameraAr = new TextBox { Location = new Point(430, 55), Size = new Size(65, 20) };
        SetCue(txtCameraAr, "e.g. 16:9");
        toolTip.SetToolTip(txtCameraAr, "Aspect ratio: 16:9, 4:3, or a number such as 1.6. "
            + "Applied by cropping what is left over.");
        grp.Controls.Add(lblSize); grp.Controls.Add(txtCameraSize);
        grp.Controls.Add(lblFps); grp.Controls.Add(txtCameraFps);
        grp.Controls.Add(lblAr); grp.Controls.Add(txtCameraAr);

        var lblZoom = new Label { Text = "Zoom:", Location = new Point(10, 90), AutoSize = true };
        txtCameraZoom = new TextBox { Location = new Point(100, 87), Size = new Size(60, 20) };
        SetCue(txtCameraZoom, "e.g. 2.0");
        toolTip.SetToolTip(txtCameraZoom, "Optical/digital zoom level. 1.0 is no zoom.");
        cbCameraHighSpeed = new CheckBox { Text = "High speed", Location = new Point(190, 89), AutoSize = true };
        toolTip.SetToolTip(cbCameraHighSpeed, "Enables the phone's high frame rate recording mode. "
            + "It heavily restricts the supported sizes and fps.");
        cbCameraTorch = new CheckBox { Text = "Torch on", Location = new Point(330, 89), AutoSize = true };
        toolTip.SetToolTip(cbCameraTorch, "Turns the flash on as a continuous light for as long as the capture lasts.");
        grp.Controls.Add(lblZoom); grp.Controls.Add(txtCameraZoom);
        grp.Controls.Add(cbCameraHighSpeed);
        grp.Controls.Add(cbCameraTorch);

        var lblNote = new Label
        {
            Text = "The camera does not allow touch control: scrcpy captures video, not the phone screen.",
            Location = new Point(10, 120),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };
        grp.Controls.Add(lblNote);

        pnl.Controls.Add(grp);
    }

    private void BuildAdvancedTab()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabAdvanced.Controls.Add(pnl);

        int y = 6;

        // --- Video ---
        var grpVideo = new GroupBox { Text = "Advanced video", Location = new Point(6, y), Size = new Size(560, 145) };
        cbNoVideo = new CheckBox { Text = "No video", Location = new Point(10, 20), AutoSize = true };
        cbNoVideoPlayback = new CheckBox { Text = "No playback on PC", Location = new Point(220, 20), AutoSize = true };
        grpVideo.Controls.Add(cbNoVideo);
        grpVideo.Controls.Add(cbNoVideoPlayback);

        var lblMaxSize = new Label { Text = "Max size (-m):", Location = new Point(10, 52), AutoSize = true };
        txtMaxSize = new TextBox { Location = new Point(120, 49), Size = new Size(70, 20) };
        var lblBitrate = new Label { Text = "Bitrate (-b):", Location = new Point(210, 52), AutoSize = true };
        txtBitrate = new TextBox { Location = new Point(295, 49), Size = new Size(70, 20) };
        var lblMaxFps = new Label { Text = "Max FPS:", Location = new Point(385, 52), AutoSize = true };
        txtMaxFps = new TextBox { Location = new Point(450, 49), Size = new Size(60, 20) };
        grpVideo.Controls.Add(lblMaxSize); grpVideo.Controls.Add(txtMaxSize);
        grpVideo.Controls.Add(lblBitrate); grpVideo.Controls.Add(txtBitrate);
        grpVideo.Controls.Add(lblMaxFps); grpVideo.Controls.Add(txtMaxFps);

        var lblCodec = new Label { Text = "Video codec:", Location = new Point(10, 82), AutoSize = true };
        cmbVideoCodec = NewCombo(new[] { "h264", "h265", "av1", "vp8", "vp9" }, new Point(100, 79), 120);
        var lblVSource = new Label { Text = "Source:", Location = new Point(240, 82), AutoSize = true };
        cmbVideoSource = NewCombo(new[] { "display", "camera" }, new Point(290, 79), 110);
        grpVideo.Controls.Add(lblCodec); grpVideo.Controls.Add(cmbVideoCodec);
        grpVideo.Controls.Add(lblVSource); grpVideo.Controls.Add(cmbVideoSource);

        var lblCrop = new Label { Text = "Crop (w:h:x:y):", Location = new Point(10, 112), AutoSize = true };
        txtCrop = new TextBox { Location = new Point(130, 109), Size = new Size(150, 20) };
        var lblOrient = new Label { Text = "Orientation:", Location = new Point(300, 112), AutoSize = true };
        cmbDisplayOrientation = NewCombo(new[] { "0", "90", "180", "270", "flip0", "flip90", "flip180", "flip270" }, new Point(375, 109), 130);
        grpVideo.Controls.Add(lblCrop); grpVideo.Controls.Add(txtCrop);
        grpVideo.Controls.Add(lblOrient); grpVideo.Controls.Add(cmbDisplayOrientation);

        pnl.Controls.Add(grpVideo);
        y += grpVideo.Height + 8;

        // --- Audio ---
        var grpAudio = new GroupBox { Text = "Advanced audio", Location = new Point(6, y), Size = new Size(560, 115) };
        cbNoAudio = new CheckBox { Text = "No audio", Location = new Point(10, 20), AutoSize = true };
        cbNoAudioPlayback = new CheckBox { Text = "No playback on PC", Location = new Point(180, 20), AutoSize = true };
        cbAudioDup = new CheckBox { Text = "Duplicate audio", Location = new Point(390, 20), AutoSize = true };
        grpAudio.Controls.Add(cbNoAudio);
        grpAudio.Controls.Add(cbNoAudioPlayback);
        grpAudio.Controls.Add(cbAudioDup);

        var lblASource = new Label { Text = "Audio source:", Location = new Point(10, 52), AutoSize = true };
        cmbAudioSource = NewCombo(new[] { "output", "playback", "mic", "mic-unprocessed", "mic-camcorder", "mic-voice-recognition", "mic-voice-communication", "voice-call", "voice-call-uplink", "voice-call-downlink", "voice-performance" }, new Point(100, 49), 190);
        var lblACodec = new Label { Text = "Codec:", Location = new Point(300, 52), AutoSize = true };
        cmbAudioCodec = NewCombo(new[] { "opus", "aac", "flac", "raw" }, new Point(345, 49), 100);
        grpAudio.Controls.Add(lblASource); grpAudio.Controls.Add(cmbAudioSource);
        grpAudio.Controls.Add(lblACodec); grpAudio.Controls.Add(cmbAudioCodec);

        var lblABitrate = new Label { Text = "Audio bitrate:", Location = new Point(10, 82), AutoSize = true };
        txtAudioBitrate = new TextBox { Location = new Point(100, 79), Size = new Size(90, 20) };
        grpAudio.Controls.Add(lblABitrate); grpAudio.Controls.Add(txtAudioBitrate);

        pnl.Controls.Add(grpAudio);
        y += grpAudio.Height + 8;

        // --- Control ---
        var grpControl = new GroupBox { Text = "Advanced control", Location = new Point(6, y), Size = new Size(560, 115) };
        cbOtg = new CheckBox { Text = "OTG mode", Location = new Point(10, 20), AutoSize = true };
        cbTurnScreenOff = new CheckBox { Text = "Turn screen off on start", Location = new Point(120, 20), AutoSize = true };
        cbKeepActive = new CheckBox { Text = "Simulate activity", Location = new Point(320, 20), AutoSize = true };
        grpControl.Controls.Add(cbOtg);
        grpControl.Controls.Add(cbTurnScreenOff);
        grpControl.Controls.Add(cbKeepActive);

        cbPowerOffOnClose = new CheckBox { Text = "Turn screen off on close", Location = new Point(10, 46), AutoSize = true };
        cbNoPowerOn = new CheckBox { Text = "Do not power on at start", Location = new Point(220, 46), AutoSize = true };
        grpControl.Controls.Add(cbPowerOffOnClose);
        grpControl.Controls.Add(cbNoPowerOn);

        var lblKeyboard = new Label { Text = "Keyboard:", Location = new Point(10, 80), AutoSize = true };
        cmbKeyboard = NewCombo(new[] { "disabled", "sdk", "uhid", "aoa" }, new Point(80, 77), 100);
        var lblMouse = new Label { Text = "Mouse:", Location = new Point(190, 80), AutoSize = true };
        cmbMouse = NewCombo(new[] { "disabled", "sdk", "uhid", "aoa" }, new Point(240, 77), 100);
        var lblGamepad = new Label { Text = "Gamepad:", Location = new Point(355, 80), AutoSize = true };
        cmbGamepad = NewCombo(new[] { "disabled", "uhid", "aoa" }, new Point(420, 77), 100);
        grpControl.Controls.Add(lblKeyboard); grpControl.Controls.Add(cmbKeyboard);
        grpControl.Controls.Add(lblMouse); grpControl.Controls.Add(cmbMouse);
        grpControl.Controls.Add(lblGamepad); grpControl.Controls.Add(cmbGamepad);

        pnl.Controls.Add(grpControl);
        y += grpControl.Height + 8;

        // --- Other ---
        var grpOther = new GroupBox { Text = "Other", Location = new Point(6, y), Size = new Size(560, 85) };
        var lblTimeLimit = new Label { Text = "Time limit (s):", Location = new Point(10, 22), AutoSize = true };
        txtTimeLimit = new TextBox { Location = new Point(110, 19), Size = new Size(60, 20) };
        var lblVerbosity = new Label { Text = "Verbosity:", Location = new Point(220, 22), AutoSize = true };
        cmbVerbosity = NewCombo(new[] { "verbose", "debug", "info", "warn", "error" }, new Point(290, 19), 110);
        grpOther.Controls.Add(lblTimeLimit); grpOther.Controls.Add(txtTimeLimit);
        grpOther.Controls.Add(lblVerbosity); grpOther.Controls.Add(cmbVerbosity);

        cbPrintFps = new CheckBox { Text = "FPS counter", Location = new Point(10, 50), AutoSize = true };
        cbNoClipboardSync = new CheckBox { Text = "No clipboard sync", Location = new Point(140, 50), AutoSize = true };
        cbKillAdbOnClose = new CheckBox { Text = "Kill adb on close", Location = new Point(360, 50), AutoSize = true };
        grpOther.Controls.Add(cbPrintFps);
        grpOther.Controls.Add(cbNoClipboardSync);
        grpOther.Controls.Add(cbKillAdbOnClose);

        pnl.Controls.Add(grpOther);
        y += grpOther.Height + 8;

        btnClearAdvanced = new Button { Text = "Clear all advanced options", Location = new Point(6, y), Size = new Size(200, 26) };
        btnClearAdvanced.Click += (s, e) => ClearAdvanced();
        pnl.Controls.Add(btnClearAdvanced);
        y += 34;

        lblExtra = new Label { Text = "Extra flags not listed above (appended as typed):", Location = new Point(6, y), AutoSize = true };
        pnl.Controls.Add(lblExtra);
        y += 20;

        txtExtra = new TextBox { Location = new Point(6, y), Size = new Size(560, 23) };
        pnl.Controls.Add(txtExtra);

        // Examples shown inside the empty boxes (they disappear as soon as you type)
        SetCue(txtMaxSize, "e.g. 1080");
        SetCue(txtBitrate, "e.g. 8M");
        SetCue(txtMaxFps, "e.g. 30");
        SetCue(txtCrop, "e.g. 1080:1920:0:0");
        SetCue(txtAudioBitrate, "e.g. 128K");
        SetCue(txtTimeLimit, "e.g. 600");
        SetCue(txtExtra, "e.g. --push-target=/sdcard/Download/");

        // Explanation shown on hover
        toolTip.SetToolTip(txtMaxSize, "Limits the maximum width and height of the image in pixels (the other side adjusts on its own to keep the ratio).\nLower value = smoother but worse looking. Empty = no limit.");
        toolTip.SetToolTip(txtBitrate, "Video quality/weight: a number followed by K (thousands) or M (millions) of bits per second.\nDefaults to 8M. For WiFi with a weak signal, try 2M.");
        toolTip.SetToolTip(txtMaxFps, "Maximum frames per second of the capture. Empty = no limit. Typical values: 30 or 60.");
        toolTip.SetToolTip(txtCrop, "Crops the screen that is sent. Format: width:height:x:y in pixels, relative to the natural orientation of the phone (usually portrait).");
        toolTip.SetToolTip(cmbVideoCodec, "Codec the phone uses to compress the image. If you pick nothing, scrcpy uses its default (h264).");
        toolTip.SetToolTip(cmbVideoSource, "What to capture: 'display' is the normal screen. 'camera' uses one of the phone cameras instead of the screen (requires Android 12+).");
        toolTip.SetToolTip(cmbDisplayOrientation, "Rotates or flips the displayed image. The numbers are degrees of clockwise rotation; 'flip' mirrors it.");
        toolTip.SetToolTip(cmbAudioSource, "Where the audio comes from: 'output' is all the sound of the phone (the default), 'mic' is the microphone, and so on.");
        toolTip.SetToolTip(cmbAudioCodec, "Audio compression format. Defaults to 'opus'.");
        toolTip.SetToolTip(txtAudioBitrate, "Audio quality: a number followed by K or M. Defaults to 128K.");
        toolTip.SetToolTip(cmbKeyboard, "How key presses are sent to the phone.\n'sdk' = normal, recommended. 'uhid'/'aoa' simulate a physical keyboard, for special cases only.");
        toolTip.SetToolTip(cmbMouse, "How mouse clicks and movements are sent to the phone.\n'sdk' = normal, recommended.");
        toolTip.SetToolTip(cmbGamepad, "How the buttons of a physical controller plugged into the PC are sent. Requires a controller to be connected.");
        toolTip.SetToolTip(txtTimeLimit, "Stops the mirroring/recording automatically after this many seconds. Handy for fixed-length recordings.");
        toolTip.SetToolTip(cmbVerbosity, "How much technical detail to show. It only affects the internal messages, not the image.");
        toolTip.SetToolTip(txtExtra, "Type here any scrcpy flag exactly as you would in a terminal, for anything not covered above.\nSeveral can be given separated by spaces. Example: --push-target=/sdcard/Download/");
    }

    private void ClearAdvanced()
    {
        cbNoVideo.Checked = false; cbNoVideoPlayback.Checked = false;
        txtMaxSize.Text = ""; txtBitrate.Text = ""; txtMaxFps.Text = ""; txtCrop.Text = "";
        cmbVideoCodec.SelectedIndex = 0; cmbVideoSource.SelectedIndex = 0; cmbDisplayOrientation.SelectedIndex = 0;

        cbNoAudio.Checked = false; cbNoAudioPlayback.Checked = false; cbAudioDup.Checked = false;
        cmbAudioSource.SelectedIndex = 0; cmbAudioCodec.SelectedIndex = 0; txtAudioBitrate.Text = "";

        cbOtg.Checked = false; cbTurnScreenOff.Checked = false; cbKeepActive.Checked = false;
        cbPowerOffOnClose.Checked = false; cbNoPowerOn.Checked = false;
        cmbKeyboard.SelectedIndex = 0; cmbMouse.SelectedIndex = 0; cmbGamepad.SelectedIndex = 0;

        txtTimeLimit.Text = ""; cmbVerbosity.SelectedIndex = 0;
        cbPrintFps.Checked = false; cbNoClipboardSync.Checked = false; cbKillAdbOnClose.Checked = false;

        txtWindowX.Text = ""; txtWindowY.Text = ""; txtWindowWidth.Text = ""; txtWindowHeight.Text = "";
        txtWindowTitle.Text = ""; cbNoWindow.Checked = false;
        cmbRecordFormat.SelectedIndex = 0; cmbRecordOrientation.SelectedIndex = 0;
        txtStartApp.Text = ""; txtNewDisplay.Text = ""; txtDisplayId.Text = "";

        txtCameraId.Text = ""; cmbCameraFacing.SelectedIndex = 0; txtCameraSize.Text = "";
        txtCameraFps.Text = ""; txtCameraAr.Text = ""; txtCameraZoom.Text = "";
        cbCameraHighSpeed.Checked = false; cbCameraTorch.Checked = false;

        txtExtra.Text = "";
        Log("Advanced options reset (including Window and capture, and Camera).");
    }

    private void ApplyLowLatencyPreset()
    {
        txtBitrate.Text = "2M";
        txtMaxSize.Text = "800";
        txtMaxFps.Text = "30";
        tabControl.SelectedTab = tabAdvanced;
        Log("Low-latency preset applied: bitrate 2M, max size 800, 30 fps (editable on the Advanced tab).");
    }

    private void Log(string text)
    {
        txtLog.AppendText(text.TrimEnd() + Environment.NewLine);
        txtLog.SelectionStart = txtLog.Text.Length;
        txtLog.ScrollToCaret();
    }

    // Looks for an executable in the system PATH. Returns null if it is not there.
    private static string FindInPath(string exe)
    {
        string path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (string dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                string full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full)) return full;
            }
            catch { }   // malformed PATH entries: ignored
        }
        return null;
    }

    // Without scrcpy in the PATH, adb returns a "the system cannot find the file
    // specified" that tells the user neither what is missing nor how to install it.
    private bool CheckDependencies()
    {
        bool hasScrcpy = FindInPath("scrcpy.exe") != null;
        bool hasAdb = FindInPath("adb.exe") != null;
        if (hasScrcpy && hasAdb) return true;

        string missing = !hasScrcpy && !hasAdb ? "scrcpy.exe nor adb.exe"
                       : !hasScrcpy ? "scrcpy.exe" : "adb.exe";
        Log("=== Cannot find " + missing + " in the system PATH ===");
        Log("scrcpy is not installed, or its folder is not in the PATH.");
        Log("To install it (adb included):    winget install Genymobile.scrcpy");
        Log("Then close this window, open it again and press 'Refresh status'.");
        Log("");
        return false;
    }

    private string RunCommandSync(string exe, string args, int timeoutMs = 15000)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                // The adb server stays alive as a daemon and holds a handle on the
                // directory it was launched from, so that directory cannot be renamed or
                // moved until it dies. With the cwd in temp, the pin lands out of the way.
                WorkingDirectory = Path.GetTempPath()
            };

            // Both streams are read at the same time, not one after the other: reading
            // stdout to the end while stderr is never drained blocks the child as soon as
            // it fills the pipe buffer (~4 KB) and neither side moves on. It showed up
            // with long outputs such as --list-apps.
            var output = new System.Text.StringBuilder();
            using (var p = new Process())
            using (var doneOut = new System.Threading.ManualResetEvent(false))
            using (var doneErr = new System.Threading.ManualResetEvent(false))
            {
                p.StartInfo = psi;
                DataReceivedEventHandler collect = (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (output) output.AppendLine(e.Data);
                };
                p.OutputDataReceived += (s, e) => { if (e.Data == null) doneOut.Set(); else collect(s, e); };
                p.ErrorDataReceived += (s, e) => { if (e.Data == null) doneErr.Set(); else collect(s, e); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    lock (output) output.AppendLine("(cancelled: more than " + timeoutMs + " ms without finishing)");
                }
                // Margin for the last lines already in flight to arrive
                doneOut.WaitOne(2000);
                doneErr.WaitOne(2000);
            }
            lock (output) return output.ToString().Trim();
        }
        catch (Exception ex)
        {
            return "ERROR running " + exe + ": " + ex.Message;
        }
    }

    // Recordings live next to the .exe, never in the current directory: the app is
    // portable and the cwd of the child processes is forced to temp.
    private static string RecordingsDir()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");
    }

    // The folder used to be called 'Grabaciones' when the interface was in Spanish.
    // Renaming it keeps the already recorded sessions reachable from the button instead
    // of stranding them in a folder the app no longer looks at.
    private void MigrateLegacyRecordingsFolder()
    {
        string legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Grabaciones");
        if (!Directory.Exists(legacy) || Directory.Exists(RecordingsDir())) return;
        try
        {
            Directory.Move(legacy, RecordingsDir());
            Log("Renamed the old 'Grabaciones' folder to 'Recordings'.");
        }
        catch (Exception ex)
        {
            Log("Could not rename the old 'Grabaciones' folder to 'Recordings': " + ex.Message);
            Log("The sessions recorded earlier are still there; move them by hand if you want them listed.");
        }
    }

    private string BuildFlags()
    {
        var parts = new System.Collections.Generic.List<string>();

        if (rbUsb.Checked) parts.Add("-d");
        else if (rbWifi.Checked) parts.Add("-e");

        if (rbFullscreen.Checked) parts.Add("-f");
        else if (rbBorderless.Checked) parts.Add("--window-borderless --always-on-top");
        else if (rbReadOnly.Checked) parts.Add("-n");

        if (cbShowTouches.Checked) parts.Add("-t");
        if (cbStayAwake.Checked) parts.Add("-w");

        if (cbRecord.Checked)
        {
            string dir = RecordingsDir();
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "scrcpy_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".mp4");
            parts.Add("-r \"" + file + "\"");
        }

        // Advanced video
        if (cbNoVideo.Checked) parts.Add("--no-video");
        if (cbNoVideoPlayback.Checked) parts.Add("--no-video-playback");
        if (txtMaxSize.Text.Trim() != "") parts.Add("-m " + txtMaxSize.Text.Trim());
        if (txtBitrate.Text.Trim() != "") parts.Add("-b " + txtBitrate.Text.Trim());
        if (txtMaxFps.Text.Trim() != "") parts.Add("--max-fps " + txtMaxFps.Text.Trim());
        if (ComboValue(cmbVideoCodec) != "") parts.Add("--video-codec=" + ComboValue(cmbVideoCodec));
        if (ComboValue(cmbVideoSource) != "") parts.Add("--video-source=" + ComboValue(cmbVideoSource));
        if (txtCrop.Text.Trim() != "") parts.Add("--crop " + txtCrop.Text.Trim());
        if (ComboValue(cmbDisplayOrientation) != "") parts.Add("--display-orientation=" + ComboValue(cmbDisplayOrientation));

        // Advanced audio
        if (cbNoAudio.Checked) parts.Add("--no-audio");
        if (cbNoAudioPlayback.Checked) parts.Add("--no-audio-playback");
        if (cbAudioDup.Checked) parts.Add("--audio-dup");
        if (ComboValue(cmbAudioSource) != "") parts.Add("--audio-source=" + ComboValue(cmbAudioSource));
        if (ComboValue(cmbAudioCodec) != "") parts.Add("--audio-codec=" + ComboValue(cmbAudioCodec));
        if (txtAudioBitrate.Text.Trim() != "") parts.Add("--audio-bit-rate=" + txtAudioBitrate.Text.Trim());

        // Advanced control
        if (cbOtg.Checked) parts.Add("--otg");
        if (cbTurnScreenOff.Checked) parts.Add("-S");
        if (cbKeepActive.Checked) parts.Add("--keep-active");
        if (cbPowerOffOnClose.Checked) parts.Add("--power-off-on-close");
        if (cbNoPowerOn.Checked) parts.Add("--no-power-on");
        if (ComboValue(cmbKeyboard) != "") parts.Add("--keyboard=" + ComboValue(cmbKeyboard));
        if (ComboValue(cmbMouse) != "") parts.Add("--mouse=" + ComboValue(cmbMouse));
        if (ComboValue(cmbGamepad) != "") parts.Add("--gamepad=" + ComboValue(cmbGamepad));

        // Other
        if (txtTimeLimit.Text.Trim() != "") parts.Add("--time-limit=" + txtTimeLimit.Text.Trim());
        if (ComboValue(cmbVerbosity) != "") parts.Add("-V " + ComboValue(cmbVerbosity));
        if (cbPrintFps.Checked) parts.Add("--print-fps");
        if (cbNoClipboardSync.Checked) parts.Add("--no-clipboard-autosync");
        if (cbKillAdbOnClose.Checked) parts.Add("--kill-adb-on-close");

        // Window
        if (txtWindowX.Text.Trim() != "") parts.Add("--window-x=" + txtWindowX.Text.Trim());
        if (txtWindowY.Text.Trim() != "") parts.Add("--window-y=" + txtWindowY.Text.Trim());
        if (txtWindowWidth.Text.Trim() != "") parts.Add("--window-width=" + txtWindowWidth.Text.Trim());
        if (txtWindowHeight.Text.Trim() != "") parts.Add("--window-height=" + txtWindowHeight.Text.Trim());
        if (txtWindowTitle.Text.Trim() != "") parts.Add("--window-title=\"" + txtWindowTitle.Text.Trim() + "\"");
        if (cbNoWindow.Checked) parts.Add("--no-window");

        // Recording
        if (ComboValue(cmbRecordFormat) != "") parts.Add("--record-format=" + ComboValue(cmbRecordFormat));
        if (ComboValue(cmbRecordOrientation) != "") parts.Add("--record-orientation=" + ComboValue(cmbRecordOrientation));

        // Apps and displays
        if (txtStartApp.Text.Trim() != "") parts.Add("--start-app=" + txtStartApp.Text.Trim());
        if (txtNewDisplay.Text.Trim() != "") parts.Add("--new-display=" + txtNewDisplay.Text.Trim());
        else if (txtDisplayId.Text.Trim() != "") parts.Add("--display-id=" + txtDisplayId.Text.Trim());

        // Camera
        if (txtCameraId.Text.Trim() != "") parts.Add("--camera-id=" + txtCameraId.Text.Trim());
        if (ComboValue(cmbCameraFacing) != "") parts.Add("--camera-facing=" + ComboValue(cmbCameraFacing));
        if (txtCameraSize.Text.Trim() != "") parts.Add("--camera-size=" + txtCameraSize.Text.Trim());
        if (txtCameraFps.Text.Trim() != "") parts.Add("--camera-fps=" + txtCameraFps.Text.Trim());
        if (txtCameraAr.Text.Trim() != "") parts.Add("--camera-ar=" + txtCameraAr.Text.Trim());
        if (txtCameraZoom.Text.Trim() != "") parts.Add("--camera-zoom=" + txtCameraZoom.Text.Trim());
        if (cbCameraHighSpeed.Checked) parts.Add("--camera-high-speed");
        if (cbCameraTorch.Checked) parts.Add("--camera-torch");

        if (!string.IsNullOrWhiteSpace(txtExtra.Text)) parts.Add(txtExtra.Text.Trim());

        return string.Join(" ", parts);
    }

    private static bool HasText(TextBox tb)
    {
        return tb.Text.Trim() != "";
    }

    // The camera fields do nothing while the video source is still the display, so it is
    // switched automatically instead of leaving the user with flags scrcpy ignores.
    private bool CameraConfigured()
    {
        return HasText(txtCameraId) || ComboValue(cmbCameraFacing) != "" || HasText(txtCameraSize)
            || HasText(txtCameraFps) || HasText(txtCameraAr) || HasText(txtCameraZoom)
            || cbCameraHighSpeed.Checked || cbCameraTorch.Checked;
    }

    // Returns the reason why it cannot be launched, or null if everything is fine.
    private string BlockingReason()
    {
        if (HasText(txtNewDisplay) && HasText(txtDisplayId))
            return "'New display' and 'existing display (id)' cannot be used at the same time. "
                 + "Clear one of the two on the 'Window and capture' tab.";

        if (HasText(txtCameraId) && ComboValue(cmbCameraFacing) != "")
            return "'Camera id' and 'Facing' cannot be used at the same time. Clear one of the two "
                 + "on the 'Camera' tab.";

        return null;
    }

    private void Launch()
    {
        try
        {
            if (!CheckDependencies()) return;
            bool running = Process.GetProcessesByName("scrcpy").Length > 0;
            if (running)
            {
                Log("scrcpy is already running.");
                return;
            }

            string reason = BlockingReason();
            if (reason != null)
            {
                Log("Cannot launch: " + reason);
                return;
            }

            if (CameraConfigured() && ComboValue(cmbVideoSource) != "camera")
            {
                cmbVideoSource.SelectedItem = "camera";
                Log("Camera options are set: video source switched to 'camera'.");
            }

            string flags = BuildFlags();
            Log("Launching: scrcpy " + flags);
            var psi = new ProcessStartInfo("scrcpy", flags) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log("ERROR starting scrcpy: " + ex.Message);
        }
    }

    private void Stop()
    {
        bool running = Process.GetProcessesByName("scrcpy").Length > 0;
        if (!running)
        {
            Log("scrcpy was not running.");
            return;
        }
        Log(RunCommandSync("taskkill", "/IM scrcpy.exe /F"));
        RefreshStatus();
    }

    private void RestartAdb()
    {
        Log("Restarting the adb server...");
        Log(RunCommandSync("adb", "kill-server"));
        Log(RunCommandSync("adb", "start-server"));
        RefreshStatus();
    }

    // On exit, the adb server is only stopped if this session started it and no scrcpy is
    // left using it. If it was already running when the app opened, it is left untouched:
    // it is a shared daemon and not ours.
    private void StopAdbIfWeStartedIt()
    {
        if (adbWasAlreadyRunning) return;
        if (Process.GetProcessesByName("scrcpy").Length > 0) return;
        if (Process.GetProcessesByName("adb").Length == 0) return;
        try { RunCommandSync("adb", "kill-server", 5000); } catch { }
    }

    private void StopAdb()
    {
        Log("Stopping the adb server...");
        Log(RunCommandSync("adb", "kill-server"));
        Log("adb server stopped. Note: it will restart on its own as soon as any adb or scrcpy action runs (including 'Refresh status').");
        bool running = Process.GetProcessesByName("adb").Length > 0;
        lblStatus.Text = "scrcpy: " + (Process.GetProcessesByName("scrcpy").Length > 0 ? "RUNNING" : "stopped") + " | adb: " + (running ? "up" : "down");
    }

    // Only local network IPs are of any use: to connect over WiFi the PC has to be able to
    // reach the phone. Mobile data gives public IPs or shared-range ones (100.64.0.0/10,
    // the carrier CGNAT) that cannot be reached from the LAN.
    private static bool IsLocalNetworkIp(string ip)
    {
        string[] o = ip.Split('.');
        if (o.Length != 4) return false;
        int a, b;
        if (!int.TryParse(o[0], out a) || !int.TryParse(o[1], out b)) return false;

        if (a == 10) return true;                          // 10.0.0.0/8
        if (a == 192 && b == 168) return true;             // 192.168.0.0/16
        if (a == 172 && b >= 16 && b <= 31) return true;   // 172.16.0.0/12
        return false;
    }

    // The WiFi interface is not called wlan0 on every phone, so if there is nothing there
    // all of them are checked. The name varies with the modem vendor (rmnet on Qualcomm,
    // ccmni on MediaTek...), so a list of names is not enough: the IP is also required to
    // be a local network one.
    private string GetPhoneIp()
    {
        string output = RunCommandSync("adb", "shell ip -f inet addr show wlan0");
        Match m = Regex.Match(output, @"inet (\d+\.\d+\.\d+\.\d+)/");
        if (m.Success && IsLocalNetworkIp(m.Groups[1].Value))
        {
            Log("Interface wlan0, IP " + m.Groups[1].Value);
            return m.Groups[1].Value;
        }

        Log("wlan0 gives no local network IP; checking the remaining interfaces...");
        output = RunCommandSync("adb", "shell ip -f inet addr show");

        string rejected = "";
        foreach (Match c in Regex.Matches(output, @"inet (\d+\.\d+\.\d+\.\d+)/\d+([^\r\n]*)"))
        {
            string candidate = c.Groups[1].Value;
            string rest = c.Groups[2].Value.TrimEnd();
            if (candidate.StartsWith("127.")) continue;
            if (!IsLocalNetworkIp(candidate))
            {
                rejected += "\r\n   " + candidate + " (" + rest.Trim() + ")";
                continue;
            }
            Log("Interface found: " + rest.Trim() + ", IP " + candidate);
            return candidate;
        }

        if (rejected != "")
        {
            Log("The phone only has IPs outside the local network, which are no use here:" + rejected);
            Log("Those are usually mobile data. Turn the phone WiFi on and connect it to the");
            Log("same network as this PC before pairing.");
        }
        else
        {
            Log("The phone has no interface with an IP. Check that WiFi is turned on.");
        }
        return null;
    }

    private void ConnectWifi()
    {
        Log("Switching the phone to TCP/IP mode (it must be connected over USB)...");
        Log(RunCommandSync("adb", "tcpip 5555"));

        System.Threading.Thread.Sleep(1500);

        string ip = GetPhoneIp();
        if (ip == null)
        {
            Log("Could not get the phone IP. Check that it is connected over USB");
            Log("and that its WiFi is on and joined to the same network as this PC.");
            return;
        }
        Log("Phone IP: " + ip);
        Log(RunCommandSync("adb", "connect " + ip + ":5555"));
        rbWifi.Checked = true;
        RefreshStatus();
    }

    private void OpenRecordingsFolder()
    {
        string dir = RecordingsDir();
        Directory.CreateDirectory(dir);
        Process.Start("explorer.exe", dir);
    }

    private void RunTool()
    {
        string args;
        switch (cmbTools.SelectedItem.ToString())
        {
            case "scrcpy version": args = "-v"; break;
            case "List encoders": args = "--list-encoders"; break;
            case "List cameras": args = "--list-cameras"; break;
            case "List camera sizes": args = "--list-camera-sizes"; break;
            case "List displays": args = "--list-displays"; break;
            case "List installed apps": args = "--list-apps"; break;
            default: args = "-v"; break;
        }
        Log("Running: scrcpy " + args);
        Log(RunCommandSync("scrcpy", args, 30000));
    }

    private void RefreshStatus()
    {
        if (!CheckDependencies())
        {
            lblStatus.Text = "scrcpy: NOT INSTALLED — see the log";
            return;
        }
        bool scrcpyRunning = Process.GetProcessesByName("scrcpy").Length > 0;
        Log("--- Devices ---");
        Log(RunCommandSync("adb", "devices -l"));
        bool adbRunning = Process.GetProcessesByName("adb").Length > 0;
        lblStatus.Text = "scrcpy: " + (scrcpyRunning ? "RUNNING" : "stopped") + " | adb: " + (adbRunning ? "up" : "down");
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LauncherForm());
    }
}
