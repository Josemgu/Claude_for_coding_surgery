using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Que corregir un campo deja UNA fila de procedencia, y que conserva lo que leyo el OCR.
/// </summary>
/// <remarks>
/// <para>
/// El defecto que estas pruebas cazan, devuelto por el pase de conexion del 2026-09-04:
/// la importacion escribe <c>procedencia_campo.campo</c> con el nombre de la columna
/// (<c>unidad_nombre</c>) y la pantalla de correccion lo escribia con el nombre de la
/// propiedad de C# (<c>UnidadNombre</c>). La clave de esa tabla es
/// (tabla, registro_id, campo), asi que no se pisaban: quedaban DOS filas del mismo
/// campo. Y como la pantalla buscaba por el nombre equivocado, no veia ni la banda del
/// papel, ni el valor que leyo el OCR, ni la confianza.
/// </para>
/// <para>
/// ⚠️ <b>Los nombres de esta clase son literales a proposito.</b> Se copian del esquema
/// —las columnas de <c>casos</c> y <c>personas</c>, ARQUITECTURA §2.2 y §2.3— y NO de
/// ninguna constante del codigo que prueban. Una prueba escrita mirando el codigo solo
/// confirma lo que su autor entendio, y pasa en verde estando mal: es exactamente asi
/// como este defecto sobrevivio.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaProcedenciaAlCorregir
{
    private const long CasoDePrueba = 200;
    private const long PersonaDePrueba = 201;

    /// <summary>Los siete nombres de columna que la pantalla corrige, copiados del esquema.</summary>
    private static readonly string[] NombresDeColumnaDelEsquema =
    [
        "numero_caso", "unidad_numero", "unidad_nombre", "fecha_viaje", "templo_nombre",
        "mrn", "nombre",
    ];

    /// <summary>Lo que el OCR leyo mal de la etiqueta de la unidad; la unica prueba del papel.</summary>
    private const string LoQueLeyoElOcr = "Castrles Branch";

    /// <summary>
    /// Dado un campo que la importacion anoto con el nombre de la columna, cuando Miguel
    /// lo corrige y guarda, entonces queda UNA fila y conserva la banda y el valor del OCR.
    /// </summary>
    [TestMethod]
    public void CorregirUnCampoDejaUnaSolaFilaDeProcedenciaConSuBanda()
    {
        var servicios = MontarComoLoDejaLaImportacion();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var unidad = modelo.Campos.Single(c => c.Campo == "unidad_nombre");
        modelo.Guardar(new Dictionary<string, string?>
        {
            [unidad.Clave] = "Castries Branch",
        });

        var delCaso = servicios.Procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba);
        var deEsteCampo = delCaso.Where(p => p.Campo == "unidad_nombre").ToList();

        // Las filas se ESCRIBEN, no solo se cuentan: es lo que se pega en la entrega, y un
        // verde sin las filas delante no dice cuantas habia ni con que nombre.
        Console.WriteLine("== procedencia_campo del caso {0} tras corregir «Unidad» ==", CasoDePrueba);
        Console.WriteLine("   {0,-16} {1,-9} {2,-18} {3}", "CAMPO", "ORIGEN", "VALOR OCR", "BANDA");
        foreach (var fila in delCaso)
        {
            Console.WriteLine(
                "   {0,-16} {1,-9} {2,-18} {3}",
                fila.Campo,
                fila.Origen,
                fila.ValorOcr ?? "(ninguno)",
                fila.BandaX0 is null
                    ? "(ninguna)"
                    : $"({fila.BandaX0}, {fila.BandaY0}) a ({fila.BandaX1}, {fila.BandaY1})");
        }

        Assert.HasCount(
            1,
            deEsteCampo,
            "La procedencia del nombre de unidad no quedo en UNA fila. Hay: "
            + string.Join(" | ", delCaso.Select(p => $"campo={p.Campo} origen={p.Origen}")));

        var quedo = deEsteCampo[0];
        Assert.AreEqual(OrigenDeCampo.Manual, quedo.Origen, "La fila que quedo no dice que la tecleo una mano.");
        Assert.AreEqual(LoQueLeyoElOcr, quedo.ValorOcr, "Se perdio lo que leyo el OCR, que es la prueba del papel.");
        Assert.AreEqual(0.08, quedo.BandaX0, "Se perdio la banda: ya no se puede iluminar donde estaba en la hoja.");
        Assert.AreEqual(0.245, quedo.BandaY1, "Se perdio la banda.");
    }

    /// <summary>
    /// Dado un caso importado, cuando se abre para corregir, entonces la pantalla ve la
    /// banda y el valor que dejo la importacion.
    /// </summary>
    /// <remarks>
    /// Es la mitad de la pantalla que el defecto apagaba: sin esto, Miguel corrige a
    /// ciegas —no se le ilumina donde estaba el dato en el escaneo ni se le dice que
    /// leyo la maquina—.
    /// </remarks>
    [TestMethod]
    public void LaPantallaVeLaBandaYLaLecturaQueDejoLaImportacion()
    {
        var modelo = ModeloSobre(MontarComoLoDejaLaImportacion());
        modelo.Cargar(CasoDePrueba);

        var unidad = modelo.Campos.Single(c => c.Campo == "unidad_nombre");

        Assert.IsNotNull(unidad.Procedencia, "La pantalla no encontro la procedencia que escribio la importacion.");
        Assert.IsNotNull(unidad.Banda, "La pantalla no encontro la banda: no puede iluminarla en el papel.");
        Assert.AreEqual(LoQueLeyoElOcr, unidad.Procedencia.ValorOcr);
        Assert.AreEqual(0.55, unidad.Procedencia.Confianza, "La pantalla no ve la confianza con la que se leyo.");
    }

    /// <summary>
    /// Dado un caso abierto, cuando se miran los nombres de campo que la pantalla maneja,
    /// entonces son los de las columnas del esquema y no los de las propiedades de C#.
    /// </summary>
    /// <remarks>
    /// Regla permanente 4: los valores que van a la base van en espanol. <c>UnidadNombre</c>
    /// no es una columna de nada.
    /// </remarks>
    [TestMethod]
    public void LosNombresDeCampoSonLosDeLasColumnasDelEsquema()
    {
        var modelo = ModeloSobre(MontarComoLoDejaLaImportacion());
        modelo.Cargar(CasoDePrueba);

        foreach (var campo in modelo.Campos)
        {
            Assert.Contains(
                campo.Campo,
                NombresDeColumnaDelEsquema,
                $"«{campo.Campo}» no es ninguna de las columnas que la pantalla corrige.");
        }
    }

    /// <summary>
    /// Dado un campo corregido y guardado, cuando se vuelve a guardar otra correccion,
    /// entonces la fila sigue siendo UNA.
    /// </summary>
    /// <remarks>
    /// Corregir dos veces es lo normal —se teclea, se mira el escaneo y se retoca— y cada
    /// pasada volvia a anotar. Si la clave fuera otra, aqui se verian tres filas.
    /// </remarks>
    [TestMethod]
    public void CorregirDosVecesSigueDejandoUnaSolaFila()
    {
        var servicios = MontarComoLoDejaLaImportacion();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var unidad = modelo.Campos.Single(c => c.Campo == "unidad_nombre");
        modelo.Guardar(new Dictionary<string, string?> { [unidad.Clave] = "Castries Branch" });
        modelo.Guardar(new Dictionary<string, string?> { [unidad.Clave] = "Castries Ward" });

        Assert.HasCount(
            1,
            servicios.Procedencia.DeRegistro(TablaDeProcedencia.Casos, CasoDePrueba)
                .Where(p => p.Campo == "unidad_nombre")
                .ToList(),
            "Corregir dos veces dejo mas de una fila de procedencia para el mismo campo.");
        Assert.AreEqual("Castries Ward", servicios.Almacen.Casos[CasoDePrueba].UnidadNombre);
    }

    /// <summary>
    /// Dada una cedula que la importacion anoto, cuando se corrige, entonces tampoco se
    /// duplica.
    /// </summary>
    /// <remarks>
    /// Los campos de <c>personas</c> van por otro camino que los del caso
    /// (<c>EscribirLasPersonas</c>), asi que se prueban aparte y no por simetria.
    /// </remarks>
    [TestMethod]
    public void CorregirLaCedulaDeUnaPersonaTampocoDuplicaSuProcedencia()
    {
        var servicios = MontarComoLoDejaLaImportacion();
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var cedula = modelo.Campos.Single(
            c => c.Tabla == TablaDeProcedencia.Personas && c.Campo == "mrn");
        modelo.Guardar(new Dictionary<string, string?> { [cedula.Clave] = "055-1111-3853" });

        var deLaPersona = servicios.Procedencia.DeRegistro(TablaDeProcedencia.Personas, PersonaDePrueba);

        Assert.HasCount(
            1,
            deLaPersona.Where(p => p.Campo == "mrn").ToList(),
            "La procedencia de la cedula no quedo en UNA fila. Hay: "
            + string.Join(" | ", deLaPersona.Select(p => $"campo={p.Campo} origen={p.Origen}")));
        Assert.AreEqual("055-1111-385", deLaPersona.Single(p => p.Campo == "mrn").ValorOcr);
    }

    /// <summary>Monta el almacen como lo deja la importacion: con los nombres de columna.</summary>
    /// <remarks>
    /// Los nombres van escritos a mano y no tomados de <c>Fichas.Lectura</c>: esta prueba
    /// tiene que fallar si la pantalla y la importacion dejan de coincidir, y tomarlos del
    /// mismo sitio que el codigo probado haria que coincidieran siempre.
    /// </remarks>
    private static ServiciosFalsos MontarComoLoDejaLaImportacion()
    {
        var servicios = new ServiciosFalsos(3, 20260904, new RelojFijo("2026-09-04"));

        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNumero = "7000011",
            UnidadNombre = LoQueLeyoElOcr,
            FechaViaje = "2026-09-12",
            TemploNombre = "Santo Domingo Dominican Republic",
            RutaPdf = @"C:\Users\josem\Documents\Fichas\pdf\lote-000.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-01",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = null,
            Nombre = "Maria Anonimo",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };

        AnotarComoLaImportacion(servicios, TablaDeProcedencia.Casos, CasoDePrueba, "unidad_nombre", LoQueLeyoElOcr);
        AnotarComoLaImportacion(servicios, TablaDeProcedencia.Personas, PersonaDePrueba, "mrn", "055-1111-385");

        return servicios;
    }

    /// <summary>Monta el modelo sobre ese almacen.</summary>
    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
        => new(servicios.Casos, servicios.Personas, servicios.Procedencia,
               servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

    /// <summary>Anota una lectura tal como la deja la importacion: con banda y con confianza.</summary>
    private static void AnotarComoLaImportacion(
        ServiciosFalsos servicios,
        TablaDeProcedencia tabla,
        long registroId,
        string columna,
        string loLeido)
        => servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = columna,
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.55,
            ValorOcr = loLeido,
            BandaX0 = 0.08,
            BandaY0 = 0.20,
            BandaX1 = 0.92,
            BandaY1 = 0.245,
        });
}
