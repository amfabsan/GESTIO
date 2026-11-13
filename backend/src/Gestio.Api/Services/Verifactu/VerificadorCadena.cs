using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;

namespace Gestio.Api.Services.Verifactu;

public sealed record IncoherenciaCadena(Guid RegistroId, int Secuencia, string Codigo, string Motivo);

public sealed record ResultadoVerificacionCadena(bool Coherente, int Revisados, IncoherenciaCadena? Incoherencia);

/// <summary>
/// Verifica la cadena de registros de UN autónomo: recalcula cada huella y comprueba que la referencia
/// y la huella anterior coinciden con el registro precedente. Se detiene en el primer registro incoherente.
/// Límites conocidos (propios de cualquier cadena de huellas):
/// - Borrar el ÚLTIMO registro (o varios del final) deja una cadena más corta pero coherente: eso lo impide la
///   inmutabilidad en la BD (capa 2), no este verificador.
/// - <c>FacturaId</c> no entra en la huella (tampoco en la de la AEAT); no se detecta un cambio de factura asociada.
/// - El llamador debe pasar solo los registros de un autónomo; el primero de la lista fija el autónomo esperado.
/// </summary>
public static class VerificadorCadena
{
    public const string SecuenciaRota = "Cadena.SecuenciaRota";
    public const string OtroAutonomo = "Cadena.OtroAutonomo";
    public const string TipoNoValido = "Cadena.TipoNoValido";
    public const string HuellaNoCoincide = "Cadena.HuellaNoCoincide";
    public const string FechaGeneracionNoCoincide = "Cadena.FechaGeneracionNoCoincide";
    public const string EnlaceNoCoincide = "Cadena.EnlaceNoCoincide";
    public const string HuellaAnteriorNoCoincide = "Cadena.HuellaAnteriorNoCoincide";

    public static ResultadoVerificacionCadena Verificar(IEnumerable<RegistroFacturacion> registros)
    {
        var orden = registros.OrderBy(r => r.Secuencia).ThenBy(r => r.Id).ToList();
        for (var i = 0; i < orden.Count; i++)
        {
            var registro = orden[i];
            var anterior = i == 0 ? null : orden[i - 1];
            var siguiente = i + 1 < orden.Count ? orden[i + 1] : null;
            var motivo = Comprobar(registro, anterior, siguiente, orden[0].AutonomoId, i + 1);
            if (motivo is not null)
                return new ResultadoVerificacionCadena(false, i + 1,
                    new IncoherenciaCadena(registro.Id, registro.Secuencia, motivo.Value.Codigo, motivo.Value.Texto));
        }
        return new ResultadoVerificacionCadena(true, orden.Count, null);
    }

    private static (string Codigo, string Texto)? Comprobar(
        RegistroFacturacion registro, RegistroFacturacion? anterior, RegistroFacturacion? siguiente, Guid autonomoId, int secuenciaEsperada)
    {
        if (registro.AutonomoId != autonomoId)
            return (OtroAutonomo, "El registro pertenece a otro autónomo.");
        // Antes que el enlace y mirando también al siguiente: con posiciones duplicadas el orden entre ellas es arbitrario,
        // así que el diagnóstico debe ser el mismo sea cual sea el duplicado que quede primero.
        if ((anterior is not null && registro.Secuencia == anterior.Secuencia) ||
            (siguiente is not null && registro.Secuencia == siguiente.Secuencia))
            return (SecuenciaRota, $"Hay más de un registro en la posición #{registro.Secuencia}: la cadena se ha dividido.");
        if (registro.Secuencia != secuenciaEsperada)
            return (SecuenciaRota, $"Se esperaba el registro #{secuenciaEsperada} y aparece el #{registro.Secuencia}: falta o sobra un registro en la cadena.");
        if (registro.Tipo != TipoRegistroFacturacion.ALTA)
            return (TipoNoValido, "El tipo de registro no es ALTA.");
        if (!string.Equals(HuellaVerifactu.Calcular(DatosHuellaAlta.De(registro)), registro.Huella, StringComparison.Ordinal))
            return (HuellaNoCoincide, "La huella recalculada no coincide con la almacenada: algún dato del registro ha cambiado.");
        if (!string.Equals(HuellaVerifactu.FormatearFechaHoraHuso(registro.Generado), registro.FechaHoraHusoGenRegistro, StringComparison.Ordinal))
            return (FechaGeneracionNoCoincide, "La fecha de generación almacenada no coincide con la usada en la huella.");
        if (registro.AnteriorId != anterior?.Id)
            return (EnlaceNoCoincide, anterior is null
                ? "El inicio de cadena no debería apuntar a ningún registro anterior."
                : "La referencia al registro anterior no coincide con el registro precedente.");
        // null y "" son equivalentes (el canónico escribe "Huella=" en ambos casos); así no depende del mapeo de la BD.
        if (!string.Equals(NullSiVacia(registro.HuellaAnterior), NullSiVacia(anterior?.Huella), StringComparison.Ordinal))
            return (HuellaAnteriorNoCoincide, "La huella anterior guardada no coincide con la huella del registro precedente.");
        return null;
    }

    private static string? NullSiVacia(string? valor) => string.IsNullOrEmpty(valor) ? null : valor;
}
