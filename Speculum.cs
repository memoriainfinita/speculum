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

    private void SetCue(TextBox tb, string ejemplo)
    {
        SendMessage(tb.Handle, EM_SETCUEBANNER, IntPtr.Zero, ejemplo);
    }

    private ToolTip toolTip;

    private Label lblEstado;
    private Button btnRefrescar;

    private TabControl tabControl;
    private TabPage tabBasico, tabAvanzado, tabVentana, tabCamara;

    // Basico
    private GroupBox grpModo;
    private RadioButton rbNormal, rbFullscreen, rbBorderless, rbSoloLectura;

    private GroupBox grpConexion;
    private RadioButton rbUsb, rbWifi;
    private Button btnConectarWifi, btnPresetBajaLatencia;

    private GroupBox grpOpciones;
    private CheckBox cbShowTouches, cbStayAwake, cbRecord;

    // Avanzado - Video
    private CheckBox cbNoVideo, cbNoVideoPlayback;
    private TextBox txtMaxSize, txtBitrate, txtMaxFps, txtCrop;
    private ComboBox cmbVideoCodec, cmbVideoSource, cmbDisplayOrientation;

    // Avanzado - Audio
    private CheckBox cbNoAudio, cbNoAudioPlayback, cbAudioDup;
    private ComboBox cmbAudioSource, cmbAudioCodec;
    private TextBox txtAudioBitrate;

    // Avanzado - Control
    private CheckBox cbOtg, cbTurnScreenOff, cbKeepActive, cbPowerOffOnClose, cbNoPowerOn;
    private ComboBox cmbKeyboard, cmbMouse, cmbGamepad;

    // Avanzado - Otros
    private TextBox txtTimeLimit;
    private ComboBox cmbVerbosity;
    private CheckBox cbPrintFps, cbNoClipboardSync, cbKillAdbOnClose;

    // Ventana y captura
    private TextBox txtWindowX, txtWindowY, txtWindowWidth, txtWindowHeight, txtWindowTitle;
    private CheckBox cbNoWindow;
    private ComboBox cmbRecordFormat, cmbRecordOrientation;
    private TextBox txtStartApp, txtNewDisplay, txtDisplayId;

    // Camara
    private TextBox txtCameraId, txtCameraSize, txtCameraFps, txtCameraAr;
    private ComboBox cmbCameraFacing;
    private CheckBox cbCameraHighSpeed, cbCameraTorch;
    private TextBox txtCameraZoom;

    private Button btnLimpiarAvanzado;
    private Label lblExtra;
    private TextBox txtExtra;

    private Button btnAbrir, btnCerrar, btnReiniciarAdb, btnCerrarAdb, btnCarpeta;

    private ComboBox cmbHerramientas;
    private Button btnHerramienta;

    private Label lblLog;
    private TextBox txtLog;

    // El servidor adb es un demonio unico de la maquina: puede estar en uso por Android
    // Studio, otra terminal u otra ventana de esta app. Solo se detiene al salir si lo
    // arranco esta sesion, para dejar el sistema como se encontro.
    private bool adbYaEstaba;

    public LauncherForm()
    {
        Text = "Speculum";
        ClientSize = new Size(620, 780);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(640, 680);
        StartPosition = FormStartPosition.CenterScreen;

        toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 300, ReshowDelay = 100, ShowAlways = true };

        lblEstado = new Label { Text = "Comprobando estado...", Location = new Point(10, 12), Size = new Size(440, 20) };
        Controls.Add(lblEstado);

        btnRefrescar = new Button { Text = "Actualizar estado", Location = new Point(470, 8), Size = new Size(140, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnRefrescar.Click += (s, e) => Refrescar();
        Controls.Add(btnRefrescar);

        tabControl = new TabControl { Location = new Point(10, 40), Size = new Size(600, 380), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        tabBasico = new TabPage("Basico");
        tabAvanzado = new TabPage("Avanzado");
        tabVentana = new TabPage("Ventana y captura");
        tabCamara = new TabPage("Camara");
        tabControl.TabPages.Add(tabBasico);
        tabControl.TabPages.Add(tabAvanzado);
        tabControl.TabPages.Add(tabVentana);
        tabControl.TabPages.Add(tabCamara);
        Controls.Add(tabControl);

        ConstruirTabBasico();
        ConstruirTabAvanzado();
        ConstruirTabVentana();
        ConstruirTabCamara();

        int y = 430;
        btnAbrir = new Button { Text = "Abrir scrcpy", Location = new Point(10, y), Size = new Size(120, 32) };
        btnAbrir.Click += (s, e) => Abrir();
        Controls.Add(btnAbrir);

        btnCerrar = new Button { Text = "Cerrar scrcpy", Location = new Point(140, y), Size = new Size(120, 32) };
        btnCerrar.Click += (s, e) => Cerrar();
        Controls.Add(btnCerrar);

        btnCarpeta = new Button { Text = "Grabaciones", Location = new Point(270, y), Size = new Size(120, 32) };
        btnCarpeta.Click += (s, e) => AbrirCarpetaGrabaciones();
        Controls.Add(btnCarpeta);

        y += 38;
        btnReiniciarAdb = new Button { Text = "Reiniciar ADB", Location = new Point(10, y), Size = new Size(120, 32) };
        btnReiniciarAdb.Click += (s, e) => ReiniciarAdb();
        Controls.Add(btnReiniciarAdb);

        btnCerrarAdb = new Button { Text = "Cerrar ADB", Location = new Point(140, y), Size = new Size(120, 32) };
        btnCerrarAdb.Click += (s, e) => CerrarAdb();
        Controls.Add(btnCerrarAdb);

        y += 42;
        cmbHerramientas = new ComboBox { Location = new Point(10, y + 1), Size = new Size(380, 23), DropDownStyle = ComboBoxStyle.DropDownList };
        cmbHerramientas.Items.AddRange(new object[] {
            "Version de scrcpy",
            "Listar codificadores",
            "Listar camaras",
            "Listar tamanos de camara",
            "Listar pantallas",
            "Listar apps instaladas"
        });
        cmbHerramientas.SelectedIndex = 0;
        Controls.Add(cmbHerramientas);

        btnHerramienta = new Button { Text = "Ejecutar", Location = new Point(400, y), Size = new Size(100, 25) };
        btnHerramienta.Click += (s, e) => EjecutarHerramienta();
        Controls.Add(btnHerramienta);

        y += 32;
        lblLog = new Label { Text = "Registro:", Location = new Point(10, y), AutoSize = true };
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
            // Antes de Refrescar(), que consulta adb y por tanto lo arrancaria.
            adbYaEstaba = Process.GetProcessesByName("adb").Length > 0;
            Refrescar();
        };

        FormClosing += (s, e) => DetenerAdbSiLoArrancamos();
    }

    private void ConstruirTabBasico()
    {
        grpModo = new GroupBox { Text = "Modo de apertura", Location = new Point(6, 6), Size = new Size(285, 150) };
        rbNormal = new RadioButton { Text = "Normal", Location = new Point(10, 22), Checked = true, AutoSize = true };
        rbFullscreen = new RadioButton { Text = "Pantalla completa", Location = new Point(10, 48), AutoSize = true };
        rbBorderless = new RadioButton { Text = "Sin bordes (para OBS)", Location = new Point(10, 74), AutoSize = true };
        rbSoloLectura = new RadioButton { Text = "Solo lectura (sin control)", Location = new Point(10, 100), AutoSize = true };
        grpModo.Controls.Add(rbNormal);
        grpModo.Controls.Add(rbFullscreen);
        grpModo.Controls.Add(rbBorderless);
        grpModo.Controls.Add(rbSoloLectura);
        tabBasico.Controls.Add(grpModo);

        grpConexion = new GroupBox { Text = "Conexion", Location = new Point(300, 6), Size = new Size(285, 150) };
        rbUsb = new RadioButton { Text = "USB", Location = new Point(10, 22), Checked = true, AutoSize = true };
        rbWifi = new RadioButton { Text = "WiFi", Location = new Point(10, 48), AutoSize = true };
        btnConectarWifi = new Button { Text = "Emparejar por WiFi (usa USB)", Location = new Point(10, 76), Size = new Size(260, 26) };
        btnConectarWifi.Click += (s, e) => ConectarWifi();
        btnPresetBajaLatencia = new Button { Text = "Preset: baja latencia WiFi", Location = new Point(10, 106), Size = new Size(260, 26) };
        btnPresetBajaLatencia.Click += (s, e) => AplicarPresetBajaLatencia();
        grpConexion.Controls.Add(rbUsb);
        grpConexion.Controls.Add(rbWifi);
        grpConexion.Controls.Add(btnConectarWifi);
        grpConexion.Controls.Add(btnPresetBajaLatencia);
        tabBasico.Controls.Add(grpConexion);

        grpOpciones = new GroupBox { Text = "Opciones", Location = new Point(6, 162), Size = new Size(579, 70) };
        cbShowTouches = new CheckBox { Text = "Mostrar toques", Location = new Point(10, 28), AutoSize = true };
        cbStayAwake = new CheckBox { Text = "Mantener pantalla activa", Location = new Point(160, 28), AutoSize = true };
        cbRecord = new CheckBox { Text = "Grabar esta sesion", Location = new Point(360, 28), AutoSize = true };
        grpOpciones.Controls.Add(cbShowTouches);
        grpOpciones.Controls.Add(cbStayAwake);
        grpOpciones.Controls.Add(cbRecord);
        tabBasico.Controls.Add(grpOpciones);
    }

    private ComboBox NuevoCombo(string[] valores, Point loc, int width)
    {
        var c = new ComboBox { Location = loc, Size = new Size(width, 21), DropDownStyle = ComboBoxStyle.DropDownList };
        c.Items.Add("(por defecto)");
        c.Items.AddRange(valores);
        c.SelectedIndex = 0;
        return c;
    }

    private string ComboValor(ComboBox c)
    {
        if (c.SelectedIndex <= 0) return "";
        return c.SelectedItem.ToString();
    }

    private void ConstruirTabVentana()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabVentana.Controls.Add(pnl);

        int y = 6;

        // --- Ventana ---
        var grpVentana = new GroupBox { Text = "Ventana de scrcpy", Location = new Point(6, y), Size = new Size(560, 115) };

        var lblWx = new Label { Text = "Posicion X:", Location = new Point(10, 26), AutoSize = true };
        txtWindowX = new TextBox { Location = new Point(85, 23), Size = new Size(60, 20) };
        var lblWy = new Label { Text = "Y:", Location = new Point(160, 26), AutoSize = true };
        txtWindowY = new TextBox { Location = new Point(180, 23), Size = new Size(60, 20) };
        var lblWw = new Label { Text = "Ancho:", Location = new Point(260, 26), AutoSize = true };
        txtWindowWidth = new TextBox { Location = new Point(305, 23), Size = new Size(60, 20) };
        var lblWh = new Label { Text = "Alto:", Location = new Point(380, 26), AutoSize = true };
        txtWindowHeight = new TextBox { Location = new Point(415, 23), Size = new Size(60, 20) };
        SetCue(txtWindowX, "ej: 0"); SetCue(txtWindowY, "ej: 0");
        SetCue(txtWindowWidth, "ej: 1280"); SetCue(txtWindowHeight, "ej: 720");
        string ayudaPos = "Posicion y tamano de la ventana al abrirse, en pixeles. Util para dejarla "
                        + "siempre en el mismo sitio y capturarla con OBS sin recolocarla cada vez.";
        toolTip.SetToolTip(txtWindowX, ayudaPos); toolTip.SetToolTip(txtWindowY, ayudaPos);
        toolTip.SetToolTip(txtWindowWidth, ayudaPos); toolTip.SetToolTip(txtWindowHeight, ayudaPos);
        grpVentana.Controls.Add(lblWx); grpVentana.Controls.Add(txtWindowX);
        grpVentana.Controls.Add(lblWy); grpVentana.Controls.Add(txtWindowY);
        grpVentana.Controls.Add(lblWw); grpVentana.Controls.Add(txtWindowWidth);
        grpVentana.Controls.Add(lblWh); grpVentana.Controls.Add(txtWindowHeight);

        var lblWt = new Label { Text = "Titulo:", Location = new Point(10, 58), AutoSize = true };
        txtWindowTitle = new TextBox { Location = new Point(85, 55), Size = new Size(240, 20) };
        SetCue(txtWindowTitle, "ej: Movil (OBS)");
        toolTip.SetToolTip(txtWindowTitle, "Texto de la barra de titulo. Ayuda a distinguir la ventana "
            + "cuando hay varias abiertas, y a seleccionarla en OBS por titulo.");
        grpVentana.Controls.Add(lblWt); grpVentana.Controls.Add(txtWindowTitle);

        cbNoWindow = new CheckBox { Text = "Sin ventana (--no-window)", Location = new Point(10, 85), AutoSize = true };
        toolTip.SetToolTip(cbNoWindow, "No abre ninguna ventana. Solo tiene sentido combinado con grabacion "
            + "o con control por OTG: si no, no veras nada.");
        grpVentana.Controls.Add(cbNoWindow);

        pnl.Controls.Add(grpVentana);
        y += grpVentana.Height + 8;

        // --- Grabacion ---
        var grpGrab = new GroupBox { Text = "Grabacion", Location = new Point(6, y), Size = new Size(560, 80) };
        var lblRf = new Label { Text = "Formato:", Location = new Point(10, 30), AutoSize = true };
        cmbRecordFormat = NuevoCombo(new[] { "mp4", "mkv", "m4a", "mka", "opus", "aac", "flac", "wav" }, new Point(75, 27), 100);
        toolTip.SetToolTip(cmbRecordFormat, "Formato del archivo grabado. Por defecto se deduce de la extension "
            + "del nombre (.mp4). Los de solo audio (m4a, opus, flac, wav) requieren desactivar el video.");
        var lblRo = new Label { Text = "Orientacion:", Location = new Point(210, 30), AutoSize = true };
        cmbRecordOrientation = NuevoCombo(new[] { "0", "90", "180", "270", "flip0", "flip90", "flip180", "flip270" }, new Point(295, 27), 120);
        toolTip.SetToolTip(cmbRecordOrientation, "Rotacion aplicada al archivo grabado. No afecta a lo que ves "
            + "en pantalla, solo a la grabacion.");
        var lblRnota = new Label { Text = "Se aplican a 'Grabar esta sesion', en la pestana Basico.", Location = new Point(10, 56), AutoSize = true, ForeColor = SystemColors.GrayText };
        grpGrab.Controls.Add(lblRf); grpGrab.Controls.Add(cmbRecordFormat);
        grpGrab.Controls.Add(lblRo); grpGrab.Controls.Add(cmbRecordOrientation);
        grpGrab.Controls.Add(lblRnota);
        pnl.Controls.Add(grpGrab);
        y += grpGrab.Height + 8;

        // --- Apps y pantallas ---
        var grpApps = new GroupBox { Text = "Apps y pantallas", Location = new Point(6, y), Size = new Size(560, 115) };

        var lblSa = new Label { Text = "Abrir app:", Location = new Point(10, 26), AutoSize = true };
        txtStartApp = new TextBox { Location = new Point(95, 23), Size = new Size(230, 20) };
        SetCue(txtStartApp, "ej: org.videolan.vlc");
        toolTip.SetToolTip(txtStartApp, "Lanza esa app en el movil al conectar. Acepta el nombre de paquete. "
            + "Con un '?' delante busca por nombre (ej: ?VLC). Usa 'Listar apps instaladas' para verlos.");
        grpApps.Controls.Add(lblSa); grpApps.Controls.Add(txtStartApp);

        var lblNd = new Label { Text = "Pantalla nueva:", Location = new Point(10, 56), AutoSize = true };
        txtNewDisplay = new TextBox { Location = new Point(110, 53), Size = new Size(130, 20) };
        SetCue(txtNewDisplay, "ej: 1920x1080");
        toolTip.SetToolTip(txtNewDisplay, "Crea una pantalla virtual nueva en el movil en vez de espejar la real. "
            + "Formato: ancho x alto, opcionalmente /dpi (ej: 1920x1080/240). Vacio = tamano por defecto.");
        var lblDi = new Label { Text = "o pantalla existente (id):", Location = new Point(260, 56), AutoSize = true };
        txtDisplayId = new TextBox { Location = new Point(410, 53), Size = new Size(60, 20) };
        SetCue(txtDisplayId, "ej: 0");
        toolTip.SetToolTip(txtDisplayId, "Espeja una pantalla concreta del movil. Usa 'Listar pantallas' para ver "
            + "los ids disponibles. No se puede combinar con 'Pantalla nueva'.");
        grpApps.Controls.Add(lblNd); grpApps.Controls.Add(txtNewDisplay);
        grpApps.Controls.Add(lblDi); grpApps.Controls.Add(txtDisplayId);

        var lblAnota = new Label { Text = "'Pantalla nueva' y 'pantalla existente' son excluyentes: usa solo una.", Location = new Point(10, 85), AutoSize = true, ForeColor = SystemColors.GrayText };
        grpApps.Controls.Add(lblAnota);

        pnl.Controls.Add(grpApps);
    }

    private void ConstruirTabCamara()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabCamara.Controls.Add(pnl);

        var lblAviso = new Label
        {
            Text = "Al rellenar cualquier campo de esta pestana, la fuente de video pasa a 'camera' automaticamente.",
            Location = new Point(8, 8),
            Size = new Size(560, 18),
            ForeColor = SystemColors.GrayText
        };
        pnl.Controls.Add(lblAviso);

        var grp = new GroupBox { Text = "Camara del movil", Location = new Point(6, 30), Size = new Size(560, 150) };

        var lblId = new Label { Text = "Id de camara:", Location = new Point(10, 26), AutoSize = true };
        txtCameraId = new TextBox { Location = new Point(100, 23), Size = new Size(60, 20) };
        SetCue(txtCameraId, "ej: 0");
        toolTip.SetToolTip(txtCameraId, "Id de la camara a usar. 'Listar camaras' muestra los disponibles. "
            + "Si se indica, no hace falta el campo 'Cara'.");
        var lblFacing = new Label { Text = "Cara:", Location = new Point(190, 26), AutoSize = true };
        cmbCameraFacing = NuevoCombo(new[] { "front", "back", "external" }, new Point(230, 23), 110);
        toolTip.SetToolTip(cmbCameraFacing, "Elige la camara por su posicion en vez de por id: frontal, trasera "
            + "o externa. No se combina con 'Id de camara'.");
        grp.Controls.Add(lblId); grp.Controls.Add(txtCameraId);
        grp.Controls.Add(lblFacing); grp.Controls.Add(cmbCameraFacing);

        var lblSize = new Label { Text = "Tamano:", Location = new Point(10, 58), AutoSize = true };
        txtCameraSize = new TextBox { Location = new Point(100, 55), Size = new Size(110, 20) };
        SetCue(txtCameraSize, "ej: 1920x1080");
        toolTip.SetToolTip(txtCameraSize, "Resolucion de captura. Usa 'Listar tamanos de camara' en el desplegable "
            + "de herramientas para ver cuales admite tu movil.");
        var lblFps = new Label { Text = "FPS:", Location = new Point(230, 58), AutoSize = true };
        txtCameraFps = new TextBox { Location = new Point(270, 55), Size = new Size(60, 20) };
        SetCue(txtCameraFps, "ej: 30");
        var lblAr = new Label { Text = "Relacion:", Location = new Point(350, 58), AutoSize = true };
        txtCameraAr = new TextBox { Location = new Point(415, 55), Size = new Size(80, 20) };
        SetCue(txtCameraAr, "ej: 16:9");
        toolTip.SetToolTip(txtCameraAr, "Relacion de aspecto: 16:9, 4:3, o un numero como 1.6. "
            + "Se aplica recortando lo que sobra.");
        grp.Controls.Add(lblSize); grp.Controls.Add(txtCameraSize);
        grp.Controls.Add(lblFps); grp.Controls.Add(txtCameraFps);
        grp.Controls.Add(lblAr); grp.Controls.Add(txtCameraAr);

        var lblZoom = new Label { Text = "Zoom:", Location = new Point(10, 90), AutoSize = true };
        txtCameraZoom = new TextBox { Location = new Point(100, 87), Size = new Size(60, 20) };
        SetCue(txtCameraZoom, "ej: 2.0");
        toolTip.SetToolTip(txtCameraZoom, "Nivel de zoom optico/digital. 1.0 es sin zoom.");
        cbCameraHighSpeed = new CheckBox { Text = "Alta velocidad", Location = new Point(190, 89), AutoSize = true };
        toolTip.SetToolTip(cbCameraHighSpeed, "Activa el modo de grabacion a alta tasa de fotogramas del movil. "
            + "Restringe mucho los tamanos y fps admitidos.");
        cbCameraTorch = new CheckBox { Text = "Linterna encendida", Location = new Point(330, 89), AutoSize = true };
        toolTip.SetToolTip(cbCameraTorch, "Enciende el flash como luz continua mientras dura la captura.");
        grp.Controls.Add(lblZoom); grp.Controls.Add(txtCameraZoom);
        grp.Controls.Add(cbCameraHighSpeed);
        grp.Controls.Add(cbCameraTorch);

        var lblNota = new Label
        {
            Text = "La camara no permite control tactil: scrcpy captura video, no la pantalla del movil.",
            Location = new Point(10, 120),
            AutoSize = true,
            ForeColor = SystemColors.GrayText
        };
        grp.Controls.Add(lblNota);

        pnl.Controls.Add(grp);
    }

    private void ConstruirTabAvanzado()
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabAvanzado.Controls.Add(pnl);

        int y = 6;

        // --- Video ---
        var grpVideo = new GroupBox { Text = "Video avanzado", Location = new Point(6, y), Size = new Size(560, 145) };
        cbNoVideo = new CheckBox { Text = "Sin video", Location = new Point(10, 20), AutoSize = true };
        cbNoVideoPlayback = new CheckBox { Text = "Sin reproduccion en PC", Location = new Point(220, 20), AutoSize = true };
        grpVideo.Controls.Add(cbNoVideo);
        grpVideo.Controls.Add(cbNoVideoPlayback);

        var lblMaxSize = new Label { Text = "Tam. max (-m):", Location = new Point(10, 52), AutoSize = true };
        txtMaxSize = new TextBox { Location = new Point(120, 49), Size = new Size(70, 20) };
        var lblBitrate = new Label { Text = "Bitrate (-b):", Location = new Point(210, 52), AutoSize = true };
        txtBitrate = new TextBox { Location = new Point(295, 49), Size = new Size(70, 20) };
        var lblMaxFps = new Label { Text = "FPS max:", Location = new Point(385, 52), AutoSize = true };
        txtMaxFps = new TextBox { Location = new Point(450, 49), Size = new Size(60, 20) };
        grpVideo.Controls.Add(lblMaxSize); grpVideo.Controls.Add(txtMaxSize);
        grpVideo.Controls.Add(lblBitrate); grpVideo.Controls.Add(txtBitrate);
        grpVideo.Controls.Add(lblMaxFps); grpVideo.Controls.Add(txtMaxFps);

        var lblCodec = new Label { Text = "Codec video:", Location = new Point(10, 82), AutoSize = true };
        cmbVideoCodec = NuevoCombo(new[] { "h264", "h265", "av1", "vp8", "vp9" }, new Point(100, 79), 120);
        var lblVSource = new Label { Text = "Fuente:", Location = new Point(240, 82), AutoSize = true };
        cmbVideoSource = NuevoCombo(new[] { "display", "camera" }, new Point(290, 79), 110);
        grpVideo.Controls.Add(lblCodec); grpVideo.Controls.Add(cmbVideoCodec);
        grpVideo.Controls.Add(lblVSource); grpVideo.Controls.Add(cmbVideoSource);

        var lblCrop = new Label { Text = "Recorte (w:h:x:y):", Location = new Point(10, 112), AutoSize = true };
        txtCrop = new TextBox { Location = new Point(130, 109), Size = new Size(150, 20) };
        var lblOrient = new Label { Text = "Orientacion:", Location = new Point(300, 112), AutoSize = true };
        cmbDisplayOrientation = NuevoCombo(new[] { "0", "90", "180", "270", "flip0", "flip90", "flip180", "flip270" }, new Point(375, 109), 130);
        grpVideo.Controls.Add(lblCrop); grpVideo.Controls.Add(txtCrop);
        grpVideo.Controls.Add(lblOrient); grpVideo.Controls.Add(cmbDisplayOrientation);

        pnl.Controls.Add(grpVideo);
        y += grpVideo.Height + 8;

        // --- Audio ---
        var grpAudio = new GroupBox { Text = "Audio avanzado", Location = new Point(6, y), Size = new Size(560, 115) };
        cbNoAudio = new CheckBox { Text = "Sin audio", Location = new Point(10, 20), AutoSize = true };
        cbNoAudioPlayback = new CheckBox { Text = "Sin reproduccion en PC", Location = new Point(180, 20), AutoSize = true };
        cbAudioDup = new CheckBox { Text = "Duplicar audio", Location = new Point(390, 20), AutoSize = true };
        grpAudio.Controls.Add(cbNoAudio);
        grpAudio.Controls.Add(cbNoAudioPlayback);
        grpAudio.Controls.Add(cbAudioDup);

        var lblASource = new Label { Text = "Fuente audio:", Location = new Point(10, 52), AutoSize = true };
        cmbAudioSource = NuevoCombo(new[] { "output", "playback", "mic", "mic-unprocessed", "mic-camcorder", "mic-voice-recognition", "mic-voice-communication", "voice-call", "voice-call-uplink", "voice-call-downlink", "voice-performance" }, new Point(100, 49), 190);
        var lblACodec = new Label { Text = "Codec:", Location = new Point(300, 52), AutoSize = true };
        cmbAudioCodec = NuevoCombo(new[] { "opus", "aac", "flac", "raw" }, new Point(345, 49), 100);
        grpAudio.Controls.Add(lblASource); grpAudio.Controls.Add(cmbAudioSource);
        grpAudio.Controls.Add(lblACodec); grpAudio.Controls.Add(cmbAudioCodec);

        var lblABitrate = new Label { Text = "Bitrate audio:", Location = new Point(10, 82), AutoSize = true };
        txtAudioBitrate = new TextBox { Location = new Point(100, 79), Size = new Size(90, 20) };
        grpAudio.Controls.Add(lblABitrate); grpAudio.Controls.Add(txtAudioBitrate);

        pnl.Controls.Add(grpAudio);
        y += grpAudio.Height + 8;

        // --- Control ---
        var grpControl = new GroupBox { Text = "Control avanzado", Location = new Point(6, y), Size = new Size(560, 115) };
        cbOtg = new CheckBox { Text = "Modo OTG", Location = new Point(10, 20), AutoSize = true };
        cbTurnScreenOff = new CheckBox { Text = "Apagar pantalla al iniciar", Location = new Point(120, 20), AutoSize = true };
        cbKeepActive = new CheckBox { Text = "Simular actividad", Location = new Point(320, 20), AutoSize = true };
        grpControl.Controls.Add(cbOtg);
        grpControl.Controls.Add(cbTurnScreenOff);
        grpControl.Controls.Add(cbKeepActive);

        cbPowerOffOnClose = new CheckBox { Text = "Apagar pantalla al cerrar", Location = new Point(10, 46), AutoSize = true };
        cbNoPowerOn = new CheckBox { Text = "No encender al iniciar", Location = new Point(220, 46), AutoSize = true };
        grpControl.Controls.Add(cbPowerOffOnClose);
        grpControl.Controls.Add(cbNoPowerOn);

        var lblKeyboard = new Label { Text = "Teclado:", Location = new Point(10, 80), AutoSize = true };
        cmbKeyboard = NuevoCombo(new[] { "disabled", "sdk", "uhid", "aoa" }, new Point(70, 77), 100);
        var lblMouse = new Label { Text = "Raton:", Location = new Point(190, 80), AutoSize = true };
        cmbMouse = NuevoCombo(new[] { "disabled", "sdk", "uhid", "aoa" }, new Point(235, 77), 100);
        var lblGamepad = new Label { Text = "Gamepad:", Location = new Point(355, 80), AutoSize = true };
        cmbGamepad = NuevoCombo(new[] { "disabled", "uhid", "aoa" }, new Point(420, 77), 100);
        grpControl.Controls.Add(lblKeyboard); grpControl.Controls.Add(cmbKeyboard);
        grpControl.Controls.Add(lblMouse); grpControl.Controls.Add(cmbMouse);
        grpControl.Controls.Add(lblGamepad); grpControl.Controls.Add(cmbGamepad);

        pnl.Controls.Add(grpControl);
        y += grpControl.Height + 8;

        // --- Otros ---
        var grpOtros = new GroupBox { Text = "Otros", Location = new Point(6, y), Size = new Size(560, 85) };
        var lblTimeLimit = new Label { Text = "Tiempo limite (s):", Location = new Point(10, 22), AutoSize = true };
        txtTimeLimit = new TextBox { Location = new Point(130, 19), Size = new Size(60, 20) };
        var lblVerbosity = new Label { Text = "Verbosidad:", Location = new Point(220, 22), AutoSize = true };
        cmbVerbosity = NuevoCombo(new[] { "verbose", "debug", "info", "warn", "error" }, new Point(300, 19), 110);
        grpOtros.Controls.Add(lblTimeLimit); grpOtros.Controls.Add(txtTimeLimit);
        grpOtros.Controls.Add(lblVerbosity); grpOtros.Controls.Add(cmbVerbosity);

        cbPrintFps = new CheckBox { Text = "Contador FPS", Location = new Point(10, 50), AutoSize = true };
        cbNoClipboardSync = new CheckBox { Text = "Sin sincronizar portapapeles", Location = new Point(140, 50), AutoSize = true };
        cbKillAdbOnClose = new CheckBox { Text = "Matar adb al cerrar", Location = new Point(360, 50), AutoSize = true };
        grpOtros.Controls.Add(cbPrintFps);
        grpOtros.Controls.Add(cbNoClipboardSync);
        grpOtros.Controls.Add(cbKillAdbOnClose);

        pnl.Controls.Add(grpOtros);
        y += grpOtros.Height + 8;

        btnLimpiarAvanzado = new Button { Text = "Limpiar todo lo avanzado", Location = new Point(6, y), Size = new Size(200, 26) };
        btnLimpiarAvanzado.Click += (s, e) => LimpiarAvanzado();
        pnl.Controls.Add(btnLimpiarAvanzado);
        y += 34;

        lblExtra = new Label { Text = "Flags adicionales no listados arriba (se anaden tal cual):", Location = new Point(6, y), AutoSize = true };
        pnl.Controls.Add(lblExtra);
        y += 20;

        txtExtra = new TextBox { Location = new Point(6, y), Size = new Size(560, 23) };
        pnl.Controls.Add(txtExtra);

        // Ejemplos visibles dentro de las cajas vacias (desaparecen al escribir)
        SetCue(txtMaxSize, "ej: 1080");
        SetCue(txtBitrate, "ej: 8M");
        SetCue(txtMaxFps, "ej: 30");
        SetCue(txtCrop, "ej: 1080:1920:0:0");
        SetCue(txtAudioBitrate, "ej: 128K");
        SetCue(txtTimeLimit, "ej: 600 (10 min)");
        SetCue(txtExtra, "ej: --push-target=/sdcard/Download/");

        // Explicacion al pasar el raton por encima
        toolTip.SetToolTip(txtMaxSize, "Limita el ancho y alto maximo de la imagen en pixels (el otro lado se ajusta solo para mantener la proporcion).\nMenor valor = va mas fluido pero se ve peor. Vacio = sin limite.");
        toolTip.SetToolTip(txtBitrate, "Calidad/peso del video: numero seguido de K (miles) o M (millones) de bits por segundo.\nPor defecto 8M. Para WiFi con poca senal, prueba 2M.");
        toolTip.SetToolTip(txtMaxFps, "Fotogramas por segundo maximos de la captura. Vacio = sin limite. Valores tipicos: 30 o 60.");
        toolTip.SetToolTip(txtCrop, "Recorta la pantalla que se envia. Formato: ancho:alto:x:y en pixels, segun la orientacion natural del movil (normalmente vertical).");
        toolTip.SetToolTip(cmbVideoCodec, "Codec de video que usa el movil para comprimir la imagen. Si no eliges nada, usa el que scrcpy elija por defecto (h264).");
        toolTip.SetToolTip(cmbVideoSource, "Que capturar: 'display' es la pantalla normal. 'camera' usa una camara del movil en vez de la pantalla (requiere Android 12+).");
        toolTip.SetToolTip(cmbDisplayOrientation, "Rota o voltea la imagen mostrada. Los numeros son grados de giro en sentido horario; 'flip' voltea en espejo.");
        toolTip.SetToolTip(cmbAudioSource, "De donde sale el audio: 'output' es todo el sonido del movil (por defecto), 'mic' es el microfono, etc.");
        toolTip.SetToolTip(cmbAudioCodec, "Formato de compresion del audio. Por defecto 'opus'.");
        toolTip.SetToolTip(txtAudioBitrate, "Calidad del audio: numero seguido de K o M. Por defecto 128K.");
        toolTip.SetToolTip(cmbKeyboard, "Como se envian las pulsaciones de teclado al movil.\n'sdk' = normal, recomendado. 'uhid'/'aoa' simulan un teclado fisico, solo para casos especiales.");
        toolTip.SetToolTip(cmbMouse, "Como se envian los clics y movimientos del raton al movil.\n'sdk' = normal, recomendado.");
        toolTip.SetToolTip(cmbGamepad, "Como se envian las pulsaciones de un mando fisico conectado al PC. Requiere tener un mando conectado.");
        toolTip.SetToolTip(txtTimeLimit, "Corta el espejado/grabacion automaticamente al llegar a estos segundos. Util para grabaciones de duracion fija.");
        toolTip.SetToolTip(cmbVerbosity, "Cuanto detalle tecnico mostrar. Solo afecta a los mensajes internos, no a la imagen.");
        toolTip.SetToolTip(txtExtra, "Escribe aqui cualquier flag de scrcpy tal cual lo pondrias en una terminal, para lo que no este cubierto arriba.\nSe pueden poner varios separados por espacio. Ejemplo: --push-target=/sdcard/Download/");
    }

    private void LimpiarAvanzado()
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
        Log("Opciones avanzadas restablecidas (incluidas Ventana y captura, y Camara).");
    }

    private void AplicarPresetBajaLatencia()
    {
        txtBitrate.Text = "2M";
        txtMaxSize.Text = "800";
        txtMaxFps.Text = "30";
        tabControl.SelectedTab = tabAvanzado;
        Log("Preset de baja latencia aplicado: bitrate 2M, tamano max 800, 30 fps (editable en la pestana Avanzado).");
    }

    private void Log(string text)
    {
        txtLog.AppendText(text.TrimEnd() + Environment.NewLine);
        txtLog.SelectionStart = txtLog.Text.Length;
        txtLog.ScrollToCaret();
    }

    // Busca un ejecutable en el PATH del sistema. Devuelve null si no esta.
    private static string BuscarEnPath(string exe)
    {
        string path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;
        foreach (string dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                string completo = Path.Combine(dir.Trim(), exe);
                if (File.Exists(completo)) return completo;
            }
            catch { }   // entradas malformadas del PATH: se ignoran
        }
        return null;
    }

    // Sin scrcpy en el PATH, adb devuelve un "no se puede encontrar el archivo
    // especificado" que no le dice al usuario ni que falta ni como instalarlo.
    private bool ComprobarDependencias()
    {
        bool haySrcpy = BuscarEnPath("scrcpy.exe") != null;
        bool hayAdb = BuscarEnPath("adb.exe") != null;
        if (haySrcpy && hayAdb) return true;

        string falta = !haySrcpy && !hayAdb ? "scrcpy.exe ni adb.exe"
                     : !haySrcpy ? "scrcpy.exe" : "adb.exe";
        Log("=== No se encuentra " + falta + " en el PATH del sistema ===");
        Log("scrcpy no esta instalado, o su carpeta no esta en el PATH.");
        Log("Para instalarlo (incluye adb):    winget install Genymobile.scrcpy");
        Log("Despues cierra esta ventana, abrela de nuevo y pulsa 'Actualizar estado'.");
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
                // El servidor adb queda vivo como demonio y retiene un handle sobre el
                // directorio desde el que se lanzo, impidiendo renombrarlo o moverlo
                // hasta que muera. Con el cwd en temp, el pinchazo cae donde no molesta.
                WorkingDirectory = Path.GetTempPath()
            };

            // Los dos streams se leen a la vez, no uno detras de otro: leyendo stdout
            // hasta el final mientras stderr se queda sin vaciar, el hijo se bloquea al
            // llenar el buffer del pipe (~4 KB) y ninguno de los dos avanza. Pasaba con
            // salidas largas como --list-apps.
            var salida = new System.Text.StringBuilder();
            using (var p = new Process())
            using (var finOut = new System.Threading.ManualResetEvent(false))
            using (var finErr = new System.Threading.ManualResetEvent(false))
            {
                p.StartInfo = psi;
                DataReceivedEventHandler recoger = (s, e) =>
                {
                    if (e.Data == null) return;
                    lock (salida) salida.AppendLine(e.Data);
                };
                p.OutputDataReceived += (s, e) => { if (e.Data == null) finOut.Set(); else recoger(s, e); };
                p.ErrorDataReceived += (s, e) => { if (e.Data == null) finErr.Set(); else recoger(s, e); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    lock (salida) salida.AppendLine("(cancelado: mas de " + timeoutMs + " ms sin terminar)");
                }
                // Margen para que lleguen las ultimas lineas ya en vuelo
                finOut.WaitOne(2000);
                finErr.WaitOne(2000);
            }
            lock (salida) return salida.ToString().Trim();
        }
        catch (Exception ex)
        {
            return "ERROR ejecutando " + exe + ": " + ex.Message;
        }
    }

    private string BuildFlags()
    {
        var parts = new System.Collections.Generic.List<string>();

        if (rbUsb.Checked) parts.Add("-d");
        else if (rbWifi.Checked) parts.Add("-e");

        if (rbFullscreen.Checked) parts.Add("-f");
        else if (rbBorderless.Checked) parts.Add("--window-borderless --always-on-top");
        else if (rbSoloLectura.Checked) parts.Add("-n");

        if (cbShowTouches.Checked) parts.Add("-t");
        if (cbStayAwake.Checked) parts.Add("-w");

        if (cbRecord.Checked)
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Grabaciones");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "scrcpy_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".mp4");
            parts.Add("-r \"" + file + "\"");
        }

        // Video avanzado
        if (cbNoVideo.Checked) parts.Add("--no-video");
        if (cbNoVideoPlayback.Checked) parts.Add("--no-video-playback");
        if (txtMaxSize.Text.Trim() != "") parts.Add("-m " + txtMaxSize.Text.Trim());
        if (txtBitrate.Text.Trim() != "") parts.Add("-b " + txtBitrate.Text.Trim());
        if (txtMaxFps.Text.Trim() != "") parts.Add("--max-fps " + txtMaxFps.Text.Trim());
        if (ComboValor(cmbVideoCodec) != "") parts.Add("--video-codec=" + ComboValor(cmbVideoCodec));
        if (ComboValor(cmbVideoSource) != "") parts.Add("--video-source=" + ComboValor(cmbVideoSource));
        if (txtCrop.Text.Trim() != "") parts.Add("--crop " + txtCrop.Text.Trim());
        if (ComboValor(cmbDisplayOrientation) != "") parts.Add("--display-orientation=" + ComboValor(cmbDisplayOrientation));

        // Audio avanzado
        if (cbNoAudio.Checked) parts.Add("--no-audio");
        if (cbNoAudioPlayback.Checked) parts.Add("--no-audio-playback");
        if (cbAudioDup.Checked) parts.Add("--audio-dup");
        if (ComboValor(cmbAudioSource) != "") parts.Add("--audio-source=" + ComboValor(cmbAudioSource));
        if (ComboValor(cmbAudioCodec) != "") parts.Add("--audio-codec=" + ComboValor(cmbAudioCodec));
        if (txtAudioBitrate.Text.Trim() != "") parts.Add("--audio-bit-rate=" + txtAudioBitrate.Text.Trim());

        // Control avanzado
        if (cbOtg.Checked) parts.Add("--otg");
        if (cbTurnScreenOff.Checked) parts.Add("-S");
        if (cbKeepActive.Checked) parts.Add("--keep-active");
        if (cbPowerOffOnClose.Checked) parts.Add("--power-off-on-close");
        if (cbNoPowerOn.Checked) parts.Add("--no-power-on");
        if (ComboValor(cmbKeyboard) != "") parts.Add("--keyboard=" + ComboValor(cmbKeyboard));
        if (ComboValor(cmbMouse) != "") parts.Add("--mouse=" + ComboValor(cmbMouse));
        if (ComboValor(cmbGamepad) != "") parts.Add("--gamepad=" + ComboValor(cmbGamepad));

        // Otros
        if (txtTimeLimit.Text.Trim() != "") parts.Add("--time-limit=" + txtTimeLimit.Text.Trim());
        if (ComboValor(cmbVerbosity) != "") parts.Add("-V " + ComboValor(cmbVerbosity));
        if (cbPrintFps.Checked) parts.Add("--print-fps");
        if (cbNoClipboardSync.Checked) parts.Add("--no-clipboard-autosync");
        if (cbKillAdbOnClose.Checked) parts.Add("--kill-adb-on-close");

        // Ventana
        if (txtWindowX.Text.Trim() != "") parts.Add("--window-x=" + txtWindowX.Text.Trim());
        if (txtWindowY.Text.Trim() != "") parts.Add("--window-y=" + txtWindowY.Text.Trim());
        if (txtWindowWidth.Text.Trim() != "") parts.Add("--window-width=" + txtWindowWidth.Text.Trim());
        if (txtWindowHeight.Text.Trim() != "") parts.Add("--window-height=" + txtWindowHeight.Text.Trim());
        if (txtWindowTitle.Text.Trim() != "") parts.Add("--window-title=\"" + txtWindowTitle.Text.Trim() + "\"");
        if (cbNoWindow.Checked) parts.Add("--no-window");

        // Grabacion
        if (ComboValor(cmbRecordFormat) != "") parts.Add("--record-format=" + ComboValor(cmbRecordFormat));
        if (ComboValor(cmbRecordOrientation) != "") parts.Add("--record-orientation=" + ComboValor(cmbRecordOrientation));

        // Apps y pantallas
        if (txtStartApp.Text.Trim() != "") parts.Add("--start-app=" + txtStartApp.Text.Trim());
        if (txtNewDisplay.Text.Trim() != "") parts.Add("--new-display=" + txtNewDisplay.Text.Trim());
        else if (txtDisplayId.Text.Trim() != "") parts.Add("--display-id=" + txtDisplayId.Text.Trim());

        // Camara
        if (txtCameraId.Text.Trim() != "") parts.Add("--camera-id=" + txtCameraId.Text.Trim());
        if (ComboValor(cmbCameraFacing) != "") parts.Add("--camera-facing=" + ComboValor(cmbCameraFacing));
        if (txtCameraSize.Text.Trim() != "") parts.Add("--camera-size=" + txtCameraSize.Text.Trim());
        if (txtCameraFps.Text.Trim() != "") parts.Add("--camera-fps=" + txtCameraFps.Text.Trim());
        if (txtCameraAr.Text.Trim() != "") parts.Add("--camera-ar=" + txtCameraAr.Text.Trim());
        if (txtCameraZoom.Text.Trim() != "") parts.Add("--camera-zoom=" + txtCameraZoom.Text.Trim());
        if (cbCameraHighSpeed.Checked) parts.Add("--camera-high-speed");
        if (cbCameraTorch.Checked) parts.Add("--camera-torch");

        if (!string.IsNullOrWhiteSpace(txtExtra.Text)) parts.Add(txtExtra.Text.Trim());

        return string.Join(" ", parts);
    }

    private static bool HayTexto(TextBox tb)
    {
        return tb.Text.Trim() != "";
    }

    // Los campos de camara no hacen nada si la fuente de video sigue siendo la pantalla,
    // asi que se cambia sola en vez de dejar al usuario con flags que scrcpy ignora.
    private bool HayCamaraConfigurada()
    {
        return HayTexto(txtCameraId) || ComboValor(cmbCameraFacing) != "" || HayTexto(txtCameraSize)
            || HayTexto(txtCameraFps) || HayTexto(txtCameraAr) || HayTexto(txtCameraZoom)
            || cbCameraHighSpeed.Checked || cbCameraTorch.Checked;
    }

    // Devuelve el motivo por el que no se puede lanzar, o null si todo esta bien.
    private string MotivoParaNoLanzar()
    {
        if (HayTexto(txtNewDisplay) && HayTexto(txtDisplayId))
            return "'Pantalla nueva' y 'pantalla existente (id)' no se pueden usar a la vez. "
                 + "Deja vacio uno de los dos en la pestana 'Ventana y captura'.";

        if (HayTexto(txtCameraId) && ComboValor(cmbCameraFacing) != "")
            return "'Id de camara' y 'Cara' no se pueden usar a la vez. Deja vacio uno de los dos "
                 + "en la pestana 'Camara'.";

        return null;
    }

    private void Abrir()
    {
        try
        {
            if (!ComprobarDependencias()) return;
            bool running = Process.GetProcessesByName("scrcpy").Length > 0;
            if (running)
            {
                Log("scrcpy ya esta abierto.");
                return;
            }

            string motivo = MotivoParaNoLanzar();
            if (motivo != null)
            {
                Log("No se puede lanzar: " + motivo);
                return;
            }

            if (HayCamaraConfigurada() && ComboValor(cmbVideoSource) != "camera")
            {
                cmbVideoSource.SelectedItem = "camera";
                Log("Hay opciones de camara configuradas: fuente de video cambiada a 'camera'.");
            }

            string flags = BuildFlags();
            Log("Lanzando: scrcpy " + flags);
            var psi = new ProcessStartInfo("scrcpy", flags) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetTempPath() };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log("ERROR al abrir scrcpy: " + ex.Message);
        }
    }

    private void Cerrar()
    {
        bool running = Process.GetProcessesByName("scrcpy").Length > 0;
        if (!running)
        {
            Log("scrcpy no estaba abierto.");
            return;
        }
        Log(RunCommandSync("taskkill", "/IM scrcpy.exe /F"));
        Refrescar();
    }

    private void ReiniciarAdb()
    {
        Log("Reiniciando adb server...");
        Log(RunCommandSync("adb", "kill-server"));
        Log(RunCommandSync("adb", "start-server"));
        Refrescar();
    }

    // Al salir, solo se detiene el servidor adb si lo arranco esta sesion y no queda
    // ningun scrcpy usandolo. Si ya estaba corriendo al abrir la app, se deja intacto:
    // es un demonio compartido y no es nuestro.
    private void DetenerAdbSiLoArrancamos()
    {
        if (adbYaEstaba) return;
        if (Process.GetProcessesByName("scrcpy").Length > 0) return;
        if (Process.GetProcessesByName("adb").Length == 0) return;
        try { RunCommandSync("adb", "kill-server", 5000); } catch { }
    }

    private void CerrarAdb()
    {
        Log("Cerrando adb server...");
        Log(RunCommandSync("adb", "kill-server"));
        Log("adb server detenido. Nota: se reiniciara solo en cuanto se ejecute cualquier accion de adb o scrcpy (incluido 'Actualizar estado').");
        bool running = Process.GetProcessesByName("adb").Length > 0;
        lblEstado.Text = "scrcpy: " + (Process.GetProcessesByName("scrcpy").Length > 0 ? "EN EJECUCION" : "cerrado") + " | adb: " + (running ? "activo" : "detenido");
    }

    // Solo sirven las IPs de red local: para conectar por WiFi el PC tiene que poder
    // alcanzar al movil. Los datos moviles dan IPs publicas o de rango compartido
    // (100.64.0.0/10, el CGNAT de las operadoras) a las que no se llega desde la LAN.
    private static bool EsIpDeRedLocal(string ip)
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

    // La interfaz WiFi no se llama wlan0 en todos los moviles, asi que si ahi no hay nada
    // se repasan todas. El nombre varia segun el fabricante del modem (rmnet en Qualcomm,
    // ccmni en MediaTek...), asi que no basta con una lista de nombres: se exige ademas
    // que la IP sea de red local.
    private string ObtenerIpMovil()
    {
        string salida = RunCommandSync("adb", "shell ip -f inet addr show wlan0");
        Match m = Regex.Match(salida, @"inet (\d+\.\d+\.\d+\.\d+)/");
        if (m.Success && EsIpDeRedLocal(m.Groups[1].Value))
        {
            Log("Interfaz wlan0, IP " + m.Groups[1].Value);
            return m.Groups[1].Value;
        }

        Log("wlan0 no da una IP de red local; repasando el resto de interfaces...");
        salida = RunCommandSync("adb", "shell ip -f inet addr show");

        string descartadas = "";
        foreach (Match c in Regex.Matches(salida, @"inet (\d+\.\d+\.\d+\.\d+)/\d+([^\r\n]*)"))
        {
            string candidata = c.Groups[1].Value;
            string resto = c.Groups[2].Value.TrimEnd();
            if (candidata.StartsWith("127.")) continue;
            if (!EsIpDeRedLocal(candidata))
            {
                descartadas += "\r\n   " + candidata + " (" + resto.Trim() + ")";
                continue;
            }
            Log("Interfaz detectada: " + resto.Trim() + ", IP " + candidata);
            return candidata;
        }

        if (descartadas != "")
        {
            Log("El movil solo tiene IPs fuera de la red local, que no valen para esto:" + descartadas);
            Log("Suelen ser los datos moviles. Enciende el WiFi del movil y conectalo a la");
            Log("misma red que este PC antes de emparejar.");
        }
        else
        {
            Log("El movil no tiene ninguna interfaz con IP. Comprueba que el WiFi este encendido.");
        }
        return null;
    }

    private void ConectarWifi()
    {
        Log("Activando modo TCP/IP en el movil (necesita estar por USB)...");
        Log(RunCommandSync("adb", "tcpip 5555"));

        System.Threading.Thread.Sleep(1500);

        string ip = ObtenerIpMovil();
        if (ip == null)
        {
            Log("No se pudo obtener la IP del movil. Comprueba que este conectado por USB");
            Log("y que tenga el WiFi encendido y conectado a la misma red que este PC.");
            return;
        }
        Log("IP del movil: " + ip);
        Log(RunCommandSync("adb", "connect " + ip + ":5555"));
        rbWifi.Checked = true;
        Refrescar();
    }

    private void AbrirCarpetaGrabaciones()
    {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Grabaciones");
        Directory.CreateDirectory(dir);
        Process.Start("explorer.exe", dir);
    }

    private void EjecutarHerramienta()
    {
        string args;
        switch (cmbHerramientas.SelectedItem.ToString())
        {
            case "Version de scrcpy": args = "-v"; break;
            case "Listar codificadores": args = "--list-encoders"; break;
            case "Listar camaras": args = "--list-cameras"; break;
            case "Listar tamanos de camara": args = "--list-camera-sizes"; break;
            case "Listar pantallas": args = "--list-displays"; break;
            case "Listar apps instaladas": args = "--list-apps"; break;
            default: args = "-v"; break;
        }
        Log("Ejecutando: scrcpy " + args);
        Log(RunCommandSync("scrcpy", args, 30000));
    }

    private void Refrescar()
    {
        if (!ComprobarDependencias())
        {
            lblEstado.Text = "scrcpy: NO INSTALADO — ver el registro";
            return;
        }
        bool scrcpyRunning = Process.GetProcessesByName("scrcpy").Length > 0;
        Log("--- Dispositivos ---");
        Log(RunCommandSync("adb", "devices -l"));
        bool adbRunning = Process.GetProcessesByName("adb").Length > 0;
        lblEstado.Text = "scrcpy: " + (scrcpyRunning ? "EN EJECUCION" : "cerrado") + " | adb: " + (adbRunning ? "activo" : "detenido");
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LauncherForm());
    }
}
