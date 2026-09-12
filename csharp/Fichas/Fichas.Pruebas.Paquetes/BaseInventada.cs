using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.Paquetes;

/// <summary>Un reloj que no se mueve, para que dos ejecuciones den la misma marca de tiempo.</summary>
/// <remarks>Sin el, una prueba que compara marcas de tiempo falla el dia que cambia el segundo.</remarks>
public sealed class RelojQuieto : IReloj
{
    /// <summary>El instante en que está parado el reloj.</summary>
    private readonly DateTime _instante;

    /// <summary>Crea el reloj parado en ese instante.</summary>
    /// <param name="instante">La fecha y hora que devolverá siempre.</param>
    public RelojQuieto(DateTime instante) => _instante = instante;

    /// <inheritdoc/>
    public string Hoy() => _instante.ToString("yyyy-MM-dd");

    /// <inheritdoc/>
    public string Ahora() => _instante.ToString("yyyy-MM-dd HH:mm:ss");

    /// <inheritdoc/>
    public string HoyMasDias(int dias) => _instante.AddDays(dias).ToString("yyyy-MM-dd");
}

/// <summary>
/// Una base inventada pequena y escrita a mano, con lo que cada prueba necesita ver.
/// </summary>
/// <remarks>
/// ⚠️ NINGUNA prueba de este proyecto abre la base real de Documentos\Fichas. Se usa el
/// almacen en memoria de Fichas.Datos.Falso, que es de otro terreno y aqui solo se lee.
/// Se llena a mano en vez de con el generador porque lo que se prueba son casos concretos
/// —dos casos con el mismo numero, una cedula que termina en letra— y un sorteo no los
/// garantiza.
/// </remarks>
public sealed class BaseInventada
{
    /// <summary>El almacen en memoria; las pruebas miran dentro para comprobar lo que se escribio.</summary>
    public AlmacenFalso Almacen { get; }

    /// <summary>El servicio de paquetes ya montado sobre esta base.</summary>
    public Fichas.Paquetes.Paquetes Paquetes { get; }

    /// <summary>El reloj parado con el que se sellan los descartes.</summary>
    public IReloj Reloj { get; }

    /// <summary>Monta una base vacia con sus repositorios y su servicio de paquetes.</summary>
    public BaseInventada()
    {
        Reloj = new RelojQuieto(new DateTime(2026, 9, 4, 10, 30, 0));
        Almacen = new AlmacenFalso(Reloj, semilla: 20260904);
        Paquetes = new Fichas.Paquetes.Paquetes(
            new RepositorioDeCasosFalso(Almacen),
            new RepositorioDePersonasFalso(Almacen),
            new RepositorioDeCompanerosFalso(Almacen),
            new RepositorioDeIlegiblesFalso(Almacen),
            Reloj);
    }

    /// <summary>Da de alta un companero y devuelve su id.</summary>
    /// <param name="nombre">El nombre del compañero, que es lo que sale en la cabecera de su hoja.</param>
    /// <returns>El número interno que le dio el almacén.</returns>
    public long Companero(string nombre)
    {
        var id = Almacen.SiguienteId();
        Almacen.Companeros[id] = new Companero { Id = id, Nombre = nombre, Activo = true, CreadoEn = Reloj.Ahora() };
        return id;
    }

    /// <summary>Da de alta un caso y devuelve su id.</summary>
    /// <remarks>
    /// <paramref name="rutaPdf"/> y <paramref name="paginaPdf"/> son de donde salio el
    /// documento, y van juntas: sin las dos no se puede recortar su hoja para el PDF unido
    /// del paquete. Por defecto van a nulo, que es como estaban todos los casos de estas
    /// pruebas antes de que existiera ese PDF.
    /// </remarks>
    /// <param name="numeroCaso">El número de caso; nulo para un caso sin número.</param>
    /// <param name="fechaViaje">La fecha de viaje en ISO-8601; nula para un caso sin fecha.</param>
    /// <param name="templo">El nombre del templo.</param>
    /// <param name="unidad">El nombre del barrio o rama, sin el número.</param>
    /// <param name="unidadNumero">El número de la unidad, aparte del nombre.</param>
    /// <param name="rutaPdf">El escaneo del que salió; nula por defecto.</param>
    /// <param name="paginaPdf">La hoja de ese escaneo; nula por defecto.</param>
    /// <returns>El número interno que le dio el almacén.</returns>
    public long Caso(
        string? numeroCaso,
        string? fechaViaje = "2026-10-15",
        string? templo = "Santo Domingo",
        string? unidad = "Cuatricentenaria",
        string? unidadNumero = "7000014",
        string? rutaPdf = null,
        int? paginaPdf = null)
    {
        var id = Almacen.SiguienteId();
        Almacen.Casos[id] = new Contratos.Modelos.Caso
        {
            Id = id,
            NumeroCaso = numeroCaso,
            FechaViaje = fechaViaje,
            TemploNombre = templo,
            UnidadNombre = unidad,
            UnidadNumero = unidadNumero,
            RutaPdf = rutaPdf,
            PaginaPdf = paginaPdf,
            CreadoEn = Reloj.Ahora(),
        };
        return id;
    }

    /// <summary>Da de alta una persona en un caso y devuelve su id.</summary>
    /// <param name="casoId">El caso al que pertenece.</param>
    /// <param name="nombre">Su nombre; nulo para una persona sin nombre leído.</param>
    /// <param name="mrn">Su cédula; nula para una persona sin cédula.</param>
    /// <param name="filaFormulario">En qué fila del papel venía.</param>
    /// <returns>El número interno que le dio el almacén.</returns>
    public long Persona(long casoId, string? nombre, string? mrn, int filaFormulario = 1)
    {
        var id = Almacen.SiguienteId();
        Almacen.Personas[id] = new Contratos.Modelos.Persona
        {
            Id = id,
            CasoId = casoId,
            Nombre = nombre,
            Mrn = mrn,
            FilaFormulario = filaFormulario,
            OrdInvestidura = true,
        };
        return id;
    }

    /// <summary>Una carpeta temporal propia de esta prueba; ningun .xlsx se queda en el repositorio.</summary>
    public static string CarpetaDePruebas()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "fichas-pruebas-paquetes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        return carpeta;
    }
}
