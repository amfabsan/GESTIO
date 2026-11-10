using Gestio.Api.Domain.Entities;
using Gestio.Api.Domain.Enums;
using Gestio.Api.Domain.Errores;
using Gestio.Api.Domain.Interfaces;
using Gestio.Api.Infrastructure.Data;
using Gestio.Api.Models.Request;
using Gestio.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Gestio.Api.Tests;

public sealed class FacturacionTests
{
    private static readonly Guid AutonomoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GestorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ContactoId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateOnly Fecha = new(2026, 6, 16);
    private static readonly IReadOnlyList<GuardarLineaFacturaRequest> LineaValida =
        [new("Servicio profesional", 1m, 100m, 21m)];

    [Fact]
    public void Calcular_AgrupaBasesCuotasYRetencion()
    {
        var resultado = CalculadoraFactura.Calcular(
        [
            new("General", 1m, 100m, 21m),
            new("Reducido", 1m, 50m, 10m)
        ], 15m);

        Assert.True(resultado.EsValido);
        Assert.Equal(150m, resultado.Calculo!.BaseImponible);
        Assert.Equal(new DesgloseIvaFactura(10m, 50m, 5m), resultado.Calculo.DesgloseIva[0]);
        Assert.Equal(new DesgloseIvaFactura(21m, 100m, 21m), resultado.Calculo.DesgloseIva[1]);
        Assert.Equal(26m, resultado.Calculo.CuotaIva);
        Assert.Equal(22.5m, resultado.Calculo.ImporteRetencion);
        Assert.Equal(153.5m, resultado.Calculo.Total);
    }

    [Fact]
    public void Calcular_RedondeaMitadesAwayFromZeroEnCuotaYRetencion()
    {
        var resultado = CalculadoraFactura.Calcular([new("Servicio", 1m, 0.05m, 10m)], 10m);

        Assert.True(resultado.EsValido);
        Assert.Equal(0.05m, resultado.Calculo!.BaseImponible);
        Assert.Equal(0.01m, resultado.Calculo.CuotaIva);
        Assert.Equal(0.01m, resultado.Calculo.ImporteRetencion);
        Assert.Equal(0.05m, resultado.Calculo.Total);
    }

    [Theory]
    [InlineData(null, 1d, 10d, 21d, "Factura.Linea.DescripcionObligatoria")]
    [InlineData("Servicio", 0d, 10d, 21d, "Factura.Linea.CantidadNoValida")]
    [InlineData("Servicio", 1d, -1d, 21d, "Factura.Linea.PrecioNoValido")]
    [InlineData("Servicio", 1d, 10d, 5d, "Factura.Linea.IvaNoValido")]
    public void Calcular_RechazaLineasInvalidas(string? descripcion, double? cantidad, double? precio, double? iva, string codigo)
    {
        var resultado = CalculadoraFactura.Calcular([new(
            descripcion,
            cantidad is null ? null : (decimal)cantidad.Value,
            precio is null ? null : (decimal)precio.Value,
            iva is null ? null : (decimal)iva.Value)], null);

        Assert.False(resultado.EsValido);
        Assert.Contains(codigo, resultado.Errores);
        Assert.Null(resultado.Calculo);
    }

    [Fact]
    public async Task GuardarBorrador_PermiteFacturaIncompletaSinNumero()
    {
        await using var db = await CrearDbAsync();
        var resultado = await Servicio(db).GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(null, Fecha, null, [new(null, null, null, null)]), default);

        Assert.True(resultado.EsCorrecto);
        Assert.Equal("BORRADOR", resultado.Valor!.Estado);
        Assert.Equal("F-2026", resultado.Valor.Serie);
        Assert.Null(resultado.Valor.Numero);
        Assert.Null(resultado.Valor.Total);
    }

    [Fact]
    public async Task Comprobar_ExigeDireccionActivaYPermiteCorregirContacto()
    {
        await using var db = await CrearDbAsync(direccion: null);
        var servicio = Servicio(db);
        var borrador = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default);
        Assert.True(borrador.EsCorrecto);

        var incompleta = await servicio.ComprobarAsync(Propietario, AutonomoId, borrador.Valor!.Id, default);
        Assert.False(incompleta.EsCorrecto);
        Assert.Contains("dirección fiscal", incompleta.Error!.Mensaje, StringComparison.OrdinalIgnoreCase);

        var guardada = await servicio.GuardarDireccionFiscalAsync(Propietario, AutonomoId, ContactoId, "Calle Mayor 10, Madrid", default);
        Assert.True(guardada.EsCorrecto);
        var comprobacion = await servicio.ComprobarAsync(Propietario, AutonomoId, borrador.Valor.Id, default);
        Assert.True(comprobacion.EsCorrecto);
        Assert.Null(comprobacion.Valor!.Numero);
    }

    [Fact]
    public async Task Emitir_ExigeDireccionFiscalDelEmisorYSoloElPropietarioPuedeGuardarla()
    {
        await using var db = await CrearDbAsync(incluirGestor: true, direccionFiscalEmisor: null);
        var facturacion = Servicio(db);
        var perfil = new PerfilesFiscalesService(db, TimeProvider.System, new GeneradorObligaciones(db, TimeProvider.System));
        var borrador = await facturacion.GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default);

        var bloqueada = await facturacion.ComprobarAsync(Propietario, AutonomoId, borrador.Valor!.Id, default);
        Assert.False(bloqueada.EsCorrecto);
        Assert.Contains("dirección fiscal del emisor", bloqueada.Error!.Mensaje, StringComparison.OrdinalIgnoreCase);
        var gestor = await perfil.GuardarDireccionFiscalAsync(new UsuarioActual(GestorId, Rol.GESTOR), AutonomoId, "Dirección no autorizada", default);
        Assert.False(gestor.EsCorrecto);
        Assert.Equal(TipoError.PROHIBIDO, gestor.Error!.Tipo);

        var guardada = await perfil.GuardarDireccionFiscalAsync(Propietario, AutonomoId, "Calle Emisor 20, Madrid", default);
        Assert.True(guardada.EsCorrecto);
        var emitida = await facturacion.EmitirAsync(Propietario, AutonomoId, borrador.Valor.Id, default);
        Assert.True(emitida.EsCorrecto);
        Assert.Equal("Calle Emisor 20, Madrid", emitida.Valor!.EmisorDireccion);
    }

    [Fact]
    public async Task GestorVinculadoSoloConsultaYGestorNoVinculadoNoAccede()
    {
        await using var db = await CrearDbAsync(incluirGestor: true);
        var servicio = Servicio(db);
        var borrador = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default);
        Assert.True(borrador.EsCorrecto);

        var consulta = await servicio.ConsultarAsync(new UsuarioActual(GestorId, Rol.GESTOR), AutonomoId, borrador.Valor!.Id, default);
        Assert.True(consulta.EsCorrecto);
        var emitir = await servicio.EmitirAsync(new UsuarioActual(GestorId, Rol.GESTOR), AutonomoId, borrador.Valor.Id, default);
        Assert.False(emitir.EsCorrecto);
        Assert.Equal(TipoError.PROHIBIDO, emitir.Error!.Tipo);
        var noVinculado = await servicio.ListarAsync(new UsuarioActual(Guid.NewGuid(), Rol.GESTOR), AutonomoId, default);
        Assert.False(noVinculado.EsCorrecto);
    }

    [Fact]
    public async Task Emitir_RepetidaConservaNumeroYSnapshotYRechazaCambiosPosteriores()
    {
        await using var db = await CrearDbAsync();
        var servicio = Servicio(db);
        var request = new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida);
        var borrador = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null, request, default);

        var emitida = await servicio.EmitirAsync(Propietario, AutonomoId, borrador.Valor!.Id, default);
        var repetida = await servicio.EmitirAsync(Propietario, AutonomoId, borrador.Valor.Id, default);
        Assert.True(emitida.EsCorrecto);
        Assert.True(repetida.EsCorrecto);
        Assert.Equal(emitida.Valor!.Numero, repetida.Valor!.Numero);
        Assert.Equal(1, emitida.Valor.Numero);
        Assert.Equal("Cliente", emitida.Valor.ClienteNombre);

        var contacto = await db.Contactos.SingleAsync(c => c.Id == ContactoId);
        contacto.Nombre = "Nombre cambiado";
        contacto.Nif = "NIFCAMBIADO";
        var autonomo = await db.Usuarios.SingleAsync(u => u.Id == AutonomoId);
        autonomo.Nombre = "Emisor cambiado";
        autonomo.DireccionFiscal = "Calle Emisor modificada";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var consultada = await servicio.ConsultarAsync(Propietario, AutonomoId, borrador.Valor.Id, default);
        Assert.Equal("Cliente", consultada.Valor!.ClienteNombre);
        Assert.Equal("12345678Z", consultada.Valor.ClienteNif);
        Assert.Equal("Autónomo Uno Dos", consultada.Valor.EmisorNombre);
        Assert.Equal("Calle Emisor original", consultada.Valor.EmisorDireccion);

        var edicion = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, borrador.Valor.Id, request, default);
        Assert.False(edicion.EsCorrecto);
        Assert.Equal(TipoError.CONFLICTO, edicion.Error!.Tipo);
        var eliminacion = await servicio.EliminarBorradorAsync(Propietario, AutonomoId, borrador.Valor.Id, default);
        Assert.False(eliminacion.EsCorrecto);
    }

    [Fact]
    public async Task Emitir_AsignaNumerosSecuencialesPorSerie()
    {
        await using var db = await CrearDbAsync();
        var servicio = Servicio(db);
        var primero = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default);
        var segundo = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null,
            new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default);

        var emitidaUno = await servicio.EmitirAsync(Propietario, AutonomoId, primero.Valor!.Id, default);
        var emitidaDos = await servicio.EmitirAsync(Propietario, AutonomoId, segundo.Valor!.Id, default);

        Assert.Equal(1, emitidaUno.Valor!.Numero);
        Assert.Equal(2, emitidaDos.Valor!.Numero);
        var indice = db.Model.FindEntityType(typeof(Factura))!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["AutonomoId", "Serie", "Numero"]));
        Assert.True(indice.IsUnique);
        Assert.Contains("Numero", indice.GetFilter());
    }

    [PostgreSqlFact]
    public async Task NumeracionConcurrente_AsignaNumerosDistintosPorBloqueoYRestriccionUnica()
    {
        var connectionString = Environment.GetEnvironmentVariable("GESTIO_TEST_POSTGRES")!;
        var schema = $"pb21_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(schemaConnection).Options;
            Guid borradorUno;
            Guid borradorDos;
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Usuarios.Add(new Usuario
                {
                    Id = AutonomoId, Nombre = "Autónomo", PrimerApellido = "Uno", SegundoApellido = "Dos",
                    Email = "autonomo@test", Nif = "87654321X", Rol = Rol.AUTONOMO, DireccionFiscal = "Calle Emisor original"
                });
                db.Contactos.Add(new Contacto
                {
                    Id = ContactoId, AutonomoId = AutonomoId, Clasificacion = "CLIENTE", Nombre = "Cliente",
                    Nif = "12345678Z", Direccion = "Calle 1", Activo = true
                });
                await db.SaveChangesAsync();
                var servicio = Servicio(db);
                borradorUno = (await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null,
                    new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default)).Valor!.Id;
                borradorDos = (await servicio.GuardarBorradorAsync(Propietario, AutonomoId, null,
                    new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default)).Valor!.Id;
                var editado = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, borradorUno,
                    new GuardarFacturaRequest(ContactoId, Fecha, null, [new("Servicio actualizado", 2m, 50m, 21m)]), default);
                Assert.True(editado.EsCorrecto);
                var editadoDeNuevo = await servicio.GuardarBorradorAsync(Propietario, AutonomoId, borradorUno,
                    new GuardarFacturaRequest(ContactoId, Fecha, null, LineaValida), default);
                Assert.True(editadoDeNuevo.EsCorrecto);
            }

            async Task<Gestio.Api.Domain.Errores.Resultado<Gestio.Api.Models.Response.FacturaResponse>> EmitirAsync(Guid id)
            {
                await using var db = new ApplicationDbContext(options);
                return await Servicio(db).EmitirAsync(Propietario, AutonomoId, id, default);
            }

            var emitidas = await Task.WhenAll(EmitirAsync(borradorUno), EmitirAsync(borradorDos));
            Assert.All(emitidas, resultado => Assert.True(resultado.EsCorrecto));
            Assert.Equal(new int?[] { 1, 2 }, emitidas.Select(x => x.Valor!.Numero).Order().ToArray());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static UsuarioActual Propietario => new(AutonomoId, Rol.AUTONOMO);

    private static FacturacionService Servicio(ApplicationDbContext db) =>
        new(db, new GeneradorObligaciones(db, TimeProvider.System), TimeProvider.System);

    private static async Task<ApplicationDbContext> CrearDbAsync(string? direccion = "Calle Original 1", bool incluirGestor = false,
        string? direccionFiscalEmisor = "Calle Emisor original")
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Usuarios.Add(new Usuario
        {
            Id = AutonomoId, Nombre = "Autónomo", PrimerApellido = "Uno", SegundoApellido = "Dos",
            Email = "autonomo@test", Nif = "87654321X", Rol = Rol.AUTONOMO, DireccionFiscal = direccionFiscalEmisor
        });
        db.Contactos.Add(new Contacto
        {
            Id = ContactoId, AutonomoId = AutonomoId, Clasificacion = "CLIENTE", Nombre = "Cliente",
            Nif = "12345678Z", Direccion = direccion, Activo = true
        });
        if (incluirGestor)
        {
            db.Usuarios.Add(new Usuario
            {
                Id = GestorId, Nombre = "Gestor", PrimerApellido = "Uno", SegundoApellido = "Dos",
                Email = "gestor@test", Nif = "GESTOR", Rol = Rol.GESTOR, CodigoAsesoria = "ASE-123456"
            });
            db.Vinculaciones.Add(new Vinculacion
            {
                Id = Guid.NewGuid(), AutonomoId = AutonomoId, CodigoAsesoria = "ASE-123456",
                Estado = EstadoVinculacion.ACTIVA, FechaSolicitud = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return db;
    }
}