# Houston

Widget flotante para Windows 11 que muestra métricas de CPU y GPU. Ver [AGENTS.md](AGENTS.md) para principios, stack y arquitectura.

## Requisitos

- SDK de .NET 10 (LTS)
- Node.js LTS (probado con v24)
- Runtime de WebView2 (viene con Windows 11)

## Compilar y ejecutar

```powershell
cd ui; npm install; npm run build      # genera src/Houston.App/wwwroot
dotnet build Houston.sln -c Release
dotnet run --project src/Houston.App -c Release
```

### Desarrollo con recarga en caliente

```powershell
cd ui; npm run dev                                  # Vite en http://localhost:5173
$env:HOUSTON_UI_URL = 'http://localhost:5173'
dotnet run --project src/Houston.App                # solo en Debug; Release ignora la variable
```

En Debug también se activan las DevTools (F12).

## Protocolo host ↔ UI

Mensajería de WebView2 (`PostWebMessageAsJson` / `chrome.webview.postMessage`). Todos los mensajes son objetos JSON con `type`.

| Dirección | `type` | Carga |
|---|---|---|
| host → UI | `sensors` | `data`: el contrato v1 de AGENTS.md, una vez por segundo |
| host → UI | `window` | `topmost`: bool. Se envía al recibir `ready` y tras cada cambio |
| UI → host | `ready` | La UI ya escucha |
| UI → host | `drag` | Botón izquierdo pulsado: el host inicia el movimiento nativo de la ventana |
| UI → host | `setTopmost` | `value`: bool |
| UI → host | `menu` | `open`: bool. El menú contextual se ha abierto o cerrado |
| host → UI | `dismissMenu` | Clic fuera del widget con el menú abierto |
| UI → host | `size` | `width`, `height`: tamaño natural del contenido a escala 1, en px CSS (= DIP). El host fija la proporción de la ventana (y su tamaño inicial) a partir de él |
| UI → host | `resize` | `edge`: 1–8 (`WMSZ_*`: izquierda, derecha, arriba, arriba-izq., arriba-der., abajo, abajo-izq., abajo-der.). El host inicia el bucle nativo de redimensionado |
| UI → host | `minimize` | Minimiza la ventana |
| UI → host | `close` | Cierra la app |

El host ignora los mensajes que no vienen de su propio origen, cancela cualquier navegación fuera de él y bloquea las ventanas nuevas.

El menú contextual lo dibuja la UI dentro del widget y se comporta como uno nativo: se cierra al elegir una opción, al hacer clic en otra parte del widget o al hacer clic fuera.

Dos trampas de WebView2 en una ventana *layered* (las dos se descubrieron cuando el widget no se activaba nunca; desde el 19/10/2026 sí se activa, y la segunda ya no aplica):

- **`mouseleave` espurios.** Chromium dispara `mouseleave`/`pointerleave` en la raíz aunque el cursor no se mueva, sobre todo justo después de repintar. No se puede usar `mouseleave` para decidir nada. Como la página tampoco se entera de los clics fuera de la ventana, el host comprueba cada 50 ms si hay un botón pulsado con el cursor fuera del widget y lo avisa con `dismissMenu`. Solo lo hace mientras el menú está abierto.
- **Clics perdidos.** `WS_EX_NOACTIVATE` no decide la activación por ratón. Los clics sobre el `HWND` hijo de WebView2 suben como `WM_MOUSEACTIVATE` a la ventana WPF y, sin respuesta explícita, algunos se perdían; con la app elevada, a menudo. La ventana responde `MA_NOACTIVATE`: no se activa, pero el clic llega a la página.

Prueba automatizada (clic derecho, comprobar que el menú sigue abierto al cabo de 1 s, clic fuera y comprobar que se cerró), con ratón real a nivel de sistema:

| Ejecución | Antes | Después |
|---|---|---|
| Sin elevar | Intermitente | 30/30 en tres tandas seguidas |
| Elevada | 0/1 a mano; 3/6 automatizado, con clics que nunca llegaban | 8/10, con todos los clics entregados (ver nota) |

Nota: en la ejecución elevada los dos fallos tienen pinta de interferencia. Hubo un clic izquierdo que el script no generó y un cierre del menú por un clic fuera que llegó antes de tiempo. Además, el cambio que mueve los manejadores de `<main>` a `window` se hizo después y no se ha podido verificar elevado porque el UAC no se aceptó.

## Identidad visual

- **Icono Saturno V:** dibujo propio en `ui/src/assets/saturn-v.svg`, fuente única para el logo del widget y para el icono del `.exe`.
  - Tras editar el SVG, regenera el `.ico` con:
    ```powershell
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-icon.ps1
    ```
  - El script rasteriza con WPF, sin dependencias. Genera `src/Houston.App/Assets/houston.ico` en 16–256 px: DIB de 32 bits hasta 64 px y PNG a 256 px. El proyecto lo incrusta con `ApplicationIcon`.
  - Solo admite un subconjunto de SVG: `<path d fill opacity>` y `<g transform="rotate(...)">`.
- **Fondo:**
  - En tema oscuro: espacio profundo con estrellas estáticas y, en cada bloque, una nebulosa con el color de su marca (azul Intel, rojo AMD, verde NVIDIA).
  - En tema claro: fondo claro con estrellas muy tenues y un tinte suave de la marca.
  - Las estrellas salen de un generador con semilla fija, así que el cielo es siempre el mismo y no hay animación en bucle.
  - El fondo es opaco: con las estrellas encima, la translucidez restaba legibilidad.
- **Coste:** el cielo y cada nebulosa van en su propia capa (`will-change: transform`). Así, con el renderizado por software, cada fotograma de animación no vuelve a pintar los degradados. Además, la interpolación dura 400 ms en vez de 600. Resultado: ~0,5–0,6 % de CPU y ~84 MB.

## UI

- Componentes en `ui/src/lib`:
  - `MetricBlock`: un bloque por CPU o GPU.
  - `Gauge`: arco de 270° con la carga.
  - `StatRow`: fila compacta con su valor.
  - `Sparkline`: carga de los últimos 60 s.
  - `ContextMenu`: el menú contextual.
- Todos los valores se interpolan entre muestras (`Smoothed`, sobre `Tween` de `svelte/motion`, 400 ms). Con `prefers-reduced-motion` cambian al instante. Entre muestras no hay ninguna animación en marcha.
- Niveles (`levels.ts`):
  - Carga: alto al 70 % y crítico al 90 %.
  - Temperatura de la CPU: 75 °C y 90 °C.
  - Temperatura de la GPU: 75 °C y 85 °C.
  - **Colores de marca.** El host envía `vendor` (`intel`, `amd`, `nvidia` o `null`) y cada bloque toma el color de su fabricante: Intel azul, AMD rojo, NVIDIA verde; si no se reconoce, gris neutro. Ese color se aplica al icono (chip para CPU, tarjeta para GPU), a la sparkline y al gauge en reposo. Son iconos propios en SVG: los logotipos son marcas registradas.
  - **Gauge:** color de marca → gris neutro → ámbar → rojo. Pasa por el neutro porque mezclar un color de marca directamente con el ámbar pasa por el verde, por ejemplo con el azul de Intel. Limitación aceptada: en AMD el reposo es rojo y se parece al «crítico».
  - **Temperatura:** color de texto → ámbar → rojo.
  - El color se mezcla en OKLCH: normal → alto en los 10 puntos anteriores al umbral, y alto → crítico hasta el siguiente. Sigue al valor interpolado, así que cambia de forma gradual.
- Sin datos se muestra «—» en color secundario. Los nombres se limpian de `(R)`/`(TM)`.

## Decisión: sensores y driver (18/10/2026)

- **LibreHardwareMonitorLib 0.9.6** (última estable, febrero de 2026). Ya no incluye WinRing0, el driver que Windows Defender marca como `Trojan:Win32/Vigorf.A`. En su lugar usa **PawnIO**, un driver de kernel firmado que ejecuta módulos limitados.
- **Houston no instala ningún driver.** La librería solo usa PawnIO si ya está instalado en el sistema: lo detecta en `HKLM\...\Uninstall\PawnIO` y abre `\Device\PawnIO`. Instalarlo es decisión del usuario, con el instalador oficial de PawnIO, el mismo que usan LibreHardwareMonitor o FanControl. Así se respeta la regla de no crear servicios ni drivers desde la app.
- Al cerrar, `Computer.Close()` libera los handles del driver.

Qué se obtiene en cada caso (medido en un Intel Core Ultra 7 155H con GPU integrada Intel Arc):

| Métrica | Sin PawnIO (con o sin admin) | Con PawnIO + admin |
|---|---|---|
| CPU: carga | ✅ | ✅ |
| CPU: frecuencia | ✅ contador de Windows (*) | ✅ |
| CPU: potencia del paquete | ✅ contador RAPL de Windows (*), solo Intel | ✅ (según la CPU) |
| CPU: temperatura | — | ✅ (según la CPU) |
| GPU NVIDIA/AMD: carga, temperatura, frecuencia, VRAM, potencia, ventilador | ✅ (APIs del fabricante en modo usuario) | ✅ |
| GPU Intel integrada: carga (motor D3D más ocupado), frecuencia, memoria compartida | ✅ | ✅ |
| GPU Intel integrada: potencia | ✅ contador RAPL `PP1` de Windows (*) | ✅ (MSR) |
| GPU Intel integrada: temperatura, ventilador | — | — (no existen) |

Solo está verificada la columna «Sin PawnIO» de las filas de CPU y de GPU Intel. La columna con PawnIO y la fila de NVIDIA/AMD se basan en el código de LibreHardwareMonitor y no se han probado en este equipo.

(*) **Respaldo sin driver** (`Sensors/WindowsCounters.cs`). Son contadores de rendimiento de Windows que no necesitan ni admin ni driver. Se leen con PDH usando los nombres en inglés (`PdhAddEnglishCounter`), así que funcionan con Windows en cualquier idioma. LibreHardwareMonitor tiene prioridad: el respaldo solo rellena lo que llega `null`.

- **Frecuencia:** `Processor Information(_Total)`, multiplicando `% Processor Performance` por `Processor Frequency`. Es la misma «velocidad» que muestra el Administrador de tareas.
- **Potencia:** `Energy Meter(RAPL_Package0_PKG)\Power` para el paquete de la CPU y `RAPL_Package0_PP1` para la GPU integrada de Intel. Los valores vienen en mW.
- **Coste medido:** la CPU no cambia (~0,6 %). La memoria sube de ~84 a ~91–93 MB, porque PDH carga sus bibliotecas en el host; sigue dentro del presupuesto de 100 MB.
- **Temperatura, descartada:** la zona térmica ACPI (`Thermal Zone Information`) se probó en este portátil y marca 27,9 °C fijos, incluso con carga. Es un sensor de chasis o ambiente, no de la CPU, y mostrarlo como temperatura de la CPU sería engañoso. Para la temperatura real hace falta PawnIO.

Sin el driver, LibreHardwareMonitor devuelve algunas potencias como `0` en lugar de `null`. Houston trata cualquier temperatura, frecuencia o potencia `≤ 0` como «sin dato».

Selección de GPU: se prefiere una dedicada; si hay varias, la de más memoria. Se elige una sola vez al arrancar, para que el widget no salte entre adaptadores. En una GPU integrada, la «VRAM» que se muestra es la memoria compartida.

## Ciclo de vida

- **Una sola instancia:** mutex con nombre `Local\Houston.SingleInstance`. Un segundo arranque sale en silencio con código 0.
- **Posición:** se guarda al terminar cada arrastre, en píxeles físicos para que sea exacta en monitores con distinto DPI, y se restaura antes de mostrar la ventana. Si ya no cae en ningún monitor (por ejemplo, porque se desconectó), se usa la posición por defecto. La escritura es atómica: un `.tmp` y luego se renombra.
- **Cierre:** se detiene el muestreo y se llama a `Computer.Close()`. Se libera WebView2 y se espera a `BrowserProcessExited`; después se borra su carpeta, se termina cualquier guardado pendiente y se llama a `Shutdown()`. Tarda 0,3–0,9 s.

## Recursos (hito 6, 18/10/2026)

Medido en un Core Ultra 7 155H (22 núcleos lógicos), sin admin, con la UI final en reposo, durante 60 s. «Memoria» es el working set privado sumado de todos los procesos, igual que la columna del Administrador de tareas.

| Configuración | Procesos | Memoria | CPU (del total) |
|---|---|---|---|
| Inicial | 7 | ~191–196 MB | ~0,5–0,6 % |
| + WebView2 `--disable-gpu` | 7 | ~121 MB | ~0,35–0,5 % |
| + `--in-process-gpu` + `NetworkServiceInProcess2` | 5 | ~111–115 MB | ~0,3–0,45 % |
| + WPF `RenderMode.SoftwareOnly` (**actual**) | 5 | **~81–83 MB** | **~0,3–0,5 %** |

Reparto actual: renderer ~33 MB, navegador de WebView2 ~25 MB, host ~21 MB y crashpad + utilidad ~5 MB.

- **El renderizado por software no cuesta CPU:** la página es diminuta y solo anima ~0,6 s por muestra, así que sale más barato que mantener un proceso de GPU.
- **WPF en modo software** evita cargar Direct3D y el driver gráfico en el host (−31 MB). WPF aquí solo pinta un fondo transparente.
- **Lo que no sirvió:** ajustes del GC de .NET (concurrente, tamaño de gen0, `ConserveMemory`), ReadyToRun y V8 *lite*.
- Los argumentos van por `CoreWebView2EnvironmentOptions`, no por la variable de entorno, porque WebView2 ignora `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` cuando la app está elevada.

## Datos persistentes

Solo `%LOCALAPPDATA%\Houston\settings.json`. WebView2 necesita una carpeta de datos (`%LOCALAPPDATA%\Houston\WebView2`). Al cerrar, la app espera a que el proceso del navegador termine (`BrowserProcessExited`) y la borra; si la app muere de golpe, se reutiliza y se borra en el siguiente cierre.

## Decisión: transparencia de la ventana (spike, 18/10/2026)

Se probaron tres formas de alojar WebView2 en una ventana flotante sin bordes, con WebView2 SDK 1.0.4258.31 y runtime 155 en Windows 11:

| Opción | Resultado |
|---|---|
| **1. `AllowsTransparency="True"` + `WindowStyle="None"` + `DefaultBackgroundColor=Transparent` + `body { background: transparent }`** | ✅ **Elegida.** Transparencia real por píxel; la UI pinta sus propias esquinas redondeadas y su fondo semitransparente. |
| 1b. `WebView2CompositionControl` | ❌ Descartada. Se cierra al arrancar: necesita el TFM `net10.0-windows10.0.17763.0` (proyección CsWinRT) y no aporta nada frente a la opción 1. |
| 2. Sin `AllowsTransparency`, `WindowChrome` + DWM (esquinas `DWMWA_WINDOW_CORNER_PREFERENCE`, backdrop Acrylic) | ❌ Descartada. Como el widget nunca se activa (`WS_EX_NOACTIVATE`), DWM pinta el acrílico en su estado inactivo: un gris opaco. Además deja visible un botón «×» residual del marco. |

**Detalle importante de la opción 1:** una ventana *layered* hace el hit-test sobre su propio canal alfa. Si el fondo de WPF es totalmente transparente, los clics atraviesan el widget y llegan al escritorio. Por eso el fondo de la ventana es `#01000000` (alfa 1/255, invisible).

Consecuencias aceptadas:

- No hay blur de acrílico ni sombra de DWM; el fondo es un color semitransparente pintado por la UI.
- Las esquinas redondeadas las dibuja el CSS (`border-radius`), no DWM.

### Comportamiento de la ventana verificado

- Siempre encima (conmutable). Desde el 19/10/2026 es una ventana normal: botón en la barra de tareas y en Alt+Tab, y se activa al hacer clic. Se quitaron `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE` y la respuesta `MA_NOACTIVATE`, porque minimizar exige poder volver a la ventana desde la barra de tareas.
- Arrastre desde cualquier punto: la UI envía `drag` y el host inicia el bucle de movimiento nativo (`WM_NCLBUTTONDOWN` + `HTCAPTION`).
- Redimensionado: la ventana lleva `ResizeMode="CanResize"` (necesita `WS_THICKFRAME`), pero con `AllowsTransparency` no tiene marco nativo, así que la UI dibuja unas franjas invisibles en los bordes y esquinas (`ResizeHandles.svelte`, con el cursor adecuado). Al pulsar una, el host lanza `WM_SYSCOMMAND` con `SC_SIZE + borde`, igual que el arrastre.
- Proporción fija: el host intercepta `WM_SIZING`, limita el ancho a 200–720 DIP y calcula el alto con la proporción que le comunicó la UI (`size`), anclando el lado opuesto al que se arrastra. La UI escala el contenido con `transform: scale()` (layout a 300 px de ancho), así que texto, gauges y sparklines crecen juntos.
- El ancho se guarda en `settings.json` al terminar de redimensionar; el alto se deduce de la proporción.
- Botones de ventana en la cabecera: minimizar y cerrar (`WindowControls.svelte`). Los elementos con `data-nodrag` no inician el arrastre. No hay maximizar: en un widget con proporción fija no aporta nada.
- Al cerrar no queda ningún proceso `Houston` ni `msedgewebview2`. El proceso del navegador de WebView2 tarda normalmente menos de 2 s en salir y, en algún caso, unos segundos más.

### Mediciones del spike (sin sensores, página de prueba con un valor animado cada segundo)

- CPU: ~0,4–0,5 % del total (22 núcleos lógicos), sumando host y WebView2.
- RAM (working set privado, la columna «Memoria» del Administrador de tareas): ~154 MB en 7 procesos (host ≈ 48 MB, renderer ≈ 47 MB, GPU ≈ 26 MB, el resto en procesos auxiliares de WebView2). Se resolvió en el hito 6; ver «Recursos».
