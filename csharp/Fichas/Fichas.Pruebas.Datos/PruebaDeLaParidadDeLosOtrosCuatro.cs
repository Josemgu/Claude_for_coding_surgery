using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Datos.Falso;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// La paridad de los otros cuatro: personas, asignaciones, procedencia e ilegibles.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PruebaDeLaParidadDelFalso"/> cubrio <c>ICasos</c> e <c>ICompaneros</c> y
/// encontro cuatro divergencias, una de ellas un hueco real en la base del dueno. Esta
/// clase termina el barrido por los cuatro puertos que quedaban: <b>cada divergencia es
/// una prueba del arbol que hoy puede estar en verde sobre nada.</b>
/// </para>
/// <para>
/// Las aserciones salen del <b>contrato</b> —lo que el resumen de cada metodo de
/// <c>Fichas.Contratos/Puertos</c> promete—, no de lo que cualquiera de las dos
/// implementaciones hace hoy. Cuando las dos discrepan, la que se corrige es la que se
/// aparta del contrato, no siempre el falso.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaParidadDeLosOtrosCuatro
{
    private const string DiaDeLasPruebas = "2026-09-05";

    // ═══════════════════════════ IPersonas ═══════════════════════════

    /// <summary>
    /// Dada una persona con un MRN raro, cuando se guarda en los dos, entonces los dos la
    /// GUARDAN y la senalan: requisito 9, avisar y nunca impedir.
    /// </summary>
    [TestMethod]
    public void UnMrnRaroEntraYSaleAvisadoEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);

            var enElDeVerdad = deVerdad.Personas.Guardar(
                new Persona { CasoId = casoDeVerdad, Mrn = "123", Nombre = "Elena" });
            var enElFalso = falso.Personas.Guardar(
                new Persona { CasoId = casoFalso, Mrn = "123", Nombre = "Elena" });

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "guardar una persona con un MRN corto");
            Assert.IsTrue(enElDeVerdad.SeEscribio, "El de verdad rechazo un MRN corto, y el dueno mando guardarlo.");
        }
    }

    /// <summary>
    /// Dada una persona sin nombre y sin MRN, cuando se guarda en los dos, entonces los
    /// dos hacen lo mismo: una fila en blanco no es una persona.
    /// </summary>
    [TestMethod]
    public void UnaPersonaEnBlancoSeTrataIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);

            var enElDeVerdad = deVerdad.Personas.Guardar(new Persona { CasoId = casoDeVerdad });
            var enElFalso = falso.Personas.Guardar(new Persona { CasoId = casoFalso });

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "guardar una persona en blanco");
        }
    }

    /// <summary>
    /// Dada la propuesta de un companero sobre una persona, cuando se anota en los dos,
    /// entonces los dos la escriben igual Y NINGUNO firma nada (regla permanente 5).
    /// </summary>
    [TestMethod]
    public void AnotarUnaPropuestaEscribeLoMismoEnLosDosYNoFirmaNada()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");
            var personaDeVerdad = deVerdad.Personas.Guardar(
                new Persona { CasoId = casoDeVerdad, Mrn = "055-1111-385A", Nombre = "Elena" }).Id;
            var personaFalsa = falso.Personas.Guardar(
                new Persona { CasoId = casoFalso, Mrn = "055-1111-385A", Nombre = "Elena" }).Id;

            var propuesta = new Persona
            {
                EstadoPropuesto = "incompleta",
                NotaCompanero = "El lider no contesto en tres intentos.",
                LlamoAlLider = true,
                PasoEntrevistas = false,
            };

            var enElDeVerdad = deVerdad.Personas.AnotarPropuesta(
                personaDeVerdad, propuesta with { Id = personaDeVerdad, CasoId = casoDeVerdad }, sandyDeVerdad);
            var enElFalso = falso.Personas.AnotarPropuesta(
                personaFalsa, propuesta with { Id = personaFalsa, CasoId = casoFalso }, sandyFalso);

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "anotar una propuesta");

            var leidaDeVerdad = deVerdad.Personas.Obtener(personaDeVerdad)!;
            var leidaFalsa = falso.Personas.Obtener(personaFalsa)!;

            Assert.AreEqual(leidaDeVerdad.EstadoPropuesto, leidaFalsa.EstadoPropuesto, "estado_propuesto");
            Assert.AreEqual(leidaDeVerdad.NotaCompanero, leidaFalsa.NotaCompanero, "nota_companero");
            Assert.AreEqual(leidaDeVerdad.LlamoAlLider, leidaFalsa.LlamoAlLider, "llamo_al_lider");
            Assert.AreEqual(leidaDeVerdad.PasoEntrevistas, leidaFalsa.PasoEntrevistas, "paso_entrevistas");
            CompararQuien(
                leidaDeVerdad.PropuestoPor, leidaFalsa.PropuestoPor, sandyDeVerdad, sandyFalso, "propuesto_por");
            CompararSiLaHay(leidaDeVerdad.PropuestoEn, leidaFalsa.PropuestoEn, "propuesto_en");

            Assert.AreEqual(
                0,
                deVerdad.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, personaDeVerdad),
                "Anotar una propuesta firmo un campo. Regla permanente 5: solo Miguel firma.");
            Assert.AreEqual(
                0,
                falso.Procedencia.ContarVerificados(TablaDeProcedencia.Personas, personaFalsa),
                "El falso firmo al anotar una propuesta.");
        }
    }

    /// <summary>
    /// Dada una persona que no existe, cuando se le anota una propuesta en los dos,
    /// entonces los dos lo dicen y ninguno finge que escribio.
    /// </summary>
    [TestMethod]
    public void AnotarSobreUnaPersonaQueNoExisteFallaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var enElDeVerdad = deVerdad.Personas.AnotarPropuesta(9999, new Persona { CasoId = 1 }, 1);
            var enElFalso = falso.Personas.AnotarPropuesta(9999, new Persona { CasoId = 1 }, 1);

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "anotar sobre una persona que no existe");
        }
    }

    // ═════════════════════════ IAsignaciones ═════════════════════════

    /// <summary>
    /// Dado un caso ya asignado a alguien, cuando se le asigna otra vez al mismo en los
    /// dos, entonces los dos hacen lo mismo: no se duplica la asignacion viva.
    /// </summary>
    [TestMethod]
    public void AsignarDosVecesAlMismoNoDuplicaEnNingunoDeLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");

            deVerdad.Asignaciones.Asignar(casoDeVerdad, sandyDeVerdad, DiaDeLasPruebas);
            falso.Asignaciones.Asignar(casoFalso, sandyFalso, DiaDeLasPruebas);

            var repiteDeVerdad = deVerdad.Asignaciones.Asignar(casoDeVerdad, sandyDeVerdad, DiaDeLasPruebas);
            var repiteFalso = falso.Asignaciones.Asignar(casoFalso, sandyFalso, DiaDeLasPruebas);

            CompararLasDosEscrituras(repiteDeVerdad, repiteFalso, "asignar dos veces al mismo companero");
            CompararLasVivas(deVerdad, falso, casoDeVerdad, casoFalso, "tras asignar dos veces");
        }
    }

    /// <summary>
    /// Dado un companero desactivado, cuando se le asigna un caso en los dos, entonces los
    /// dos lo rechazan: el contrato dice «a un companero ACTIVO».
    /// </summary>
    [TestMethod]
    public void AsignarAUnCompaneroDesactivadoSeRechazaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");

            deVerdad.Companeros.Desactivar(sandyDeVerdad, DiaDeLasPruebas);
            falso.Companeros.Desactivar(sandyFalso, DiaDeLasPruebas);

            var enElDeVerdad = deVerdad.Asignaciones.Asignar(casoDeVerdad, sandyDeVerdad, DiaDeLasPruebas);
            var enElFalso = falso.Asignaciones.Asignar(casoFalso, sandyFalso, DiaDeLasPruebas);

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "asignar a un companero desactivado");
            Assert.IsFalse(
                enElDeVerdad.SeEscribio,
                "El de verdad asigno trabajo a alguien que ya no esta.");
        }
    }

    /// <summary>
    /// Dado un caso que no existe, cuando se asigna en los dos, entonces los dos lo dicen.
    /// </summary>
    [TestMethod]
    public void AsignarUnCasoQueNoExisteFallaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");

            var enElDeVerdad = deVerdad.Asignaciones.Asignar(9999, sandyDeVerdad, DiaDeLasPruebas);
            var enElFalso = falso.Asignaciones.Asignar(9999, sandyFalso, DiaDeLasPruebas);

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "asignar un caso que no existe");
        }
    }

    /// <summary>
    /// Dada una asignacion viva, cuando se retira en los dos, entonces los dos la
    /// desactivan con su fecha y ninguno la borra; y sin fecha, los dos igual.
    /// </summary>
    [TestMethod]
    public void RetirarDejaLoMismoEnLosDosYSinFechaTambien()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");

            var laDeVerdad = deVerdad.Asignaciones.Asignar(casoDeVerdad, sandyDeVerdad, DiaDeLasPruebas).Id;
            var laFalsa = falso.Asignaciones.Asignar(casoFalso, sandyFalso, DiaDeLasPruebas).Id;

            CompararLasDosEscrituras(
                deVerdad.Asignaciones.Retirar(laDeVerdad, string.Empty),
                falso.Asignaciones.Retirar(laFalsa, string.Empty),
                "retirar una asignacion sin fecha");

            CompararLasDosEscrituras(
                deVerdad.Asignaciones.Retirar(laDeVerdad, DiaDeLasPruebas),
                falso.Asignaciones.Retirar(laFalsa, DiaDeLasPruebas),
                "retirar una asignacion");

            CompararLasVivas(deVerdad, falso, casoDeVerdad, casoFalso, "tras retirar");

            CompararLasDosEscrituras(
                deVerdad.Asignaciones.Retirar(9999, DiaDeLasPruebas),
                falso.Asignaciones.Retirar(9999, DiaDeLasPruebas),
                "retirar una asignacion que no existe");
        }
    }

    // ═════════════════════════ IProcedencia ═════════════════════════

    /// <summary>
    /// Dado un campo recien extraido, cuando se anota en los dos, entonces NINGUNO lo deja
    /// verificado: es la regla permanente 5 y el unico camino a verificado es firmar.
    /// </summary>
    [TestMethod]
    public void AnotarLaProcedenciaNoVerificaEnNingunoDeLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);

            var enElDeVerdad = deVerdad.Procedencia.Anotar(UnCampo(casoDeVerdad));
            var enElFalso = falso.Procedencia.Anotar(UnCampo(casoFalso));

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "anotar la procedencia de un campo");

            Assert.AreEqual(
                0,
                deVerdad.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoDeVerdad),
                "El de verdad dejo un campo verificado al anotarlo.");
            Assert.AreEqual(
                0,
                falso.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoFalso),
                "El falso dejo un campo verificado al anotarlo.");
        }
    }

    /// <summary>
    /// Dado un campo anotado, cuando Miguel lo firma en los dos, entonces los dos lo
    /// cuentan como verificado; y al retirar la firma, los dos la retiran.
    /// </summary>
    /// <remarks>
    /// Retirar la firma es «el fallo mas grave que ha tenido este proyecto», segun el
    /// propio contrato: un campo que decia «firmado por Miguel» sobre un dato que Miguel
    /// nunca vio. Si el falso y el de verdad no lo hacen igual, la prueba que lo vigila
    /// puede estar mirando al lado que si funciona.
    /// </remarks>
    [TestMethod]
    public void FirmarYRetirarLaFirmaHacenLoMismoEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (miguelDeVerdad, miguelFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Miguel");
            deVerdad.Procedencia.Anotar(UnCampo(casoDeVerdad));
            falso.Procedencia.Anotar(UnCampo(casoFalso));

            CompararLasDosEscrituras(
                deVerdad.Procedencia.Firmar(
                    TablaDeProcedencia.Casos, casoDeVerdad, "fecha_viaje", miguelDeVerdad, DiaDeLasPruebas),
                falso.Procedencia.Firmar(
                    TablaDeProcedencia.Casos, casoFalso, "fecha_viaje", miguelFalso, DiaDeLasPruebas),
                "firmar un campo");

            Assert.AreEqual(
                deVerdad.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoDeVerdad),
                falso.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoFalso),
                "Tras firmar, los dos no cuentan los mismos campos verificados.");
            Assert.AreEqual(
                1,
                deVerdad.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoDeVerdad),
                "Firmar no dejo el campo verificado en el de verdad.");

            CompararLasDosEscrituras(
                deVerdad.Procedencia.RetirarLaFirma(TablaDeProcedencia.Casos, casoDeVerdad, "fecha_viaje"),
                falso.Procedencia.RetirarLaFirma(TablaDeProcedencia.Casos, casoFalso, "fecha_viaje"),
                "retirar la firma de un campo");

            Assert.AreEqual(
                0,
                deVerdad.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoDeVerdad),
                "La firma no se retiro en el de verdad.");
            Assert.AreEqual(
                0,
                falso.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, casoFalso),
                "La firma no se retiro en el falso.");
        }
    }

    /// <summary>
    /// Dada una firma que no existe, cuando se retira en los dos, entonces los dos avisan
    /// y ninguno levanta: no habia nada que retirar, y eso no es un error.
    /// </summary>
    [TestMethod]
    public void RetirarUnaFirmaQueNoEstaAvisaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            CompararLasDosEscrituras(
                deVerdad.Procedencia.RetirarLaFirma(TablaDeProcedencia.Casos, 9999, "fecha_viaje"),
                falso.Procedencia.RetirarLaFirma(TablaDeProcedencia.Casos, 9999, "fecha_viaje"),
                "retirar una firma que no esta");
        }
    }

    // ══════════════════════════ IIlegibles ══════════════════════════

    /// <summary>
    /// Dado un PDF ilegible y una fila del Excel sin par, cuando se registran en los dos,
    /// entonces los dos los guardan y los dos los devuelven al listarlos.
    /// </summary>
    [TestMethod]
    public void RegistrarUnIlegibleYUnaDescartadaHacenLoMismoEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");

            var elRenglon = new RenglonIlegible
            {
                RutaPdf = @"C:\escaneos\hoja0.pdf",
                Motivo = "sin_texto",
                RegistradoEn = DiaDeLasPruebas + " 07:24:00",
            };

            CompararLasDosEscrituras(
                deVerdad.Ilegibles.Registrar(elRenglon),
                falso.Ilegibles.Registrar(elRenglon),
                "registrar un documento ilegible");

            Assert.AreEqual(
                deVerdad.Ilegibles.Contar(new FiltroDeIlegibles()),
                falso.Ilegibles.Contar(new FiltroDeIlegibles()),
                "Uno de los dos guardo el renglon ilegible y el otro no.");

            var laFila = new FilaDescartada
            {
                CompaneroId = 0,
                RutaExcel = @"C:\paquetes\por_verificar.xlsx",
                FilaExcel = 7,
                NumeroCaso = "CASP2609",
                Mrn = "055-1111-385A",
                Nombre = "Elena",
                Motivo = "Fila 7: sin par.",
                RegistradoEn = DiaDeLasPruebas + " 08:00:00",
            };

            CompararLasDosEscrituras(
                deVerdad.Ilegibles.RegistrarDescartada(laFila with { CompaneroId = sandyDeVerdad }),
                falso.Ilegibles.RegistrarDescartada(laFila with { CompaneroId = sandyFalso }),
                "registrar una fila descartada");

            Assert.AreEqual(
                deVerdad.Ilegibles.ListarDescartadas(sandyDeVerdad, PrimeraPagina).TotalDisponible,
                falso.Ilegibles.ListarDescartadas(sandyFalso, PrimeraPagina).TotalDisponible,
                "Uno de los dos guardo la fila descartada y el otro no.");
        }
    }

    /// <summary>
    /// Dada una fila descartada de un companero que no existe, cuando se registra en los
    /// dos, entonces los dos hacen lo mismo.
    /// </summary>
    /// <remarks>
    /// El de verdad tiene una clave foranea hacia <c>companeros</c> y el falso no tiene
    /// ninguna: es el sitio donde un doble se separa del original sin que nadie lo note.
    /// </remarks>
    [TestMethod]
    public void UnaDescartadaDeUnCompaneroQueNoExisteHaceLoMismoEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var laFila = new FilaDescartada
            {
                CompaneroId = 9999,
                RutaExcel = @"C:\paquetes\por_verificar.xlsx",
                FilaExcel = 7,
                Motivo = "Fila 7: sin par.",
                RegistradoEn = DiaDeLasPruebas + " 08:00:00",
            };

            CompararLasDosEscrituras(
                deVerdad.Ilegibles.RegistrarDescartada(laFila),
                falso.Ilegibles.RegistrarDescartada(laFila),
                "registrar una descartada de un companero que no existe");
        }
    }

    // ─────────────────────────── el andamio ───────────────────────────

    private static readonly Pagina PrimeraPagina = new(0, 50);

    private sealed record Juego(
        RepositorioDeCasos Casos,
        RepositorioDeCompaneros Companeros,
        RepositorioDeAsignaciones Asignaciones,
        RepositorioDePersonas Personas,
        RepositorioDeProcedencia Procedencia,
        RepositorioDeIlegibles Ilegibles);

    private sealed record JuegoFalso(
        RepositorioDeCasosFalso Casos,
        RepositorioDeCompanerosFalso Companeros,
        RepositorioDeAsignacionesFalso Asignaciones,
        RepositorioDePersonasFalso Personas,
        RepositorioDeProcedenciaFalso Procedencia,
        RepositorioDeIlegiblesFalso Ilegibles);

    private static (Juego DeVerdad, JuegoFalso Falso, IDisposable Cerrar) MontarLosDos()
    {
        var baseDePrueba = BaseDePrueba.Nueva();
        var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);

        var deVerdad = new Juego(
            new RepositorioDeCasos(baseDePrueba.Conexion),
            new RepositorioDeCompaneros(baseDePrueba.Conexion),
            new RepositorioDeAsignaciones(baseDePrueba.Conexion),
            new RepositorioDePersonas(baseDePrueba.Conexion),
            new RepositorioDeProcedencia(baseDePrueba.Conexion),
            new RepositorioDeIlegibles(baseDePrueba.Conexion));

        var falso = new JuegoFalso(
            new RepositorioDeCasosFalso(almacen),
            new RepositorioDeCompanerosFalso(almacen),
            new RepositorioDeAsignacionesFalso(almacen),
            new RepositorioDePersonasFalso(almacen),
            new RepositorioDeProcedenciaFalso(almacen),
            new RepositorioDeIlegiblesFalso(almacen));

        return (deVerdad, falso, baseDePrueba);
    }

    private static (long DeVerdad, long Falso) SembrarUnCasoEnLosDos(Juego deVerdad, JuegoFalso falso)
    {
        var caso = new Caso
        {
            NumeroCaso = "CASP2609",
            FechaViaje = "2026-09-08",
            CreadoEn = DiaDeLasPruebas + " 10:00:00",
        };

        return (deVerdad.Casos.Guardar(caso).Id, falso.Casos.Guardar(caso).Id);
    }

    private static (long DeVerdad, long Falso) SembrarUnCompaneroEnLosDos(
        Juego deVerdad, JuegoFalso falso, string nombre)
    {
        var quienEs = new Companero { Nombre = nombre, CreadoEn = DiaDeLasPruebas + " 10:00:00" };

        return (deVerdad.Companeros.Guardar(quienEs).Id, falso.Companeros.Guardar(quienEs).Id);
    }

    private static ProcedenciaDeCampo UnCampo(long casoId) => new()
    {
        Tabla = TablaDeProcedencia.Casos,
        RegistroId = casoId,
        Campo = "fecha_viaje",
        Origen = OrigenDeCampo.Ocr,
        Confianza = 0.42,
        ValorOcr = "September 7, 2026",
    };

    /// <summary>
    /// Dadas las seis clases de fila que deciden «listo para asignar», cuando se leen en
    /// bloque en los dos, entonces los dos devuelven exactamente las mismas.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es la prueba que sostiene el arreglo del 2026-09-06.</b>
    /// <c>LasQuePesanEnElVeredicto</c> lleva las cinco condiciones escritas dos veces: una en
    /// el <c>WHERE</c> de SQLite y otra en LINQ sobre el almacen inventado. Si se separan, las
    /// pruebas de la pantalla —que corren sobre el falso— saldrian verdes sobre una regla que
    /// la base de verdad no aplica, y el dueno veria documentos «listos» con datos que nadie
    /// miro. Es exactamente el defecto que este archivo existe para cazar.
    /// </remarks>
    [TestMethod]
    public void LasQuePesanEnElVeredictoDevuelvenLoMismoEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);

            // Las seis clases de fila, una por columna del caso; la sexta —«sin fila
            // ninguna»— es no anotar nada de `templo_nombre`, y por eso no aparece abajo.
            SembrarLasSeisClases(deVerdad.Procedencia, casoDeVerdad);
            SembrarLasSeisClases(falso.Procedencia, casoFalso);

            var pesanDeVerdad = Retratar(deVerdad.Procedencia.LasQuePesanEnElVeredicto(0.6), casoDeVerdad);
            var pesanFalsas = Retratar(falso.Procedencia.LasQuePesanEnElVeredicto(0.6), casoFalso);

            var loDelDeVerdad = string.Join(" | ", pesanDeVerdad);
            var loDelFalso = string.Join(" | ", pesanFalsas);

            CollectionAssert.AreEqual(
                pesanDeVerdad,
                pesanFalsas,
                "El de verdad y el falso no dejan pasar las mismas filas. Con eso, una pantalla "
                + "probada contra el falso da un veredicto que la base de verdad no da. "
                + $"De verdad: {loDelDeVerdad}. Falso: {loDelFalso}.");

            Assert.HasCount(
                3, pesanDeVerdad,
                "Tienen que pesar tres: la de poca confianza, la de confianza nula y la tachada.");
        }
    }

    /// <summary>
    /// Dados unos campos anotados, cuando se preguntan en los dos, entonces los dos dicen
    /// los MISMOS campos y en el mismo orden.
    /// </summary>
    /// <remarks>
    /// Sin esto, «este campo no tiene fila» —que es uno de los seis casos y el unico que
    /// distingue «se leyo bien» de «no consta de donde salio»— podria contestarse distinto en
    /// cada implementacion sin que nada se pusiera rojo.
    /// </remarks>
    [TestMethod]
    public void CamposAnotadosDeDiceLosMismosCamposEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            SembrarLasSeisClases(deVerdad.Procedencia, casoDeVerdad);
            SembrarLasSeisClases(falso.Procedencia, casoFalso);

            var enElDeVerdad = deVerdad.Procedencia.CamposAnotadosDe(TablaDeProcedencia.Casos);
            var enElFalso = falso.Procedencia.CamposAnotadosDe(TablaDeProcedencia.Casos);

            CollectionAssert.AreEqual(
                enElDeVerdad[casoDeVerdad].ToArray(),
                enElFalso[casoFalso].ToArray(),
                "Los dos tienen que decir los mismos campos anotados, y en el mismo orden.");

            Assert.DoesNotContain(
                "templo_nombre",
                enElDeVerdad[casoDeVerdad],
                "De templo_nombre no se anoto nada: no puede constar como anotado.");

            Assert.IsFalse(
                enElDeVerdad.ContainsKey(999999),
                "Un registro sin ninguna fila no sale en el diccionario, no sale con lista vacia.");
            Assert.AreEqual(
                enElDeVerdad.ContainsKey(999999),
                enElFalso.ContainsKey(999999),
                "Y los dos tienen que estar de acuerdo en eso.");
        }
    }

    /// <summary>
    /// Anota en un caso las cinco clases de fila que existen, una por columna.
    /// </summary>
    /// <remarks>
    /// La sexta clase —un campo con valor y SIN fila— es <c>templo_nombre</c>, que a
    /// proposito no se anota: no anotarlo es lo que la monta.
    /// <para>
    /// La firmada se deja fuera porque firmar necesita un companero en cada base y eso ya lo
    /// cubre <c>FirmarYRetirarLaFirmaHacenLoMismoEnLosDos</c>; aqui interesa que el filtro
    /// trate igual las que se pueden sembrar por <c>Anotar</c>.
    /// </para>
    /// </remarks>
    private static void SembrarLasSeisClases(IProcedencia procedencia, long casoId)
    {
        Anotar(procedencia, casoId, "numero_caso", 0.99, tachon: false, ausente: false);
        Anotar(procedencia, casoId, "unidad_numero", 0.42, tachon: false, ausente: false);
        Anotar(procedencia, casoId, "unidad_nombre", null, tachon: false, ausente: false);
        Anotar(procedencia, casoId, "fecha_viaje", 0.95, tachon: true, ausente: false);
    }

    /// <summary>Anota una fila de procedencia con las cuatro cosas que decide el filtro.</summary>
    private static void Anotar(
        IProcedencia procedencia, long casoId, string campo, double? confianza, bool tachon, bool ausente)
        => procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = TablaDeProcedencia.Casos,
            RegistroId = casoId,
            Campo = campo,
            Origen = OrigenDeCampo.Ocr,
            Confianza = confianza,
            AnuladoPorTachon = tachon,
            AusenteEnElPapel = ausente,
        });

    /// <summary>
    /// Retrata las filas de un caso en texto comparable, sin el id, que difiere entre bases.
    /// </summary>
    private static string[] Retratar(IReadOnlyList<ProcedenciaDeCampo> filas, long casoId)
        => [.. filas
            .Where(fila => fila.Tabla == TablaDeProcedencia.Casos && fila.RegistroId == casoId)
            .Select(fila =>
                $"{fila.Campo}|conf={fila.Confianza?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "nula"}"
                + $"|tachon={fila.AnuladoPorTachon}|ausente={fila.AusenteEnElPapel}|firmado={fila.Verificado}")];

    /// <summary>Cuantas asignaciones vivas tiene el caso en cada uno; tienen que ser las mismas.</summary>
    private static void CompararLasVivas(
        Juego deVerdad, JuegoFalso falso, long casoDeVerdad, long casoFalso, string cuando)
    {
        var vivasDeVerdad = deVerdad.Asignaciones.VivasDeCaso(casoDeVerdad).Count;
        var vivasFalsas = falso.Asignaciones.VivasDeCaso(casoFalso).Count;

        Assert.AreEqual(
            vivasDeVerdad,
            vivasFalsas,
            $"{cuando}, el de verdad deja {vivasDeVerdad} asignacion(es) viva(s) y el falso {vivasFalsas}.");
    }

    private static void CompararQuien(
        long? deVerdad, long? falso, long quienDeVerdad, long quienFalso, string columna)
    {
        Assert.AreEqual(
            deVerdad is null, falso is null, $"`{columna}`: uno de los dos la deja nula y el otro no.");

        if (deVerdad is not null)
        {
            Assert.AreEqual(quienDeVerdad, deVerdad.Value, $"`{columna}` del de verdad apunta a otro.");
            Assert.AreEqual(quienFalso, falso!.Value, $"`{columna}` del falso apunta a otro.");
        }
    }

    private static void CompararSiLaHay(string? deVerdad, string? falso, string columna)
        => Assert.AreEqual(
            string.IsNullOrEmpty(deVerdad),
            string.IsNullOrEmpty(falso),
            $"`{columna}`: uno de los dos la escribe y el otro la deja vacia.");

    /// <summary>Compara el veredicto y la gravedad de los avisos, no su redaccion.</summary>
    private static void CompararLasDosEscrituras(
        ResultadoDeEscritura deVerdad, ResultadoDeEscritura falso, string queSeHizo)
    {
        Assert.AreEqual(
            deVerdad.SeEscribio,
            falso.SeEscribio,
            $"Al {queSeHizo}, uno de los dos escribio y el otro no.");

        CollectionAssert.AreEqual(
            deVerdad.Avisos.Select(a => a.Gravedad).OrderBy(g => g).ToArray(),
            falso.Avisos.Select(a => a.Gravedad).OrderBy(g => g).ToArray(),
            $"Al {queSeHizo}, los avisos no pesan lo mismo en los dos.\n" +
            $"  de verdad: {string.Join(" | ", deVerdad.Avisos.Select(a => a.Gravedad + ": " + a.Linea))}\n" +
            $"  falso    : {string.Join(" | ", falso.Avisos.Select(a => a.Gravedad + ": " + a.Linea))}");
    }
}
