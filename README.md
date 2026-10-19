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

Ejecútalo como administrador para ver temperaturas y potencias. Para esas lecturas hace falta además [PawnIO](https://pawnio.eu), que Houston no instala.

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
- No incluye PawnIO: sin él, el PC de destino mostrará «—» en temperaturas y en algunas potencias.
