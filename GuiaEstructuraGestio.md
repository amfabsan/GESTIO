# Guía de Estructura y Responsabilidades del Repositorio - Gestio

Este documento detalla la arquitectura de directorios del monorepo ligero de **Gestio**, definiendo el propósito técnico de cada carpeta y el listado de archivos y clases C# y TypeScript a implementar en Backend y Frontend.

## 1. Visión Global del Repositorio

```
gestio/
├── .github/workflows/          # Automatización de integración continua (CI/CD)
├── backend/                    # Solución .NET 10 (Web API + EF Core + Tests)
├── frontend/                   # Single Page Application (React 19 + Vite + TypeScript)
├── docker/                     # Archivos de contenerización y despliegue
├── scripts/                    # Utilidades de desarrollo y sincronización de contratos
├── docker-compose.yml          # Orquestación de desarrollo local (PostgreSQL)
├── README.md                   # Instrucciones de arranque y flujos de trabajo
└── TechStack.md                # Ficha de versiones y dependencias aprobadas

```

## 2. Backend (.NET 10 / C# 14)

El backend sigue un patrón de **Clean Architecture simplificada** contenido en un único proyecto Web API (`Gestio.Api`), acompañado de su respectivo proyecto de pruebas unitarias y de integración (`Gestio.Api.Tests`).

### 2.1. Raíz del Backend (`backend/`)

| Directorio / Archivo | Propósito técnico | Clases y archivos a crear | 
 | ----- | ----- | ----- | 
| `backend/Gestio.slnx` | Archivo de solución XML moderno que orquesta los proyectos de la API y de pruebas sin indexar artefactos del frontend. | `Gestio.slnx` | 
| `backend/global.json` | Fija la versión de compilación del SDK de .NET 10 para garantizar builds homogéneos en todo el equipo. | `global.json` | 

### 2.2. Proyecto API (`backend/src/Gestio.Api/`)

```
backend/src/Gestio.Api/
├── Controllers/
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Interfaces/
├── Infrastructure/
│   ├── Data/
│   │   ├── Configurations/
│   │   └── Migrations/
│   ├── AI/
│   └── VeriFactu/
├── Services/
├── Models/
│   ├── Requests/
│   └── Responses/
├── Common/
├── Program.cs
├── appsettings.json
└── appsettings.Development.json

```

#### A. `Controllers/`

* **Propósito:** Exponer los endpoints HTTP de la API REST. Validan las peticiones entrantes vía DTOs, delegan la ejecución a los servicios de aplicación y devuelven códigos de estado HTTP estandarizados.

* **Archivos a crear:**

  * `AuthController.cs`: Endpoints de registro, login JWT y refresco de tokens para autónomos y gestores.

  * `DocumentosController.cs`: Subida de tickets/facturas de gasto (multipart/form-data), descarga del binario y listado de comprobantes.

  * `FacturasController.cs`: Gestión de borradores, consulta y emisión final con encadenamiento VeriFactu.

  * `GestorController.cs`: Bandeja de validación de comprobantes, acciones de validación/corrección y gestión de cartera de clientes.

  * `DashboardController.cs`: Resumen de previsión fiscal (semáforo de IVA/IRPF) y checklist trimestral.

  * `HealthController.cs`: Endpoint de comprobación del estado de la API y de la conexión con PostgreSQL.

#### B. `Domain/Entities/`

* **Propósito:** Entidades del modelo de dominio de Gestio persistidas mediante EF Core en PostgreSQL. Representan el estado y las reglas de negocio esenciales.

* **Archivos a crear:**

  * `Usuario.cs`: Datos de identidad, email, hash de contraseña y rol en la plataforma.

  * `PerfilFiscal.cs`: Configuración tributaria del autónomo (NIF, nombre fiscal, epígrafe IAE, régimen de IVA/IRPF, retención por defecto).

  * `Documento.cs`: Justificante de gasto subido por el autónomo (almacena nombre de archivo, `StorageKey`, hash SHA-256 del fichero y estado del ciclo de vida).

  * `ExtraccionIa.cs`: Datos estructurados extraídos automáticamente por IA (proveedor, NIF, fecha, base imponible, porcentaje de IVA, cuota, total y nivel de confianza).

  * `RevisionDocumento.cs`: Registro de auditoría con las correcciones introducidas por el gestor sobre la extracción automática.

  * `Factura.cs`: Factura emitida por el autónomo a sus clientes con numeración, serie, cliente destinatario, estado e importes en tipo `decimal`.

  * `LineaFactura.cs`: Concepto individual facturado (descripción, cantidad, precio unitario, IVA y retención).

  * `RegistroVerifactu.cs`: Registro criptográfico inmutable vinculado a la factura (huella anterior, huella actual SHA-256, URL/payload del QR normativo y marca temporal UTC).

  * `Anomalia.cs`: Alerta generada por el asistente auditor (duplicado, descuadre de cálculo, deducibilidad dudosa).

  * `ChecklistItem.cs`: Obligación o factura recurrente esperada en el trimestre para el checklist proactivo.

#### C. `Domain/Enums/`

* **Propósito:** Tipos enumerados fuertemente tipados que gobiernan las transiciones de estado del sistema.

* **Archivos a crear:**

  * `RolUsuario.cs`: `Autonomo`, `Gestor`, `Administrador`.

  * `EstadoDocumento.cs`: `Subido`, `Procesando`, `PendienteValidacion`, `Validado`, `AclaracionSolicitada`, `Error`.

  * `EstadoFactura.cs`: `Borrador`, `Emitida`, `Rectificada`, `Anulada`.

  * `EstadoAnomalia.cs`: `Abierta`, `Resuelta`, `Descartada`.

  * `TipoIva.cs`: `General21`, `Reducido10`, `Superreducido4`, `Exento0`.

#### D. `Domain/Interfaces/`

* **Propósito:** Contratos e interfaces que abstraen servicios de infraestructura externa y casos de uso, permitiendo inyección de dependencias y pruebas unitarias aisladas.

* **Archivos a crear:**

  * `IVeriFactuService.cs`: Contrato para el cálculo determinista de huellas SHA-256 y ensamblado del código QR normativo.

  * `IAiExtractionService.cs`: Contrato para la ingestión multimodal de imágenes/PDFs y generación de esquemas JSON estructurados.

  * `IFileStorageService.cs`: Contrato para el almacenamiento de archivos físicos (disco local o buckets en la nube).

  * `ICurrentUserService.cs`: Acceso al contexto del usuario autenticado (ID y rol) a partir de los claims del token JWT.

#### E. `Infrastructure/Data/`

* **Propósito:** Capa de acceso a datos que gestiona la comunicación con PostgreSQL a través de Entity Framework Core.

* **Archivos a crear:**

  * `ApplicationDbContext.cs`: Contexto principal de EF Core que expone los `DbSet<T>` y aplica las configuraciones de Fluent API.

  * `DbInitializer.cs`: Lógica de seeding para poblar datos de prueba (usuarios de Ramón y Pedro, comprobantes simulados y catálogos fijos).

#### F. `Infrastructure/Data/Configurations/`

* **Propósito:** Mapeo desacoplado de entidades mediante Fluent API (`IEntityTypeConfiguration<T>`). Configura tipos nativos de PostgreSQL, precisión `numeric(18,2)` para dinero, campos `jsonb` e índices únicos.

* **Archivos a crear:**

  * `UsuarioConfiguration.cs`

  * `PerfilFiscalConfiguration.cs`

  * `DocumentoConfiguration.cs` (incluye índice único sobre el hash del archivo para evitar duplicados)

  * `ExtraccionIaConfiguration.cs`

  * `FacturaConfiguration.cs`

  * `RegistroVerifactuConfiguration.cs` (índices sobre huellas y unicidad 1:1 con la factura)

  * `AnomaliaConfiguration.cs`

#### G. `Infrastructure/Data/Migrations/`

* **Propósito:** Historial versionado de migraciones C# autogeneradas por `dotnet ef migrations add`.

* **Archivos gestionados:**

  * `XXXXXX_InitialCreate.cs`

  * `ApplicationDbContextModelSnapshot.cs`

#### H. `Infrastructure/AI/`

* **Propósito:** Integración con modelos de lenguaje y visión mediante la abstracción oficial `Microsoft.Extensions.AI`.

* **Archivos a crear:**

  * `AiExtractionService.cs`: Implementación del análisis OCR/visión multimodal que extrae JSON estructurado con `GPT-4o-mini` o `Claude 3.5 Sonnet`.

  * `AiAnomalyDetector.cs`: Reglas y prompts para detectar tickets dudosos o discordancias aritméticas.

  * `Prompts/PromptsPlantillas.cs`: Catálogo centralizado de prompts de extracción y auditoría para evitar textos dispersos en el código.

#### I. `Infrastructure/VeriFactu/`

* **Propósito:** Lógica técnica e inmutable requerida por la normativa antifraude y VeriFactu de la AEAT.

* **Archivos a crear:**

  * `VeriFactuService.cs`: Orquestador que calcula la huella encadenada en mayúsculas utilizando `System.Security.Cryptography.SHA256` y construye la URL oficial de cotejo.

  * `QrCodeGenerator.cs`: Generador gráfico de matrices QR mediante la biblioteca `QRCoder`.

#### J. `Services/`

* **Propósito:** Capa de aplicación y orquestación de reglas de negocio. Maneja transacciones de base de datos y la coordinación entre entidades y servicios de infraestructura.

* **Archivos a crear:**

  * `AuthService.cs`: Registro de usuarios, verificación de contraseñas con hashing seguro y generación de tokens JWT.

  * `FacturacionService.cs`: Creación y edición de borradores; emisión atómica de facturas con bloqueo inmutable y generación de registro VeriFactu.

  * `DocumentosService.cs`: Procesamiento asíncrono de tickets, cálculo de hash SHA-256 de archivos y coordinación de la extracción por IA.

  * `GestorService.cs`: Aprobación, rechazo y modificación de gastos con trazabilidad en el historial de revisiones.

  * `AuditoriaFiscalService.cs`: Cálculo continuo del IVA devengado frente a deducible y control de checklists trimestrales.

#### K. `Models/Requests/` y `Models/Responses/`

* **Propósito:** Data Transfer Objects (DTOs) que aíslan el contrato público de la API de las entidades internas del dominio.

* **Archivos en `Requests/`:**

  * `LoginRequest.cs`, `RegisterRequest.cs`

  * `CreateFacturaRequest.cs`, `EmitirFacturaRequest.cs`

  * `SubirDocumentoRequest.cs`

  * `ValidarDocumentoRequest.cs`, `CorregirDocumentoRequest.cs`

* **Archivos en `Responses/`:**

  * `AuthResponse.cs` (token, expiración, datos de perfil)

  * `FacturaResponse.cs`, `FacturaDetalleResponse.cs`

  * `DocumentoResponse.cs`, `ExtraccionIaResponse.cs`

  * `RegistroVerifactuResponse.cs` (huella digital y matriz/URL del QR)

  * `PrevisionFiscalResponse.cs` (totales trimestrales para el semáforo)

#### L. `Common/`

* **Propósito:** Elementos transversales que dan soporte al pipeline HTTP de ASP.NET Core.

* **Archivos a crear:**

  * `GlobalExceptionHandlerMiddleware.cs`: Captura centralizada de excepciones no controladas con formato estándar RFC 7807 (`ProblemDetails`).

  * `ApiResponse.cs`: Envoltorio genérico para estandarizar las respuestas exitosas y con errores (`Result<T>`).

  * `SecurityExtensions.cs`: Métodos de extensión para configurar la validación de tokens JWT en el contenedor de servicios.

#### M. Archivos de Raíz del Proyecto API

* `Program.cs`: Configuración de servicios, inyección de dependencias, configuración de OpenAPI y pipeline de middlewares HTTP.

* `appsettings.json` y `appsettings.Development.json`: Cadenas de conexión a PostgreSQL, claves secretas JWT y configuración del seeder.

* `Gestio.Api.csproj`: Definición de paquetes NuGet y configuración de compilación sobre .NET 10.

### 2.3. Proyecto de Tests (`backend/tests/Gestio.Api.Tests/`)

```
backend/tests/Gestio.Api.Tests/
├── Controllers/
├── Services/
├── Infrastructure/
└── Gestio.Api.Tests.csproj

```

* **Propósito:** Batería de pruebas automatizadas con `xUnit`, `Shouldly`y `Moq`.

* **Archivos clave a crear:**

  * `Infrastructure/VeriFactuServiceTests.cs`: Comprueba que la huella SHA-256 encadenada es exacta y determinista ante las especificaciones normativas.

  * `Services/FacturacionServiceTests.cs`: Valida que una factura en estado `EMITIDA` no pueda ser alterada ni eliminada.

  * `Services/DocumentosServiceTests.cs`: Verifica la detección de archivos duplicados mediante el hash SHA-256 antes de llamar a la IA.

  * `Controllers/DocumentosControllerTests.cs`: Pruebas de integración sobre los endpoints de subida.

## 3. Frontend (React 19 + TypeScript + Vite)

El frontend adopta una **arquitectura basada en funcionalidades (*Feature-Based Architecture*)**, agrupando componentes, hooks y llamadas a la API por contexto de negocio para facilitar el trabajo simultáneo sin conflictos de Git.

### 3.1. Raíz del Frontend (`frontend/`)

| Archivo | Propósito técnico | 
 | ----- | ----- | 
| `package.json` | Declaración de scripts (`dev`, `build`, `lint`, `generate:types`) y dependencias (React 19, Lucide, Tailwind, React Router). | 
| `vite.config.ts` | Configuración del servidor de desarrollo, alias de rutas (`@/`) y plugins de React y Tailwind CSS 4. | 
| `tsconfig.json` | Configuración del compilador de TypeScript en modo estricto. | 
| `.env.example` | Plantilla de variables de entorno del cliente web (ej. `VITE_API_URL=http://localhost:5000/api`). | 

### 3.2. Código Fuente (`frontend/src/`)

```
frontend/src/
├── assets/
├── components/
│   ├── ui/
│   ├── layout/
│   └── feedback/
├── features/
│   ├── auth/
│   ├── documentos/
│   ├── facturacion/
│   ├── validacion-gestor/
│   └── dashboard/
├── hooks/
├── routes/
├── services/
├── types/
├── App.tsx
├── main.tsx
└── index.css

```

#### A. `assets/`

* **Propósito:** Archivos estáticos empaquetados por Vite, fuentes tipográficas, isotipos de Gestio e iconografía personalizada.

* **Archivos a crear:**

  * `logo-gestio.svg`: Isotipo corporativo de la marca.

  * `index.css`: Hoja de estilos principal con la importación y variables temáticas de Tailwind CSS 4.

#### B. `components/ui/`

* **Propósito:** Componentes atómicos de interfaz sin lógica de negocio propia. Sirven de base reutilizable y facilitan la futura integración visual de Bellas Artes (Sprint 3).

* **Archivos a crear:**

  * `Button.tsx`: Botón con variantes visuales (primario, secundario, peligro, fantasma) y soporte de estado de carga.

  * `Input.tsx`: Campo de texto con validación y estados de error accesibles.

  * `Modal.tsx`: Diálogo accesible para confirmaciones y previsualizaciones.

  * `Table.tsx`: Tabla estilizada para listados fiscales con cabeceras y paginación.

  * `Card.tsx`: Contenedor modular para métricas, facturas y paneles.

  * `Badge.tsx`: Etiqueta para representar estados visuales (ej. verde para `VALIDADO`, amarillo para `PROCESANDO`, rojo para `ERROR`).

#### C. `components/layout/`

* **Propósito:** Estructuras visuales globales que envuelven las distintas vistas según el rol autenticado.

* **Archivos a crear:**

  * `Navbar.tsx`: Barra superior con datos de sesión, notificaciones y cambio de perfil.

  * `Sidebar.tsx`: Menú lateral de navegación con enlaces dinámicos según el rol (`Autónomo` vs. `Gestor`).

  * `PageLayout.tsx`: Contenedor común con márgenes y rejilla responsiva.

  * `RoleLayout.tsx`: Envoltorio que conmuta la disposición según el portal actual.

#### D. `components/feedback/`

* **Propósito:** Elementos de comunicación sobre el estado de las operaciones del usuario.

* **Archivos a crear:**

  * `LoadingSpinner.tsx`: Indicador de carga para operaciones asíncronas.

  * `Toast.tsx`: Notificación flotante de confirmación o alerta de error.

  * `AlertBanner.tsx`: Cuadro de aviso para avisos normativos de VeriFactu o anomalías detectadas.

#### E. `features/auth/`

* **Propósito:** Módulo de autenticación, control de accesos y gestión de sesión.

* **Archivos a crear:**

  * `components/LoginForm.tsx`: Formulario de inicio de sesión con validación de credenciales.

  * `components/RegisterForm.tsx`: Formulario de alta para autónomos y gestores.

  * `hooks/useAuth.ts`: Custom hook que expone el estado de autenticación, usuario actual, login y logout.

  * `authApi.ts`: Funciones HTTP de autenticación contra `/api/auth`.

#### F. `features/documentos/`

* **Propósito:** Buzón inteligente para la recepción y extracción automática de comprobantes mediante IA.

* **Archivos a crear:**

  * `components/DropzoneSubida.tsx`: Zona de arrastre para subida de fotos (JPG/PNG) y PDFs de tickets/facturas.

  * `components/DocumentoVisor.tsx`: Visor lateral que renderiza el PDF o la imagen del ticket recibido.

  * `components/DatosExtraidosCard.tsx`: Formulario con los datos detectados por la IA para su revisión preliminar.

  * `components/DocumentosListado.tsx`: Tabla con el historial de documentos y su estado de procesamiento.

  * `hooks/useDocumentos.ts`: Gestión de la lista de documentos y sus mutaciones.

  * `hooks/useUploadDocumento.ts`: Control del progreso de subida y polling del estado `PROCESANDO`.

  * `documentosApi.ts`: Funciones de llamada HTTP para documentos y extracciones.

#### G. `features/facturacion/`

* **Propósito:** Generación, emisión y consulta de facturas de venta con cumplimiento de VeriFactu.

* **Archivos a crear:**

  * `components/FacturaForm.tsx`: Formulario de confección de factura con líneas de concepto, cálculos dinámicos de bases, tipos de IVA y retenciones.

  * `components/FacturaVisor.tsx`: Vista de impresión y detalle de factura con el código QR normativo y el pie legal de la AEAT.

  * `components/FacturasTabla.tsx`: Listado de facturas emitidas y borradores con filtros por fecha y cliente.

  * `hooks/useFacturas.ts`: Operaciones de creación, listado y emisión de facturas.

  * `facturacionApi.ts`: Comunicación HTTP contra los endpoints de `/api/facturas`.

#### H. `features/validacion-gestor/`

* **Propósito:** Bandeja de revisión ágil de comprobantes destinada al perfil del gestor (Pedro).

* **Archivos a crear:**

  * `components/BandejaRevision.tsx`: Cola de trabajo priorizada con documentos en estado `PENDIENTE_VALIDACION`.

  * `components/DocumentoComparador.tsx`: Interfaz partida (split view) que confronta la imagen original del ticket con los datos extraídos por la IA.

  * `components/AccionesValidacionBar.tsx`: Botonera de resolución rápida: `Aprobar en 1 clic`, `Corregir datos` o `Solicitar aclaración`.

  * `hooks/useBandejaGestor.ts`: Gestión de la cola de revisión y acciones rápidas.

  * `gestorApi.ts`: Métodos de actualización de estados de comprobantes desde el punto de vista del gestor.

#### I. `features/dashboard/`

* **Propósito:** Paneles informativos con previsión fiscal y control preventivo.

* **Archivos a crear:**

  * `components/SemaforoFiscal.tsx`: Indicador visual del balance estimado de IVA trimestral (repercutido menos soportado).

  * `components/ChecklistTrimestral.tsx`: Lista de comprobación de gastos habituales que aún no han sido subidos en el periodo.

  * `components/MetricasCard.tsx`: Tarjetas de resumen con importes facturados y gastos computados.

  * `hooks/useDashboardFiscal.ts`: Obtención y formateo de métricas fiscales agregadas.

  * `dashboardApi.ts`: Consulta de los datos consolidados del dashboard.

#### J. `hooks/`

* **Propósito:** Custom hooks transversales de React reutilizables en múltiples pantallas.

* **Archivos a crear:**

  * `useDebounce.ts`: Optimización de entradas de texto y filtros de búsqueda.

  * `useToastNotifications.ts`: Control del disparo y cierre de alertas flotantes.

  * `useLocalStorage.ts`: Persistencia de preferencias del usuario en el navegador.

#### K. `routes/`

* **Propósito:** Enrutamiento centralizado y control de accesos mediante React Router 7.

* **Archivos a crear:**

  * `AppRoutes.tsx`: Definición de las rutas públicas (`/login`, `/registro`) y protegidas (`/dashboard`, `/gastos`, `/facturas`, `/revision`).

  * `ProtectedRoute.tsx`: Componente de orden superior que comprueba la existencia de sesión activa y restringe rutas según el rol del usuario.

#### L. `services/`

* **Propósito:** Cliente HTTP centralizado para peticiones a la API.

* **Archivos a crear:**

  * `apiClient.ts`: Instancia de Axios configurada con `baseURL`, timeout y un interceptor de peticiones que inyecta automáticamente la cabecera `Authorization: Bearer <token>`. Incluye el manejo global de respuestas con código 401 (redirección a login si el token ha expirado).

#### M. `types/`

* **Propósito:** Definiciones de interfaces y tipos TypeScript globales.

* **Archivos a crear:**

  * `api.ts`: **Contrato tipado autogenerado**. Se produce ejecutando `npm run generate:types` a partir del esquema OpenAPI de .NET, conteniendo todos los DTOs de C# convertidos a TypeScript sin duplicación manual.

  * `ui.ts`: Tipos auxiliares de presentación (estados visuales, variantes de botones, temas).

#### N. Archivos Raíz de `src/`

* `App.tsx`: Componente raíz que orquesta los proveedores globales (tema, notificaciones, contexto de autenticación y enrutador).

* `main.tsx`: Punto de entrada que inicializa React 19 y monta la aplicación en el DOM (`index.html`).

## 4. Directorios de Infraestructura Compartida

```
gestio/
├── .github/workflows/
│   └── ci.yml                 # Pipeline automatizado (build .NET, tests, lint y build frontend)
├── docker/
│   ├── Dockerfile.backend     # Compilación multi-stage sobre Alpine (.NET 10)
│   ├── Dockerfile.frontend    # Compilación Vite y servicio con Nginx
│   └── nginx.conf             # Soporte para rutas SPA en React sin error 404
├── scripts/
│   └── generate-types.sh      # Script CLI para consultar OpenAPI y regenerar api.ts
└── docker-compose.yml         # Contenedor local de PostgreSQL 17 con volumen persistente

```

## 5. Reglas de Acoplamiento y Flujo de Trabajo

1. **Inmutabilidad de VeriFactu:** Las entidades y servicios de VeriFactu (`IVeriFactuService`, `RegistroVerifactu`) residen de forma estricta en el backend. El frontend se limita a recibir la huella SHA-256 y la URL/Base64 del QR ya calculadas para su visualización o impresión en PDF.

2. **Contrato Único Tipado:** No se crean interfaces a mano en `frontend/src/types/api.ts`. Cualquier cambio en los modelos de .NET debe reflejarse en los controladores y sincronizarse ejecutando `npm run generate:types`.

3. **Aislamiento de Features:** Los componentes dentro de `features/<nombre>/` solo deben resolver la lógica de ese dominio. Los componentes transversales reutilizables se ubican en `components/ui/` para que la guía de estilo de Bellas Artes (Sprint 3) se incorpore modificando únicamente ese directorio base.