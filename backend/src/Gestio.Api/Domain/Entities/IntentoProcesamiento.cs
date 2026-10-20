namespace Gestio.Api.Domain.Entities;

public sealed class IntentoProcesamiento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentoId { get; set; }
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public string Procesador { get; set; } = "tesseract-poppler/disposicion-4";
    public string? Version { get; set; }
    public string Resultado { get; set; } = "PROCESANDO";
    public string? Error { get; set; }
    public string? ValoresJson { get; set; }
}
