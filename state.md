# Speculum — estado del proyecto

## Estado actual
**Funcional y en uso.** Compilado, probado (compilación limpia, arranque sin excepciones,
pruebas de clic reales sobre la ventana) y en uso diario por el propietario.

Revisión de código del 2026-08-28: corregidos un cuelgue indefinido al leer la salida de
los procesos, la falta de aviso cuando scrcpy no está instalado y la suposición de que la
interfaz WiFi del móvil se llama `wlan0`. Corregido también que los procesos hijos
heredaran el directorio de trabajo del launcher, lo que dejaba al servidor adb reteniendo
la carpeta del `.exe` y bloqueando moverla o renombrarla. Ver TODO y Decisiones de diseño.
Pendiente de publicar el repo.

Entorno de la revisión: scrcpy **4.1** instalado con `winget install Genymobile.scrcpy` el
2026-08-28 (no estaba en la máquina), y un <test-phone> (<codename>, Android 15) por USB.

Verificado contra ese scrcpy 4.1 real, no contra documentación: los 20 flags añadidos
existen, y los 30 flags largos y 12 cortos que la app ya usaba siguen siendo válidos —
ninguno se ha renombrado ni retirado.

## Qué es
Herramienta de escritorio para Windows que envuelve [scrcpy](https://github.com/Genymobile/scrcpy)
(espejo y control de Android por USB/WiFi) en una interfaz gráfica, para no tener que usar
la terminal ni recordar flags de la línea de comandos.

Nace porque scrcpy se instaló con `winget` (paquete `Genymobile.scrcpy`) y aunque funciona
perfectamente por línea de comandos, no hay ninguna forma cómoda de lanzarlo, pararlo,
grabar, o conectar por WiFi sin escribir comandos cada vez.

El nombre: `speculum` es espejo en latín, que es literalmente lo que hace la herramienta.
Sigue la convención de <workspace> de nombrar los proyectos con un sustantivo latino real.

## Estructura de archivos (esta carpeta)
- `Speculum.cs` — código fuente en C# (WinForms). Un único archivo, sin dependencias
  externas más allá de .NET Framework (que ya viene con Windows).
- `Speculum.exe` — el programa final, listo para ejecutar. Doble clic y ya. No está
  versionado: se genera desde el `.cs` y se publica adjunto a cada release.
- `README.md`, `LICENSE` (GPLv3), `.gitignore` y `state.md` (este archivo).
- `docs/` — capturas de las cuatro pestañas, usadas por el README.
- `.archive/` — fuera del repo: versiones previas del fuente y los dos menús de consola
  (`scrcpy-menu.bat`, `scrcpy-menu-completo.bat`) que precedieron a la interfaz gráfica.
- `Grabaciones/` — la crea la app junto al `.exe` al grabar. Fuera del repo.

## Cómo compilar el .exe desde el .cs
No requiere Visual Studio ni el SDK de .NET. Usa el compilador de C# que ya trae Windows
(`csc.exe`, parte de .NET Framework):

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /out:Speculum.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Speculum.cs
```

(`Add-Type -OutputType WindowsApplication` de PowerShell **no** sirve para esto en
PowerShell 7/pwsh — solo funciona en Windows PowerShell 5.1 clásico. Por eso se usa
`csc.exe` directamente.)

## Qué hace la interfaz
- **Pestaña Básico**: modo de apertura (normal / pantalla completa / sin bordes para OBS /
  solo lectura), selector USB/WiFi, botón de emparejado automático por WiFi, mostrar
  toques, mantener pantalla activa, grabar sesión con nombre de archivo automático
  (fecha/hora) en una carpeta `Grabaciones` junto al `.exe`.
- **Pestaña Avanzado**: checkboxes y desplegables para prácticamente todas las opciones de
  scrcpy agrupadas por categoría (Video, Audio, Control, Otros) — sin tener que escribir
  flags a mano. Cada campo de texto libre tiene un ejemplo visible dentro (ej: "8M") y un
  tooltip con la explicación completa al pasar el ratón. Un campo de texto libre al final
  cubre cualquier flag no representado en la interfaz.
- **Gestión**: abrir/cerrar scrcpy, reiniciar o cerrar el servidor adb por separado, ver
  estado de dispositivos conectados, herramientas informativas (versión, listar cámaras,
  codificadores, pantallas, apps instaladas).

## Decisiones de diseño (por qué está hecho así)
- **WinForms + csc.exe en vez de un `.bat`**: el usuario pidió algo "más cómodo" que un
  menú de consola — sin ventana negra parpadeando, con controles seleccionables en vez de
  tener que recordar sintaxis de flags.
- **Pestaña Avanzado con controles reales (no solo texto libre)**: primera versión tenía
  un único cuadro de texto para "flags avanzados". El usuario pidió explícitamente que
  fuera seleccionable como el resto, sin tener que memorizar opciones — de ahí los
  checkboxes/desplegables por categoría.
- **Ejemplos + tooltips en los campos de texto**: seguía sin quedar claro qué formato
  esperaba cada campo (bitrate, recorte, etc.). Se añadió cue banner (placeholder nativo
  de Windows vía `EM_SETCUEBANNER`) más `ToolTip` con la explicación larga.
- **Botón "Cerrar ADB" separado de "Reiniciar ADB"**: el usuario preguntó si había forma de
  cerrar adb sin reiniciarlo. Importante: adb se reinicia solo en cuanto cualquier acción
  vuelve a tocarlo (comportamiento estándar de la herramienta, no arreglable desde la app).
- **Preset de "baja latencia WiFi"**: rellena los campos de bitrate/tamaño/fps en la
  pestaña Avanzado en vez de aplicar flags ocultos, para que no haya duplicados ni magia
  invisible — todo lo que se envía a scrcpy es visible y editable en los campos.
- **Directorio de trabajo de los procesos hijos en `%TEMP%`**: sin `WorkingDirectory`, el
  hijo hereda el cwd del launcher, y el servidor adb se queda con un handle abierto sobre
  esa carpeta mientras siga vivo. Siendo una app portable, eso pincha la carpeta del propio
  `.exe`: el usuario no puede moverla, renombrarla ni expulsar el USB, sin ninguna pista de
  por qué. Se fija `WorkingDirectory = Path.GetTempPath()` en los dos `ProcessStartInfo`.
  Ninguna ruta de la app depende del cwd: `Grabaciones` se resuelve con
  `AppDomain.CurrentDomain.BaseDirectory`.
- **El servidor adb solo se detiene al salir si lo arrancó la app**: adb es un demonio único
  de la máquina y puede estar en uso por Android Studio, otra terminal u otra ventana del
  launcher, así que matarlo incondicionalmente sería apagar algo ajeno. `adbYaEstaba` se
  calcula en el `Load` del formulario, antes de `Refrescar()` (que consulta adb y por tanto
  lo arrancaría), y `FormClosing` hace `kill-server` solo si la app lo arrancó y no queda
  ningún scrcpy abierto. Verificado en las dos ramas: con adb parado de inicio, se detiene
  al cerrar; con adb ya corriendo, sigue vivo tras cerrar.

## Contexto de depuración relevante (por si reaparece)
- Hubo un problema de conexión WiFi que **no era de scrcpy**: Tailscale tenía instalada una
  ruta que capturaba el tráfico hacia la red local (`<lan-subnet>`) y lo mandaba por el
  túnel VPN en vez de salir por WiFi directo. Si el emparejado por WiFi falla con timeout
  aunque el móvil y el PC estén en la misma red, revisar `Get-NetRoute` por si Tailscale
  (u otra VPN) está secuestrando esa subred.
- Señal WiFi débil (RSSI por debajo de -75/-80 dBm) causa cortes de audio/vídeo aunque la
  conexión esté establecida — no es un bug, es física de radio. La red de 2.4GHz suele
  tener más alcance que la de 5GHz a la misma distancia.

## TODO

Detectados en la revisión de código del 2026-08-28, antes de publicar el repo.

- [x] **Deadlock en `RunCommandSync`.** Leía `StandardOutput.ReadToEnd()` completo y
  después `StandardError.ReadToEnd()`, llamando a `WaitForExit(timeoutMs)` solo al final.
  Con el hijo escribiendo en los dos streams, llenaba el buffer del pipe sin vaciar (~4 KB)
  y se bloqueaba. Peor de lo estimado: el bloqueo ocurre *antes* del `WaitForExit`, así que
  el timeout no llegaba a aplicarse nunca y el cuelgue era **indefinido**, no de 15 s.
  Reproducido con un proceso que escribe 4000 líneas en cada stream: la versión antigua se
  quedó colgada sin recuperación; la nueva termina en menos de 2 s con las 8000 líneas
  íntegras. Arreglado con `BeginOutputReadLine`/`BeginErrorReadLine` y dos
  `ManualResetEvent`. Hecho 2026-08-28.

  Matiz comprobado después con el móvil conectado: **`--list-apps` no dispara el cuelgue**,
  pese a ser el candidato que parecía más probable. Con 222 líneas de salida la versión
  antigua terminó sin problema; scrcpy no satura los dos pipes a la vez. El fallo es real,
  pero hace falta un proceso que escriba mucho en ambos streams simultáneamente.

  Medida la espera de los `ManualResetEvent` por si añadía latencia: no la añade. El fin de
  stream llega a los 1741 ms, antes de que retorne `WaitForExit`, y el total coincide con
  la duración del proceso. Una comparación anterior que daba 6 s frente a 1,8 s era
  engañosa: medía la primera ejecución de scrcpy, que sube el `scrcpy-server` de 733 KB al
  móvil y procesa las apps, contra una segunda ya caliente.
- [x] **Sin comprobación de dependencias al arrancar.** Añadidos `BuscarEnPath()` y
  `ComprobarDependencias()`, llamados desde `Refrescar()` y `Abrir()`. Sin scrcpy, la barra
  de estado muestra "scrcpy: NO INSTALADO" y el registro indica qué falta y el
  `winget install Genymobile.scrcpy`, en lugar del "no se puede encontrar el archivo
  especificado" de Windows. Verificado en una máquina sin scrcpy. Hecho 2026-08-28.
- [x] **`ConectarWifi` asumía la interfaz `wlan0`.** Extraído a `ObtenerIpMovil()`: prueba
  `wlan0` primero y, si no da nada, repasa el resto de interfaces. Hecho 2026-08-28.

  La primera versión filtraba por nombre de interfaz (`rmnet`, `dummy`, `tun`) y **falló al
  probarla con el móvil real**: un <test-phone> con el WiFi apagado no tiene `wlan0`,
  y sus interfaces de datos son `ccmni0`/`ccmni1` — así se llaman en los módems MediaTek,
  mientras que `rmnet` es el nombre en Qualcomm. Devolvía `<cgnat-ip>`, una IP del
  rango CGNAT de la operadora (100.64.0.0/10) inalcanzable desde la LAN: justo el error que
  el filtro pretendía evitar.

  Corregido con `EsIpDeRedLocal()`, que exige que la IP esté en un rango privado
  (10/8, 192.168/16, 172.16/12) en vez de fiarse del nombre de la interfaz, que cambia
  según el fabricante del módem. Si no hay ninguna válida, lista las descartadas con su
  interfaz y dice que encienda el WiFi. Verificado contra el móvil: 12 casos de
  clasificación de IP más la ejecución real, que ahora devuelve `null` con el aviso.
- [x] **`LICENSE`** GPLv3 añadida, la misma que acta y memoria. Hecho 2026-08-28.
- [x] **Repositorio git iniciado** (2026-08-28). Rama `main`, commit inicial `48e73a6`.
  Versionados `Speculum.cs`, `README.md`, `LICENSE`, `state.md` y `.gitignore`.
  Fuera del control de versiones: `.archive/` (versiones previas del fuente y los dos
  menús `.bat`, que se movieron ahí por estar superados por la interfaz gráfica),
  `Speculum.exe` y la carpeta `Grabaciones/`. El README avisa de que el binario no
  está en el repo y hay que descargarlo de las releases o compilarlo.
- [x] **Renombrada la carpeta `scrcpy-menus` a `scrcpy-launcher`.** Hecho 2026-08-28.
  Lo que lo impedía era `adb.exe`: el servidor queda vivo como demonio y retiene un handle
  sobre el directorio desde el que se lanzó, y sobrevive a la sesión que lo arrancó. Se
  libera con `adb kill-server`. Claude Code también abre handles sobre la carpeta mientras
  trabaja en ella, y esos solo se sueltan al cerrar la sesión.
- [x] **Repo publicado en GitHub** el 2026-08-28: `memoriainfinita/speculum`, en privado
  por ahora. Rama `main` con `origin` configurado.
- [ ] **Renombrar la carpeta local a `speculum`.** El repo, el fuente, el binario y la
  documentación ya usan el nombre nuevo; falta solo la carpeta, bloqueada por los handles
  de la sesión de Claude Code que hizo el cambio. Hacerlo con esa sesión cerrada. No afecta
  a git.
- [ ] **Adjuntar el `.exe` a una release** y decidir cuando pasar el repo a publico. Antes
  de hacerlo publico, revisar que `state.md` incluye IPs de la LAN (<phone-ip>/<pc-ip>) y el
  modelo del movil.
- [ ] **Pasar la app a inglés.** Toda la interfaz está en español: etiquetas, pestañas,
  tooltips, mensajes del registro y textos de ayuda. Afecta a las cadenas de
  `Speculum.cs`, no a la lógica. Decidir de paso si el README y `state.md` también
  pasan a inglés, como en acta, o se quedan en español.
- [ ] **Mejorar la interfaz.** Pendiente de definir alcance. Detectado de paso: la interfaz
  no lleva ni una tilde ("Basico", "Conexion", "Version", "Grabar esta sesion"). Si se pasa
  a inglés esto se resuelve solo, así que conviene hacer antes la traducción y no corregir
  tildes que van a desaparecer. Hasta entonces, los textos nuevos se escriben sin tilde por
  coherencia con el resto.
- [x] **Cotejo de cobertura de flags.** Comparado el man page de scrcpy (master) contra
  `BuildFlags()`: 108 flags documentados, 42 cubiertos por la interfaz (39%). Los 66
  restantes, por área: cámara 9, ventana 10, conexión/adb 8, teclado/ratón 7, pantallas 6,
  codecs 5, buffers 4, grabación 2, V4L2 2 (solo Linux), otros 13. Hecho 2026-08-28.
- [x] **Añadidos los flags de alto valor.** Dos pestañas nuevas, 19 controles:
  - *Ventana y captura*: `--window-x/y/width/height`, `--window-title`, `--no-window`;
    `--record-format`, `--record-orientation`; `--start-app`, `--new-display`,
    `--display-id`.
  - *Cámara*: `--camera-id`, `--camera-facing`, `--camera-size`, `--camera-fps`,
    `--camera-ar`, `--camera-zoom`, `--camera-high-speed`, `--camera-torch`.
  - `--list-camera-sizes` añadido al desplegable de herramientas.
  - Al configurar cualquier campo de cámara, la fuente de vídeo pasa a `camera` sola y se
    avisa en el registro: si no, scrcpy ignora esos flags en silencio.
  - `MotivoParaNoLanzar()` bloquea las combinaciones incompatibles (`--new-display` con
    `--display-id`, `--camera-id` con `--camera-facing`) explicando cuál quitar.
  - `LimpiarAvanzado()` extendido a los campos nuevos; si no, quedaban flags activos
    invisibles tras pulsar "Limpiar todo".
  - Cobertura tras el cambio: 62 de 108 flags (57%), medida con el mismo cotejo. Lo que
    queda fuera es ajuste fino (codecs, buffers), específico de Linux (V4L2), de conexión
    con varios dispositivos, o alcanzable por el campo de flags libres.
  - Verificado con 30 comprobaciones automáticas que instancian el formulario real y
    contrastan la salida de `BuildFlags()`, incluidas las validaciones y una regresión de
    las opciones que ya existían. Hecho 2026-08-28.
- [x] **Prueba con móvil real.** <test-phone> por USB, scrcpy 4.1. Comprobados:
  detección del dispositivo, `--list-apps` (222 líneas), `--list-cameras` (3 cámaras),
  `--list-camera-sizes` (la opción nueva) y `--list-displays`. Espejo lanzado desde la app
  con los flags nuevos: la ventana sale con el `--window-title` indicado y la posición y
  tamaño de `--window-x/y/width/height` se aplican. Hecho 2026-08-28.

  Cámara probada de extremo a extremo: con `--camera-id=0`, `--camera-size=1920x1080` y
  `--camera-fps=30`, y la fuente de vídeo dejada a propósito en "(por defecto)". La app
  detectó los campos, cambió la fuente a `camera` sola y lo registró, y scrcpy mostró la
  imagen en vivo de la cámara trasera. La ventana sale horizontal (~16:9) frente al espejo
  de pantalla, que es vertical (1080x2400): confirma que la fuente cambió de verdad.
  Tamaños admitidos por este móvil, vía `--list-camera-sizes`: de 3264x2448 a 720x480 en
  la trasera, con fps {10, 15, 20, 30}.

  Al medir la ventana, `GetWindowRect` engaña: incluye el borde invisible de DWM y da una
  posición y un tamaño que no son los visibles. Para comprobar dónde está de verdad hay que
  usar `DwmGetWindowAttribute` con `DWMWA_EXTENDED_FRAME_BOUNDS` (9). Además scrcpy coloca
  el área de vídeo, no la ventana: la barra de título añade unos 38 px por encima. Y si el
  alto pedido no cabe en la pantalla, el sistema lo recorta y ajusta el ancho a la
  proporción del móvil, que parece que los flags se ignoren cuando no es así.

- [x] **Emparejado WiFi probado de extremo a extremo.** Con el móvil en `wlan0`
  <phone-ip>/24 y el PC en <pc-ip>, y con los datos móviles activos a la vez
  (`ccmni0`, <cgnat-ip>): `ConectarWifi()` puso el móvil en TCP, eligió la IP de
  `wlan0` descartando la de datos, y `adb connect <phone-ip>:5555` conectó. Después,
  espejo lanzado con `-e` desde la app, con bitrate 2M, 800 de tamaño y 30 fps: imagen
  correcta y `--window-title` / `--window-x` aplicados. Hecho 2026-08-28.

  De paso quedó confirmado en vivo el problema de Tailscale que ya estaba anotado más
  abajo: instalaba una ruta `<lan-subnet>` con métrica 0 que ganaba a la de Ethernet
  (métrica 256), mandando el tráfico de la LAN por el túnel. El emparejado funcionó igual,
  pero la latencia al móvil era de 154 ms en el primer ping. Tras desinstalar Tailscale
  queda solo la ruta por Ethernet y baja a 3-5 ms estables.
- [x] **Revisión visual de las pestañas nuevas** (2026-08-28). Cámara se veía bien. En
  "Ventana y captura" el grupo "Apps y pantallas" salía cortado por abajo: los tres grupos
  suman ~332 px y el `TabControl` tenía 305 px de alto fijo, así que a los campos de
  pantalla nueva e id solo se llegaba haciendo scroll dentro de la pestaña. Agrandar la
  ventana no servía: el `TabControl` está anclado `Top|Left|Right`, sin `Bottom`, y no
  crece. Corregido subiendo el `TabControl` a 380 px, `ClientSize` a 620x780 y
  `MinimumSize` a 640x680, y bajando botones, herramientas y registro de `y=355` a `y=430`.
  Verificado en pantalla: ya entra todo sin scroll.

## Pendiente / ideas no implementadas
- No hay icono personalizado para el `.exe` (usa el icono por defecto de Windows Forms).
