using Microsoft.EntityFrameworkCore;
using Gestio.Api.Common;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Infrastructure.Identidad;
using Gestio.Api.Infrastructure.Storage;
using Gestio.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Gestio.Api.Infrastructure.Data.ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<Gestio.Api.Infrastructure.VeriFactu.VeriFactuService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDevelopment", policy => policy
        .WithOrigins("http://localhost:5173", "https://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, UsuarioActualDesdeCabecera>();
builder.Services.AddScoped<PanelGestorService>();

builder.Services.Configure<DocumentosOptions>(builder.Configuration.GetSection(DocumentosOptions.Seccion));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<DocumentosService>();
builder.Services.AddScoped<AccesoDocumentos>();
builder.Services.AddScoped<HistorialDocumentoService>();
builder.Services.AddScoped<ILectorDocumento, LectorDocumentoLocal>();
builder.Services.AddSingleton<MotorOcrLatin>();
builder.Services.AddScoped<VinculacionesService>();
builder.Services.AddScoped<ContactosService>();
builder.Services.AddScoped<FacturacionService>();
builder.Services.AddScoped<Gestio.Api.Services.Verifactu.RegistroFacturacionService>();
// UT23 (PB-23): QR y consulta pública. La URL base se valida al arrancar: sin ella no se emite ningún QR roto.
builder.Services.AddOptions<VerifactuOptions>()
    .Bind(builder.Configuration.GetSection(VerifactuOptions.Seccion))
    .Validate(o => VerifactuOptions.EsUrlBaseValida(o.UrlPublicaBase?.Trim()),
        $"{VerifactuOptions.Seccion}:UrlPublicaBase debe ser una URL http(s) absoluta sin query ni fragmento.")
    .ValidateOnStart();
builder.Services.AddSingleton<Gestio.Api.Infrastructure.VeriFactu.QrFacturaService>();
builder.Services.AddScoped<Gestio.Api.Services.Verifactu.ConsultaPublicaFacturaService>();
builder.Services.AddScoped<PerfilesFiscalesService>();
builder.Services.AddScoped<GeneradorObligaciones>();
builder.Services.AddScoped<ObligacionesFiscalesService>();
builder.Services.AddScoped<ChecklistObligacionesService>();
builder.Services.AddScoped<VinculacionEvidenciasChecklistService>();
builder.Services.AddScoped<EstimacionIvaService>();
builder.Services.AddScoped<InclusionGastoService>();
// UT27: canal de WhatsApp.
builder.Services.Configure<WhatsappOptions>(builder.Configuration.GetSection(WhatsappOptions.Seccion));
builder.Services.AddScoped<Gestio.Api.Services.Whatsapp.VerificacionNumeroWhatsappService>();
builder.Services.AddScoped<Gestio.Api.Services.Whatsapp.RecepcionWhatsappService>();
builder.Services.AddSingleton<Gestio.Api.Services.Whatsapp.ColaMensajesWhatsapp>();
builder.Services.AddHostedService<Gestio.Api.Services.Whatsapp.ProcesadorMensajesWhatsapp>();
// La capa 4 registra el proveedor de Meta cuando Whatsapp:Habilitado = true.
builder.Services.AddSingleton<IProveedorWhatsapp, Gestio.Api.Infrastructure.Whatsapp.ProveedorWhatsappDesactivado>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();

    if (builder.Configuration.GetValue<bool>("DatabaseConfig:SeedSampleData"))
    {
        await DbInitializer.SeedAsync(dbContext, incluirFacturasRamon: false);
        await DbInitializer.SeedSolicitudesDemoAsync(dbContext);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("FrontendDevelopment");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
