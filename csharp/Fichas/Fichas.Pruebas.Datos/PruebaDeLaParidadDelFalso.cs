using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;
using Fichas.Datos.Repositorios;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Que el repositorio FALSO se comporte igual que el de verdad. Uno a uno.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Por que existe esta clase.</b> El 2026-09-05 se midio que
/// <c>RepositorioDeCasosFalso.MarcarEstado</c> escribia <c>EstadoDelCompanero</c>,
/// <c>_por</c> y <c>_en</c>, y que <c>RepositorioDeCasos</c> —el de verdad— no las
/// escribia por ningun camino. Las pruebas que corren sobre el falso pasaban en verde
/// sobre un hueco real: en la base del dueno esas tres columnas llevaban desde la
/// migracion 14 vacias, y nadie lo vio porque el doble mentia a favor.
/// </para>
/// <para>
/// <b>Un falso que no se comporta como el original no prueba nada.</b> Lo que esta clase
/// hace es correr LA MISMA operacion contra los dos y comparar el resultado campo por
/// campo, en vez de leer los dos archivos y opinar que se parecen. Cada divergencia que
/// aparezca aqui es un sitio donde una prueba del arbol puede estar en verde por nada.
/// </para>
/// <para>
/// Lo que NO se compara, y se dice: lo que depende del reloj —<c>EstadoMarcadoEn</c>,
/// <c>CreadoEn</c>— porque el falso usa <see cref="RelojFijo"/> y el de verdad la hora de
/// la maquina. Se comprueba que los dos la escriban o los dos la dejen nula, que es la
/// parte que importa.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaParidadDelFalso
{
    /// <summary>La fecha del <see cref="RelojFijo"/> del falso y de las marcas sembradas a mano en el de verdad.</summary>
    private const string DiaDeLasPruebas = "2026-09-05";

    // ═════════════════════════════ Casos ═════════════════════════════

    /// <summary>
    /// Dado un caso marcado a mano, cuando se marca por los dos caminos, entonces los dos
    /// escriben el estado vigente Y NINGUNO toca lo que dijo la hoja del companero.
    /// </summary>
    /// <remarks>
    /// La divergencia que se cierra: el falso escribia las dos mitades y el de verdad
    /// solo una. Escribir las dos es lo INCORRECTO —la migracion 14 las desdoblo para que
    /// la correccion de Miguel no pisara el nombre del companero—, asi que quien se
    /// corrige es el falso.
    /// </remarks>
    [TestMethod]
    public void MarcarAManoEscribeLoMismoEnElFalsoQueEnElDeVerdad()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (quienDeVerdad, quienFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Miguel");

            deVerdad.Casos.MarcarEstado(
                casoDeVerdad, EstadoDeRecomendacion.Completa, quienDeVerdad, "a mano en la pantalla Revisar");
            falso.Casos.MarcarEstado(
                casoFalso, EstadoDeRecomendacion.Completa, quienFalso, "a mano en la pantalla Revisar");

            CompararLosDosCasos(
                deVerdad.Casos.Obtener(casoDeVerdad)!,
                falso.Casos.Obtener(casoFalso)!,
                quienDeVerdad,
                quienFalso);
        }
    }

    /// <summary>
    /// Dada la hoja que devuelve un companero, cuando se aplica por los dos caminos,
    /// entonces los dos escriben las DOS mitades: la vigente y el registro de lo que dijo.
    /// </summary>
    [TestMethod]
    public void LaHojaDelCompaneroEscribeLoMismoEnElFalsoQueEnElDeVerdad()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");
            const string LaHoja = @"C:\paquetes\por_verificar.xlsx";

            deVerdad.Casos.MarcarEstadoDelCompanero(
                casoDeVerdad,
                EstadoDeRecomendacion.NoCompleta,
                MotivoDeNoCompletar.NoSePudoComunicar,
                sandyDeVerdad,
                LaHoja);
            falso.Casos.MarcarEstadoDelCompanero(
                casoFalso,
                EstadoDeRecomendacion.NoCompleta,
                MotivoDeNoCompletar.NoSePudoComunicar,
                sandyFalso,
                LaHoja);

            var leidoDeVerdad = deVerdad.Casos.Obtener(casoDeVerdad)!;
            CompararLosDosCasos(leidoDeVerdad, falso.Casos.Obtener(casoFalso)!, sandyDeVerdad, sandyFalso);

            // Y que de verdad escribio algo, no que los dos lo dejaran igual de vacio.
            Assert.AreEqual("no_completa", leidoDeVerdad.EstadoDelCompanero);
            Assert.AreEqual(sandyDeVerdad, leidoDeVerdad.EstadoDelCompaneroPor);
            Assert.AreEqual(MotivoDeNoCompletar.NoSePudoComunicar, leidoDeVerdad.MotivoQueDijoElCompanero);
        }
    }

    /// <summary>
    /// Dado un caso, cuando se archiva y se desarchiva por los dos caminos, entonces los
    /// dos lo dejan igual, incluida la fecha que desaparece al desarchivar.
    /// </summary>
    [TestMethod]
    public void ArchivarYDesarchivarDejanLoMismoEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);

            deVerdad.Casos.Archivar(casoDeVerdad, true, DiaDeLasPruebas);
            falso.Casos.Archivar(casoFalso, true, DiaDeLasPruebas);
            CompararLosDosCasos(deVerdad.Casos.Obtener(casoDeVerdad)!, falso.Casos.Obtener(casoFalso)!, 0, 0);

            deVerdad.Casos.Archivar(casoDeVerdad, false, string.Empty);
            falso.Casos.Archivar(casoFalso, false, string.Empty);

            var desarchivadoDeVerdad = deVerdad.Casos.Obtener(casoDeVerdad)!;
            CompararLosDosCasos(desarchivadoDeVerdad, falso.Casos.Obtener(casoFalso)!, 0, 0);
            Assert.IsFalse(desarchivadoDeVerdad.Archivado);
            Assert.IsNull(desarchivadoDeVerdad.FechaArchivado);
        }
    }

    /// <summary>
    /// Dado un caso que no existe, cuando se le marca el estado en los dos, entonces los
    /// dos lo dicen y ninguno finge que escribio.
    /// </summary>
    [TestMethod]
    public void MarcarUnCasoQueNoExisteFallaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var enElDeVerdad = deVerdad.Casos.MarcarEstado(
                9999, EstadoDeRecomendacion.Completa, 1, "a mano en la pantalla Revisar");
            var enElFalso = falso.Casos.MarcarEstado(
                9999, EstadoDeRecomendacion.Completa, 1, "a mano en la pantalla Revisar");

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "marcar un caso que no existe");
        }
    }

    // ═══════════════════════════ Companeros ═══════════════════════════

    /// <summary>
    /// Dado un companero sin nombre, cuando se guarda en los dos, entonces los dos lo
    /// rechazan: una firma sin nombre no dice quien firmo.
    /// </summary>
    /// <remarks>
    /// La divergencia que se cierra: el de verdad devolvia
    /// <c>NoSeEscribio</c> con un aviso de problema y el falso lo GUARDABA con una
    /// advertencia. Una pantalla probada contra el falso creeria que el alta salio bien.
    /// </remarks>
    [TestMethod]
    public void UnCompaneroSinNombreSeRechazaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var enElDeVerdad = deVerdad.Companeros.Guardar(new Companero { Nombre = "   " });
            var enElFalso = falso.Companeros.Guardar(new Companero { Nombre = "   " });

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "guardar un companero sin nombre");
            Assert.IsFalse(enElDeVerdad.SeEscribio, "El de verdad dejo entrar un companero sin nombre.");
            Assert.IsEmpty(
                falso.Companeros.Activos(),
                "El falso guardo un companero sin nombre y el de verdad no.");
        }
    }

    /// <summary>
    /// Dado un companero que se desactiva sin fecha, cuando se hace en los dos, entonces
    /// los dos lo rechazan en vez de inventarse la fecha.
    /// </summary>
    /// <remarks>
    /// La divergencia que se cierra: el falso ponia <c>Reloj.Ahora()</c> cuando la fecha
    /// venia vacia. Inventar el dato que falta es justo lo que la regla permanente 1
    /// prohibe, y ademas tapaba que quien llama se hubiera olvidado de pasarla.
    /// </remarks>
    [TestMethod]
    public void DesactivarSinFechaSeRechazaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");

            var enElDeVerdad = deVerdad.Companeros.Desactivar(sandyDeVerdad, string.Empty);
            var enElFalso = falso.Companeros.Desactivar(sandyFalso, string.Empty);

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "desactivar sin fecha");
            Assert.IsTrue(
                deVerdad.Companeros.Obtener(sandyDeVerdad)!.Activo,
                "El de verdad desactivo sin fecha.");
            Assert.IsTrue(
                falso.Companeros.Obtener(sandyFalso)!.Activo,
                "El falso desactivo sin fecha y el de verdad no.");
        }
    }

    /// <summary>
    /// Dado un companero con casos vivos, cuando se desactiva en los dos, entonces los dos
    /// lo desactivan Y los dos avisan de cuantos casos se quedan colgando.
    /// </summary>
    /// <remarks>
    /// La divergencia que se cierra, y esta vez el que estaba corto era el DE VERDAD: el
    /// falso avisaba de las asignaciones vivas y el de verdad las desactivaba en silencio.
    /// El aviso es del dueno —«las asignaciones NO se retiran solas: nada se borra sin
    /// preguntar»— asi que se sube al de verdad en vez de bajarlo del falso.
    /// </remarks>
    [TestMethod]
    public void DesactivarAQuienLlevaCasosAvisaIgualEnLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var (sandyDeVerdad, sandyFalso) = SembrarUnCompaneroEnLosDos(deVerdad, falso, "Sandy");
            var (casoDeVerdad, casoFalso) = SembrarUnCasoEnLosDos(deVerdad, falso);

            deVerdad.Asignaciones.Asignar(casoDeVerdad, sandyDeVerdad, DiaDeLasPruebas);
            falso.Asignaciones.Asignar(casoFalso, sandyFalso, DiaDeLasPruebas);

            var enElDeVerdad = deVerdad.Companeros.Desactivar(sandyDeVerdad, DiaDeLasPruebas);
            var enElFalso = falso.Companeros.Desactivar(sandyFalso, DiaDeLasPruebas);

            CompararLasDosEscrituras(enElDeVerdad, enElFalso, "desactivar a quien lleva casos");
            Assert.IsTrue(enElDeVerdad.SeEscribio, "El de verdad no desactivo.");
            Assert.IsNotEmpty(
                enElDeVerdad.Avisos,
                "El de verdad desactivo en silencio a quien todavia lleva un caso.");
            Console.WriteLine("   de verdad: {0}", enElDeVerdad.Avisos[0].Linea);
            Console.WriteLine("   falso    : {0}", enElFalso.Avisos[0].Linea);
        }
    }

    /// <summary>
    /// Dado un rol y una categoria, cuando se guardan en los dos, entonces los dos los
    /// devuelven igual: el vocabulario nuevo llega entero a los dos lados.
    /// </summary>
    [TestMethod]
    public void ElRolYLaCategoriaVuelvenIgualDeLosDos()
    {
        var (deVerdad, falso, cerrar) = MontarLosDos();
        using (cerrar)
        {
            var quienEs = new Companero
            {
                Nombre = "Ana",
                Rol = RolDeCompanero.Gerente,
                Categoria = 3,
                CreadoEn = DiaDeLasPruebas + " 12:00:00",
            };

            var idDeVerdad = deVerdad.Companeros.Guardar(quienEs).Id;
            var idFalso = falso.Companeros.Guardar(quienEs).Id;

            var leidoDeVerdad = deVerdad.Companeros.Obtener(idDeVerdad)!;
            var leidoFalso = falso.Companeros.Obtener(idFalso)!;

            Assert.AreEqual(leidoDeVerdad.Rol, leidoFalso.Rol, "El rol no vuelve igual de los dos.");
            Assert.AreEqual(
                leidoDeVerdad.Categoria, leidoFalso.Categoria, "La categoria no vuelve igual de los dos.");
            Assert.AreEqual(RolDeCompanero.Gerente, leidoDeVerdad.Rol);
            Assert.AreEqual(3, leidoDeVerdad.Categoria);
        }
    }

    // ─────────────────────────── el andamio ───────────────────────────

    /// <summary>Los dos juegos de repositorios sobre los que corre cada prueba.</summary>
    private sealed record Juego(
        RepositorioDeCasos Casos,
        RepositorioDeCompaneros Companeros,
        RepositorioDeAsignaciones Asignaciones);

    /// <summary>Los repositorios falsos, con la misma forma que el juego de verdad.</summary>
    private sealed record JuegoFalso(
        RepositorioDeCasosFalso Casos,
        RepositorioDeCompanerosFalso Companeros,
        RepositorioDeAsignacionesFalso Asignaciones);

    /// <summary>Monta los dos juegos y devuelve lo que hay que cerrar al final.</summary>
    private static (Juego DeVerdad, JuegoFalso Falso, IDisposable Cerrar) MontarLosDos()
    {
        var baseDePrueba = BaseDePrueba.Nueva();
        var almacen = new AlmacenFalso(new RelojFijo(DiaDeLasPruebas), semilla: 1);

        var deVerdad = new Juego(
            new RepositorioDeCasos(baseDePrueba.Conexion),
            new RepositorioDeCompaneros(baseDePrueba.Conexion),
            new RepositorioDeAsignaciones(baseDePrueba.Conexion));

        var falso = new JuegoFalso(
            new RepositorioDeCasosFalso(almacen),
            new RepositorioDeCompanerosFalso(almacen),
            new RepositorioDeAsignacionesFalso(almacen));

        return (deVerdad, falso, baseDePrueba);
    }

    /// <summary>El mismo caso guardado en los dos repositorios; los ids no coinciden porque cada almacén numera aparte.</summary>
    /// <param name="deVerdad">Los repositorios sobre SQLite.</param>
    /// <param name="falso">Los repositorios sobre el almacén en memoria.</param>
    /// <returns>El id que dio cada uno.</returns>
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

    /// <summary>El mismo compañero guardado en los dos repositorios.</summary>
    /// <param name="deVerdad">Los repositorios sobre SQLite.</param>
    /// <param name="falso">Los repositorios sobre el almacén en memoria.</param>
    /// <param name="nombre">El nombre del compañero.</param>
    /// <returns>El id que dio cada uno.</returns>
    private static (long DeVerdad, long Falso) SembrarUnCompaneroEnLosDos(
        Juego deVerdad, JuegoFalso falso, string nombre)
    {
        var quienEs = new Companero { Nombre = nombre, CreadoEn = DiaDeLasPruebas + " 10:00:00" };

        return (deVerdad.Companeros.Guardar(quienEs).Id, falso.Companeros.Guardar(quienEs).Id);
    }

    /// <summary>
    /// Compara los dos casos campo por campo, saltando lo que no puede coincidir: el id
    /// —los dos almacenes numeran aparte—, las marcas de tiempo y los ids de companero.
    /// </summary>
    /// <remarks>
    /// De las marcas de tiempo se compara si HAY o NO hay, que es lo unico que dice si
    /// una de las dos implementaciones se olvido de escribirla.
    /// </remarks>
    private static void CompararLosDosCasos(
        Caso deVerdad, Caso falso, long quienDeVerdad, long quienFalso)
    {
        Assert.AreEqual(deVerdad.NumeroCaso, falso.NumeroCaso, "numero_caso");
        Assert.AreEqual(deVerdad.FechaViaje, falso.FechaViaje, "fecha_viaje");
        Assert.AreEqual(deVerdad.Archivado, falso.Archivado, "archivado");
        Assert.AreEqual(deVerdad.FechaArchivado, falso.FechaArchivado, "fecha_archivado");
        Assert.AreEqual(deVerdad.EstadoRecomendacion, falso.EstadoRecomendacion, "estado_recomendacion");
        Assert.AreEqual(deVerdad.EstadoMarcadoOrigen, falso.EstadoMarcadoOrigen, "estado_marcado_origen");
        Assert.AreEqual(deVerdad.EstadoDelCompanero, falso.EstadoDelCompanero, "estado_del_companero");
        Assert.AreEqual(deVerdad.MotivoNoCompleta, falso.MotivoNoCompleta, "motivo_no_completa");
        Assert.AreEqual(deVerdad.MotivoDelCompanero, falso.MotivoDelCompanero, "motivo_del_companero");

        CompararQuien(deVerdad.EstadoMarcadoPor, falso.EstadoMarcadoPor, quienDeVerdad, quienFalso, "estado_marcado_por");
        CompararQuien(
            deVerdad.EstadoDelCompaneroPor, falso.EstadoDelCompaneroPor, quienDeVerdad, quienFalso, "estado_del_companero_por");

        CompararSiLaHay(deVerdad.EstadoMarcadoEn, falso.EstadoMarcadoEn, "estado_marcado_en");
        CompararSiLaHay(deVerdad.EstadoDelCompaneroEn, falso.EstadoDelCompaneroEn, "estado_del_companero_en");
    }

    /// <summary>Los ids no coinciden entre los dos almacenes: se compara a QUIEN apuntan.</summary>
    private static void CompararQuien(
        long? deVerdad, long? falso, long quienDeVerdad, long quienFalso, string columna)
    {
        Assert.AreEqual(
            deVerdad is null,
            falso is null,
            $"`{columna}`: uno de los dos la deja nula y el otro no.");

        if (deVerdad is not null)
        {
            Assert.AreEqual(quienDeVerdad, deVerdad.Value, $"`{columna}` del de verdad apunta a otro.");
            Assert.AreEqual(quienFalso, falso!.Value, $"`{columna}` del falso apunta a otro.");
        }
    }

    /// <summary>De una marca de tiempo solo se compara si la hay: los relojes son distintos.</summary>
    private static void CompararSiLaHay(string? deVerdad, string? falso, string columna)
        => Assert.AreEqual(
            string.IsNullOrEmpty(deVerdad),
            string.IsNullOrEmpty(falso),
            $"`{columna}`: uno de los dos la escribe y el otro la deja vacia.");

    /// <summary>Compara el veredicto y la gravedad de los avisos, no su redaccion.</summary>
    /// <remarks>
    /// El texto NO se compara a proposito: dos mensajes distintos que digan lo mismo no
    /// son un defecto. Lo que si lo es: que uno escriba y el otro no, o que uno avise de
    /// un problema donde el otro solo advierte.
    /// </remarks>
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
