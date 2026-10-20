# Extracción local de documentos

La carga conserva primero el archivo y su registro. Después espera la extracción
del original almacenado, sin colas ni tareas en segundo plano. Un error OCR no
elimina el archivo: deja el documento en ERROR y permite reintentar desde el
buzón. Nunca valida automáticamente.

## Instalación

Instalar Tesseract 5 y las herramientas Poppler
`pdftotext` y `pdftoppm` en el equipo que ejecuta el backend. No requieren claves
ni servicios externos. Tesseract documenta Windows y español en
https://tesseract-ocr.github.io/tessdoc/Installation.html ; Poppler en
https://poppler.freedesktop.org/ . En Linux las distribuciones suelen ofrecer
`tesseract-ocr`, `tesseract-ocr-spa` y `poppler-utils`.

En Windows se han instalado y comprobado Tesseract 5.4.0.20240606 y Poppler
25.07.0 con `winget install --id UB-Mannheim.TesseractOCR --exact --source winget`
y `winget install --id oschwartz10612.Poppler --exact --source winget`.
El lector detecta la instalación habitual de Tesseract y el PATH persistido de
Windows, incluso cuando el IDE conserva el PATH anterior a la instalación.

El proyecto incluye únicamente el modelo español de
https://github.com/tesseract-ocr/tessdata_fast/tree/4.1.0 en
`Ocr/tessdata/spa.traineddata`, con su licencia Apache 2.0. Se copia al compilar
y publicar y se pasa mediante `--tessdata-dir`; no hace falta escribir en
Program Files. SHA-256:
`6F2E04D02774A18F01BED44B1111F2CD7F3BA7AC9DC4373CD3F898A40EA6B464`.
La configuración opcional `Ocr:Tessdata` permite sustituir esa carpeta.

Para rutas específicas usar configuración
.NET (por ejemplo variables de entorno, sin modificar appsettings):

```text
Ocr__Tesseract=C:\Program Files\Tesseract-OCR\tesseract.exe
Ocr__PdfToText=C:\poppler\Library\bin\pdftotext.exe
Ocr__PdfToPpm=C:\poppler\Library\bin\pdftoppm.exe
```

Comprobar en la cuenta que ejecuta el backend:

```text
tesseract --version
tesseract --list-langs
pdftotext -v
pdftoppm -v
```

Con el modelo incluido, comprobar español añadiendo a `--list-langs` el
argumento `--tessdata-dir backend/src/Gestio.Api/Ocr/tessdata` desde la raíz del
repositorio. Debe aparecer `spa`. Aplicar la migración `ExtraccionDocumentos` mediante el
procedimiento habitual de EF Core; en Development Program aplica migraciones
al arrancar. SkiaSharp y su paquete nativo Linux (3.119.1) preparan las imágenes
localmente; Windows utiliza los nativos incluidos por SkiaSharp. No hay servicios de pago.

## Uso y límites

En Buzón, subir PNG, JPEG o PDF y abrir «Ver datos propuestos». La petición
espera el resultado; mientras tanto la carga indica actividad. Otros clientes
que ven PROCESANDO consultan la API cada dos segundos y detienen el sondeo al
terminar o desmontar la vista. Un error muestra su mensaje y «Reintentar
extracción». Los documentos pendientes también ofrecen «Volver a extraer» para
aplicar mejoras sin volver a subirlos; los documentos validados no admiten
reprocesamiento. Se mantienen el original, los intentos y el historial al recargar.

PDF usa el texto con coordenadas de Poppler (`-bbox-layout`) cuando es útil y
OCR para las páginas sin texto. El XML se lee sin resolver DTD ni recursos externos.
Máximo 50 páginas y tres minutos por documento; rasterización hasta 3500 píxeles.
PDF protegido, corrupto, formatos no admitidos o motores ausentes dan error.

Las imágenes se preparan en copias temporales, sin modificar el original ni
reducir su resolución durante la carga. Se aplica orientación EXIF, gris y
ampliación limitada (hasta 3500 píxeles y 40 megapíxeles de entrada). Se conserva
la variante gris; se compara con umbral adaptativo y, cuando hay evidencia
geométrica, con una copia enderezada. Se detecta inclinación hasta ±8 grados
por proyección de tinta y se rectifica la perspectiva lateral mediante una
homografía únicamente si ambos bordes del papel tienen contraste y consenso
suficientes. No resuelve papel curvado, esquinas ocultas ni toda perspectiva.
La rotación amplía el lienzo para evitar cortar los márgenes.

Se comparan las segmentaciones 3, 6 y 11 de Tesseract por campo, manteniendo
las cajas, página, variante, geometría y puntuaciones reales de cada palabra.
Se releen hasta siete líneas, cuatro palabras, cabecera y resumen por variante,
con borde blanco, tamaño normalizado y dos segmentaciones por recorte.
Las anclas se buscan en todas las segmentaciones, no sólo en la página con
mejor puntuación global. Las palabras no se unen con cajas de otras pasadas:
se limita su tamaño para evitar que un recorte abarque media página.
Las regiones se superponen sólo a lecturas de su propia geometría.
Estas puntuaciones internas no se presentan como confianza por campo; el texto
nativo de PDF tampoco recibe una confianza OCR inventada.

Estos tratamientos siguen las recomendaciones de calidad de Tesseract:
https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html .

El extractor asocia cada columna fiscal con la cifra situada debajo, aunque
TOTAL esté a su derecha en esa misma fila. Lee números bajo cabeceras de
factura simplificada y separa emisor y destinatario cuando están en columnas
paralelas. Las relecturas concordantes pueden sustituir una lectura general
débil; las contradicciones entre lecturas fiables quedan null. Los
identificadores discrepantes no se corrigen sustituyendo letras o cifras.
Las posiciones y lecturas se conservan en DatosCrudosJson, junto al texto.

Las reglas leen etiquetas de emisor/proveedor/razón social, nombres legales,
NIF/CIF, número de factura/ticket, fechas numéricas con dos o cuatro cifras de
año, base, cuota IVA, porcentaje y total. Admiten etiquetas y valores en líneas
separadas y tablas de base/tipo/cuota. Se rechazan candidatos
contradictorios y se interrumpe el bloque del emisor al encontrar cliente,
receptor o destinatario. Diseños con columnas entremezcladas, nombres sin
etiquetar y tablas de varios tipos de IVA pueden quedar parcialmente vacíos.
No se calculan importes; ausencias y confianza por campo quedan null. El
detalle muestra «No extraído» y «No disponible», respectivamente. Si se reconoce
texto pero no se identifica ningún campo, el intento termina en ERROR con un
mensaje comprensible; no se presenta una extracción vacía como correcta.

Cada intento conserva fechas, procesador, versión disponible, resultado,
error y propuestas JSON. La versión de reglas figura en el identificador del
procesador y la versión del ejecutable se conserva tras la lectura correcta.
El estado es token de concurrencia; un índice único parcial impide dos
intentos abiertos. Cada SaveChanges confirma estado e historial junto al
intento, sin mantener transacciones durante OCR. Una caída abrupta del proceso
puede dejar PROCESANDO: no hay recuperación automática de intentos abandonados.

## Verificación y autenticación

Ejecutados `dotnet test backend/Gestio.slnx --artifacts-path artifacts/ocr` (62 pruebas correctas),
`npm.cmd run build` y `npm.cmd run lint` en frontend. Las pruebas cubren normalización, ambigüedad,
ausencias, error por motor ausente, conservación del original, reintento,
recarga, acceso denegado sin vínculo activo, tablas por coordenadas,
relecturas contradictorias, columnas de cliente y texto nativo sin confianza.
Las pruebas unitarias usan EF InMemory; el flujo correcto se prueba
con un doble unitario de ILectorDocumento, sin presentarlo como prueba OCR real.

Comprobado por HTTP con PostgreSQL subiendo los JPEG reales conservados por el
usuario. En «images» se recuperan emisor, CIF, fecha, base 171,36 €, IVA 10 %,
cuota 17,14 € y total 188,50 €, coincidentes con la imagen. El número queda
null por discrepancias entre lecturas. El total y el CIF proceden de palabras
releídas; no se calcula el total ni se cambia un 8 por una B en el código.
Los otros tickets conservan sus importes de 45,65 € y 109,70 €, con campos
vacíos cuando hay ausencia o conflicto de lectura. No hay datos de esos
documentos ni reglas por nombre de archivo o por empresa en el procesador.

Renombrar «images» produce exactamente los mismos campos. Los resultados
persisten al consultar de nuevo el detalle y el original descargado tiene el
mismo SHA-256 que los bytes subidos. Reprocesar conserva los intentos anteriores.
Dos reintentos simultáneos por HTTP devuelven 200 y 409 y crean un único
intento nuevo. Un autónomo ajeno recibe 404 en detalle, original y reintento.
Un PNG blanco válido termina en ERROR indicando que no se reconoce texto útil.

También se probó PNG y PDF escaneado convirtiendo el JPEG original sin añadir
datos. PNG conserva los siete campos de «images». El PDF escaneado recupera
base, IVA, cuota y total, aunque puede perder el CIF o leer incorrectamente el
número: la rasterización y los errores OCR siguen requiriendo revisión. No
se dispone de una factura PDF nativa real para una comparación visual completa;
esa asociación se verifica mediante pruebas unitarias con palabras posicionadas.
Las copias y recortes temporales de verificación se retiran, conservando los originales.
No se garantiza la lectura de todos los campos en imágenes borrosas, logos,
tablas complejas o cualquier diseño; los datos siguen requiriendo revisión.

## Cambios de precisión: disposicion-4

`ExtraccionIa.DesglosesIva` conserva una lista de base/tipo/cuota: lee tablas
y líneas `IVA 21% 12,34` o `TAX/IVA 10% 4,50`. Las bases ausentes quedan null.
Con varios tipos, los campos escalares tipo/cuota quedan null; el buzón muestra
las filas leídas. No suma bases ni cuotas para rellenar campos ausentes.
La lista y las advertencias se guardan en `DatosCrudosJson` y en la instantánea
de cada intento. No requiere nueva migración: reutiliza la columna JSONB.
Los registros antiguos sin estas propiedades devuelven listas vacías.

Se comprueba el control del NIF y la coherencia entre importes leídos; los
avisos se muestran en el buzón y no sustituyen cifras. Un NIF de control válido
puede descartar candidatos que no lo pasan. Identificadores de puntuación baja
necesitan coincidencia literal en preparaciones y regiones distintas, además
del control válido. Las contradicciones fiables siguen quedando null.
Una lectura débil discrepante no descarta un campo corroborado por lecturas
fiables; «pendiente de cobro» no reemplaza un TOTAL presente pero ilegible.
Se admiten también `Fac. Simplif`, `Tiquet`, `TOTAL IVA INC` y NIF sin etiqueta
si ocupa una línea completa de cabecera y supera el control.

Verificación actual: 62 pruebas automatizadas correctas; build y lint del
frontend correctos. Con cinco JPEG reales se han contrastado 16 expectativas
seleccionadas de los originales: 14 coinciden y 2 quedan null (base y total
de `CLvK8e1WoAAcSZN-225x300.jpg`). Renombrar los cinco archivos no cambia los
resultados. El ticket pequeño continúa recuperando sólo la fecha: las cifras
correctas no aparecen con evidencia suficiente en las lecturas; no se han
introducido valores específicos para ese archivo. No debe presentarse como
una solución completa para esa miniatura. El modelo `tessdata_best` español
4.1.0 se contrastó localmente y no mejoró el conjunto; se mantiene `fast`.

Resultados contrastados: `images` mantiene siete campos y total 188,50;
`una-factura` recupera fecha 14/09/2019, base 41,50, tipo 10%, cuota 4,15 y
total 45,65; `factura-chech` recupera CIF, número, fecha, base 289,26, cuota
60,74 y total 350,00, dejando el porcentaje ausente null;
`factura-de-la-comida` recupera número, fecha y total 225,70 sin inventar IVA.
La comprobación HTTP verifica recarga del JSON, original con idéntico SHA256,
acceso del gestor vinculado y rechazo del autónomo ajeno en detalle, original
y reintento. Dos reintentos simultáneos crean sólo un intento adicional.
El soporte de varios IVA se comprueba con pruebas unitarias; el único original
real disponible con dos tipos es la miniatura y no permite confirmar un
desglose completo mediante OCR.

Se ha repetido la verificación HTTP con PNG y PDF escaneado, convirtiendo
únicamente la imagen original de `images`, sin insertar texto. Ambos conservan
base 171,36, tipo 10%, cuota 17,14 y total 188,50. PNG mantiene los siete campos;
PDF conserva CIF y fecha pero pierde el nombre. Un PNG blanco termina en ERROR
con «No se reconoce texto útil. Sube un documento más legible.»; reintentarlo
conserva los dos intentos fallidos y el original accesible. Las seis copias de
prueba se retiran del buzón; no se alteran los documentos del usuario.

Para repetir la comparación con originales privados:

```text
dotnet run --project backend/tools/VerificarOcr --artifacts-path artifacts/verificador -- manifiesto.json artifacts/resultados --renombrar
```

El manifiesto es un array con `Archivo` (ruta), `TipoMime` y `Esperado`
(objeto de campos, incluidos null). Los esperados se comparan después del OCR;
no intervienen en él. El programa devuelve código 1 ante discrepancias y
guarda lecturas y resultados por documento para revisar los errores. No subir
originales ni informes fiscales privados al repositorio. Los informes locales
de esta comprobación están en `artifacts/mejora-ocr`, ignorado por Git.

## Cambios de precisión: disposicion-5

Preparación: si el papel ocupa menos del 85 % de la foto, está entero en el
encuadre y sus cuatro lados tienen salto de contraste real con el fondo, se
recorta y endereza con una homografía, se corrige la iluminación (sombras de
pliegues) y se enfoca si se ha ampliado. Las relecturas se hacen sólo sobre esa
geometría: los recortes de la foto inclinada mezclaban líneas. Sin papel claro
se sigue el flujo anterior, con corrección de iluminación sólo si hay sombras.
Tesseract parte a veces «109,70€» en «109,» y «70€»: dos trozos contiguos de
la misma línea con esa forma se reúnen en un único importe.

Reglas nuevas, todas sobre lo leído y sin calcular cifras:
- Fechas con el mes en letra: `04/Mar/2017`, `4 de marzo de 2017`, `15-DIC-19`.
- Fila fiscal con cabecera ilegible: se reconoce por coherencia exacta
  (base × tipo = cuota redondeada o truncada, base + cuota = total) con un tipo
  de IVA, IGIC o IPSI vigente. Si sólo falla la cuota, base × (1 + tipo) = total
  identifica base, tipo y total y la cuota queda null. Sin tolerancia: un dígito
  mal leído (12,39 por 12,38) no pasa.
- Emisor sin etiqueta: la línea en mayúsculas más cercana por encima de la
  primera dirección, código postal o teléfono de la cabecera. Se descartan las
  que terminan en «palabra,» (dirección partida) y las de tres letras iguales
  seguidas (asteriscos mal leídos). Su confianza se mide con las palabras que lo
  forman, no con palabras sueltas de la página.
- NIF/CIF con guion tras la letra (`B-81205791`) y `TVA` como etiqueta de IVA.
- No se toma total de una línea TOTAL con la cifra partida («109,» + ruido +
  «70€»), ni número de factura de palabras con minúsculas o una sola cifra
  («Preci0»).

Comprobado con Tesseract 5.4.0 y cinco imágenes, sin valores por archivo:
`factura-con-iva` pasa de ERROR a fecha, base 123,83, tipo 10, cuota 12,38 y
total 136,21; `factura-chech` añade emisor y número (todos los campos impresos);
el ticket El Raco y su versión lejana, inclinada y con sombra recuperan el total
109,70; la miniatura de 225×300 añade el CIF B07574296. En esa miniatura base,
total, número y segundo IVA siguen sin leerse: las cifras no tienen resolución
suficiente y las lecturas se contradicen; no se rellenan.

Para aplicar las mejoras en el buzón: reiniciar el backend y pulsar «Volver a
extraer» en los documentos existentes. Las nuevas cargas usan el procesador
`disposicion-5` automáticamente. En los JPEG comprobados, aproximadamente
20–40 segundos por extracción.

La aplicación existente usa una cabecera X-Usuario-Id elegida en pantalla y
carece de sesión autenticada. Se conserva ICurrentUserService y el filtro de
propietario/vinculación ACTIVA en consulta, original y reintento. La cabecera
es suplantable: cumplir el requisito de identidad autenticada exige sustituir
esa implementación por un login real, fuera de estos cambios.
