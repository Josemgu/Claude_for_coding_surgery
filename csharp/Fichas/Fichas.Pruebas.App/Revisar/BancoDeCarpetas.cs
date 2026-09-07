using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// La base de prueba de la agrupación por carpetas: casos con fecha de viaje y unidad.
/// </summary>
/// <remarks>
/// <para>Va aparte de <c>Fichas.Pruebas.App/Asignar/BaseDePrueba.cs</c> a propósito y no por
/// gusto: aquella fija los siete casos del criterio C5-4 —uno por estado— y no deja poner
/// el <b>número</b> de unidad, que es justo la mitad de lo que el dueño pidió ver en la
/// carpeta («el nombre y número de la unidad»). Además esa carpeta tiene una guarda que
/// otro programador acaba de apretar y no se toca.</para>
///
/// <para>Los relojes se paran en <see cref="ElDiaDeLaPrueba"/>: una prueba que dependa del
/// día en que se corre acaba roja sola dentro de un mes.</para>
/// </remarks>
internal sealed class BancoDeCarpetas
{
    /// <summary>El día en el que se paran todos los relojes de estas pruebas.</summary>
    public const string ElDiaDeLaPrueba = "2026-09-05";

    /// <summary>Monta una base vacía de casos, con los compañeros del generador.</summary>
    public BancoDeCarpetas()
    {
        Reloj = new RelojFijo(ElDiaDeLaPrueba);
        Servicios = new ServiciosFalsos(0, 20260905, Reloj);
        Avisos = new BuzonDeAvisos();
        Tablero = new TableroDeRevisar(Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Reloj);
        Acciones = new AccionesDeRevisar(Servicios.Casos, Reloj, Avisos);
    }

    /// <summary>El reloj parado.</summary>
    public RelojFijo Reloj { get; }

    /// <summary>Los servicios falsos sobre los que corre todo.</summary>
    public ServiciosFalsos Servicios { get; }

    /// <summary>El buzón donde caen los avisos.</summary>
    public BuzonDeAvisos Avisos { get; }

    /// <summary>El tablero de la pantalla de Revisar.</summary>
    public TableroDeRevisar Tablero { get; }

    /// <summary>Lo que escribe la pantalla de Revisar.</summary>
    public AccionesDeRevisar Acciones { get; }

    /// <summary>Mete un caso con su fecha de viaje y su unidad, y devuelve su id.</summary>
    /// <param name="numero">Número de caso del papel; nulo si no se leyó.</param>
    /// <param name="fechaViaje">Fecha ISO-8601 del viaje, o nula si no se sabe.</param>
    /// <param name="unidadNumero">Número de la unidad, 6 o 7 dígitos.</param>
    /// <param name="unidadNombre">Nombre de la unidad tal como se leyó.</param>
    /// <param name="rutaPdf">Ruta del PDF del que salió; si es nula se compone una.</param>
    /// <param name="pagina">Hoja del PDF, base 1.</param>
    public long Meter(
        string? numero,
        string? fechaViaje,
        string? unidadNumero,
        string? unidadNombre,
        string? rutaPdf = null,
        int pagina = 1)
        => Servicios.Casos.Guardar(new Caso
        {
            NumeroCaso = numero,
            FechaViaje = fechaViaje,
            UnidadNumero = unidadNumero,
            UnidadNombre = unidadNombre,
            RutaPdf = rutaPdf ?? $@"C:\pdf\{numero ?? "sin-numero"}.pdf",
            PaginaPdf = pagina,
            CreadoEn = Reloj.Ahora(),
        }).Id;

    /// <summary>
    /// Mete un caso con su fecha de viaje y el estado que trajo el Excel del compañero.
    /// </summary>
    /// <remarks>
    /// El estado se escribe directamente en la base y no con <see cref="AccionesDeRevisar"/> a
    /// propósito: en la vida real ese estado lo marca el Excel que devuelve el compañero
    /// (CLAUDE.md §1.5), no la mano de Miguel, y estas pruebas miran cómo se LEE un estado ya
    /// puesto. Archivar sí pasa por la acción de verdad: ver <see cref="Archivar"/>.
    /// </remarks>
    /// <param name="numero">Número de caso del papel; nulo si no se leyó.</param>
    /// <param name="fechaViaje">Fecha ISO-8601 del viaje, o nula si no se sabe.</param>
    /// <param name="estado">El estado que trae el caso desde la base.</param>
    public long MeterConEstado(string? numero, string? fechaViaje, EstadoDeRecomendacion estado)
        => Servicios.Casos.Guardar(new Caso
        {
            NumeroCaso = numero,
            FechaViaje = fechaViaje,
            RutaPdf = $@"C:\pdf\{numero ?? "sin-numero"}.pdf",
            PaginaPdf = 1,
            EstadoRecomendacion = Caso.EscribirEstado(estado),
            CreadoEn = Reloj.Ahora(),
        }).Id;

    /// <summary>Archiva un caso por el mismo camino que la pantalla: la acción de verdad.</summary>
    public void Archivar(long casoId) => Acciones.ArchivarEnLote([casoId]);

    /// <summary>Mete una persona en un caso; es lo que va dentro de la carpeta de la unidad.</summary>
    public long MeterPersona(long casoId, string nombre, string mrn)
        => Servicios.Personas.Guardar(new Persona
        {
            CasoId = casoId,
            Nombre = nombre,
            Mrn = mrn,
            FilaFormulario = 1,
        }).Id;

    /// <summary>Carga el tablero como lo carga la pantalla: sin los archivados.</summary>
    public IReadOnlyList<TarjetaDeDocumento> ComoLoVeLaPantalla(string texto = "")
    {
        Tablero.Cargar(texto, conArchivados: false);
        return Tablero.Todas(FiltroDeTarjeta.Todo);
    }
}
