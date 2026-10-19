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
