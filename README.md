# Gestio

Monorepo para la gestión fiscal, con backend ASP.NET Core y frontend React.

## Requisitos

- .NET SDK 10
- Node.js 22 y npm
- Docker Desktop

## Desarrollo local

```bash
docker compose up -d
dotnet watch --project backend/src/Gestio.Api run
cd frontend && npm install && npm run dev
```

La API expone OpenAPI en `/openapi/v1/openapi.json`. Para regenerar los tipos de TypeScript:

```bash
API_URL=https://localhost:7001/openapi/v1/openapi.json ./scripts/generate-types.sh
```

## Tests y builds

```bash
dotnet test backend/Gestio.slnx
cd frontend && npm run lint && npm run build
```

El hash SHA-256, el encadenamiento de huellas y los códigos QR de VeriFactu se calculan exclusivamente en el backend.

Consulta [TechStack.md](TechStack.md) para las dependencias acordadas.
