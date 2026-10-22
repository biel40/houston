# Houston

Widget flotante para Windows 11 que muestra métricas de CPU y GPU. Principios, stack y arquitectura en [AGENTS.md](AGENTS.md).

## Requisitos

- SDK de .NET 10
- Node.js LTS
- Runtime de WebView2 (incluido en Windows 11)

## Compilar y ejecutar

```powershell
cd ui; npm install; npm run build
dotnet build Houston.sln -c Release
dotnet run --project src/Houston.App -c Release
```

Ejecútalo como administrador para ver temperaturas y potencias. Para esas lecturas hace falta además [PawnIO](https://pawnio.eu). El MSI lo instala solo; si compilas a mano, instálalo tú.

## Desarrollo

```powershell
cd ui; npm run dev
$env:HOUSTON_UI_URL = 'http://localhost:5173'
dotnet run --project src/Houston.App   # solo Debug
```

## Datos persistentes

Solo `%LOCALAPPDATA%\Houston\settings.json` (más la carpeta temporal de WebView2, que se borra al cerrar).

## Instalador

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-installer.ps1
```

Genera `publish/Houston-<versión>.msi` (unos 50 MB): compila la UI, publica el host *self-contained* (el PC de destino no necesita .NET) y lo empaqueta con WiX v6 (`installer/`, paquete NuGet; no hay que instalar nada). La versión sale de `<Version>` en `src/Houston.App/Houston.App.csproj`.

- Un clic: sin asistente, solo UAC y barra de progreso. Instala en `C:\Program Files\Houston` y crea *Houston* en el menú Inicio.
- Desinstalación desde *Configuración → Aplicaciones*. Solo queda `%LOCALAPPDATA%\Houston\settings.json`.
- El MSI no está firmado: SmartScreen avisará la primera vez (*Más información → Ejecutar de todas formas*).
- **Incluye PawnIO**, el driver con el que se leen las temperaturas de la CPU en cualquier PC Intel o AMD:
  - `build-installer.ps1` descarga `PawnIO_setup.exe` (versión fijada, hoy 2.2.0) en `installer/deps/`, que está ignorada por git, y comprueba su SHA-256. Si no coincide, lo borra y se detiene.
  - **Al instalar:** si PawnIO no está, el MSI ejecuta `PawnIO_setup.exe -install -silent`, sin ventanas, y lo anota en `HKLM\SOFTWARE\Houston\InstalledPawnIO`. Si ya estaba (por FanControl, LibreHardwareMonitor…), no lo toca.
  - **Al desinstalar:** solo quita PawnIO (`-uninstall -silent`) si esa marca existe, y nunca durante una actualización de Houston.
  - **Si PawnIO falla al instalarse:** Houston se instala igual y muestra «—» en las temperaturas.
  - **Rastro visible:** PawnIO aparece como entrada propia en *Configuración → Aplicaciones*, porque así lo registra su instalador. Mientras está instalado, su driver queda cargado (servicio `PawnIO`, inicio manual) aunque Houston esté cerrado.
- **Verificado en el portátil de pruebas** (21/10/2026), con instalación, desinstalación y reinstalación silenciosas:
  - La instalación deja PawnIO 2.2.0 y su driver en marcha.
  - Con PawnIO, la temperatura de la CPU aparece: 51 °C.
  - La desinstalación quita Houston, PawnIO y la marca. Justo después, el servicio `PawnIO` seguía listado como detenido: probablemente Windows lo borra al reiniciar, pero no está comprobado.

## Licencias de terceros

`src/Houston.App/Licenses/` se copia junto al ejecutable y entra en el MSI:

- `THIRD-PARTY-NOTICES.txt`: aviso de PawnIO (incluida su excepción para programas que lo usan por IOCTL, como Houston) y enlaces a su código fuente.
- `PawnIO-GPL-2.0.txt`: licencia del driver PawnIO.
- `PawnIO.Modules-LGPL-2.1.txt`: licencia de los módulos de PawnIO, que ya van dentro de `LibreHardwareMonitorLib.dll`.

El MSI incluye el instalador oficial de PawnIO (`PawnIO_setup.exe`), que es gratuito pero propietario. Para uso personal no hay problema; antes de publicar Houston para otras personas, conviene confirmar con su autor (admin@namazso.eu) que se puede redistribuir.

## Créditos

Houston existe gracias a estos proyectos. Las licencias son las que declaran sus propios paquetes, comprobadas el 21/10/2026.

### Lectura del hardware

| Proyecto | Para qué lo usa Houston | Licencia |
|---|---|---|
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (`LibreHardwareMonitorLib`) | Leer los sensores de CPU y GPU | MPL-2.0 |
| [PawnIO](https://pawnio.eu), de namazso ([driver](https://github.com/namazso/PawnIO), [módulos](https://github.com/namazso/PawnIO.Modules)) | Driver de kernel para temperaturas y otras lecturas de bajo nivel | Driver: GPL-2.0 con excepción · Módulos: LGPL-2.1 · Instalador: gratuito, propietario |
| [BlackSharp.Core](https://github.com/Blacktempel/BlackSharp), [DiskInfoToolkit](https://github.com/Blacktempel/DiskInfoToolkit), [RAMSPDToolkit](https://github.com/Blacktempel/RAMSPDToolkit), de Blacktempel | Dependencias de LibreHardwareMonitor | MPL-2.0 |
| [HidSharp](https://software.seekye.com/hidsharp), de James F. Bellinger | Dependencia de LibreHardwareMonitor | Apache-2.0 |
| Mono.Posix.NETStandard | Dependencia de LibreHardwareMonitor | La que indica su paquete NuGet |

### Aplicación e interfaz

| Proyecto | Para qué lo usa Houston | Licencia |
|---|---|---|
| [.NET](https://dot.net) y WPF, de Microsoft (`System.Management`, `System.IO.Ports`…) | Aplicación de escritorio; el MSI incluye el runtime | MIT |
| [WebView2](https://aka.ms/webview), de Microsoft (`Microsoft.Web.WebView2`) | Mostrar la interfaz web dentro de la ventana | BSD-3-Clause (Microsoft) |
| [Svelte](https://svelte.dev) (con `clsx` y `esm-env`) | Interfaz y animaciones | MIT |
| [Vite](https://vite.dev) y `@sveltejs/vite-plugin-svelte` | Compilar la interfaz | MIT |
| [TypeScript](https://www.typescriptlang.org) | Tipado de la interfaz | Apache-2.0 |

### Instalador

| Proyecto | Para qué lo usa Houston | Licencia |
|---|---|---|
| [WiX Toolset](https://wixtoolset.org) v6 | Generar el instalador MSI | MS-RL, más el acuerdo de cuota de mantenimiento (OSMF). La cuota solo afecta a quien obtiene ingresos con él; Houston, gratuito y personal, está exento. |

### Notas

- **Componentes MPL-2.0** (LibreHardwareMonitor y dependencias): se usan sin modificar y su código fuente está en los enlaces de arriba.
- **Componentes GPL y LGPL** (PawnIO): sus textos completos van en `src/Houston.App/Licenses/`.
- **Herramientas solo de desarrollo** (`svelte-check`, `@tsconfig/svelte`, `@types/node`): son MIT y no se incluyen en Houston.
- **Saturno V** del logo e icono: dibujo propio, no basado en imágenes de terceros. Las marcas Intel, AMD y NVIDIA se citan solo para identificar el hardware.
