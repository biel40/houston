# AGENTS.md — Houston

Guía para agentes de IA (y humanos) que trabajen en este repositorio. Léela entera antes de tocar código.

## Qué es Houston

Widget flotante para **Windows 11** que muestra métricas de hardware (por ahora **solo CPU y GPU**) de forma ligera, visual y con animaciones cuidadas. Es un CPUID/HWMonitor personal: más simple, más bonito, sin bloatware.

Idioma: la conversación con el usuario es en **español**; el código, los identificadores, los commits y los comentarios técnicos van en **inglés**.

## Principios de producto (no negociables)

1. **No intrusivo.** Al cerrar la aplicación, desaparece del todo: el icono de la bandeja existe solo mientras la app está abierta, y no queda ningún proceso residual, sin servicio, sin tarea programada, sin autoarranque. Al salir hay que liberar los sensores (`Computer.Close()`) para que el driver del kernel también se descargue.
2. **Ligero.** Presupuesto orientativo: < 1 % de CPU en reposo y < 100 MB de RAM entre todos los procesos (host + WebView2). Nada de polling frenético ni animaciones que mantengan la GPU ocupada sin motivo.
3. **Simple.** Solo métricas útiles. Nada de gráficos recargados, ajustes infinitos ni telemetría. Cero conexiones de red.
4. **Visual.** Las animaciones son parte del producto: valores interpolados (nunca saltan), transiciones suaves, estética coherente con Windows 11.
5. **Instalación fácil.** Un único instalador MSI de un clic (`installer/`, WiX v6): sin asistente, instala por máquina en `Archivos de programaHouston`, crea un acceso directo en el menú Inicio y se desinstala desde *Configuración → Aplicaciones* sin dejar nada (salvo `settings.json`). **Decisión del usuario (19/10/2026):** se adelanta el instalador; las actualizaciones automáticas siguen fuera de alcance.

## Stack

| Capa | Tecnología |
|---|---|
| Host de escritorio | **.NET 10 (LTS)**, **WPF** (`net10.0-windows`) — se eligió en lugar de .NET 8, cuyo soporte termina el 10/11/2026 |
| Contenedor web | **Microsoft.Web.WebView2** (el runtime viene con Windows 11) |
| Sensores | **LibreHardwareMonitorLib** (NuGet) |
| UI | **Svelte 5 + Vite + TypeScript** |
| Gráficos | SVG / Canvas propios; uPlot solo si hace falta histórico |

No añadir Electron, Tauri, servidores HTTP/WebSocket ni frameworks de UI pesados sin consultarlo.

## Estructura prevista

```
Houston/
├─ AGENTS.md
├─ Houston.sln
├─ src/
│  └─ Houston.App/           # WPF + WebView2 + sensores
│     ├─ App.xaml(.cs)
│     ├─ MainWindow.xaml(.cs)
│     ├─ Sensors/            # lectura de hardware y modelos de datos
│     ├─ Interop/            # mensajería host <-> UI
│     ├─ app.manifest        # requireAdministrator
│     └─ wwwroot/            # build de la UI (generado, no se edita a mano)
├─ installer/                # MSI (WiX v6, fuera de Houston.sln)
├─ tools/                    # build-icon.ps1, build-installer.ps1
└─ ui/                       # proyecto Svelte + Vite
   ├─ src/
   └─ package.json
```

## Arquitectura

```
LibreHardwareMonitorLib ──► SensorService (C#, tick cada ~1000 ms)
                                   │  JSON vía WebView2.PostWebMessageAsJson
                                   ▼
                          Svelte UI (store reactivo) ──► gauges / animaciones
                                   │  (acciones: cerrar, siempre visible, mover)
                                   ▼
                          WebMessageReceived ──► MainWindow
```

- **Sin servidor local.** En producción la UI se sirve con `CoreWebView2.SetVirtualHostNameToFolderMapping` apuntando a `wwwroot`. En desarrollo se usa el servidor de Vite (`localhost`) detrás de una opción de configuración/`#if DEBUG`.
- **El host solo lee y empuja datos; la UI solo pinta.** Nada de lógica de sensores en JS, nada de layout en C#.
- Contrato de datos (versionado, tolerante a nulos):

```json
{
  "v": 1,
  "t": 1760000000000,
  "cpu": { "name": "…", "vendor": "intel", "load": 12.5, "temp": 48.0, "clockMhz": 4300, "powerW": 35.2 },
  "gpu": { "name": "…", "vendor": "nvidia", "load": 7.0, "temp": 41.0, "clockMhz": 1800, "vramUsedMb": 1900, "vramTotalMb": 8192, "powerW": 25.0, "fanRpm": 0 }
}
```

`vendor` es `"intel"`, `"amd"`, `"nvidia"` o `null` y solo sirve para los colores de marca de la UI. Cualquier campo puede ser `null` (sensor ausente o sin permisos): la UI debe mostrar «—» sin romperse. Si hay varias GPU, usar la dedicada con más carga/VRAM y dejarlo preparado para ampliar.

## Comportamiento del widget

- Ventana sin bordes, pequeña, arrastrable desde cualquier zona libre, esquinas redondeadas, esquema claro/oscuro según Windows (en oscuro, fondo espacial; en claro, fondo claro con estrellas tenues).
- Identidad: logo e icono del `.exe` con un Saturno V (`ui/src/assets/saturn-v.svg` → `tools/build-icon.ps1`); cada bloque se tiñe con el color de la marca de su componente.
- Siempre encima (activable/desactivable). **Decisión del usuario (19/10/2026):** se comporta como una ventana normal de Windows: botones de minimizar, maximizar/restaurar y cerrar, redimensionable por bordes y esquinas, botón en la barra de tareas y en Alt+Tab. Al redimensionar se mantiene la proporción y el contenido escala; maximizada ocupa el área de trabajo con el contenido centrado.
- Icono en el área de notificación (bandeja) mientras la app está abierta: clic izquierdo muestra/restaura la ventana; clic derecho abre un menú nativo con *Mostrar Houston*, *Siempre visible* y *Cerrar Houston*. Se elimina al cerrar.
- Menú contextual (clic derecho): *Siempre visible*, *Cerrar*. Nada más por ahora.
- Recuerda la posición y el ancho en `%LOCALAPPDATA%\Houston\settings.json`. Es el único dato persistente permitido.
- **Una sola instancia** (mutex con nombre).
- Cierre limpio: detener el timer, `Computer.Close()`, `Application.Shutdown()`, sin hilos huérfanos.

## Permisos y driver

- Las temperaturas, potencias y ventiladores requieren **administrador** y un driver de kernel. El `app.manifest` pide `requireAdministrator` (UAC en cada arranque; es aceptable por ahora).
- **Prohibido** crear servicios, tareas programadas o claves de autoarranque para evitar el UAC. Va contra el principio «no intrusivo».
- Si no hay elevación o falla el driver, la app arranca igualmente, muestra lo que pueda (cargas) y deja «—» en el resto, sin diálogos de error molestos.
- Comprobar qué driver usa la versión de LibreHardwareMonitorLib elegida (WinRing0 puede ser marcado por Windows Defender; versiones recientes usan PawnIO) y **documentar** la decisión.

## Riesgo técnico conocido: transparencia

WPF + WebView2 + ventana transparente puede dar problemas (fondo negro, «airspace», rendimiento). Antes de construir la UI final, hacer un **spike corto** que pruebe, por orden:

1. `AllowsTransparency="True"` + `WindowStyle="None"` + `WebView2.DefaultBackgroundColor = Transparent` + `body { background: transparent }`.
2. Fallback: sin `AllowsTransparency`, con `DwmSetWindowAttribute` (esquinas redondeadas `DWMWA_WINDOW_CORNER_PREFERENCE`, backdrop Acrylic/Mica) y fondo semitransparente pintado por la UI.

Elegir la opción que funcione de forma estable y anotar el resultado en el README.

## Comandos habituales

```powershell
# UI
cd ui; npm install; npm run dev        # desarrollo (Vite)
cd ui; npm run build                   # genera ../src/Houston.App/wwwroot

# Host
dotnet build Houston.sln -c Release
dotnet run --project src/Houston.App   # requiere terminal elevado para ver todos los sensores

# Instalador (genera publish/Houston-<versión>.msi)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-installer.ps1
```

## Convenciones de código

- **C#**: nullable habilitado, `async` cuando haya I/O, sin `.Result`/`.Wait()`, `record`s para los modelos de datos, un fichero por tipo, nombres en inglés. Sin lógica pesada en el hilo de UI: el muestreo va en su propio `PeriodicTimer`/tarea.
- **Svelte/TS**: TypeScript estricto, runes de Svelte 5, componentes pequeños (`Gauge`, `Sparkline`, `StatRow`…), estilos con variables CSS para el tema. Animaciones con CSS/Web Animations o `svelte/motion`; respetar `prefers-reduced-motion`.
- Comentarios solo cuando expliquen el *porqué*. Sin código muerto ni dependencias «por si acaso».
- Antes de añadir una dependencia: ¿se puede hacer en 20 líneas? Si sí, no la añadas.

## Cómo trabajar

1. Lee este archivo.
2. Avanza por hitos pequeños y comprobables; tras cada uno, compila y ejecuta.
3. **Verifica de verdad**: `dotnet build` limpio, `npm run build` limpio, y arranca la app para comprobar que los datos llegan y que cerrar la ventana deja **cero** procesos `Houston`/`msedgewebview2` huérfanos.
4. Si algo es ambiguo o contradice estos principios, pregunta al usuario en vez de decidir en silencio.
5. No hagas commits, `push` ni instalaciones globales sin que el usuario lo pida. Si inicializas git, que sea solo `git init` + `.gitignore`.
6. Informa con honestidad: qué funciona, qué no pudiste probar (p. ej. sensores sin admin) y qué decisiones tomaste.

## Fuera de alcance (por ahora)

RAM, disco, red y otras métricas · autoarranque · actualizaciones automáticas · ajustes avanzados · temas personalizables · multi-monitor avanzado.
