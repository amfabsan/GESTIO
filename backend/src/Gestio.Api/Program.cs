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

builder.Services.Configure<DocumentosOptions>(builder.Configuration.GetSection(DocumentosOptions.Seccion));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<DocumentosService>();
builder.Services.AddScoped<VinculacionesService>();

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
        await DbInitializer.SeedAsync(dbContext);
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