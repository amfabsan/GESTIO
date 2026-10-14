namespace Gestio.Api.Common;

public sealed class DocumentosOptions
{
    public const string Seccion = "Documentos";

    public long TamanoMaximoBytes { get; set; } = 10 * 1024 * 1024;

    public string RutaAlmacenamiento { get; set; } = "storage/documentos";
    
}