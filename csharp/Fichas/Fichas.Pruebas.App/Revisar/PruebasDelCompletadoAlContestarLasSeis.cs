using Fichas.App.Correccion;
using Fichas.App.Grupo;
using Fichas.App.Revisar;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// El completado que se deriva de las seis en «sí», probado SIN VENTANA.
/// </summary>
/// <remarks>
/// <para>
/// Del dueño, 2026-09-14: <i>«cuando se marcan las 6 preguntas que sí, de manera automática
/// debe marcarse como completado»</i>. Hasta hoy eran dos clics a propósito —guardar las seis
/// y, aparte, «Sí, completa»—; desde hoy el segundo se deriva del primero.
/// </para>
/// <para>
/// ⚠️ <b>Las aserciones salen del criterio de cierre del pase</b>, no del código: con una
/// persona y todos los campos se marca; con dos personas y las seis de una sola NO se marca
/// y el acuse dice quién falta; con un campo que falta NO se marca y el acuse dice cuál; sin
/// administrador NO se marca y el acuse lo dice. Y un «sí» que vuelve a «no» después NO
/// desmarca solo.
/// </para>
/// <para>
/// ⛔ La firma es la del administrador por la regla de <see cref="ElAdministrador"/>: uno
/// activo → él; ninguno o dos → no se marca y se dice por qué. Nunca «el primero activo».
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDelCompletadoAlContestarLasSeis
{
    /// <summary>El día en el que se paran los relojes de estas pruebas; es el de la petición del dueño.</summary>
    private const string DiaDeLasPruebas = "2026-09-14";

    // ═════════ 1. Una persona y todos los campos: se marca en el mismo gesto ═════════

    /// <summary>
    /// Dado un documento con una persona y todos sus campos, cuando se contestan sus seis en
    /// «sí», entonces el documento queda completado con la firma del administrador y su
    /// fecha, y el acuse lo dice con el número del documento y el nombre de quien firma.
    /// </summary>
    [TestMethod]
    public void ConUnaPersonaYTodosLosCamposLasSeisEnSiDejanElDocumentoCompletado()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(1);
        var persona = banco.Personas(caso)[0].Id;

        var resultado = banco.ContestarYDerivar(persona, LasSeisEnSi);
        var despues = banco.Documento(caso);

        Console.WriteLine("== {0} ==", string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));
        Console.WriteLine(
            "   estado={0} por={1} en={2} origen={3}",
            despues.EstadoRecomendacion, despues.EstadoMarcadoPor, despues.EstadoMarcadoEn, despues.EstadoMarcadoOrigen);

        Assert.IsTrue(resultado.SeEscribio, "No se marcó el documento con las seis en «sí».");
        Assert.AreEqual(EstadoDeRecomendacion.Completa, despues.Estado, "El documento no quedó completado.");
        Assert.AreEqual(banco.Miguel, despues.EstadoMarcadoPor, "La marca no la firmó el administrador.");
        Assert.AreEqual(DiaDeLasPruebas + " 12:00:00", despues.EstadoMarcadoEn, "La marca no lleva la fecha del reloj.");
        Assert.AreEqual(CompletadoAlContestarLasSeis.Origen, despues.EstadoMarcadoOrigen, "El origen no dice que vino de las seis.");

        var acuse = resultado.Avisos.Single(a => a.Gravedad == GravedadDeAviso.Informacion).Linea;
        Assert.Contains("SURB2609", acuse, "El acuse no nombra el documento.");
        Assert.Contains("queda completado", acuse, "El acuse no dice que quedó completado.");
        Assert.Contains("firma: Miguel", acuse, "El acuse no dice quién firmó.");
    }

    /// <summary>
    /// Dado un documento que tenía un motivo de no completar, cuando las seis lo dejan
    /// completado, entonces el motivo se vacía: «completa porque el líder no lo hizo» no
    /// puede quedar escrito.
    /// </summary>
    [TestMethod]
    public void AlCompletarseSeVaciaElMotivoDeNoCompletarQueHubiera()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(1);
        banco.PonerMotivo(caso, MotivoDeNoCompletar.ElLiderNoLoHizo);

        banco.ContestarYDerivar(banco.Personas(caso)[0].Id, LasSeisEnSi);
        var despues = banco.Documento(caso);

        Console.WriteLine("== estado={0} motivo={1} ==", despues.EstadoRecomendacion, despues.MotivoNoCompleta);

        Assert.AreEqual(EstadoDeRecomendacion.Completa, despues.Estado);
        Assert.AreEqual(MotivoDeNoCompletar.SinMotivo, despues.Motivo, "Quedó un motivo huérfano sobre un documento completo.");
    }

    // ═════════ 2. Dos personas: hasta que la segunda no tiene las seis, no ═════════

    /// <summary>
    /// Dado un documento con dos personas, cuando se contestan las seis de una sola,
    /// entonces NO se marca completado, el acuse nombra a la que falta, y las seis de la
    /// primera quedan guardadas igual; cuando se contestan las de la otra, entonces sí.
    /// </summary>
    [TestMethod]
    public void ConDosPersonasLasSeisDeUnaSolaNoCompletanYLasDeLaOtraSi()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(2);
        var personas = banco.Personas(caso);

        var primera = banco.ContestarYDerivar(personas[0].Id, LasSeisEnSi);
        var aMedias = banco.Documento(caso);

        Console.WriteLine("== tras la primera: {0} ==", string.Join(" | ", primera.Avisos.Select(a => a.Linea + " — " + a.Detalle)));

        Assert.IsFalse(primera.SeEscribio, "Se marcó completado con una persona sin contestar.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, aMedias.Estado, "El documento cambió de estado con una persona a medias.");
        Assert.IsTrue(banco.TieneLasSeis(personas[0].Id), "Las seis de la primera no se guardaron.");
        var porque = primera.Avisos.Single(a => a.Gravedad != GravedadDeAviso.Informacion);
        Assert.Contains("Persona 2", porque.Linea + porque.Detalle, "El acuse no dice qué persona falta.");
        Assert.DoesNotContain("Persona 1", porque.Linea + porque.Detalle, "El acuse culpa a la persona que sí tiene las seis.");

        var segunda = banco.ContestarYDerivar(personas[1].Id, LasSeisEnSi);
        var despues = banco.Documento(caso);

        Console.WriteLine("== tras la segunda: {0} ==", string.Join(" | ", segunda.Avisos.Select(a => a.Linea)));

        Assert.IsTrue(segunda.SeEscribio, "Con las dos personas en «sí» no se marcó.");
        Assert.AreEqual(EstadoDeRecomendacion.Completa, despues.Estado);
        Assert.AreEqual(banco.Miguel, despues.EstadoMarcadoPor);
    }

    /// <summary>
    /// Dado un documento con dos personas y una de ellas con un paso en «no», cuando la otra
    /// tiene las seis en «sí», entonces NO se marca y el acuse dice en qué paso se quedó la
    /// que falta.
    /// </summary>
    [TestMethod]
    public void UnaPersonaConUnPasoEnNoImpideElCompletadoYElAcuseDiceElPaso()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(2);
        var personas = banco.Personas(caso);
        banco.ContestarYDerivar(personas[1].Id, LasSeisEnSi with { Entrevistas = false });

        var resultado = banco.ContestarYDerivar(personas[0].Id, LasSeisEnSi);
        var porque = resultado.Avisos.Single(a => a.Gravedad != GravedadDeAviso.Informacion);

        Console.WriteLine("== {0} — {1} ==", porque.Linea, porque.Detalle);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, banco.Documento(caso).Estado);
        Assert.Contains("Persona 2", porque.Linea + porque.Detalle);
        Assert.Contains("Entrevistas", porque.Linea + porque.Detalle, "El acuse no dice el paso en «no».");
    }

    // ═════════ 3. Un campo que falta: no se marca y se dice cuál ═════════

    /// <summary>
    /// Dado un documento al que le falta la fecha de viaje, cuando se contestan las seis de
    /// su única persona, entonces NO se marca completado, el acuse dice «Fecha de viaje», y
    /// las seis quedan guardadas igual.
    /// </summary>
    [TestMethod]
    public void ConUnCampoQueFaltaNoSeMarcaYElAcuseDiceElCampo()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(1);
        banco.QuitarLaFechaDeViaje(caso);
        var persona = banco.Personas(caso)[0].Id;

        var resultado = banco.ContestarYDerivar(persona, LasSeisEnSi);
        var porque = resultado.Avisos.Single(a => a.Gravedad != GravedadDeAviso.Informacion);

        Console.WriteLine("== {0} — {1} ==", porque.Linea, porque.Detalle);

        Assert.IsFalse(resultado.SeEscribio, "Se marcó completado con un campo que falta.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, banco.Documento(caso).Estado);
        Assert.Contains(LoQueLeFalta.RotuloDeLaFechaDeViaje, porque.Linea + porque.Detalle, "El acuse no dice qué campo falta.");
        Assert.IsTrue(banco.TieneLasSeis(persona), "Las seis no se guardaron.");
    }

    // ═════════ 4. Sin administrador, o con dos: no se firma a nombre de nadie ═════════

    /// <summary>
    /// Dado un documento con las seis de su persona en «sí» y ningún administrador activo,
    /// cuando se intenta derivar el completado, entonces NO se marca y el acuse dice que
    /// falta el administrador.
    /// </summary>
    /// <remarks>
    /// ⚠️ Por la ventana no se llega aquí: sin administrador, <c>AccionesDeLasPreguntas</c>
    /// tampoco guarda las seis (criterio C19-10). Las seis se escriben por el puerto
    /// directamente, como las escribiría el Excel de un compañero, y se prueba la
    /// derivación sola: es la defensa de que este camino tampoco firme a nombre de nadie.
    /// </remarks>
    [TestMethod]
    public void SinAdministradorActivoNoSeMarcaYElAcuseLoDice()
    {
        var banco = new BancoDelCompletado(conAdministrador: false);
        var caso = banco.SembrarUnDocumentoCompleto(1);
        banco.EscribirLasSeisComoElExcel(banco.Personas(caso)[0].Id, LasSeisEnSi);

        var resultado = banco.Derivar(caso);
        var porque = resultado.Avisos.Single(a => a.Gravedad != GravedadDeAviso.Informacion);

        Console.WriteLine("== {0} — {1} ==", porque.Linea, porque.Detalle);

        Assert.IsFalse(resultado.SeEscribio, "Se marcó sin nadie que firmara.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, banco.Documento(caso).Estado);
        Assert.IsNull(banco.Documento(caso).EstadoMarcadoPor, "Quedó una firma sin administrador.");
        Assert.Contains("administrador", porque.Linea + porque.Detalle, "El acuse no dice que falta el administrador.");
        Assert.DoesNotContain("Sandy", porque.Linea + porque.Detalle, "El acuse ofrece un nombre que no es administrador.");
    }

    /// <summary>
    /// Dados dos administradores activos, cuando se intenta derivar el completado, entonces
    /// NO se marca y el acuse dice cuántos hay: elegir uno sería adivinar.
    /// </summary>
    [TestMethod]
    public void ConDosAdministradoresActivosNoSeMarcaYElAcuseDiceCuantosHay()
    {
        var banco = new BancoDelCompletado();
        banco.DarDeAltaOtroAdministrador("Segundo");
        var caso = banco.SembrarUnDocumentoCompleto(1);
        banco.EscribirLasSeisComoElExcel(banco.Personas(caso)[0].Id, LasSeisEnSi);

        var resultado = banco.Derivar(caso);
        var porque = resultado.Avisos.Single(a => a.Gravedad != GravedadDeAviso.Informacion);

        Console.WriteLine("== {0} — {1} ==", porque.Linea, porque.Detalle);

        Assert.IsFalse(resultado.SeEscribio);
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, banco.Documento(caso).Estado);
        Assert.Contains("2 administradores", porque.Linea + porque.Detalle);
    }

    // ═════════ Lo que NO hace: desmarcar, ni pisar una marca que ya estaba ═════════

    /// <summary>
    /// Dado un documento ya completado por las seis, cuando un «sí» vuelve a «no», entonces
    /// el documento sigue completado: desmarcarlo lo decide él a mano.
    /// </summary>
    [TestMethod]
    public void VolverUnSiANoDespuesNoDesmarcaElCompletado()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(1);
        var persona = banco.Personas(caso)[0].Id;
        banco.ContestarYDerivar(persona, LasSeisEnSi);

        var resultado = banco.ContestarYDerivar(persona, LasSeisEnSi with { CitaDelTemplo = false });
        var despues = banco.Documento(caso);

        Console.WriteLine("== estado={0} avisos={1} ==", despues.EstadoRecomendacion, resultado.Avisos.Count);

        Assert.AreEqual(EstadoDeRecomendacion.Completa, despues.Estado, "Un «no» posterior desmarcó solo el completado.");
        Assert.AreEqual("no", PreguntasDeUnDocumento.DecirLaRespuesta(PreguntasDeUnDocumento.LasSeisDe(banco.Personas(caso)[0])[2].Respuesta), "El «no» no se guardó.");
    }

    /// <summary>
    /// Dado un documento que el Excel de Sandy ya marcó completo, cuando las seis quedan en
    /// «sí», entonces la marca de Sandy NO se pisa: sigue siendo suya.
    /// </summary>
    [TestMethod]
    public void UnDocumentoQueYaEstabaCompletoNoSeVuelveAMarcar()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(1);
        banco.MarcarComoElExcelDeSandy(caso);
        var antes = banco.Documento(caso);

        var resultado = banco.ContestarYDerivar(banco.Personas(caso)[0].Id, LasSeisEnSi);
        var despues = banco.Documento(caso);

        Console.WriteLine("== {0} ==", string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));

        Assert.AreEqual(antes.EstadoMarcadoPor, despues.EstadoMarcadoPor, "Se pisó la firma de Sandy.");
        Assert.AreEqual(antes.EstadoMarcadoOrigen, despues.EstadoMarcadoOrigen, "Se pisó el origen de la marca de Sandy.");
        Assert.AreEqual(EstadoDeRecomendacion.Completa, despues.Estado);
    }

    /// <summary>
    /// Dado un documento cuya persona tiene un paso en «no», cuando se guardan esas seis,
    /// entonces no se intenta completar y no hay acuse de completado: un «no» no es un
    /// «sí» a medias.
    /// </summary>
    [TestMethod]
    public void GuardarUnNoNoIntentaCompletarNiDiceNada()
    {
        var banco = new BancoDelCompletado();
        var caso = banco.SembrarUnDocumentoCompleto(1);

        var resultado = banco.ContestarYDerivar(banco.Personas(caso)[0].Id, LasSeisEnSi with { Preparacion = false });

        Console.WriteLine("== avisos={0} ==", string.Join(" | ", resultado.Avisos.Select(a => a.Linea)));

        Assert.IsTrue(resultado.SeEscribio, "Las seis con un «no» no se guardaron.");
        Assert.AreEqual(EstadoDeRecomendacion.SinMarcar, banco.Documento(caso).Estado);
        Assert.IsFalse(
            resultado.Avisos.Any(a => a.Linea.Contains("completado", StringComparison.Ordinal)),
            "Con un «no» se habló del completado, y no había nada que derivar.");
    }

    // ═════════ La regla, sola: qué lo impide ═════════

    /// <summary>
    /// Dada la lista de impedimentos, entonces se enumeran los tres tipos a la vez —campos,
    /// personas y administrador— y vacía significa que se puede.
    /// </summary>
    [TestMethod]
    public void LoQueLoImpideEnumeraCamposPersonasYAdministradorALaVez()
    {
        var sinContestar = new Persona { Id = 7, CasoId = 1, Nombre = "Ana", Mrn = "055-1111-3850", FilaFormulario = 1 };
        var conLasSeis = new Persona
        {
            Id = 8, CasoId = 1, Nombre = "Beto", Mrn = "055-1111-3851", FilaFormulario = 2,
            PasoPreparacion = true, PasoInformacion = true, PasoCitaDelTemplo = true,
            PasoAccionesRequeridas = true, PasoEntrevistas = true, PasoListoParaElTemplo = true,
        };

        var todo = CompletadoAlContestarLasSeis.LoQueLoImpide(
            [sinContestar, conLasSeis], [LoQueLeFalta.RotuloDelTemplo], []);
        var nada = CompletadoAlContestarLasSeis.LoQueLoImpide(
            [conLasSeis], [], [new Companero { Id = 1, Nombre = "Miguel", Rol = RolDeCompanero.Administrador }]);

        Console.WriteLine("== {0} ==", string.Join(" / ", todo));

        Assert.HasCount(3, todo, "No salen los tres impedimentos.");
        Assert.Contains(LoQueLeFalta.RotuloDelTemplo, todo[0]);
        Assert.Contains("Ana", todo[1]);
        Assert.Contains("administrador", todo[2]);
        Assert.IsEmpty(nada, "Con todo en orden sigue habiendo impedimentos.");
    }

    // ───────────────────────────── el banco de la prueba ─────────────────────────────

    /// <summary>Las seis en «sí», que es lo que el dueño marca cuando la persona está lista.</summary>
    private static RespuestaALosPasos LasSeisEnSi => new(true, true, true, true, true, true);

    /// <summary>
    /// Un almacén inventado con su equipo y su procedencia, para probar la derivación sin
    /// abrir ninguna ventana.
    /// </summary>
    /// <remarks>
    /// El equipo se siembra con <b>Sandy la primera y Miguel después</b>: es el orden que
    /// caza la regla vieja de «el primer activo», que firmaría como Sandy.
    /// </remarks>
    private sealed class BancoDelCompletado
    {
        /// <summary>El almacén en memoria sobre el que corren los repositorios falsos.</summary>
        private readonly AlmacenFalso _almacen;
        /// <summary>Los documentos de la prueba.</summary>
        private readonly RepositorioDeCasosFalso _casos;
        /// <summary>Las personas, por donde se escriben las seis.</summary>
        private readonly RepositorioDePersonasFalso _personas;
        /// <summary>El equipo: Sandy primero y, si se pide, Miguel administrador después.</summary>
        private readonly RepositorioDeCompanerosFalso _companeros;
        /// <summary>La procedencia de campos: sin fila, un campo con valor cuenta como que falta.</summary>
        private readonly RepositorioDeProcedenciaFalso _procedencia;
        /// <summary>Las acciones de la ventana, que guardan las seis con la firma del administrador.</summary>
        private readonly AccionesDeLasPreguntas _acciones;
        /// <summary>Lo que se prueba: la derivación del completado.</summary>
        private readonly CompletadoAlContestarLasSeis _completado;

        /// <summary>Siembra el equipo y monta las acciones y la derivación sobre los mismos falsos.</summary>
        /// <param name="conAdministrador">Si se da de alta a Miguel como administrador; sin él nadie firma.</param>
        public BancoDelCompletado(bool conAdministrador = true)
        {
            _almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);
            _casos = new RepositorioDeCasosFalso(_almacen);
            _personas = new RepositorioDePersonasFalso(_almacen);
            _companeros = new RepositorioDeCompanerosFalso(_almacen);
            _procedencia = new RepositorioDeProcedenciaFalso(_almacen);

            Sandy = _companeros.Guardar(new Companero
            {
                Nombre = "Sandy",
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Companero,
            }).Id;

            if (conAdministrador) Miguel = DarDeAltaOtroAdministrador("Miguel");

            _acciones = new AccionesDeLasPreguntas(_personas, _companeros);
            _completado = new CompletadoAlContestarLasSeis(_casos, _personas, _procedencia, _companeros);
        }

        /// <summary>El número interno de Sandy, la compañera que NO es administradora.</summary>
        public long Sandy { get; }

        /// <summary>El número interno de Miguel, el administrador; 0 si se sembró sin él.</summary>
        public long Miguel { get; }

        /// <summary>Da de alta un administrador activo más, con ese nombre.</summary>
        /// <param name="nombre">Cómo se llama.</param>
        /// <returns>Su número interno.</returns>
        public long DarDeAltaOtroAdministrador(string nombre)
            => _companeros.Guardar(new Companero
            {
                Nombre = nombre,
                CreadoEn = DiaDeLasPruebas + " 09:00:00",
                Rol = RolDeCompanero.Administrador,
            }).Id;

        /// <summary>
        /// Mete un documento con todos sus campos leídos y con procedencia, y esas personas.
        /// </summary>
        /// <remarks>
        /// La procedencia se anota campo por campo con origen OCR y confianza alta: sin fila,
        /// <see cref="LoQueLeFalta"/> cuenta el campo como que no consta de dónde salió, y la
        /// prueba mediría otra cosa.
        /// </remarks>
        /// <param name="cuantas">Cuántas personas trae el documento.</param>
        /// <returns>El número interno del documento.</returns>
        public long SembrarUnDocumentoCompleto(int cuantas)
        {
            var caso = _casos.Guardar(new Caso
            {
                NumeroCaso = "SURB2609",
                UnidadNumero = "325535",
                UnidadNombre = "Paramaribo",
                FechaViaje = "2026-09-17",
                TemploNombre = "Santo Domingo",
                RutaPdf = @"C:\Fichas\entrada\SURB2609_grupo.pdf",
                CreadoEn = DiaDeLasPruebas + " 10:00:00",
            }).Id;
            foreach (var columna in LoQueLeFalta.ColumnasDelCaso) AnotarLeido(TablaDeProcedencia.Casos, caso, columna);

            for (var i = 0; i < cuantas; i++)
            {
                var persona = _personas.Guardar(new Persona
                {
                    CasoId = caso,
                    Mrn = $"055-1111-38{50 + i}",
                    Nombre = $"Persona {i + 1}",
                    FilaFormulario = i + 1,
                }).Id;
                foreach (var columna in LoQueLeFalta.ColumnasDeLaPersona) AnotarLeido(TablaDeProcedencia.Personas, persona, columna);
            }

            return caso;
        }

        /// <summary>Le quita la fecha de viaje al documento y su procedencia: pasa a faltarle.</summary>
        /// <param name="casoId">El documento.</param>
        public void QuitarLaFechaDeViaje(long casoId)
        {
            _almacen.Casos[casoId] = _almacen.Casos[casoId] with { FechaViaje = null };
            _procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = TablaDeProcedencia.Casos,
                RegistroId = casoId,
                Campo = LoQueLeFalta.ColumnaDeLaFechaDeViaje,
                Origen = OrigenDeCampo.Vacio,
            });
        }

        /// <summary>Deja escrito un motivo de no completar, como lo dejaría Miguel a mano.</summary>
        /// <param name="casoId">El documento.</param>
        /// <param name="motivo">El motivo que se escribe.</param>
        public void PonerMotivo(long casoId, MotivoDeNoCompletar motivo)
            => _almacen.Casos[casoId] = _almacen.Casos[casoId] with { MotivoNoCompleta = Caso.EscribirMotivo(motivo) };

        /// <summary>Marca el documento completo como lo marcaría el Excel devuelto por Sandy.</summary>
        /// <param name="casoId">El documento.</param>
        public void MarcarComoElExcelDeSandy(long casoId)
            => _casos.MarcarEstadoDelCompanero(
                casoId, EstadoDeRecomendacion.Completa, MotivoDeNoCompletar.SinMotivo, Sandy, @"C:\Fichas\devueltos\Sandy.xlsx");

        /// <summary>Escribe las seis por el puerto, a nombre de Sandy, como lo haría su Excel.</summary>
        /// <param name="personaId">La persona.</param>
        /// <param name="respuesta">Las seis.</param>
        public void EscribirLasSeisComoElExcel(long personaId, RespuestaALosPasos respuesta)
            => _personas.ResponderLosPasos(personaId, respuesta, Sandy, @"C:\Fichas\devueltos\Sandy.xlsx");

        /// <summary>Las personas de un documento, releídas de la base.</summary>
        /// <param name="casoId">El documento.</param>
        public IReadOnlyList<Persona> Personas(long casoId) => _personas.DeCaso(casoId);

        /// <summary>Si esa persona tiene las seis en «sí» en la base, releída.</summary>
        /// <param name="personaId">La persona.</param>
        public bool TieneLasSeis(long personaId) => CompletadoAlContestarLasSeis.TocaIntentarlo(_personas.Obtener(personaId)!);

        /// <summary>El documento, releído de la base.</summary>
        /// <param name="casoId">El documento.</param>
        public Caso Documento(long casoId) => _casos.Obtener(casoId)!;

        /// <summary>
        /// Contesta las seis de una persona por el camino de la ventana y, si quedaron en
        /// «sí», deriva el completado: es el mismo gesto que hace la ventana al guardar.
        /// </summary>
        /// <param name="personaId">La persona.</param>
        /// <param name="respuesta">Las seis, cada una en sí, no o en blanco.</param>
        /// <returns>Lo de la derivación si se intentó; si no, lo del guardado de las seis.</returns>
        public ResultadoDeEscritura ContestarYDerivar(long personaId, RespuestaALosPasos respuesta)
        {
            var guardado = _acciones.Guardar(personaId, respuesta);
            Assert.IsTrue(guardado.SeEscribio, "Las seis no se guardaron: " + string.Join(" | ", guardado.Avisos.Select(a => a.Linea)));

            var persona = _personas.Obtener(personaId)!;
            return CompletadoAlContestarLasSeis.TocaIntentarlo(persona)
                ? _completado.SiCorresponde(persona.CasoId)
                : guardado;
        }

        /// <summary>Deriva el completado de un documento sin pasar por el guardado de las seis.</summary>
        /// <param name="casoId">El documento.</param>
        public ResultadoDeEscritura Derivar(long casoId) => _completado.SiCorresponde(casoId);

        /// <summary>Anota un campo como leído por OCR con confianza alta: el lector lo trajo bien.</summary>
        /// <param name="tabla">Si el campo es del caso o de una persona.</param>
        /// <param name="registroId">El número interno de la fila.</param>
        /// <param name="campo">La columna.</param>
        private void AnotarLeido(TablaDeProcedencia tabla, long registroId, string campo)
            => _procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = tabla,
                RegistroId = registroId,
                Campo = campo,
                Origen = OrigenDeCampo.Ocr,
                Confianza = 0.95,
            });
    }
}
