using Gestio.Api.Common;
using Gestio.Api.Infrastructure.VeriFactu;
using Microsoft.Extensions.Options;

namespace Gestio.Api.Tests;

/// <summary>UT23: QrFacturaService con una URL base fija para las pruebas que construyen FacturacionService.</summary>
internal static class QrDePrueba
{
    public const string Base = "http://192.168.1.20:5173";

    public static QrFacturaService Servicio(string baseConfigurada = Base) =>
        new(Options.Create(new VerifactuOptions { UrlPublicaBase = baseConfigurada }));
}
