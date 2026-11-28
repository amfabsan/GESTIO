using System.Threading.Channels;

namespace Gestio.Api.Services.Whatsapp;

// UT27 (D1): cola en memoria de mensajes con archivo pendientes de procesar. Solo lleva el id del mensaje; los datos
// están en mensajes_whatsapp, así que si el proceso se reinicia nada se pierde (ProcesadorMensajesWhatsapp vuelve a
// encolar los EN_CURSO al arrancar).
public sealed class ColaMensajesWhatsapp
{
    private readonly Channel<string> canal = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public void Encolar(string mensajeId) => canal.Writer.TryWrite(mensajeId);

    public IAsyncEnumerable<string> LeerAsync(CancellationToken cancellationToken) => canal.Reader.ReadAllAsync(cancellationToken);

    public bool TryLeer(out string mensajeId) => canal.Reader.TryRead(out mensajeId!);
}
