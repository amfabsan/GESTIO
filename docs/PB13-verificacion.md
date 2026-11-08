# PB-13 — Implementación y verificación local

Fecha: 8 de noviembre de 2026. Fuente funcional: [Worki, UT 498](https://cliente.tuneupprocess.com/web/#/wum/498). Las 12 PA se abrieron individualmente y se leyeron sus condiciones, pasos, resultados y observaciones antes de implementar. También se revisaron las seis imágenes UX de la UT. No se modificó Worki.

## A. Archivos creados y modificados

Rutas relativas a la raíz del repositorio.

| Archivo | Cambio y motivo |
|---|---|
| `backend/src/Gestio.Api/Domain/Entities/ObligacionFiscal.cs` | Entidad persistida e historial de obligaciones. |
| `backend/src/Gestio.Api/Domain/Enums/EstadoObligacion.cs` | Estados PENDIENTE y PRESENTADA; no existe una operación nueva de presentación. |
| `backend/src/Gestio.Api/Infrastructure/Data/Configurations/ObligacionFiscalConfiguration.cs` | Mapeos, FK, fechas y unicidad por autónomo/modelo/ejercicio/periodo. |
| `backend/src/Gestio.Api/Infrastructure/Data/Migrations/20261108161005_ObligacionesFiscales.cs` | Migración generada con EF Core para las dos tablas e índices. |
| `backend/src/Gestio.Api/Infrastructure/Data/Migrations/20261108161005_ObligacionesFiscales.Designer.cs` | Metadatos generados de la migración. |
| `backend/src/Gestio.Api/Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs` | Snapshot actualizado por EF. |
| `backend/src/Gestio.Api/Services/CatalogoObligacionesDemo.cs` | Única definición del catálogo académico confirmado por el usuario. |
| `backend/src/Gestio.Api/Services/GeneradorObligaciones.cs` | Reconciliación idempotente, bloqueo y coordinación transaccional compartida con PB-12. |
| `backend/src/Gestio.Api/Services/ObligacionesFiscalesService.cs` | Consultas, detalle, generación y autorización reutilizando PB-12. |
| `backend/src/Gestio.Api/Models/Response/ObligacionFiscalResponses.cs` | Contratos de calendario, resumen de perfil y detalle con historial. |
| `backend/src/Gestio.Api/Controllers/ObligacionesFiscalesController.cs` | Endpoints HTTP con ICurrentUserService y Resultado/ProblemDetails. |
| `backend/src/Gestio.Api/Services/PerfilesFiscalesService.cs` | Guardado atómico de PB-12 con actualización de calendarios ya generados; valida el rango de ejercicio necesario para calcular enero del año siguiente. |
| `backend/src/Gestio.Api/Program.cs` | Registro de los dos servicios nuevos en DI. |
| `backend/tests/Gestio.Api.Tests/ObligacionesFiscalesTests.cs` | Pruebas del catálogo, fechas, permisos, idempotencia, historial, cambios de perfil y errores. |
| `backend/tests/Gestio.Api.Tests/PerfilesFiscalesTests.cs` | Adaptación de la construcción del servicio a su dependencia de generación. |
| `frontend/src/features/obligaciones/obligacionesApi.ts` | Tipos y cliente Axios que reutiliza apiClient y X-Usuario-Id. |
| `frontend/src/features/obligaciones/CalendarioMes.tsx` | Calendario mensual accesible y adaptable a móvil. |
| `frontend/src/pages/CalendarioFiscalPage.tsx` | Listado, calendario, detalle, ejercicios, generación, filtros y estados vacíos/errores. |
| `frontend/src/routes/AppRoutes.tsx` | Rutas protegidas por los RoleGuard existentes. |
| `frontend/src/components/layout/Navbar.tsx` | Acceso al calendario del autónomo y a la selección de cliente del gestor. |
| `frontend/src/pages/PerfilFiscalPage.tsx` | Enlaces al calendario desde PB-12 y desde los clientes vinculados. |
| `docs/PB13-verificacion.md` | Este informe. |

Las evidencias y scripts auxiliares se guardan en `artifacts/pb13/`, ya ignorado por Git. No se añadieron dependencias ni se cambiaron los mecanismos de autenticación.

## B. Funcionalidad implementada

### Catálogo confirmado

Este catálogo procede de la confirmación expresa del usuario, posterior a la revisión de Worki; sustituye los ejemplos parciales de sus imágenes.

| Dato de PB-12 | Modelo |
|---|---|
| IVA GENERAL o SIMPLIFICADO | 303 — Autoliquidación del IVA |
| IVA RECARGO_EQUIVALENCIA | Ninguna obligación periódica de IVA en este catálogo |
| IRPF ESTIMACION_DIRECTA_NORMAL o ESTIMACION_DIRECTA_SIMPLIFICADA | 130 — Pago fraccionado del IRPF |
| IRPF ESTIMACION_OBJETIVA | 131 — Pago fraccionado del IRPF |

Cada modelo genera 1T, 2T, 3T y 4T, con fechas ficticias del 25 de abril, julio y octubre del ejercicio y 25 de enero del siguiente. No se usa el año actual para calcularlas. Los indicadores de trabajadores y alquiler de PB-12 no añaden modelos en este catálogo reducido.

### Backend y persistencia

- La consulta no genera datos. La generación explícita crea obligaciones PENDIENTE y se puede repetir.
- Un índice único PostgreSQL impide duplicar autónomo/modelo/ejercicio/periodo.
- Generación y guardado de PB-12 toman el mismo bloqueo de fila del autónomo antes de leer el perfil. SaveChanges y commit incluyen el perfil, su historial y la reconciliación de obligaciones.
- Al guardar PB-12 se actualizan los calendarios ya generados: se eliminan las pendientes que dejan de corresponder, se añaden las nuevas y se conservan las aplicables. Las presentadas se omiten íntegramente, incluso si dejan de pertenecer al catálogo del nuevo perfil.
- La generación no añade otra fila cuando una combinación ya corresponde a una presentada. Su identidad, fecha, estado e historial permanecen intactos.
- Las consultas de perfil y obligaciones se hacen desde una misma instantánea PostgreSQL para evitar respuestas mezcladas durante una regeneración concurrente.
- Las presentadas y su historial se pueden consultar. No hay endpoint ni botón para presentar declaraciones.

### Endpoints

Prefijo: `/api/autonomos/{autonomoId}/obligaciones-fiscales`.

| Método y ruta | Operación |
|---|---|
| `GET /{ejercicio}` | Calendario persistido, perfil configurado y aviso de fechas. |
| `POST /{ejercicio}/generar` | Generación o reconciliación idempotente. |
| `GET /detalle/{obligacionId}` | Detalle e historial limitado al autónomo de la ruta. |

Solo accede el autónomo propietario o un gestor cuya asesoría tenga vinculación ACTIVA. Se reutilizan los usuarios predefinidos, `X-Usuario-Id`, `ICurrentUserService` y el control de acceso de PB-12. Errores comprobados: 400 sin perfil/ejercicio inválido, 401 sin usuario, 403 sin permiso y 404 para detalle inexistente o de otro propietario.

### Frontend

Rutas `/autonomo/calendario` y `/gestor/clientes/:autonomoId/calendario`. La UI reutiliza AppLayout, Navbar, RoleGuard, PageHeader, Vacio y los estilos de tarjetas y botones. Permite seleccionar 2026/2027, como PB-12; listado y calendario comparten el conjunto de datos recibido de la API. Se puede navegar hasta enero del ejercicio siguiente para ver el 4T.

El panel de detalle consulta el endpoint correspondiente y muestra modelo, ejercicio, periodo, fecha, estado e historial. Las presentadas se pueden mostrar/ocultar. Las fechas se identifican expresamente como «Fechas configuradas para la demostración» y ficticias, sin verificación de plazos oficiales de la AEAT.

## C. PA oficiales verificadas

Los enlaces abren las definiciones oficiales. No hay una PA 1619 en el listado de PB-13 consultado.

| PA | Nombre oficial | Resultado | Evidencia ejecutada |
|---|---|---|---|
| [1610](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1610) | Generar el calendario de obligaciones fiscales | PASS | Catálogo completo reducido en xUnit y HTTP. Desde Chrome, el autónomo generó 0 → 8 obligaciones de 2027 mediante el botón. Fechas 25/04, 25/07, 25/10 y 25/01 del siguiente año. |
| [1611](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1611) | Consultar el calendario de obligaciones fiscales | PASS | Chrome: alternancia listado/calendario; julio contiene las entradas 130 y 303 del 2T del listado. Navegación mensual y móvil comprobadas. |
| [1612](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1612) | Consultar el detalle de una obligación fiscal | PASS | Apertura desde listado y día del calendario; modelo, ejercicio, periodo, fecha y estado coinciden con la API. KO: detalle inexistente 404 y detalle cruzado sin exposición de datos. |
| [1613](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1613) | Evitar la generación de obligaciones fiscales duplicadas | PASS | Ocho primeras generaciones simultáneas devolvieron las mismas ocho filas e IDs; tres repeticiones más. Repetición desde Chrome sin cambiar los IDs. Intento de duplicado directo rechazado por el índice PostgreSQL. |
| [1614](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1614) | Impedir la generación de obligaciones fiscales de un autónomo sin perfil fiscal | PASS | Antes de crear su perfil 2027, el segundo autónomo vio el estado sin perfil en Chrome; POST devolvió 400 SinPerfil y no insertó obligaciones. |
| [1615](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1615) | Actualizar obligaciones pendientes tras modificar el perfil fiscal | PASS | API y formulario PB-12: cambio GENERAL/directa → RECARGO/objetiva elimina pendientes 303/130 y crea cuatro 131. Actualización automática y sin duplicados. |
| [1616](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1616) | Conservar las obligaciones fiscales presentadas tras modificar el perfil fiscal | PASS | Precondición preparada solo en datos nuevos de prueba mediante SQL. 303/1T PRESENTADA conserva el mismo ID y detalle/historial completo tras cambiar el perfil, regenerar y restaurar. Chrome muestra la presentada y su historial. |
| [1617](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1617) | Consultar únicamente las obligaciones fiscales propias | PASS | Dos autónomos de prueba con perfiles y calendarios distintos; cambio de usuario en Chrome y consultas HTTP. GET, POST y detalle de otro autónomo devuelven 403. |
| [1618](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1618) | Consultar las obligaciones desde una asesoría vinculada | PASS | Pedro, usuario predefinido: selección de cliente, navegación a calendario y detalle desde Chrome; API 200 mientras la vinculación está ACTIVA. |
| [1620](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1620) | Denegar al gestor la consulta de obligaciones fiscales de un autónomo sin vinculación activa | PASS | Gestor de otra asesoría: error visible en URL directa y cero obligaciones mostradas. API 403 en listado, generación y detalle. También comprobadas vinculaciones PENDIENTE, RECHAZADA y FINALIZADA. |
| [1621](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1621) | Mostrar las fechas de las obligaciones fiscales como datos de demostración | PASS | Aviso visible en ambas vistas y detalle de Chrome; el endpoint devuelve el mismo aviso. No se presentan fechas como plazos oficiales. |
| [1622](https://cliente.tuneupprocess.com/web/#/pas/pam/10/1622) | Generar obligaciones utilizando el perfil fiscal del ejercicio correcto | PASS | Primer autónomo: ocho obligaciones 303/130 de 2026 y cuatro 131 de 2027; 4T de 2027 tiene fecha 25/01/2028. Selección anual comprobada en Chrome y API. |

Evidencia HTTP: [primera ejecución](../artifacts/pb13/resultados-api.json) y [reverificación final](../artifacts/pb13/resultados-api-final.json). Los scripts están en la misma carpeta y actúan únicamente sobre sus propios datos de prueba. La reverificación final se ejecutó antes de crear, desde Chrome, el perfil 2027 del segundo autónomo para comprobar la primera generación por interfaz.

## D. Ejecución local

| Comprobación | Resultado |
|---|---|
| Compilación backend | PASS. Compilado durante build, migración, dotnet run y tests. |
| Tests PB-12 + PB-13 | PASS: 23/23. |
| Batería completa backend final | PASS: 172/172, cero omitidos. Repetida después del ajuste de lectura consistente. |
| Frontend TypeScript y Vite | PASS: `npm run build`. |
| Frontend ESLint | PASS: `npm run lint`. |
| Tests frontend | No existe un script ni framework de tests unitarios frontend en el proyecto; interacción ejecutada con Playwright MCP sobre Chrome. |
| Migración PostgreSQL | PASS: `20261108161005_ObligacionesFiscales` aplicada a la base local real. Al reiniciar, base actualizada sin migraciones pendientes. |
| Snapshot de EF | PASS: `migrations has-pending-model-changes` indica que no hay cambios pendientes. |
| Backend dotnet run | PASS: `http://localhost:5017`, entorno Development, seeding desactivado en esta ejecución. |
| Frontend npm run dev | PASS: `http://localhost:5174`, proxy `/api` al backend 5017. El frontend previo en 5173 se conserva. |
| Persistencia | PASS: registros e historial recuperados después de reiniciar la API y recargar la web. |
| Concurrencia real | PASS: ocho generaciones paralelas y 30 peticiones mixtas de guardar PB-12, generar y consultar. Cada respuesta de lectura contiene perfil y calendario coherentes. |
| Integridad de datos previos | No se alteraron perfiles ajenos a las pruebas. El perfil de Ramón anterior a estas pruebas se comparó antes/después sin cambios. |

Comandos principales ejecutados:

```powershell
dotnet test backend/Gestio.slnx --artifacts-path artifacts/pb13/build --no-restore
dotnet run --project backend/src/Gestio.Api --configuration PB13 --launch-profile http -- --DatabaseConfig:SeedSampleData=false
# Desde frontend:
npm.cmd run build
npm.cmd run lint
npm.cmd run dev -- --port 5174 --strictPort
```

La configuración de compilación PB13 separa los binarios de la API previamente abierta. No se cambió el csproj para crearla. EF 10.0.4 se instaló en la carpeta de artefactos, sin añadir dependencias al proyecto.

Incidencias resueltas durante la comprobación: la carpeta nueva de compilación requería restore; la descarga de la herramienta EF requirió acceso a NuGet fuera del sandbox; se reinició únicamente la API identificada de GESTIO; se corrigió el recorte de textos del calendario en móvil. La compilación emite NU1900 porque la consulta de vulnerabilidades de NuGet no está disponible; no impide compilar ni ejecutar los tests. No se desactivó esa comprobación.

## E. Comparación con prototipos

Se abrieron y revisaron visualmente los seis PNG de Documentos de la UT 498:

1. `01_listado_obligaciones_2026.png`: disposición listado/detalle, ejercicios y aviso.
2. `02_vista_calendario_julio.png`: cuadrícula mensual, selección y detalle lateral.
3. `03_obligacion_presentada_1T.png`: filtro de presentadas, estado e información conservada.
4. `04_ejercicio_sin_perfil_no_genera.png`: estado vacío de ejercicio sin perfil.
5. `05_gestor_obligaciones_cliente_vinculado.png`: acceso desde el cliente vinculado.
6. `06_otro_perfil_recargo_equivalencia_sin_303.png`: perfil alternativo sin pendientes 303.

Se conserva la composición a dos columnas en escritorio, detalle seleccionado, fondo claro, tarjetas, selección destacada, ejercicios y aviso. En móvil los paneles se apilan; se verificó a 390 px, sin desbordamiento horizontal. Se reutilizan los estilos actuales de GESTIO.

Diferencias deliberadas respecto a las imágenes: el catálogo confirmado contiene solo 303/130/131 y fechas del día 25; no se copian los modelos 111/115/180, cantidades ni fechas del día 20/30 de los ejemplos originales. Estados únicamente PENDIENTE/PRESENTADA. Se excluyen checklist, evidencias, recordatorios y otros botones de PB-14/PB-18. El selector mensual permite todos los meses del ejercicio y enero del siguiente, en vez de los accesos rápidos del prototipo. El historial corresponde a datos persistidos reales.

Capturas locales revisadas:

- [Listado](../artifacts/pb13/ux-listado.png).
- [Calendario](../artifacts/pb13/ux-calendario.png).
- [Recargo y modelo 131](../artifacts/pb13/ux-recargo.png).
- [Presentada consultada por gestor](../artifacts/pb13/ux-gestor-presentada.png).
- [Sin perfil](../artifacts/pb13/ux-sin-perfil.png).
- [Móvil corregido](../artifacts/pb13/ux-movil.png).

## F. Pendientes y límites

No quedan requisitos de PB-13 ni PA oficiales sin verificar. No se ha comprobado la exactitud normativa de las fechas ni obligaciones: son simplificaciones ficticias expresamente aprobadas para esta demostración.

La UI ofrece los ejercicios 2026 y 2027, igual que PB-12. El backend calcula a partir del ejercicio solicitado y valida el rango 1–9998; las pruebas automatizadas incluyen 2031/2032 para comprobar que no depende del año actual.

Se conservan datos de prueba identificados como `Prueba PB13 Autonomo Uno`, `Prueba PB13 Autonomo Dos` y `Prueba PB13 Gestor Externo`, con IDs recogidos en `artifacts/pb13/fixture.json`. Los dos autónomos se vincularon a Pedro exclusivamente para estas pruebas; no se cambiaron vinculaciones anteriores. El estado PRESENTADA se preparó solo como precondición de prueba mediante SQL y no representa ningún envío real. No se eliminaron registros previos para preparar el entorno.

No se han realizado commits ni enviado cambios a Git. Las capturas, scripts auxiliares y registros locales de navegador permanecen ignorados.
