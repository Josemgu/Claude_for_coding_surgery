using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Decir «esto no está en el papel» y que el campo deje de pedir un dato que no existe.
/// </summary>
/// <remarks>
/// Palabras del dueno el 2026-09-05: «falta el boton de "esta informacion no es necesaria"
/// para guardar el documento».
/// <para>
/// La columna ya existe desde la version 14 del esquema:
/// <c>procedencia_campo.ausente_en_el_papel</c>, que la escribio el Python en
/// <c>datos/migraciones_de_revision.py</c> con este motivo textual: «un campo que el papel
/// no trae no es un fallo de lectura ni un borrado [...] sin ella, "no esta en el papel" se
/// ve exactamente igual que "el OCR no supo leerlo"».
/// </para>
/// <para>
/// ⛔ <b>Marcar NO es firmar.</b> Regla permanente 5: esto deja
/// <c>ausente_en_el_papel = 1</c> y <c>verificado = 0</c>. Son dos actos y no se mezclan.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLoQueNoEstaEnElPapel
{
    private const long CasoDePrueba = 200;
    private const long PersonaDePrueba = 201;
    private const long CompaneroDePrueba = 1;

    /// <summary>Un caso con el templo vacio, que es como llega de los escaneos del dueno.</summary>
    private static (ProcedenciaComoLaDeVerdad Procedencia, ModeloDeCorreccion Modelo) Montar(string? templo = null)
    {
        var servicios = new ServiciosFalsos(3, 20260905, new RelojFijo("2026-09-05"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-08",
            TemploNombre = templo,
            RutaPdf = @"C:\no-se-abre.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-05",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Ana Prueba",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };

        var procedencia = new ProcedenciaComoLaDeVerdad(CompaneroDePrueba);
        var modelo = new ModeloDeCorreccion(
            servicios.Casos, servicios.Personas, procedencia,
            servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);
        modelo.Cargar(CasoDePrueba);
        return (procedencia, modelo);
    }

    /// <summary>El campo del caso que se llama asi.</summary>
    private static CampoEnPantalla CampoDelCaso(ModeloDeCorreccion modelo, string campo)
        => modelo.Campos.Single(c => c.Tabla == TablaDeProcedencia.Casos && c.Campo == campo);

    // ---- el criterio de cierre -------------------------------------------

    /// <summary>
    /// Dado un campo vacio, cuando se marca que no está en el papel, entonces la base lo
    /// registra y el campo deja de contar como pendiente.
    /// </summary>
    [TestMethod]
    public void MarcarloLoRegistraEnLaBaseYLoSacaDeLoPendiente()
    {
        var (procedencia, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");
        Assert.IsTrue(modelo.Dudosos.Any(c => c.Campo == "templo_nombre"));

        var resultado = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));
        var fila = procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba)
            .Single(p => p.Campo == "templo_nombre");
        Assert.IsTrue(fila.AusenteEnElPapel);
        Assert.IsFalse(modelo.Dudosos.Any(c => c.Campo == "templo_nombre"));
    }

    /// <summary>Marcarlo NO lo firma: no pone verificado ni inventa quien.</summary>
    /// <remarks>Regla permanente 5. Son dos actos distintos y se miden por separado.</remarks>
    [TestMethod]
    public void MarcarloNoEsFirmarlo()
    {
        var (procedencia, modelo) = Montar();
        modelo.MarcarQueNoEstaEnElPapel(CampoDelCaso(modelo, "templo_nombre"), marcado: true);

        var fila = procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba)
            .Single(p => p.Campo == "templo_nombre");
        Assert.IsFalse(fila.Verificado);
        Assert.IsNull(fila.VerificadoPor);
        Assert.IsNull(fila.VerificadoEn);
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>En la pantalla se ve que esta marcado, con una palabra y no solo con un color.</summary>
    [TestMethod]
    public void EnLaPantallaSeVeQueEstaMarcadoYConPalabras()
    {
        var (_, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");
        modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        var estado = modelo.EstadoDe(templo);
        Assert.AreEqual(EstadoDeCampo.NoEstaEnElPapel, estado);
        Assert.AreEqual("no está en el papel", EstadosDeCampo.Palabra(estado));
        Assert.IsTrue(modelo.EstaMarcadoComoAusente(templo));
    }

    /// <summary>Se puede deshacer: lo que se marca por error se desmarca.</summary>
    [TestMethod]
    public void SePuedeDeshacer()
    {
        var (procedencia, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");
        modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        var resultado = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: false);

        Assert.IsTrue(resultado.SeEscribio);
        var fila = procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba)
            .Single(p => p.Campo == "templo_nombre");
        Assert.IsFalse(fila.AusenteEnElPapel);
        Assert.IsTrue(modelo.Dudosos.Any(c => c.Campo == "templo_nombre"));
    }

    /// <summary>
    /// Un campo que SI tiene un dato guardado no se marca como ausente, y se dice por que.
    /// </summary>
    /// <remarks>
    /// Marcar «el papel no trae este campo» sobre un campo con un dato dentro son dos
    /// afirmaciones que se contradicen, y la unica forma de arreglarlo por dentro seria
    /// borrar el dato en silencio. Se pide primero borrarlo y guardarlo, que es un acto
    /// de Miguel y se ve.
    /// </remarks>
    [TestMethod]
    public void UnCampoConDatoNoSeMarcaYSeDicePorQue()
    {
        var (procedencia, modelo) = Montar(templo: "Santo Domingo Dominican Republic");
        var templo = CampoDelCaso(modelo, "templo_nombre");

        var resultado = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsTrue(resultado.HayAvisos);
        Assert.IsEmpty(procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba)
            .Where(p => p.Campo == "templo_nombre" && p.AusenteEnElPapel));
    }

    /// <summary>
    /// Lo tecleado sin guardar tampoco deja marcarlo: primero se decide que hacer con ello.
    /// </summary>
    [TestMethod]
    public void ConAlgoTecleadoSinGuardarNoSeMarca()
    {
        var (_, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");
        modelo.Teclear(templo.Clave, "Caracas Venezuela");

        var resultado = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.IsTrue(resultado.HayAvisos);
    }

    /// <summary>Marcar un campo ya firmado le retira la firma: son dos respuestas distintas.</summary>
    /// <remarks>
    /// No puede quedar a la vez «Miguel dio por bueno este dato» y «este campo no está en el
    /// papel». La firma se cae, como se cae cuando el valor cambia.
    /// </remarks>
    [TestMethod]
    public void MarcarUnCampoFirmadoLeRetiraLaFirma()
    {
        var (procedencia, modelo) = Montar();
        var templo = CampoDelCaso(modelo, "templo_nombre");
        modelo.Teclear(templo.Clave, "Santo Domingo Dominican Republic");
        modelo.Firmar(templo, CompaneroDePrueba);
        modelo.Guardar(new Dictionary<string, string?> { [templo.Clave] = null });

        var resultado = modelo.MarcarQueNoEstaEnElPapel(templo, marcado: true);

        Assert.IsTrue(resultado.SeEscribio, string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));
        Assert.AreEqual(0, procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }
}
