using System.Security.Cryptography;
using System.Text;

namespace Gestio.Api.Infrastructure.VeriFactu;

public sealed class VeriFactuService
{
    public string CalcularHuella(string datosFactura, string? huellaAnterior)
    {
        var payload = $"{huellaAnterior ?? string.Empty}|{datosFactura}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
