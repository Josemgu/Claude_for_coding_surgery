using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Asignar;

/// <summary>
/// La base de prueba del criterio C5-4: UN caso en cada estado, y ni uno mas.
/// </summary>
/// <remarks>
/// El criterio pide exactamente eso —«un caso en cada estado: sin verificar, verificado,
/// completa, no_completa, sin fecha de viaje, con fecha pasada y archivado»— para poder
/// decir el denominador sin discusion: N en la base, N ofrecidos.
///
/// Se construye a mano y no con el generador: el generador reparte al azar y no garantiza
/// que haya uno de cada, que es justo lo que hay que probar.
/// </remarks>
internal sealed class BaseDePrueba
{
    /// <summary>El dia en el que se paran todos los relojes de estas pruebas.</summary>
    public const string ElDiaDeLaPrueba = "2026-09-04";

    /// <summary>Monta una base vacia de casos con los companeros del generador.</summary>
    public BaseDePrueba(int casosInventados = 0)
    {
        Reloj = new RelojFijo(ElDiaDeLaPrueba);
        Servicios = new ServiciosFalsos(casosInventados, 20260904, Reloj);
        Avisos = new BuzonDeAvisos();
        Operacion = new OperacionDeAsignar(Servicios.Asignaciones, Reloj, Avisos);
        Personas = new EspiaDePersonas(Servicios.Personas);
        Lista = new ListaParaAsignar(Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Personas);
        Tablero = new TableroDeRevisar(Servicios.Casos, Servicios.Asignaciones, Servicios.Companeros, Reloj);
        Acciones = new AccionesDeRevisar(Servicios.Casos, Reloj, Avisos);
    }

    /// <summary>El reloj parado.</summary>
    public RelojFijo Reloj { get; }

    /// <summary>Los servicios falsos sobre los que corre todo.</summary>
    public ServiciosFalsos Servicios { get; }

    /// <summary>El buzon donde caen los avisos, para comprobar que se avisa y no se impide.</summary>
    public BuzonDeAvisos Avisos { get; }

    /// <summary>La unica puerta de asignar.</summary>
    public OperacionDeAsignar Operacion { get; }

    /// <summary>
    /// Las personas, envueltas en el espia que cuenta cuantas veces se le pregunta.
    /// </summary>
    /// <remarks>
    /// La lista de Asignar lee por aqui SIEMPRE, tambien en las pruebas que no miran el
    /// contador: un espia que solo se enchufa en una prueba mide una tuberia distinta de la que
    /// usan las demas.
    /// </remarks>
    public EspiaDePersonas Personas { get; }

    /// <summary>La lista de la pantalla de Asignar.</summary>
    public ListaParaAsignar Lista { get; }

    /// <summary>El tablero de la pantalla de Revisar.</summary>
    public TableroDeRevisar Tablero { get; }

    /// <summary>Lo que escribe la pantalla de Revisar.</summary>
    public AccionesDeRevisar Acciones { get; }

    /// <summary>Los companeros activos, ordenados por nombre.</summary>
    public IReadOnlyList<Companero> Activos => Servicios.Companeros.Activos();

    /// <summary>
    /// Mete los SIETE casos del criterio C5-4, uno por estado, y devuelve sus ids en orden.
    /// </summary>
    public IReadOnlyList<long> MeterUnCasoDeCadaEstado()
    {
        var quien = Activos[0].Id;
        var ids = new List<long>
        {
            Meter("AAAA0001", FechaEn(30), null, false),                 // sin verificar, sin marcar
            Meter("AAAA0002", FechaEn(31), null, false),                 // verificado en sus campos
            Meter("AAAA0003", FechaEn(32), null, false),                 // se marcara completa
            Meter("AAAA0004", FechaEn(33), null, false),                 // se marcara no completa
            Meter("AAAA0005", null, null, false),                        // sin fecha de viaje
            Meter("AAAA0006", FechaEn(-10), null, false),                // con la fecha ya pasada
            Meter("AAAA0007", FechaEn(40), Reloj.HoyMasDias(-2), true),  // archivado
        };

        // El estado lo escribe el motor con su firma, no se cuela por el constructor: asi
        // la prueba mide lo mismo que hace la pantalla.
        Servicios.Casos.MarcarEstado(ids[2], EstadoDeRecomendacion.Completa, quien, "hoja devuelta por el companero");
        Servicios.Casos.MarcarEstado(ids[3], EstadoDeRecomendacion.NoCompleta, quien, "hoja devuelta por el companero");

        // El segundo esta VERIFICADO de verdad, firmado por el unico camino que hay
        // (regla permanente 5). Si aqui solo pusiera la palabra, la prueba no probaria
        // que un caso ya verificado se sigue ofreciendo: probaria una etiqueta mia.
        FirmarUnCampo(ids[1], quien);
        return ids;
    }

    /// <summary>Firma un campo de un caso por el unico camino que existe: <c>IProcedencia.Firmar</c>.</summary>
    public void FirmarUnCampo(long casoId, long quienFirma)
    {
        Servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = nameof(Caso.NumeroCaso),
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.9,
        });
        Servicios.Procedencia.Firmar(
            TablaDeProcedencia.Casos, casoId, nameof(Caso.NumeroCaso), quienFirma, Reloj.Ahora());
    }

    /// <summary>Mete un caso con lo justo y devuelve su id.</summary>
    public long Meter(string? numero, string? fechaViaje, string? fechaArchivado, bool archivado)
    {
        var resultado = Servicios.Casos.Guardar(new Caso
        {
            NumeroCaso = numero,
            FechaViaje = fechaViaje,
            Archivado = archivado,
            FechaArchivado = fechaArchivado,
            RutaPdf = $@"C:\pdf\{numero ?? "sin-numero"}.pdf",
            PaginaPdf = 1,
            UnidadNombre = "Rama de prueba",
            CreadoEn = Reloj.Ahora(),
        });
        return resultado.Id;
    }

    /// <summary>
    /// Mete en un caso las personas que viajan, en el orden del formulario, y devuelve sus ids.
    /// </summary>
    /// <remarks>
    /// ⚠️ Los nombres que se le pasan estan INVENTADOS y asi tienen que seguir: los del dueno
    /// son personas de verdad y una prueba se lee en cualquier pantalla.
    /// </remarks>
    public IReadOnlyList<long> MeterPersonas(long casoId, params string[] nombres)
    {
        ArgumentNullException.ThrowIfNull(nombres);
        var ids = new List<long>(nombres.Length);
        for (var fila = 1; fila <= nombres.Length; fila++)
        {
            var resultado = Servicios.Personas.Guardar(new Persona
            {
                CasoId = casoId,
                Nombre = nombres[fila - 1],
                FilaFormulario = fila,
                PaginaPdf = Math.Min(fila, 6),
            });
            ids.Add(resultado.Id);
        }

        return ids;
    }

    /// <summary>
    /// Mete una persona de la que NO se leyo el nombre y si la cedula, y devuelve su id.
    /// </summary>
    /// <remarks>
    /// El esquema no admite una fila sin nombre y sin MRN (<c>CHECK</c> de la version 1), asi
    /// que este es el unico anonimo que puede existir de verdad en la base del dueno.
    /// </remarks>
    public long MeterPersonaSoloConCedula(long casoId, string mrn)
        => Servicios.Personas.Guardar(new Persona
        {
            CasoId = casoId,
            Nombre = null,
            Mrn = mrn,
            FilaFormulario = 1,
            PaginaPdf = 1,
        }).Id;

    /// <summary>Una fecha a N dias del dia de la prueba.</summary>
    public string FechaEn(int dias) => Reloj.HoyMasDias(dias);
}
