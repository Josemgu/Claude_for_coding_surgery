using Fichas.App.Correccion;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Pruebas.App.Inicio;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Corrección lee la lista de casos UNA vez por pasada, y el tablero y el veredicto salen de
/// esa misma lectura (R-5 del plan del 2026-09-15).
/// </summary>
/// <remarks>
/// <para>Criterio: <b>dado</b> que <c>LlenarLosGrupos</c> arma el tablero de Revisar y lo que le
/// falta a cada documento, <b>cuando</b> los dos leen la base, <b>entonces</b>
/// <c>ICasos.Listar</c> se llama <b>una</b> vez y no una por lector; y el veredicto de cada
/// documento es <b>el mismo</b> que sin la pasada, porque sigue saliendo de
/// <c>LoQueLeFalta</c>.</para>
///
/// <para>Se cuenta con un doble de <see cref="ICasos"/> y no con un cronómetro, por lo mismo
/// que <c>ProcedenciaQueSeCuenta</c>: el número de llamadas es el mismo en cualquier máquina.
/// Los milisegundos se miden aparte, con la ventana abierta y <c>--falso 3000</c>.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosCasosLeidosUnaVez
{
    /// <summary>Dadas dos lecturas iguales, cuando pasan por la pasada, entonces la base recibe una y las dos devuelven lo mismo.</summary>
    [TestMethod]
    public void DosLecturasIgualesSonUnaSolaLlamadaALaBase()
    {
        var servicios = BaseDeInicio.MontarServicios(5);
        var contados = new CasosQueSeCuentan(servicios.Casos);
        var pasada = new CasosLeidosUnaVez(contados);

        var primera = pasada.Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue));
        var segunda = pasada.Listar(new FiltroDeCasos(IncluirArchivados: false), new Pagina(0, int.MaxValue));

        Assert.AreEqual(1, contados.VecesQueSeListo);
        Assert.AreSame(primera, segunda, "la segunda es la misma lectura, no una copia");
        Assert.HasCount(5, primera.Elementos);
    }

    /// <summary>Dados dos filtros distintos, cuando se leen, entonces son dos llamadas: la pasada no mezcla poblaciones.</summary>
    [TestMethod]
    public void DosFiltrosDistintosSonDosLlamadas()
    {
        var servicios = BaseDeInicio.MontarServicios(5);
        var contados = new CasosQueSeCuentan(servicios.Casos);
        var pasada = new CasosLeidosUnaVez(contados);

        pasada.Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue));
        pasada.Listar(new FiltroDeCasos(IncluirArchivados: true), Pagina.Primera(int.MaxValue));

        Assert.AreEqual(2, contados.VecesQueSeListo);
    }

    /// <summary>Dada una escritura entre dos lecturas, cuando se vuelve a leer, entonces se va a la base otra vez: lo leído antes ya no vale.</summary>
    [TestMethod]
    public void UnaEscrituraObligaAVolverALeer()
    {
        var servicios = BaseDeInicio.MontarServicios(5);
        var contados = new CasosQueSeCuentan(servicios.Casos);
        var pasada = new CasosLeidosUnaVez(contados);

        var antes = pasada.Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue));
        pasada.Archivar(antes.Elementos[0].Id, archivado: true, "2026-09-15");
        var despues = pasada.Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue));

        Assert.AreEqual(2, contados.VecesQueSeListo);
        Assert.HasCount(4, despues.Elementos, "el archivado ya no está");
    }

    /// <summary>Lo que no es Listar pasa tal cual: Obtener, Contar y ContarPersonasDe no se guardan ni se cuentan como lectura de la lista.</summary>
    [TestMethod]
    public void LoDemasPasaTalCual()
    {
        var servicios = BaseDeInicio.MontarServicios(3);
        var contados = new CasosQueSeCuentan(servicios.Casos);
        var pasada = new CasosLeidosUnaVez(contados);
        var ids = servicios.Casos.Listar(FiltroDeCasos.Todo, Pagina.Primera(int.MaxValue)).Elementos.Select(c => c.Id).ToList();

        Assert.AreEqual(3, pasada.Contar(FiltroDeCasos.Todo));
        Assert.IsNotNull(pasada.Obtener(ids[0]));
        Assert.HasCount(3, pasada.ContarPersonasDe(ids));
        Assert.AreEqual(0, contados.VecesQueSeListo);
    }

    /// <summary>
    /// La premisa del pase, medida: SIN la pasada, el tablero y el veredicto piden la lista
    /// DOS veces a la base (el plan decía tres; contado con el doble, son dos).
    /// </summary>
    [TestMethod]
    public void SinLaPasadaLaListaSePideDosVeces()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var contados = new CasosQueSeCuentan(servicios.Casos);

        var tablero = new TableroDeRevisar(contados, servicios.Asignaciones, servicios.Companeros, servicios.Reloj);
        tablero.Cargar();
        LoQueLeFaltaACadaDocumento.DeTodaLaBase(contados, servicios.Personas, servicios.Procedencia);

        Assert.AreEqual(2, contados.VecesQueSeListo);
    }

    /// <summary>
    /// Dado el tablero de Revisar y lo que le falta a cada documento sobre la misma pasada,
    /// cuando se arman como en <c>LlenarLosGrupos</c>, entonces la base recibe UNA lectura de la
    /// lista y el veredicto de cada documento es el mismo que sin la pasada.
    /// </summary>
    [TestMethod]
    public void ElTableroYElVeredictoSalenDeUnaSolaLectura()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);
        var contados = new CasosQueSeCuentan(servicios.Casos);
        var pasada = new CasosLeidosUnaVez(contados);

        var tablero = new TableroDeRevisar(pasada, servicios.Asignaciones, servicios.Companeros, servicios.Reloj);
        tablero.Cargar();
        var conPasada = LoQueLeFaltaACadaDocumento.DeTodaLaBase(pasada, servicios.Personas, servicios.Procedencia);

        Assert.AreEqual(1, contados.VecesQueSeListo, "una lectura de la lista para los dos lectores");
        // La base inventada archiva algunos: el tablero trae los no archivados, como siempre.
        Assert.AreEqual(servicios.Casos.Contar(FiltroDeCasos.Todo), tablero.Total);

        var sinPasada = LoQueLeFaltaACadaDocumento.DeTodaLaBase(servicios.Casos, servicios.Personas, servicios.Procedencia);
        foreach (var tarjeta in tablero.Cargadas)
        {
            Assert.AreEqual(sinPasada.De(tarjeta.CasoId), conPasada.De(tarjeta.CasoId), $"caso {tarjeta.CasoId}");
            Assert.AreEqual(sinPasada.LeFaltaAlgo(tarjeta.CasoId), conPasada.LeFaltaAlgo(tarjeta.CasoId), $"caso {tarjeta.CasoId}");
        }
    }

    /// <summary>Un <see cref="ICasos"/> que solo cuenta cuántas veces se le pide la lista; todo lo demás pasa tal cual.</summary>
    private sealed class CasosQueSeCuentan : ICasos
    {
        /// <summary>A quien se le pasa todo.</summary>
        private readonly ICasos _deVerdad;

        /// <summary>Envuelve al de verdad.</summary>
        /// <param name="deVerdad">El almacén que contesta.</param>
        public CasosQueSeCuentan(ICasos deVerdad) => _deVerdad = deVerdad;

        /// <summary>Cuántas veces se pidió la lista.</summary>
        public int VecesQueSeListo { get; private set; }

        /// <inheritdoc />
        public PaginaDe<Caso> Listar(FiltroDeCasos filtro, Pagina trozo)
        {
            VecesQueSeListo++;
            return _deVerdad.Listar(filtro, trozo);
        }

        /// <inheritdoc />
        public int Contar(FiltroDeCasos filtro) => _deVerdad.Contar(filtro);

        /// <inheritdoc />
        public Caso? Obtener(long id) => _deVerdad.Obtener(id);

        /// <inheritdoc />
        public IReadOnlyDictionary<long, int> ContarPersonasDe(IReadOnlyList<long> casoIds) => _deVerdad.ContarPersonasDe(casoIds);

        /// <inheritdoc />
        public ResultadoDeEscritura Guardar(Caso caso) => _deVerdad.Guardar(caso);

        /// <inheritdoc />
        public ResultadoDeEscritura MarcarEstado(long casoId, EstadoDeRecomendacion estado, long companeroId, string origen)
            => _deVerdad.MarcarEstado(casoId, estado, companeroId, origen);

        /// <inheritdoc />
        public ResultadoDeEscritura MarcarEstadoDelCompanero(long casoId, EstadoDeRecomendacion estado, MotivoDeNoCompletar motivo, long companeroId, string origen)
            => _deVerdad.MarcarEstadoDelCompanero(casoId, estado, motivo, companeroId, origen);

        /// <inheritdoc />
        public ResultadoDeEscritura Archivar(long casoId, bool archivado, string fechaDeArchivado)
            => _deVerdad.Archivar(casoId, archivado, fechaDeArchivado);
    }
}
