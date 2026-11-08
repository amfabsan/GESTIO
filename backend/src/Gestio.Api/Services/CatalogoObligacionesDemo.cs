using Gestio.Api.Domain.Entities;

namespace Gestio.Api.Services;

// Catálogo académico reducido confirmado para PB-13. No determina obligaciones reales ni plazos AEAT.
public static class CatalogoObligacionesDemo
{
    public const string Aviso = "Fechas configuradas para la demostración";
    public const string Version = "PB13-reducido-v1";

    public sealed record Entrada(string Modelo, string Nombre, string Periodo, DateOnly FechaPrevista);

    public static IReadOnlyList<Entrada> Calcular(PerfilFiscal perfil)
    {
        var modelos = new List<(string Codigo, string Nombre)>();
        if (perfil.RegimenIva is "GENERAL" or "SIMPLIFICADO")
            modelos.Add(("303", "Autoliquidación del IVA"));
        if (perfil.RegimenIrpf is "ESTIMACION_DIRECTA_NORMAL" or "ESTIMACION_DIRECTA_SIMPLIFICADA")
            modelos.Add(("130", "Pago fraccionado del IRPF"));
        if (perfil.RegimenIrpf == "ESTIMACION_OBJETIVA")
            modelos.Add(("131", "Pago fraccionado del IRPF"));

        return modelos.SelectMany(m => Enumerable.Range(1, 4).Select(t => new Entrada(m.Codigo, m.Nombre,
            $"{t}T", t == 4 ? new DateOnly(perfil.Ejercicio + 1, 1, 25) : new DateOnly(perfil.Ejercicio, t * 3 + 1, 25)))).ToArray();
    }
}
