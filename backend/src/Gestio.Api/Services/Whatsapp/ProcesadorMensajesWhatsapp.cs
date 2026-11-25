namespace Gestio.Api.Services.Whatsapp;

// UT27 (D1): procesa en segundo plano los archivos recibidos por WhatsApp, de uno en uno, cada uno en su propio
// ámbito de inyección (su propio DbContext). Al arrancar vuelve a encolar los que quedaron EN_CURSO.
public sealed class ProcesadorMensajesWhatsapp(
    IServiceScopeFactory scopeFactory,
    ColaMensajesWhatsapp cola,
    ILogger<ProcesadorMensajesWhatsapp> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ReencolarPendientesAsync(stoppingToken);

            await foreach (var mensajeId in cola.LeerAsync(stoppingToken))
                await ProcesarAsync(mensajeId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal. Lo que quedara en la cola sigue EN_CURSO y se reencola al arrancar.
        }
    }

    public async Task ReencolarPendientesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recepcion = scope.ServiceProvider.GetRequiredService<RecepcionWhatsappService>();
            foreach (var mensajeId in await recepcion.PendientesAsync(cancellationToken))
                cola.Encolar(mensajeId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "No se pudieron recuperar los mensajes de WhatsApp pendientes al arrancar");
        }
    }

    public async Task ProcesarAsync(string mensajeId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RecepcionWhatsappService>().ProcesarArchivoAsync(mensajeId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo inesperado no detiene la cola. El mensaje queda EN_CURSO y una nueva entrega del proveedor
            // pasados 5 minutos, o el siguiente arranque, lo vuelve a intentar.
            logger.LogError(ex, "Fallo inesperado al procesar el mensaje de WhatsApp {MensajeId}", mensajeId);
        }
    }
}
