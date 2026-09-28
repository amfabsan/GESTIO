# Tech stack

## Backend

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 con `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Microsoft.AspNetCore.Authentication.JwtBearer` para autenticación JWT
- `Microsoft.AspNetCore.OpenApi` para el contrato OpenAPI
- `QRCoder` para la representación QR de VeriFactu
- `Microsoft.Extensions.AI` para integrar proveedores de IA sin acoplar la aplicación
- xUnit para tests automatizados

## Frontend

- React 19
- TypeScript 5
- Vite
- React Router
- Axios
- Tailwind CSS 4
- `openapi-typescript` para generar `src/types/api.ts`

## Infraestructura

- PostgreSQL 17 en desarrollo
- Docker multi-stage
- Nginx para servir el frontend compilado
- GitHub Actions para restore, build, tests y lint

Las versiones exactas instaladas quedan fijadas en `backend/src/Gestio.Api/Gestio.Api.csproj`, `frontend/package.json` y `frontend/package-lock.json`.