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
    // One tab per thing you configure, not per how advanced it is. The earlier
    // Basic/Advanced split sorted by the user's skill instead of by subject, and every
    // feature ended up spread over several tabs: recording lived in three of them, and
    // the video source sat on a different tab from the camera settings it governs.
    private TabPage tabConnection, tabVideo, tabAudio, tabWindow, tabRecording, tabOther;

    // Connection
    private RadioButton rbUsb, rbWifi;
    private Button btnConnectWifi, btnLowLatencyPreset;

    // Video
    private CheckBox cbNoVideo, cbNoVideoPlayback;
    private TextBox txtMaxSize, txtBitrate, txtMaxFps, txtCrop;
    private ComboBox cmbVideoCodec, cmbVideoSource, cmbDisplayOrientation;
    private TextBox txtCameraId, txtCameraSize, txtCameraFps, txtCameraAr;
    private ComboBox cmbCameraFacing;
    private CheckBox cbCameraHighSpeed, cbCameraTorch;
    private TextBox txtCameraZoom;

    // Audio
    private CheckBox cbNoAudio, cbNoAudioPlayback, cbAudioDup;
    private ComboBox cmbAudioSource, cmbAudioCodec;
    private TextBox txtAudioBitrate;

    // Window
    private RadioButton rbNormal, rbFullscreen, rbBorderless, rbReadOnly;
    private TextBox txtWindowX, txtWindowY, txtWindowWidth, txtWindowHeight, txtWindowTitle;
    private CheckBox cbNoWindow;
    private TextBox txtStartApp, txtNewDisplay, txtDisplayId;

    // Recording
    private CheckBox cbRecord;
    private ComboBox cmbRecordFormat, cmbRecordOrientation;
    private TextBox txtTimeLimit;

    // Control and other
    private CheckBox cbShowTouches, cbStayAwake;
    private CheckBox cbOtg, cbTurnScreenOff, cbKeepActive, cbPowerOffOnClose, cbNoPowerOn;
    private ComboBox cmbKeyboard, cmbMouse, cmbGamepad;
    private ComboBox cmbVerbosity;
    private CheckBox cbPrintFps, cbNoClipboardSync, cbKillAdbOnClose;

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
        // Layout. What stays outside the tabs is only what gets pressed every time:
        // Start, Stop and the status. Everything else moved to the tab whose subject it
        // belongs to — the ADB buttons to Connection, Recordings to Recording, the tools
        // dropdown to the Diagnostics group. The bottom strip used to be 350 px, 45% of
        // the window, giving an action pressed every time, maintenance pressed once a
        // month and diagnostics all the same visual weight, with a 208 px log presiding
        // over the lot to show four lines of adb noise.
        const int margin = 10;
        const int actionBar = 32;
        const int logHeight = 70;       // about four lines; drag the splitter for more
        const int bottomPanel = actionBar + 8 + 16 + 2 + logHeight;
        const int splitterH = 5;
        const int tabsWanted = 400;     // the tallest tab is ~368 px plus its header
        const int tabsMin = 150;        // below this the panels scroll instead

        // Never open taller than the screen: on a laptop that would push the buttons under
        // the taskbar with no way to reach them.
        int chrome = 39;                // provisional; corrected below from the real frame
        int maxClient = Screen.PrimaryScreen.WorkingArea.Height - chrome;
        int clientH = Math.Min(margin + tabsWanted + splitterH + bottomPanel + margin, maxClient);
        clientH = Math.Max(clientH, margin + tabsMin + splitterH + bottomPanel + margin);

        FormBorderStyle = FormBorderStyle.Sizable;
        Padding = new Padding(margin);
        ClientSize = new Size(620, clientH);
        // Now that the form has its real frame, size the minimum from the actual chrome
        // instead of a guess, and keep the minimum width equal to the default so the
        // 10 px margins stay even on both sides.
        int frameH = Height - ClientSize.Height;
        int frameW = Width - ClientSize.Width;
        MinimumSize = new Size(frameW + 620, frameH + margin + tabsMin + splitterH + bottomPanel + margin);
        StartPosition = FormStartPosition.CenterScreen;

        toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ReshowDelay = 100, ShowAlways = true };

        // The tabs are docked to fill, so they take every pixel the bottom strip does not.
        tabControl = new TabControl { Dock = DockStyle.Fill };
        tabConnection = new TabPage("Connection");
        tabVideo = new TabPage("Video");
        tabAudio = new TabPage("Audio");
        tabWindow = new TabPage("Window");
        tabRecording = new TabPage("Recording");
        tabOther = new TabPage("Control and other");
        tabControl.TabPages.Add(tabConnection);
        tabControl.TabPages.Add(tabVideo);
        tabControl.TabPages.Add(tabAudio);
        tabControl.TabPages.Add(tabWindow);
        tabControl.TabPages.Add(tabRecording);
        tabControl.TabPages.Add(tabOther);
        Controls.Add(tabControl);

        BuildConnectionTab();
        BuildVideoTab();
        BuildAudioTab();
        BuildWindowTab();
        BuildRecordingTab();
        BuildOtherTab();

        // --- the strip that never moves ---
        // The size is set before any child is added, not left to the dock: a right-anchored
        // child records its distance to the right edge when it is added, so on a panel
        // still at its default width the offset comes out negative and the control lands
        // off the window once the dock widens it.
        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Size = new Size(600, bottomPanel) };

        btnStart = new Button { Text = "Start scrcpy", Location = new Point(0, 0), Size = new Size(120, actionBar) };
        btnStart.Click += (s, e) => Launch();
        pnlBottom.Controls.Add(btnStart);

        btnStop = new Button { Text = "Stop scrcpy", Location = new Point(130, 0), Size = new Size(120, actionBar) };
        btnStop.Click += (s, e) => Stop();
        pnlBottom.Controls.Add(btnStop);

        // Status sits next to the buttons whose effect it reports, not in a corner of its
        // own at the top of the window.
        lblStatus = new Label
        {
            Text = "Checking status...",
            Location = new Point(260, 8),
            Size = new Size(220, 20),
            TextAlign = ContentAlignment.MiddleRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        pnlBottom.Controls.Add(lblStatus);

        btnRefresh = new Button { Text = "Refresh", Location = new Point(490, 3), Size = new Size(110, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnRefresh.Click += (s, e) => RefreshStatus();
        pnlBottom.Controls.Add(btnRefresh);

        lblLog = new Label { Text = "Log — drag the divider above to make it taller", Location = new Point(0, 40), AutoSize = true, ForeColor = SystemColors.GrayText };
        pnlBottom.Controls.Add(lblLog);

        txtLog = new TextBox
        {
            Location = new Point(0, 58),
            Size = new Size(600, logHeight),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlBottom.Controls.Add(txtLog);

        // The splitter lets the log be dragged taller when something needs reading, and
        // shrink back afterwards, instead of the layout having to guess one right height.
        var split = new Splitter { Dock = DockStyle.Bottom, Height = splitterH, MinSize = 90, MinExtra = 220 };
        Controls.Add(split);
        Controls.Add(pnlBottom);

        Load += (s, e) =>
        {
            // Before RefreshStatus(), which queries adb and would therefore start it.
            adbWasAlreadyRunning = Process.GetProcessesByName("adb").Length > 0;
            MigrateLegacyRecordingsFolder();
            RefreshStatus();
        };

        FormClosing += (s, e) => StopAdbIfWeStartedIt();
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

    // Every tab hosts its controls in an AutoScroll panel, so a window smaller than the
    // content scrolls instead of hiding it. At the default size nothing needs to.
    private Panel NewTabPanel(TabPage page)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        page.Controls.Add(pnl);
        return pnl;
    }

    private void BuildConnectionTab()
    {
        var pnl = NewTabPanel(tabConnection);

        var grp = new GroupBox { Text = "How to reach the phone", Location = new Point(6, 6), Size = new Size(560, 180) };

        rbUsb = new RadioButton { Text = "USB", Location = new Point(10, 25), Checked = true, AutoSize = true };
        rbWifi = new RadioButton { Text = "WiFi", Location = new Point(10, 51), AutoSize = true };
        toolTip.SetToolTip(rbUsb, "Use the phone connected by cable (-d).");
        toolTip.SetToolTip(rbWifi, "Use the phone over the network (-e). Pair it first with the button below.");

        btnConnectWifi = new Button { Text = "Pair over WiFi (uses USB)", Location = new Point(10, 79), Size = new Size(260, 26) };
        btnConnectWifi.Click += (s, e) => ConnectWifi();
        btnLowLatencyPreset = new Button { Text = "Preset: low-latency WiFi", Location = new Point(10, 109), Size = new Size(260, 26) };
        btnLowLatencyPreset.Click += (s, e) => ApplyLowLatencyPreset();
        toolTip.SetToolTip(btnLowLatencyPreset, "Fills in bitrate, size and fps on the Video tab with values that cope "
            + "with a weak signal. Nothing hidden: every value it sets is visible and editable there.");

        // Let the label wrap on its own: hard line breaks inside a sentence fight the
        // control width, and the last line ends up clipped as soon as the text grows.
        var lblHint = new Label
        {
            Text = "Pairing needs the cable connected once. After that you can unplug it.\r\n\r\n"
                 + "The phone forgets WiFi debugging when it reboots, so it has to be paired "
                 + "again after a restart.\r\n\r\n"
                 + "Use the ADB buttons below if the connection stops responding.",
            Location = new Point(290, 25),
            Size = new Size(260, 145),
            ForeColor = SystemColors.GrayText
        };

        grp.Controls.Add(rbUsb); grp.Controls.Add(rbWifi);
        grp.Controls.Add(btnConnectWifi); grp.Controls.Add(btnLowLatencyPreset);
        grp.Controls.Add(lblHint);
        pnl.Controls.Add(grp);

        // adb is how the connection is made, so its maintenance belongs here rather than
        // in a permanent strip at the bottom of the window.
        var grpAdb = new GroupBox { Text = "adb server", Location = new Point(6, 194), Size = new Size(560, 95) };

        btnRestartAdb = new Button { Text = "Restart ADB", Location = new Point(10, 25), Size = new Size(130, 30) };
        btnRestartAdb.Click += (s, e) => RestartAdb();
        btnStopAdb = new Button { Text = "Stop ADB", Location = new Point(150, 25), Size = new Size(130, 30) };
        btnStopAdb.Click += (s, e) => StopAdb();
        grpAdb.Controls.Add(btnRestartAdb); grpAdb.Controls.Add(btnStopAdb);

        var lblAdbNote = new Label
        {
            Text = "adb restarts itself as soon as anything needs it again, 'Refresh' included, "
                 + "so stopping it does not keep it stopped for long.",
            Location = new Point(10, 62),
            Size = new Size(540, 28),
            ForeColor = SystemColors.GrayText
        };
        grpAdb.Controls.Add(lblAdbNote);

        pnl.Controls.Add(grpAdb);
    }

    private void BuildVideoTab()
    {
        var pnl = NewTabPanel(tabVideo);

        // --- Video ---
        var grpVideo = new GroupBox { Text = "Video", Location = new Point(6, 6), Size = new Size(560, 145) };
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

        // --- Camera ---
        // The camera settings sit under the video source they depend on: they only mean
        // anything when the source is the camera, and the app switches it for you.
        var grpCam = new GroupBox { Text = "Camera (used when the source above is 'camera')", Location = new Point(6, 159), Size = new Size(560, 175) };

        var lblId = new Label { Text = "Camera id:", Location = new Point(10, 26), AutoSize = true };
        txtCameraId = new TextBox { Location = new Point(100, 23), Size = new Size(60, 20) };
        SetCue(txtCameraId, "e.g. 0");
        toolTip.SetToolTip(txtCameraId, "Id of the camera to use. 'Ask scrcpy' on the Control and other tab lists them. "
            + "If set, the 'Facing' field is not needed.");
        var lblFacing = new Label { Text = "Facing:", Location = new Point(190, 26), AutoSize = true };
        cmbCameraFacing = NewCombo(new[] { "front", "back", "external" }, new Point(240, 23), 110);
        toolTip.SetToolTip(cmbCameraFacing, "Picks the camera by its position instead of by id: front, back "
            + "or external. Cannot be combined with 'Camera id'.");
        grpCam.Controls.Add(lblId); grpCam.Controls.Add(txtCameraId);
        grpCam.Controls.Add(lblFacing); grpCam.Controls.Add(cmbCameraFacing);

        var lblSize = new Label { Text = "Size:", Location = new Point(10, 58), AutoSize = true };
        txtCameraSize = new TextBox { Location = new Point(100, 55), Size = new Size(110, 20) };
        SetCue(txtCameraSize, "e.g. 1920x1080");
        toolTip.SetToolTip(txtCameraSize, "Capture resolution. Use 'Ask scrcpy' on the Control and other tab "
            + "to list the sizes your phone supports.");
        var lblFps = new Label { Text = "FPS:", Location = new Point(230, 58), AutoSize = true };
        txtCameraFps = new TextBox { Location = new Point(270, 55), Size = new Size(60, 20) };
        SetCue(txtCameraFps, "e.g. 30");
        var lblAr = new Label { Text = "Aspect ratio:", Location = new Point(350, 58), AutoSize = true };
        txtCameraAr = new TextBox { Location = new Point(430, 55), Size = new Size(65, 20) };
        SetCue(txtCameraAr, "e.g. 16:9");
        toolTip.SetToolTip(txtCameraAr, "Aspect ratio: 16:9, 4:3, or a number such as 1.6. "
            + "Applied by cropping what is left over.");
        grpCam.Controls.Add(lblSize); grpCam.Controls.Add(txtCameraSize);
        grpCam.Controls.Add(lblFps); grpCam.Controls.Add(txtCameraFps);
        grpCam.Controls.Add(lblAr); grpCam.Controls.Add(txtCameraAr);

        var lblZoom = new Label { Text = "Zoom:", Location = new Point(10, 90), AutoSize = true };
        txtCameraZoom = new TextBox { Location = new Point(100, 87), Size = new Size(60, 20) };
        SetCue(txtCameraZoom, "e.g. 2.0");
        toolTip.SetToolTip(txtCameraZoom, "Optical/digital zoom level. 1.0 is no zoom.");
        cbCameraHighSpeed = new CheckBox { Text = "High speed", Location = new Point(190, 89), AutoSize = true };
        toolTip.SetToolTip(cbCameraHighSpeed, "Enables the phone's high frame rate recording mode. "
            + "It heavily restricts the supported sizes and fps.");
        cbCameraTorch = new CheckBox { Text = "Torch on", Location = new Point(330, 89), AutoSize = true };
        toolTip.SetToolTip(cbCameraTorch, "Turns the flash on as a continuous light for as long as the capture lasts.");
        grpCam.Controls.Add(lblZoom); grpCam.Controls.Add(txtCameraZoom);
        grpCam.Controls.Add(cbCameraHighSpeed);
        grpCam.Controls.Add(cbCameraTorch);

        var lblCamNote = new Label
        {
            Text = "Filling in any of these switches the source above to 'camera' automatically.\r\n"
                 + "There is no touch control from a camera: scrcpy captures video, not the screen.",
            Location = new Point(10, 120),
            Size = new Size(540, 40),
            ForeColor = SystemColors.GrayText
        };
        grpCam.Controls.Add(lblCamNote);

        pnl.Controls.Add(grpCam);

        // Examples and explanations for the video fields
        SetCue(txtMaxSize, "e.g. 1080");
        SetCue(txtBitrate, "e.g. 8M");
        SetCue(txtMaxFps, "e.g. 30");
        SetCue(txtCrop, "e.g. 1080:1920:0:0");
        toolTip.SetToolTip(txtMaxSize, "Limits the maximum width and height of the image in pixels (the other side adjusts on its own to keep the ratio).\nLower value = smoother but worse looking. Empty = no limit.");
        toolTip.SetToolTip(txtBitrate, "Video quality/weight: a number followed by K (thousands) or M (millions) of bits per second.\nDefaults to 8M. For WiFi with a weak signal, try 2M.");
        toolTip.SetToolTip(txtMaxFps, "Maximum frames per second of the capture. Empty = no limit. Typical values: 30 or 60.");
        toolTip.SetToolTip(txtCrop, "Crops the screen that is sent. Format: width:height:x:y in pixels, relative to the natural orientation of the phone (usually portrait).");
        toolTip.SetToolTip(cmbVideoCodec, "Codec the phone uses to compress the image. If you pick nothing, scrcpy uses its default (h264).");
        toolTip.SetToolTip(cmbVideoSource, "What to capture: 'display' is the normal screen. 'camera' uses one of the phone cameras instead of the screen (requires Android 12+).");
        toolTip.SetToolTip(cmbDisplayOrientation, "Rotates or flips the displayed image. The numbers are degrees of clockwise rotation; 'flip' mirrors it.");
    }

    private void BuildAudioTab()
    {
        var pnl = NewTabPanel(tabAudio);

        var grpAudio = new GroupBox { Text = "Audio", Location = new Point(6, 6), Size = new Size(560, 115) };
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

        SetCue(txtAudioBitrate, "e.g. 128K");
        toolTip.SetToolTip(cmbAudioSource, "Where the audio comes from: 'output' is all the sound of the phone (the default), 'mic' is the microphone, and so on.");
        toolTip.SetToolTip(cmbAudioCodec, "Audio compression format. Defaults to 'opus'.");
        toolTip.SetToolTip(txtAudioBitrate, "Audio quality: a number followed by K or M. Defaults to 128K.");
    }

    private void BuildWindowTab()
    {
        var pnl = NewTabPanel(tabWindow);

        // --- How it opens ---
        var grpMode = new GroupBox { Text = "How the window opens", Location = new Point(6, 6), Size = new Size(560, 70) };
        rbNormal = new RadioButton { Text = "Normal", Location = new Point(10, 30), Checked = true, AutoSize = true };
        rbFullscreen = new RadioButton { Text = "Fullscreen", Location = new Point(100, 30), AutoSize = true };
        rbBorderless = new RadioButton { Text = "Borderless (for OBS)", Location = new Point(210, 30), AutoSize = true };
        rbReadOnly = new RadioButton { Text = "Read-only (no control)", Location = new Point(370, 30), AutoSize = true };
        grpMode.Controls.Add(rbNormal); grpMode.Controls.Add(rbFullscreen);
        grpMode.Controls.Add(rbBorderless); grpMode.Controls.Add(rbReadOnly);
        toolTip.SetToolTip(rbReadOnly, "Mirrors the phone without sending it any click or keystroke.");
        pnl.Controls.Add(grpMode);

        // --- Position and title ---
        var grpWindow = new GroupBox { Text = "Position, size and title", Location = new Point(6, 84), Size = new Size(560, 115) };

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

        // --- Apps and displays ---
        var grpApps = new GroupBox { Text = "What to show in it", Location = new Point(6, 207), Size = new Size(560, 115) };

        var lblSa = new Label { Text = "Start app:", Location = new Point(10, 26), AutoSize = true };
        txtStartApp = new TextBox { Location = new Point(95, 23), Size = new Size(230, 20) };
        SetCue(txtStartApp, "e.g. org.videolan.vlc");
        toolTip.SetToolTip(txtStartApp, "Launches that app on the phone when connecting. Takes the package name. "
            + "With a leading '?' it searches by name (e.g. ?VLC). 'Ask scrcpy' can list them.");
        grpApps.Controls.Add(lblSa); grpApps.Controls.Add(txtStartApp);

        var lblNd = new Label { Text = "New display:", Location = new Point(10, 56), AutoSize = true };
        txtNewDisplay = new TextBox { Location = new Point(110, 53), Size = new Size(130, 20) };
        SetCue(txtNewDisplay, "e.g. 1920x1080");
        toolTip.SetToolTip(txtNewDisplay, "Creates a new virtual display on the phone instead of mirroring the real one. "
            + "Format: width x height, optionally /dpi (e.g. 1920x1080/240). Empty = default size.");
        var lblDi = new Label { Text = "or existing display (id):", Location = new Point(260, 56), AutoSize = true };
        txtDisplayId = new TextBox { Location = new Point(410, 53), Size = new Size(60, 20) };
        SetCue(txtDisplayId, "e.g. 0");
        toolTip.SetToolTip(txtDisplayId, "Mirrors a specific display of the phone. 'Ask scrcpy' lists the available "
            + "ids. Cannot be combined with 'New display'.");
        grpApps.Controls.Add(lblNd); grpApps.Controls.Add(txtNewDisplay);
        grpApps.Controls.Add(lblDi); grpApps.Controls.Add(txtDisplayId);

        var lblAnote = new Label { Text = "'New display' and 'existing display' are mutually exclusive: use only one.", Location = new Point(10, 85), AutoSize = true, ForeColor = SystemColors.GrayText };
        grpApps.Controls.Add(lblAnote);

        pnl.Controls.Add(grpApps);
    }

    private void BuildRecordingTab()
    {
        var pnl = NewTabPanel(tabRecording);

        // Recording used to be spread over three tabs: the checkbox on Basic, the format
        // on Window and capture, and the time limit under Advanced/Other.
        var grp = new GroupBox { Text = "Record the session to a file", Location = new Point(6, 6), Size = new Size(560, 160) };

        cbRecord = new CheckBox { Text = "Record this session", Location = new Point(10, 25), AutoSize = true };
        toolTip.SetToolTip(cbRecord, "Saves the session to a file named with the date and time, in the "
            + "Recordings folder next to the .exe.");
        grp.Controls.Add(cbRecord);

        var lblRf = new Label { Text = "Format:", Location = new Point(10, 58), AutoSize = true };
        cmbRecordFormat = NewCombo(new[] { "mp4", "mkv", "m4a", "mka", "opus", "aac", "flac", "wav" }, new Point(75, 55), 100);
        toolTip.SetToolTip(cmbRecordFormat, "Format of the recorded file. By default it is inferred from the "
            + "file extension (.mp4). The audio-only ones (m4a, opus, flac, wav) require video to be disabled.");
        var lblRo = new Label { Text = "Orientation:", Location = new Point(210, 58), AutoSize = true };
        cmbRecordOrientation = NewCombo(new[] { "0", "90", "180", "270", "flip0", "flip90", "flip180", "flip270" }, new Point(295, 55), 120);
        toolTip.SetToolTip(cmbRecordOrientation, "Rotation applied to the recorded file. It does not affect what you "
            + "see on screen, only the recording.");
        grp.Controls.Add(lblRf); grp.Controls.Add(cmbRecordFormat);
        grp.Controls.Add(lblRo); grp.Controls.Add(cmbRecordOrientation);

        var lblTl = new Label { Text = "Time limit (s):", Location = new Point(10, 90), AutoSize = true };
        txtTimeLimit = new TextBox { Location = new Point(110, 87), Size = new Size(60, 20) };
        SetCue(txtTimeLimit, "e.g. 600");
        toolTip.SetToolTip(txtTimeLimit, "Stops the session automatically after this many seconds, recording or not. "
            + "Handy for fixed-length recordings.");
        grp.Controls.Add(lblTl); grp.Controls.Add(txtTimeLimit);

        // The button that opens the folder belongs next to the setting that fills it.
        btnRecordings = new Button { Text = "Open Recordings folder", Location = new Point(10, 120), Size = new Size(180, 28) };
        btnRecordings.Click += (s, e) => OpenRecordingsFolder();
        grp.Controls.Add(btnRecordings);

        var lblNote = new Label
        {
            Text = "Files are named with the date and time, next to the .exe.",
            Location = new Point(200, 126),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };
        grp.Controls.Add(lblNote);

        pnl.Controls.Add(grp);
    }

    private void BuildOtherTab()
    {
        var pnl = NewTabPanel(tabOther);

        // --- Control and input ---
        var grpControl = new GroupBox { Text = "Control and input", Location = new Point(6, 6), Size = new Size(560, 145) };
        cbShowTouches = new CheckBox { Text = "Show touches", Location = new Point(10, 22), AutoSize = true };
        cbStayAwake = new CheckBox { Text = "Keep screen awake", Location = new Point(150, 22), AutoSize = true };
        cbOtg = new CheckBox { Text = "OTG mode", Location = new Point(330, 22), AutoSize = true };
        grpControl.Controls.Add(cbShowTouches);
        grpControl.Controls.Add(cbStayAwake);
        grpControl.Controls.Add(cbOtg);

        cbTurnScreenOff = new CheckBox { Text = "Turn screen off on start", Location = new Point(10, 48), AutoSize = true };
        cbPowerOffOnClose = new CheckBox { Text = "Turn screen off on close", Location = new Point(220, 48), AutoSize = true };
        grpControl.Controls.Add(cbTurnScreenOff);
        grpControl.Controls.Add(cbPowerOffOnClose);

        cbNoPowerOn = new CheckBox { Text = "Do not power on at start", Location = new Point(10, 74), AutoSize = true };
        cbKeepActive = new CheckBox { Text = "Simulate activity", Location = new Point(220, 74), AutoSize = true };
        grpControl.Controls.Add(cbNoPowerOn);
        grpControl.Controls.Add(cbKeepActive);

        var lblKeyboard = new Label { Text = "Keyboard:", Location = new Point(10, 108), AutoSize = true };
        cmbKeyboard = NewCombo(new[] { "disabled", "sdk", "uhid", "aoa" }, new Point(80, 105), 100);
        var lblMouse = new Label { Text = "Mouse:", Location = new Point(190, 108), AutoSize = true };
        cmbMouse = NewCombo(new[] { "disabled", "sdk", "uhid", "aoa" }, new Point(240, 105), 100);
        var lblGamepad = new Label { Text = "Gamepad:", Location = new Point(355, 108), AutoSize = true };
        cmbGamepad = NewCombo(new[] { "disabled", "uhid", "aoa" }, new Point(420, 105), 100);
        grpControl.Controls.Add(lblKeyboard); grpControl.Controls.Add(cmbKeyboard);
        grpControl.Controls.Add(lblMouse); grpControl.Controls.Add(cmbMouse);
        grpControl.Controls.Add(lblGamepad); grpControl.Controls.Add(cmbGamepad);

        pnl.Controls.Add(grpControl);

        // --- Diagnostics ---
        var grpOther = new GroupBox { Text = "Diagnostics", Location = new Point(6, 159), Size = new Size(560, 120) };
        var lblVerbosity = new Label { Text = "Verbosity:", Location = new Point(10, 22), AutoSize = true };
        cmbVerbosity = NewCombo(new[] { "verbose", "debug", "info", "warn", "error" }, new Point(80, 19), 110);
        cbPrintFps = new CheckBox { Text = "FPS counter", Location = new Point(210, 22), AutoSize = true };
        cbNoClipboardSync = new CheckBox { Text = "No clipboard sync", Location = new Point(330, 22), AutoSize = true };
        grpOther.Controls.Add(lblVerbosity); grpOther.Controls.Add(cmbVerbosity);
        grpOther.Controls.Add(cbPrintFps);
        grpOther.Controls.Add(cbNoClipboardSync);

        cbKillAdbOnClose = new CheckBox { Text = "Kill adb on close", Location = new Point(10, 50), AutoSize = true };
        grpOther.Controls.Add(cbKillAdbOnClose);

        // Asking scrcpy what the phone supports is diagnostics, so it lives with the rest
        // of it instead of competing with Start and Stop for attention.
        var lblTools = new Label { Text = "Ask scrcpy:", Location = new Point(10, 87), AutoSize = true };
        cmbTools = new ComboBox { Location = new Point(85, 84), Size = new Size(345, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        cmbTools.Items.AddRange(new object[] {
            "scrcpy version",
            "List encoders",
            "List cameras",
            "List camera sizes",
            "List displays",
            "List installed apps"
        });
        cmbTools.SelectedIndex = 0;
        btnRunTool = new Button { Text = "Run", Location = new Point(440, 83), Size = new Size(100, 25) };
        btnRunTool.Click += (s, e) => RunTool();
        toolTip.SetToolTip(cmbTools, "Runs scrcpy just to ask it something and prints the answer in the log. "
            + "Nothing is mirrored and no setting on any tab is used.");
        grpOther.Controls.Add(lblTools); grpOther.Controls.Add(cmbTools); grpOther.Controls.Add(btnRunTool);

        pnl.Controls.Add(grpOther);

        btnClearAdvanced = new Button { Text = "Clear every option", Location = new Point(6, 287), Size = new Size(200, 26) };
        btnClearAdvanced.Click += (s, e) => ClearAdvanced();
        toolTip.SetToolTip(btnClearAdvanced, "Resets every field on every tab except the connection, so nothing is "
            + "left set on a tab you are not looking at.");
        pnl.Controls.Add(btnClearAdvanced);

        lblExtra = new Label { Text = "Extra flags not listed anywhere above (appended as typed):", Location = new Point(6, 321), AutoSize = true };
        pnl.Controls.Add(lblExtra);

        txtExtra = new TextBox { Location = new Point(6, 345), Size = new Size(560, 23) };
        pnl.Controls.Add(txtExtra);

        SetCue(txtExtra, "e.g. --push-target=/sdcard/Download/");
        toolTip.SetToolTip(cmbKeyboard, "How key presses are sent to the phone.\n'sdk' = normal, recommended. 'uhid'/'aoa' simulate a physical keyboard, for special cases only.");
        toolTip.SetToolTip(cmbMouse, "How mouse clicks and movements are sent to the phone.\n'sdk' = normal, recommended.");
        toolTip.SetToolTip(cmbGamepad, "How the buttons of a physical controller plugged into the PC are sent. Requires a controller to be connected.");
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
        Log("Every option reset, on all tabs except the connection.");
    }

    private void ApplyLowLatencyPreset()
    {
        txtBitrate.Text = "2M";
        txtMaxSize.Text = "800";
        txtMaxFps.Text = "30";
        tabControl.SelectedTab = tabVideo;
        Log("Low-latency preset applied: bitrate 2M, max size 800, 30 fps (editable on the Video tab).");
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
        Log("Then close this window, open it again and press 'Refresh'.");
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
                 + "Clear one of the two on the 'Window' tab.";

        if (HasText(txtCameraId) && ComboValue(cmbCameraFacing) != "")
            return "'Camera id' and 'Facing' cannot be used at the same time. Clear one of the two "
                 + "in the camera group on the 'Video' tab.";

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
        Log("adb server stopped. Note: it will restart on its own as soon as any adb or scrcpy action runs (including 'Refresh').");
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
