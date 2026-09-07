using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Pruebas.App.Correccion;

namespace Fichas.Pruebas.App.Grupo;

/// <summary>
/// Las dos preguntas de «listo para asignar», hechas sobre EL MISMO documento, contestan lo
/// mismo.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> <c>DECISIONES.md</c>, 2026-09-06: hasta ese dia habia dos
/// sitios que decidian si a un documento le falta algo —<see cref="ModeloDeCorreccion"/>,
/// que miraba vacio, forma, <b>confianza</b> y <b>tachon</b>; y <see cref="LoQueLeFalta"/>,
/// que miraba vacio y forma y <b>nada mas</b>— y las dos citaban la misma frase del dueno del
/// 2026-09-05, «ninguno vacio, ninguno dudoso». Un documento podia quedarse fuera de las dos
/// listas: no entraba en la cola de Completar —que se construye con <c>LoQueLeFalta</c>— y
/// tampoco salia como listo —que lo decia Correccion—.</para>
///
/// <para><b>La regla, una sola:</b> «ninguno vacio, ninguno dudoso», con «dudoso» entendido
/// como lo entiende <see cref="EstadosDeCampo.EsDudoso"/>, incluida su exencion: si Miguel
/// marco que un dato <b>no esta en el papel</b>, el hueco esta cerrado y no vuelve.</para>
///
/// <para>⛔ <b>Estas pruebas se derivan del criterio de aceptacion, no del codigo.</b> Cada
/// caso de abajo es una fila de la tabla que el supervisor midio antes de este pase, y se
/// escribieron ANTES de unificar nada: las cinco primeras salieron rojas al escribirlas, que
/// es la medicion de la premisa.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeUnSoloVeredicto
{
    private const long ElCaso = 400;
    private const long LaPersona = 401;
    private const long ElCompanero = 1;

    /// <summary>La cedula que el papel trae y que cumple su forma.</summary>
    private const string CedulaBuena = "055-1111-3853";

    // ---- el montaje ------------------------------------------------------

    /// <summary>
    /// Un documento leido entero y bien, sobre el que cada prueba estropea UNA sola cosa.
    /// </summary>
    /// <remarks>
    /// Nace entero a proposito: asi, cuando una prueba se pone roja, lo unico que puede
    /// haberla puesto roja es el retoque que ella misma hizo.
    /// </remarks>
    /// <param name="retoque">Lo que esta prueba cambia de la procedencia recien anotada.</param>
    /// <param name="temploLeido">El templo; nulo es la forma de dejar ese campo vacio.</param>
    /// <param name="conPersona">Si el documento trae persona; falso deja el caso sin ninguna.</param>
    /// <param name="conFilaDeLaCedula">
    /// Si la cedula nace con su fila de procedencia. Ponerlo en falso es la forma de montar
    /// el campo que TIENE valor y del que no consta de donde salio.
    /// </param>
    private static Montaje Montar(
        Action<ProcedenciaComoLaDeVerdad>? retoque = null,
        string? temploLeido = "Panama City, Panama",
        bool conPersona = true,
        bool conFilaDeLaCedula = true)
    {
        var servicios = new ServiciosFalsos(0, 20260906, new RelojFijo("2026-09-06"));
        servicios.Almacen.Casos[ElCaso] = new Caso
        {
            Id = ElCaso,
            NumeroCaso = "CASP2609",
            UnidadNumero = "700001",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-08",
            TemploNombre = temploLeido,
            RutaPdf = @"Z:\INVENTADO\NO-EXISTE\uno.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-06",
        };

        if (conPersona)
        {
            servicios.Almacen.Personas[LaPersona] = new Persona
            {
                Id = LaPersona,
                CasoId = ElCaso,
                Mrn = CedulaBuena,
                Nombre = "Ana Prueba",
                FilaFormulario = 1,
                PaginaPdf = 1,
            };
        }

        var procedencia = new ProcedenciaComoLaDeVerdad(ElCompanero);
        Anotar(procedencia, TablaDeProcedencia.Casos, ElCaso, "numero_caso", OrigenDeCampo.Anotacion, 1.00);
        Anotar(procedencia, TablaDeProcedencia.Casos, ElCaso, "fecha_viaje", OrigenDeCampo.Anotacion, 1.00);
        Anotar(procedencia, TablaDeProcedencia.Casos, ElCaso, "unidad_numero", OrigenDeCampo.Ocr, 0.99);
        Anotar(procedencia, TablaDeProcedencia.Casos, ElCaso, "unidad_nombre", OrigenDeCampo.Ocr, 0.99);
        Anotar(procedencia, TablaDeProcedencia.Casos, ElCaso, "templo_nombre",
               OrigenDeCampo.Ocr, temploLeido is null ? null : 0.99);
        if (conPersona)
        {
            Anotar(procedencia, TablaDeProcedencia.Personas, LaPersona, "nombre", OrigenDeCampo.Ocr, 0.97);
            if (conFilaDeLaCedula)
                Anotar(procedencia, TablaDeProcedencia.Personas, LaPersona, "mrn", OrigenDeCampo.Ocr, 0.95);
        }

        retoque?.Invoke(procedencia);
        return new Montaje(servicios, procedencia);
    }

    private static void Anotar(
        ProcedenciaComoLaDeVerdad procedencia, TablaDeProcedencia tabla, long registroId,
        string campo, OrigenDeCampo origen, double? confianza,
        bool tachado = false, bool ausente = false)
        => procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = origen,
            Confianza = confianza,
            AnuladoPorTachon = tachado,
            AusenteEnElPapel = ausente,
        });

    /// <summary>El documento montado, con las dos preguntas ya cableadas.</summary>
    private sealed class Montaje
    {
        private readonly ServiciosFalsos _servicios;
        private readonly ProcedenciaComoLaDeVerdad _procedencia;

        internal Montaje(ServiciosFalsos servicios, ProcedenciaComoLaDeVerdad procedencia)
        {
            _servicios = servicios;
            _procedencia = procedencia;
        }

        /// <summary>Lo que contesta la pantalla de Correccion: cuantos campos le faltan.</summary>
        internal int SegunCorreccion => Modelo().CamposQueLeFaltan;

        /// <summary>Si la pantalla de Correccion lo da por listo para asignar.</summary>
        internal bool CorreccionLoDaPorListo => Modelo().ListoParaAsignar;

        /// <summary>Lo que contesta la cola de Completar: cuantas cosas le faltan.</summary>
        internal int SegunLaCola => LoQueLeFalta.DeUnDocumento(Caso, Personas, Procedencias).Count;

        /// <summary>Si la cola lo da por listo, que es no meterlo en ella.</summary>
        internal bool LaColaLoDaPorListo => LoQueLeFalta.EstaListo(Caso, Personas, Procedencias);

        /// <summary>Lo que la cola dice que falta, para que un fallo diga QUE falta.</summary>
        internal string LoQueDiceLaCola
            => string.Join(", ", LoQueLeFalta.DeUnDocumento(Caso, Personas, Procedencias));

        /// <summary>
        /// La procedencia leida por el camino EN BLOQUE, que es el que usan Inicio y la cola.
        /// </summary>
        /// <remarks>
        /// Se lee por ese camino a proposito y no por el de un documento: es el que la
        /// pantalla de verdad usa, y el que trae menos informacion —solo las filas que
        /// pesan—. Una prueba que usara el otro dejaria sin cubrir justo la parte fragil.
        /// </remarks>
        private ProcedenciasDeUnaPasada Procedencias
            => ProcedenciasDeUnaPasada.DeTodaLaBase(_procedencia);

        private Caso Caso => _servicios.Almacen.Casos[ElCaso];

        private IReadOnlyList<Persona> Personas => _servicios.Almacen.PersonasDe(ElCaso);

        private ModeloDeCorreccion Modelo()
        {
            var modelo = new ModeloDeCorreccion(
                _servicios.Casos, _servicios.Personas, _procedencia,
                _servicios.LecturaDePdf, _servicios.Extraccion, _servicios.Reloj, _servicios.Companeros);
            modelo.Cargar(ElCaso);
            return modelo;
        }
    }

    /// <summary>Las dos preguntas, hechas sobre el mismo documento, tienen que dar lo mismo.</summary>
    private static void LasDosDicenLoMismo(Montaje montaje, string caso)
        => Assert.AreEqual(
            montaje.CorreccionLoDaPorListo,
            montaje.LaColaLoDaPorListo,
            $"{caso}: Corrección dice que le faltan {montaje.SegunCorreccion} y la cola dice "
            + $"que le faltan {montaje.SegunLaCola} «{montaje.LoQueDiceLaCola}». "
            + "Un documento no puede estar listo en una pantalla y no listo en la otra.");

    // ---- las seis filas de la tabla --------------------------------------

    /// <summary>
    /// Dado un documento cuya cedula el OCR leyo con confianza 0,42 y con forma valida,
    /// cuando se hacen las dos preguntas, entonces contestan lo mismo.
    /// </summary>
    [TestMethod]
    public void UnaCedulaDePocaConfianzaConFormaValidaCuentaEnLasDos()
    {
        var montaje = Montar(procedencia =>
            Anotar(procedencia, TablaDeProcedencia.Personas, LaPersona, "mrn", OrigenDeCampo.Ocr, 0.42));

        LasDosDicenLoMismo(montaje, "cédula con confianza 0,42 y forma válida");
        Assert.IsFalse(montaje.LaColaLoDaPorListo, "Un dato del que la propia máquina desconfía no está listo.");
    }

    /// <summary>
    /// Dado un documento cuya cedula esta tachada en el papel y conserva el valor debajo,
    /// cuando se hacen las dos preguntas, entonces contestan lo mismo.
    /// </summary>
    [TestMethod]
    public void UnaCedulaTachadaConValorDebajoCuentaEnLasDos()
    {
        var montaje = Montar(procedencia =>
            Anotar(procedencia, TablaDeProcedencia.Personas, LaPersona, "mrn", OrigenDeCampo.Ocr, 0.95, tachado: true));

        LasDosDicenLoMismo(montaje, "cédula tachada en el papel, con valor debajo");
        Assert.IsFalse(montaje.LaColaLoDaPorListo, "El papel dice que ese dato está mal; no está listo.");
    }

    /// <summary>
    /// Dado un documento cuya cedula tiene valor y NINGUNA fila de procedencia, cuando se
    /// hacen las dos preguntas, entonces contestan lo mismo.
    /// </summary>
    /// <remarks>
    /// Sin fila no se sabe de donde salio el valor, y un campo sin fila tampoco se puede
    /// firmar: <c>IProcedencia.Firmar</c> es un <c>UPDATE</c> y cambia cero filas.
    /// </remarks>
    [TestMethod]
    public void UnaCedulaSinFilaDeProcedenciaCuentaEnLasDos()
    {
        var montaje = Montar(conFilaDeLaCedula: false);

        LasDosDicenLoMismo(montaje, "cédula con valor y SIN fila de procedencia");
        Assert.IsFalse(montaje.LaColaLoDaPorListo, "De ese valor no se sabe de dónde salió.");
    }

    /// <summary>
    /// Dado un documento cuya cedula tiene fila de OCR con la confianza NULA, cuando se hacen
    /// las dos preguntas, entonces contestan lo mismo.
    /// </summary>
    [TestMethod]
    public void UnaCedulaConConfianzaNulaCuentaEnLasDos()
    {
        var montaje = Montar(procedencia =>
            Anotar(procedencia, TablaDeProcedencia.Personas, LaPersona, "mrn", OrigenDeCampo.Ocr, null));

        LasDosDicenLoMismo(montaje, "cédula con fila de OCR y confianza NULA");
        Assert.IsFalse(montaje.LaColaLoDaPorListo, "Confianza nula es «no se sabe», no «está bien».");
    }

    /// <summary>
    /// Dado un templo vacio que Miguel marco como que NO esta en el papel, cuando se hacen
    /// las dos preguntas, entonces contestan lo mismo: <b>listo</b>, y el hueco no vuelve.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esta va al reves que las otras cuatro y es la peor.</b> Correccion lo soltaba
    /// como listo y la cola —que solo veia un campo vacio— se lo quedaba para siempre: el
    /// documento volvia a la cola cada vez y Miguel no tenia forma de sacarlo. Marcar que un
    /// dato no esta en el papel es cerrar el hueco, no dejarlo abierto.
    /// </remarks>
    [TestMethod]
    public void UnTemploMarcadoComoQueNoEstaEnElPapelSaleDeLasDos()
    {
        var montaje = Montar(
            procedencia => Anotar(
                procedencia, TablaDeProcedencia.Casos, ElCaso, "templo_nombre",
                OrigenDeCampo.Vacio, null, ausente: true),
            temploLeido: null);

        LasDosDicenLoMismo(montaje, "templo vacío marcado «no está en el papel»");
        Assert.IsTrue(
            montaje.LaColaLoDaPorListo,
            "Si Miguel dijo que ese dato no está en el papel, el hueco está cerrado y el "
            + $"documento no vuelve a la cola. La cola dice: «{montaje.LoQueDiceLaCola}».");
    }

    /// <summary>El control: leido entero y con confianza alta, las dos dicen que esta listo.</summary>
    /// <remarks>
    /// Sin este, una regla que dijera «no está listo» siempre pasaria las otras cinco.
    /// </remarks>
    [TestMethod]
    public void ElControlLeidoEnteroYConConfianzaAltaEstaListoEnLasDos()
    {
        var montaje = Montar();

        LasDosDicenLoMismo(montaje, "todo con confianza alta (el control)");
        Assert.IsTrue(montaje.LaColaLoDaPorListo, $"La cola dice: «{montaje.LoQueDiceLaCola}».");
        Assert.AreEqual(0, montaje.SegunCorreccion);
        Assert.AreEqual(0, montaje.SegunLaCola);
    }

    /// <summary>
    /// Y la sexta, que no venia en la tabla del pase: un documento sin NINGUNA persona leida.
    /// </summary>
    /// <remarks>
    /// La encontro este pase midiendo, y va con las otras porque es la misma clase de fallo:
    /// la cola dice «sin ninguna persona leída» y Correccion —que solo mira los cinco campos
    /// del caso— lo daba por listo. Un documento sin personas no se le puede mandar a un
    /// agente: no hay a quien recomendar.
    /// </remarks>
    [TestMethod]
    public void UnDocumentoSinNingunaPersonaLeidaNoEstaListoEnNinguna()
    {
        var montaje = Montar(conPersona: false);

        LasDosDicenLoMismo(montaje, "documento sin ninguna persona leída");
        Assert.IsFalse(montaje.LaColaLoDaPorListo, "No hay a quién recomendar.");
    }
}
