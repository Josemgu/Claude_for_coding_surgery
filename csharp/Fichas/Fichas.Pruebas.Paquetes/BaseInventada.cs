using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.Paquetes;

/// <summary>Un reloj que no se mueve, para que dos ejecuciones den la misma marca de tiempo.</summary>
/// <remarks>Sin el, una prueba que compara marcas de tiempo falla el dia que cambia el segundo.</remarks>
public sealed class RelojQuieto : IReloj
{
    private readonly DateTime _instante;

    /// <summary>Crea el reloj parado en ese instante.</summary>
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
