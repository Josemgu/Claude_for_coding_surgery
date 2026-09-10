using ClosedXML.Excel;
using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Paquetes;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;
using Fichas.Paquetes;

namespace Fichas.Pruebas.App.Paquetes;

/// <summary>
/// La ida y la vuelta de verdad: se genera el Excel, se rellena como lo rellenaria un agente
/// y se devuelve.
/// </summary>
/// <remarks>
/// <para>Se usa el motor de Excel REAL —<c>Fichas.Paquetes</c>— sobre el almacen en memoria.
/// Con un doble que devolviera marcas inventadas, la prueba diria que el boton firma lo que
/// el doble quiso: lo que hay que comprobar es que firma lo que casa de verdad, y eso lo
/// decide la reconciliacion, no la pantalla.</para>
///
/// <para>⚠️ Ninguna prueba de este archivo abre la base de <c>Documentos\Fichas</c>. El
/// almacen es el de <c>Fichas.Datos.Falso</c> y los .xlsx van a una carpeta temporal que se
/// borra al terminar.</para>
/// </remarks>
internal sealed class BaseDelPaquete : IDisposable
{
    /// <summary>El instante en el que se para el reloj de todas estas pruebas.</summary>
    public const string ElInstante = "2026-09-06 12:00:00";

    private readonly string _carpeta;

    /// <summary>Monta el almacen VACIO, el motor de Excel de verdad y una carpeta temporal.</summary>
    /// <remarks>
    /// ⚠️ Se monta sobre un <see cref="AlmacenFalso"/> desnudo y NO sobre
    /// <c>ServiciosFalsos</c>: aquel da de alta un equipo inventado aunque se le pidan cero
    /// casos, y entonces «cuantos administradores activos hay» o «quien se llama Sandy» los
    /// contestaria el generador y no la prueba.
    /// </remarks>
    public BaseDelPaquete()
    {
        Reloj = new RelojFijo("2026-09-06");
        Almacen = new AlmacenFalso(Reloj, semilla: 20260906);
        Casos = new RepositorioDeCasosFalso(Almacen);
        Personas = new RepositorioDePersonasFalso(Almacen);
        Companeros = new RepositorioDeCompanerosFalso(Almacen);
        Procedencia = new RepositorioDeProcedenciaFalso(Almacen);
        Ilegibles = new RepositorioDeIlegiblesFalso(Almacen);
        Asignaciones = new RepositorioDeAsignacionesFalso(Almacen);
        Paquetes = new Fichas.Paquetes.Paquetes(Casos, Personas, Companeros, Ilegibles, Reloj);
        Firma = new FirmaEnBloque(Procedencia, Reloj);
        Avisos = new BuzonDeAvisos();
        Reparto = new OperacionDeAsignar(Asignaciones, Reloj, Avisos);
        // Con la limpieza puesta, que es como la monta la pantalla desde el 2026-09-07: montarla
        // sin ella dejaria las pruebas midiendo una tuberia mas corta que la del programa.
        Vuelta = new OperacionDeLaVuelta(
            Paquetes, Ilegibles, Casos, Personas,
            new LimpiezaAlVolver(Asignaciones, Casos, Reparto));
        Ida = new OperacionDelPaquete(Paquetes, Asignaciones, Casos);
        _carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-app-paquetes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
    }

    /// <summary>El reloj parado; sin el, comparar <c>verificado_en</c> fallaria al cambiar el segundo.</summary>
    public RelojFijo Reloj { get; }

    /// <summary>El almacen en memoria; las pruebas miran dentro para contar lo que se escribio.</summary>
    public AlmacenFalso Almacen { get; }

    /// <summary>Los casos.</summary>
    public ICasos Casos { get; }

    /// <summary>Las personas.</summary>
    public IPersonas Personas { get; }

    /// <summary>Los companeros.</summary>
    public ICompaneros Companeros { get; }

    /// <summary>La procedencia de cada campo.</summary>
    public IProcedencia Procedencia { get; }

    /// <summary>Las filas descartadas.</summary>
    public IIlegibles Ilegibles { get; }

    /// <summary>Quien lleva que caso; la unica puerta por la que se sabe.</summary>
    public IAsignaciones Asignaciones { get; }

    /// <summary>Donde caen los avisos del reparto, para poder leerlos en la prueba.</summary>
    public BuzonDeAvisos Avisos { get; }

    /// <summary>La unica puerta de asignar y de retirar del programa.</summary>
    public OperacionDeAsignar Reparto { get; }

    /// <summary>La ida: lo que lleva un companero y el Excel que se le genera.</summary>
    public OperacionDelPaquete Ida { get; }

    /// <summary>El motor de Excel de verdad, montado sobre el almacen en memoria.</summary>
    public Fichas.Paquetes.Paquetes Paquetes { get; }

    /// <summary>La vuelta con los cuatro puertos, que es la que usa la pantalla.</summary>
    public OperacionDeLaVuelta Vuelta { get; }

    /// <summary>El boton de darlo por bueno en bloque.</summary>
    public FirmaEnBloque Firma { get; }

    /// <summary>Da de alta un companero con su rol y devuelve la fila guardada.</summary>
    public Companero Alta(string nombre, RolDeCompanero rol = RolDeCompanero.Companero)
    {
        var escritura = Companeros.Guardar(new Companero
        {
            Nombre = nombre,
            Activo = true,
            Rol = rol,
            CreadoEn = Reloj.Ahora(),
        });
        return Companeros.Obtener(escritura.Id)!;
    }

    /// <summary>Da de alta un documento con los cinco campos que se firman y devuelve su id.</summary>
    public long Caso(
        string? numeroCaso,
        string? fechaViaje = "2026-10-15",
        string? templo = "Santo Domingo",
        string? unidadNombre = "Cuatricentenaria",
        string? unidadNumero = "7000014")
        => Casos.Guardar(new Caso
        {
            NumeroCaso = numeroCaso,
            FechaViaje = fechaViaje,
            TemploNombre = templo,
            UnidadNombre = unidadNombre,
            UnidadNumero = unidadNumero,
            RutaPdf = @"C:\pdf\de-prueba.pdf",
            PaginaPdf = 1,
            CreadoEn = Reloj.Ahora(),
        }).Id;

    /// <summary>Da de alta una persona en un documento y devuelve su id.</summary>
    public long Persona(long casoId, string? nombre, string? mrn, int fila = 1)
        => Personas.Guardar(new Persona
        {
            CasoId = casoId,
            Nombre = nombre,
            Mrn = mrn,
            FilaFormulario = fila,
        }).Id;

    /// <summary>Le da esos documentos a ese companero por la unica puerta de asignar que hay.</summary>
    /// <remarks>
    /// Por <see cref="OperacionDeAsignar"/> y no escribiendo la fila a mano: una prueba que se
    /// salta la puerta mide una tuberia distinta de la que usa la pantalla.
    /// </remarks>
    public void Dar(Companero aQuien, params long[] casoIds)
    {
        ArgumentNullException.ThrowIfNull(aQuien);
        ArgumentNullException.ThrowIfNull(casoIds);
        foreach (var casoId in casoIds)
        {
            Assert.IsTrue(
                Reparto.Asignar(casoId, aQuien.Id).SeEscribio,
                $"No se pudo asignar el caso {casoId} a «{aQuien.Nombre}» al montar la prueba.");
        }
    }

    /// <summary>Cuantas asignaciones vivas tiene ese companero ahora mismo, leidas de la base.</summary>
    public int VivasDe(long companeroId)
        => Asignaciones.Contar(new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: true));

    /// <summary>Que casos lleva vivos ahora mismo, leidos de la base y sin repetir.</summary>
    /// <remarks>
    /// Por el mismo puerto y con el mismo filtro que miran Asignar, Inicio y el paquete
    /// siguiente. Contarlo de otra manera mediria otra cosa.
    /// </remarks>
    public IReadOnlyList<long> CasosVivosDe(long companeroId)
        => [.. Asignaciones
            .Listar(new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: true), Pagina.Primera(int.MaxValue))
            .Elementos
            .Select(asignacion => asignacion.CasoId)
            .Distinct()];

    /// <summary>Todas sus asignaciones, vivas y retiradas: es donde se ve que nada se borro.</summary>
    public IReadOnlyList<Asignacion> TodasLasDe(long companeroId)
        => Asignaciones
            .Listar(new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: false), Pagina.Primera(int.MaxValue))
            .Elementos;

    /// <summary>
    /// El paquete entero de ida y vuelta: se genera, se contesta lo mismo en todas las filas y se aplica.
    /// </summary>
    /// <remarks>
    /// Es el camino de verdad y no un atajo que escriba el estado a mano: lo que hay que probar
    /// es que el paquete SIGUIENTE mira lo que dejo la hoja, y si la prueba escribiera esa
    /// columna por su cuenta estaria comprobando su propia suposicion.
    /// </remarks>
    /// <param name="deQuien">El companero que recibe el paquete y lo devuelve.</param>
    /// <param name="respuesta">«Sí» en las siete casillas, o «No»; lo mismo en todas las filas.</param>
    /// <param name="casoIds">Los documentos que van dentro.</param>
    public void IdaYVuelta(Companero deQuien, string respuesta, params long[] casoIds)
    {
        ArgumentNullException.ThrowIfNull(casoIds);

        var ruta = Generar(deQuien, casoIds);
        var cuantasFilas = ContarLasFilas(ruta);
        for (var fila = Columnas.PrimeraFilaDeDatos; fila < Columnas.PrimeraFilaDeDatos + cuantasFilas; fila++)
            Contestar(ruta, fila, respuesta);

        var vuelta = Vuelta.AplicarYRevisar(deQuien, ruta);
        Assert.IsTrue(
            vuelta.Resumen.SalioBien,
            $"La vuelta de «{deQuien.Nombre}» no entro al montar la prueba: {vuelta.Resumen.Linea}");
    }

    /// <summary>Cuantos renglones de datos trae un Excel generado.</summary>
    public static int ContarLasFilas(string ruta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        var ultima = hoja.LastRowUsed()?.RowNumber() ?? Columnas.FilaDeLaCabecera;
        return Math.Max(0, ultima - Columnas.FilaDeLaCabecera);
    }

    /// <summary>Genera el Excel de ida de ese companero con esos documentos y devuelve su ruta.</summary>
    public string Generar(Companero aQuien, params long[] casoIds)
    {
        var ruta = Path.Combine(_carpeta, $"paquete-{Guid.NewGuid():N}.xlsx");
        var escritura = Paquetes.GenerarExcelDeCompanero(aQuien.Id, casoIds, ruta);
        Assert.IsTrue(escritura.SeEscribio, "No se pudo generar el Excel de ida de la prueba.");
        return ruta;
    }

    /// <summary>Contesta las siete casillas de una fila, como haria el agente en su hoja.</summary>
    /// <param name="ruta">El Excel generado.</param>
    /// <param name="filaExcel">Que renglon; el primero es <c>Columnas.PrimeraFilaDeDatos</c>.</param>
    /// <param name="respuesta">«Sí» o «No»; lo mismo en las siete.</param>
    public static void Contestar(string ruta, int filaExcel, string respuesta)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        foreach (var paso in Pasos.Todos)
            hoja.Cell(filaExcel, Columnas.IndiceDe(paso.Nombre)).SetValue(respuesta);
        hoja.Cell(filaExcel, Columnas.IndiceDe(Pasos.ColumnaDeLaLlamada)).SetValue(respuesta);
        libro.Save();
    }

    /// <summary>Borra la clave de una fila, que es como un agente rompe una fila sin querer.</summary>
    public static void BorrarLaClave(string ruta, int filaExcel)
    {
        using var libro = new XLWorkbook(ruta);
        var hoja = libro.Worksheet(Columnas.NombreDeLaHoja);
        hoja.Cell(filaExcel, Columnas.IndiceDe(Columnas.ColumnaDeLaClave)).Clear(XLClearOptions.Contents);
        libro.Save();
    }

    /// <summary>Cuantos campos de una fila estan firmados hoy.</summary>
    public int Firmados(TablaDeProcedencia tabla, long registroId)
        => Procedencia.DeRegistro(tabla, registroId).Count(campo => campo.Verificado);

    /// <summary>Las filas de procedencia firmadas de todo el almacen, para contarlas en bloque.</summary>
    public IReadOnlyList<ProcedenciaDeCampo> TodoLoFirmado()
        => [.. Almacen.Procedencias.Values.Where(campo => campo.Verificado)];

    /// <summary>Borra la carpeta temporal; ningun .xlsx se queda en el disco.</summary>
    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); }
        catch (IOException) { /* si Windows aún tiene la manija, la carpeta temporal la limpia el sistema */ }
        catch (UnauthorizedAccessException) { /* lo mismo: es una carpeta temporal, no un dato */ }
    }
}
