using System.Diagnostics;
using System.Globalization;
using Fichas.Datos.Esquema;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// La migracion 18: el vocabulario del trabajo nuevo (FASE C10 de PENDIENTES.md).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Las pruebas salen de los criterios C10-1 a C10-7, no del codigo que prueban.</b>
/// Ninguna asercion lee una constante de <c>Fichas.Datos</c> para compararla consigo
/// misma: los valores de las listas cerradas se escriben aqui a mano, copiados de
/// <c>ADR-0005</c> §3.3 y §4.3 y de las palabras del dueno recogidas en DECISIONES.md
/// el 2026-09-05.
/// </para>
/// <para>
/// ⚠️ <b>Son CUATRO columnas y no tres, y la desviacion esta declarada.</b> El ADR-0005
/// §4.3 propone solo <c>companeros.rol</c> con lista cerrada de tres nombres; el dueno,
/// en la entrada «Categorias de agentes, y la escalera del que no consiguio hablar»,
/// pide ademas un NUMERO por companero cuyos peldanos pone el: <i>«los agentes categoria
/// 1 no pudieron comunicarse... debo pasarlo a los agentes de categoria 2; si los de
/// categoria 2 no pudieron, a los de categoria 3»</i>. Una lista cerrada de tres nombres
/// no puede tener un cuarto peldano sin otra migracion, asi que <c>rol</c> y
/// <c>categoria</c> son dos ejes distintos y hacen falta los dos: <c>rol</c> contesta
/// «quien es el administrador» (criterio C16-3) y <c>categoria</c> contesta «a quien le
/// toca la vuelta siguiente» (criterio C15-2).
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaMigracion18
{
    /// <summary>La version que esta fase engancha, escrita a mano y no derivada del catalogo.</summary>
    private const int VersionDeEstaFase = 18;

    /// <summary>Los tres motivos, en clave y no en prosa (ADR-0005 §3.3).</summary>
    private static readonly string[] MotivosQueSeAdmiten =
        ["no_se_pudo_comunicar", "el_lider_no_lo_hizo", "otra_razon"];

    /// <summary>Los tres roles de la lista cerrada (ADR-0005 §4.3).</summary>
    private static readonly string[] RolesQueSeAdmiten =
        ["companero", "gerente", "administrador"];

    // ═══════════════════════ C10-1: engancha al final ═══════════════════════

    /// <summary>
    /// Dado el catalogo de migraciones, cuando se busca la 18, entonces esta enganchada,
    /// dice que cambio, y el aplicador llega hasta ella o mas alla.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Cambiada el 2026-09-05, al enganchar la 19.</b> Antes exigia que la 18 fuese
    /// LA ULTIMA del catalogo, y eso convertia esta prueba en una puerta cerrada: cada
    /// migracion nueva la ponia roja sin que nada estuviera mal. Lo que la fase C10 queria
    /// asegurar —que la 18 este en la lista, que diga que cambio y que
    /// <c>VersionAlDia</c> se derive de la lista en vez de estar escrito a mano— se
    /// comprueba igual sin esa exigencia. Que la ULTIMA sea la que toca lo comprueba la
    /// prueba de la migracion que se enganche cada vez.
    /// </remarks>
    [TestMethod]
    public void LaDieciochoEstaEnganchadaYLaVersionAlDiaSubeSola()
    {
        var laDieciocho = CatalogoDeMigraciones.Todas.SingleOrDefault(m => m.Version == VersionDeEstaFase);

        Assert.IsNotNull(laDieciocho, "La 18 no esta en el catalogo, o esta declarada dos veces.");

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(laDieciocho.Descripcion),
            "La 18 no dice que cambio.");

        Assert.AreEqual(
            CatalogoDeMigraciones.Todas[^1].Version,
            AplicadorDeEsquema.VersionAlDia,
            "`VersionAlDia` no sale de la ultima del catalogo: se derivaria de un numero " +
            "escrito a mano, que es lo que el catalogo prohibe.");

        Assert.IsGreaterThanOrEqualTo(
            VersionDeEstaFase,
            AplicadorDeEsquema.VersionAlDia,
            "El aplicador no llega ni a la 18, asi que la migracion no se aplica nunca.");
    }

    // ═══════════════ C10-2: cuatro ADD COLUMN, cero reconstrucciones ═══════════════

    /// <summary>
    /// Dada una base parada en la 17, cuando se aplica la 18, entonces las columnas que ya
    /// tenia siguen con su nombre Y EN SU ORDEN, y las nuevas van pegadas al final.
    /// </summary>
    /// <remarks>
    /// Es la huella que deja un <c>ADD COLUMN</c> y que una reconstruccion no deja: quien
    /// rehace una tabla la escribe entera y puede cambiar el orden sin que nadie lo note.
    /// No PRUEBA que no haya reconstruccion —eso lo dice el codigo de la migracion, que
    /// solo tiene ALTER— pero si la detectaria si alguien la metiera despues.
    /// </remarks>
    [TestMethod]
    public void LasColumnasDeAntesSiguenEnSuSitioYLasNuevasVanAlFinal()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);

        var casosAntes = ColumnasDe(baseDePrueba.Conexion, "casos");
        var companerosAntes = ColumnasDe(baseDePrueba.Conexion, "companeros");

        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);

        var casosDespues = ColumnasDe(baseDePrueba.Conexion, "casos");
        var companerosDespues = ColumnasDe(baseDePrueba.Conexion, "companeros");

        CollectionAssert.AreEqual(
            casosAntes,
            casosDespues.Take(casosAntes.Length).ToArray(),
            "La 18 movio de sitio alguna columna de `casos`.");
        CollectionAssert.AreEqual(
            new[] { "motivo_no_completa", "motivo_del_companero" },
            casosDespues.Skip(casosAntes.Length).ToArray(),
            "`casos` no acabo con los dos motivos pegados al final.");

        CollectionAssert.AreEqual(
            companerosAntes,
            companerosDespues.Take(companerosAntes.Length).ToArray(),
            "La 18 movio de sitio alguna columna de `companeros`.");
        CollectionAssert.AreEqual(
            new[] { "rol", "categoria" },
            companerosDespues.Skip(companerosAntes.Length).ToArray(),
            "`companeros` no acabo con el rol y la categoria pegados al final.");
    }

    // ═══════════ C10-3: la base de antes sube sin perder nada y con su defecto ═══════════

    /// <summary>
    /// Dado un companero dado de alta ANTES de la 18, cuando la base migra, entonces queda
    /// con <c>rol = 'companero'</c> y <c>categoria = 1</c>, que es el primer peldano.
    /// </summary>
    [TestMethod]
    public void UnCompaneroDeAntesDeLaDieciochoQuedaEnElPrimerPeldano()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);
        var sandy = PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");

        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);

        Assert.AreEqual(
            "companero",
            Leer(baseDePrueba.Conexion, "SELECT rol FROM companeros WHERE id = " + sandy),
            "Sandy no quedo con el rol por defecto al migrar.");
        Assert.AreEqual(
            1L,
            Convert.ToInt64(
                Leer(baseDePrueba.Conexion, "SELECT categoria FROM companeros WHERE id = " + sandy),
                CultureInfo.InvariantCulture),
            "Sandy no quedo en la categoria 1 al migrar.");
    }

    /// <summary>
    /// Dada una base parada en la 17 con filas dentro, cuando se migra al dia, entonces no
    /// se pierde ni una, y el conteo se ESCRIBE para que se pueda pegar en la entrega.
    /// </summary>
    [TestMethod]
    public void UnaBaseParadaEnLaDiecisieteLlegaAlDiaSinPerderFilas()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);

        PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");
        var caso = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");
        PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-385A", "Elena");
        PruebaDeMigraciones.InsertarPersona(baseDePrueba.Conexion, caso, "055-1111-3853", "Julia");

        var antes = ContarPorTabla(baseDePrueba.Conexion);
        var version = AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        var despues = ContarPorTabla(baseDePrueba.Conexion);

        Console.WriteLine("== Filas de una base parada en la 17: version 17 -> {0} ==", version);
        Console.WriteLine("   {0,-24} {1,6} {2,8}", "TABLA", "ANTES", "DESPUES");
        foreach (var (tabla, filas) in antes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Console.WriteLine("   {0,-24} {1,6} {2,8}", tabla, filas, despues[tabla]);
        }

        Console.WriteLine("   {0,-24} {1,6} {2,8}", "TOTAL", antes.Values.Sum(), despues.Values.Sum());

        // Pasa POR la 18 y sigue hasta donde llegue el catalogo: exigir que se pare en la
        // 18 pondria esta prueba roja el dia que se enganche la siguiente, sin que nada
        // este mal. Lo que se mide es que ninguna de las que aplique pierda una fila.
        Assert.IsGreaterThanOrEqualTo(VersionDeEstaFase, version, "No llego ni a la 18.");
        foreach (var (tabla, filas) in antes)
        {
            Assert.AreEqual(filas, despues[tabla], $"La tabla '{tabla}' cambio de numero de filas.");
        }
    }

    // ═══════ C10-4: 3 000 casos, sin reconstruir ninguna tabla, integridad ═══════

    /// <summary>
    /// Dada una base de 3 000 casos y 16 500 personas parada en la 17, cuando se aplica la
    /// 18, entonces no reconstruye ni una tabla, no pierde ni un caso y la integridad sigue
    /// en <c>ok</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Esta prueba media el RELOJ hasta el 2026-09-05 y se ponia roja sola.</b> El
    /// tope era 500 ms; corriendo la suite entera media <b>519,86 ms</b> en esta maquina y
    /// fallaba, y corriendo sola pasaba. Un umbral de tiempo no mide la migracion: mide lo
    /// cargada que esta la maquina, y una prueba que depende de eso deja de ser una red —se
    /// aprende a ignorarla, y el dia que se ponga roja por un motivo de verdad tampoco se
    /// mirara—.
    /// </para>
    /// <para>
    /// <b>Lo que el tope queria decir de verdad, dicho sin cronometro.</b> La 18 es rapida
    /// porque son cuatro <c>ALTER TABLE ADD COLUMN</c> y ninguna reconstruccion, y eso es
    /// una PROPIEDAD comprobable: reconstruir una tabla es crearla de nuevo, copiar,
    /// borrar y renombrar, y la tabla nueva estrena <c>rootpage</c>. Que el <c>rootpage</c>
    /// no cambie prueba lo mismo, y no cambia de resultado segun quien mas este corriendo.
    /// La documentacion oficial lo respalda
    /// (<see href="https://sqlite.org/lang_altertable.html"/>, consultada el 2026-09-05):
    /// el coste de <c>ADD COLUMN</c> «is independent of the amount of data in the table».
    /// </para>
    /// <para>
    /// El tiempo <b>se sigue imprimiendo</b>, porque sirve para mirarlo; lo que ya no hace
    /// es decidir si la prueba pasa.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void SobreTresMilCasosLaDieciochoNoReconstruyeNingunaTablaYNoRompeNada()
    {
        using var baseDePrueba = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDePrueba.Conexion, VersionDeEstaFase - 1);
        var personas = SembrarTresMilCasos(baseDePrueba.Conexion);

        var raicesAntes = PaginasRaiz(baseDePrueba.Conexion);

        var reloj = Stopwatch.StartNew();
        AplicadorDeEsquema.Aplicar(baseDePrueba.Conexion);
        reloj.Stop();

        var raicesDespues = PaginasRaiz(baseDePrueba.Conexion);
        var casos = Convert.ToInt64(
            Leer(baseDePrueba.Conexion, "SELECT COUNT(*) FROM casos"), CultureInfo.InvariantCulture);
        var integridad = Convert.ToString(
            Leer(baseDePrueba.Conexion, "PRAGMA integrity_check"), CultureInfo.InvariantCulture);

        var reconstruidas = raicesAntes
            .Where(par => raicesDespues.TryGetValue(par.Key, out var ahora) && ahora != par.Value)
            .Select(par => par.Key)
            .ToList();

        Console.WriteLine(
            "== Migracion 18 sobre {0} casos y {1} personas: {2:F2} ms (informativo) · " +
            "tablas reconstruidas {3} · integrity_check = {4} ==",
            casos,
            personas,
            reloj.Elapsed.TotalMilliseconds,
            reconstruidas.Count,
            integridad);

        Assert.AreEqual(3000L, casos, "Se perdieron casos al migrar los 3 000.");
        Assert.AreEqual("ok", integridad, "`pragma integrity_check` no devolvio ok.");
        Assert.IsEmpty(
            reconstruidas,
            $"La 18 reconstruyo {string.Join(", ", reconstruidas)}, y son cuatro " +
            "ADD COLUMN: ninguna tabla tiene por que estrenar pagina raiz.");
    }

    /// <summary>La pagina raiz que el motor le tiene asignada a cada tabla de la base.</summary>
    /// <remarks>
    /// Es el dato con el que se comprueba que una migracion anadio columnas en vez de
    /// rehacer la tabla: una tabla reconstruida es otra tabla y estrena pagina.
    /// </remarks>
    private static Dictionary<string, long> PaginasRaiz(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "SELECT name, rootpage FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        using var lector = orden.ExecuteReader();

        var raices = new Dictionary<string, long>(StringComparer.Ordinal);
        while (lector.Read()) raices[lector.GetString(0)] = lector.GetInt64(1);
        return raices;
    }

    // ═════════════════ C10-5: la lista cerrada rechaza lo que no esta ═════════════════

    /// <summary>
    /// Dados los tres motivos de la lista, cuando se escriben en las dos columnas de motivo,
    /// entonces entran; y el nulo tambien, que es «todavia nadie ha dicho por que».
    /// </summary>
    [TestMethod]
    public void LosTresMotivosQueElDuenoNombroSeEscribenYSeLeen()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        foreach (var motivo in MotivosQueSeAdmiten)
        {
            var caso = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");
            Ejecutar(
                baseDePrueba.Conexion,
                "UPDATE casos SET estado_recomendacion = 'no_completa', " +
                "motivo_no_completa = $motivo, motivo_del_companero = $motivo WHERE id = " + caso,
                ("$motivo", motivo));

            Assert.AreEqual(
                motivo,
                Leer(baseDePrueba.Conexion, "SELECT motivo_no_completa FROM casos WHERE id = " + caso),
                $"El motivo «{motivo}» no volvio como se escribio.");
            Assert.AreEqual(
                motivo,
                Leer(baseDePrueba.Conexion, "SELECT motivo_del_companero FROM casos WHERE id = " + caso),
                $"El motivo del companero «{motivo}» no volvio como se escribio.");
        }

        var sinMotivo = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "BALC2609");
        Assert.IsNull(
            Leer(baseDePrueba.Conexion, "SELECT motivo_no_completa FROM casos WHERE id = " + sinMotivo),
            "Un caso nuevo nace con motivo, y deberia nacer sin ninguno.");
    }

    /// <summary>
    /// Dado un motivo que no esta en la lista, cuando se intenta escribir, entonces el
    /// <c>CHECK</c> del motor lo rechaza y la fila se queda como estaba.
    /// </summary>
    [TestMethod]
    public void UnMotivoFueraDeLaListaLoRechazaElCheck()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var caso = PruebaDeMigraciones.InsertarCaso(baseDePrueba.Conexion, "CASP2609");

        foreach (var columna in new[] { "motivo_no_completa", "motivo_del_companero" })
        {
            var fallo = Assert.ThrowsExactly<SqliteException>(
                () => Ejecutar(
                    baseDePrueba.Conexion,
                    $"UPDATE casos SET {columna} = 'se me olvido' WHERE id = " + caso),
                $"La columna `{columna}` acepto un motivo que no esta en la lista.");

            Console.WriteLine("   {0}: {1}", columna, fallo.Message);
            StringAssert.Contains(fallo.Message, "CHECK constraint failed");
        }
    }

    /// <summary>
    /// Dado un rol que no esta en la lista, cuando se intenta escribir, entonces el
    /// <c>CHECK</c> lo rechaza; y los tres de la lista entran.
    /// </summary>
    [TestMethod]
    public void LosTresRolesEntranYCualquierOtroLoRechazaElCheck()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companero = PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");

        foreach (var rol in RolesQueSeAdmiten)
        {
            Ejecutar(
                baseDePrueba.Conexion,
                "UPDATE companeros SET rol = $rol WHERE id = " + companero,
                ("$rol", rol));
            Assert.AreEqual(
                rol,
                Leer(baseDePrueba.Conexion, "SELECT rol FROM companeros WHERE id = " + companero),
                $"El rol «{rol}» no volvio como se escribio.");
        }

        var fallo = Assert.ThrowsExactly<SqliteException>(
            () => Ejecutar(
                baseDePrueba.Conexion,
                "UPDATE companeros SET rol = 'jefe' WHERE id = " + companero),
            "`companeros.rol` acepto un rol que no esta en la lista.");

        Console.WriteLine("   rol: {0}", fallo.Message);
        StringAssert.Contains(fallo.Message, "CHECK constraint failed");
    }

    /// <summary>
    /// Dada la escalera de categorias del dueno, cuando se pone un peldano cualquiera,
    /// entonces entra; y el cero o un negativo no, porque no hay peldano antes del primero.
    /// </summary>
    /// <remarks>
    /// El tope de arriba NO se pone: sus palabras son <i>«asi puedes agregarle a los
    /// gerentes categoria y a los agentes categorias»</i>, o sea que el numero de peldanos
    /// lo pone el, no el programa. Por eso se comprueba que el 7 entra igual que el 1.
    /// </remarks>
    [TestMethod]
    public void LaEscaleraDeCategoriasNoTieneTopeArribaYSiEmpiezaEnUno()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();
        var companero = PruebaDeMigraciones.InsertarCompanero(baseDePrueba.Conexion, "Sandy");

        foreach (var peldano in new[] { 1, 2, 3, 7 })
        {
            Ejecutar(
                baseDePrueba.Conexion,
                "UPDATE companeros SET categoria = $categoria WHERE id = " + companero,
                ("$categoria", peldano));
            Assert.AreEqual(
                (long)peldano,
                Convert.ToInt64(
                    Leer(baseDePrueba.Conexion, "SELECT categoria FROM companeros WHERE id = " + companero),
                    CultureInfo.InvariantCulture),
                $"La categoria {peldano} no volvio como se escribio.");
        }

        var fallo = Assert.ThrowsExactly<SqliteException>(
            () => Ejecutar(
                baseDePrueba.Conexion,
                "UPDATE companeros SET categoria = 0 WHERE id = " + companero),
            "`companeros.categoria` acepto un peldano anterior al primero.");

        Console.WriteLine("   categoria: {0}", fallo.Message);
        StringAssert.Contains(fallo.Message, "CHECK constraint failed");
    }

    // ═══════════ Convergencia: venga de donde venga, el mismo DDL ═══════════

    /// <summary>
    /// Dada una base nueva y otra que se quedo en la 17, cuando las dos llegan al dia,
    /// entonces <c>casos</c> y <c>companeros</c> tienen EL MISMO DDL, letra por letra.
    /// </summary>
    [TestMethod]
    public void UnaBaseNuevaYUnaMigradaAcabanConElMismoDdl()
    {
        using var nueva = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.Aplicar(nueva.Conexion);

        using var migrada = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(migrada.Conexion, VersionDeEstaFase - 1);
        PruebaDeMigraciones.InsertarCompanero(migrada.Conexion, "Sandy");
        AplicadorDeEsquema.Aplicar(migrada.Conexion);

        foreach (var tabla in new[] { "casos", "companeros" })
        {
            var deLaNueva = DdlDe(nueva.Conexion, tabla);
            var deLaMigrada = DdlDe(migrada.Conexion, tabla);

            Console.WriteLine("== DDL de `{0}` en una base NUEVA ==", tabla);
            Console.WriteLine(deLaNueva);
            Console.WriteLine("== DDL de `{0}` en una base MIGRADA desde la 17 ==", tabla);
            Console.WriteLine(deLaMigrada);

            Assert.AreEqual(
                deLaNueva,
                deLaMigrada,
                $"`{tabla}` no tiene el mismo esquema segun de donde venga la base.");
        }
    }

    // ─────────────────────────── utiles de esta clase ───────────────────────────

    /// <summary>Siembra 3 000 casos y de 1 a 10 personas cada uno, como la base del ADR-0005.</summary>
    private static long SembrarTresMilCasos(SqliteConnection conexion)
    {
        using var transaccion = conexion.BeginTransaction();

        using var elCaso = conexion.CreateCommand();
        elCaso.CommandText =
            "INSERT INTO casos (numero_caso, fecha_viaje, creado_en) " +
            "VALUES ($numero, $viaje, '2026-09-05 10:00:00'); SELECT last_insert_rowid()";
        var numero = elCaso.Parameters.Add("$numero", SqliteType.Text);
        var viaje = elCaso.Parameters.Add("$viaje", SqliteType.Text);

        using var laPersona = conexion.CreateCommand();
        laPersona.CommandText =
            "INSERT INTO personas (caso_id, mrn, nombre) VALUES ($caso, $mrn, $nombre)";
        var deQueCaso = laPersona.Parameters.Add("$caso", SqliteType.Integer);
        var mrn = laPersona.Parameters.Add("$mrn", SqliteType.Text);
        var nombre = laPersona.Parameters.Add("$nombre", SqliteType.Text);

        long personas = 0;
        for (var i = 0; i < 3000; i++)
        {
            numero.Value = "CASP" + (2609 + (i % 3)).ToString(CultureInfo.InvariantCulture);
            viaje.Value = "2026-09-" + (1 + (i % 30)).ToString("D2", CultureInfo.InvariantCulture);
            var casoId = Convert.ToInt64(elCaso.ExecuteScalar(), CultureInfo.InvariantCulture);

            deQueCaso.Value = casoId;
            for (var p = 0; p <= i % 10; p++)
            {
                mrn.Value = "055-1111-" + (1000 + p).ToString(CultureInfo.InvariantCulture);
                nombre.Value = "Persona " + personas.ToString(CultureInfo.InvariantCulture);
                laPersona.ExecuteNonQuery();
                personas++;
            }
        }

        transaccion.Commit();
        return personas;
    }

    /// <summary>Los nombres de las columnas de la tabla, en el orden en que el motor los devuelve.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="tabla">El nombre de la tabla; sale de las listas literales de esta clase, nunca de fuera.</param>
    private static string[] ColumnasDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = $"PRAGMA table_info(\"{tabla}\")";
        using var lector = orden.ExecuteReader();

        var columnas = new List<string>();
        while (lector.Read())
        {
            columnas.Add(lector.GetString(1));
        }

        return [.. columnas];
    }

    /// <summary>El <c>CREATE TABLE</c> que el motor guarda en <c>sqlite_master</c>, o una frase si la tabla no existe.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="tabla">El nombre de la tabla; sale de las listas literales de esta clase, nunca de fuera.</param>
    private static string DdlDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $tabla";
        orden.Parameters.AddWithValue("$tabla", tabla);
        return orden.ExecuteScalar() as string ?? $"(no hay ninguna tabla '{tabla}')";
    }

    /// <summary>Filas por tabla, sin <c>version_esquema</c>, que crece al migrar y no sirve para comparar antes y después.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    private static Dictionary<string, long> ContarPorTabla(SqliteConnection conexion)
    {
        var conteo = new Dictionary<string, long>(StringComparer.Ordinal);

        using (var orden = conexion.CreateCommand())
        {
            orden.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'table' " +
                "AND name NOT LIKE 'sqlite_%' AND name <> 'version_esquema' ORDER BY name";
            using var lector = orden.ExecuteReader();
            while (lector.Read())
            {
                conteo[lector.GetString(0)] = 0;
            }
        }

        foreach (var tabla in conteo.Keys.ToList())
        {
            conteo[tabla] = Convert.ToInt64(
                Leer(conexion, $"SELECT COUNT(*) FROM \"{tabla}\""), CultureInfo.InvariantCulture);
        }

        return conteo;
    }

    /// <summary>El primer valor de la primera fila, o nulo.</summary>
    private static object? Leer(SqliteConnection conexion, string consulta)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = consulta;
        var valor = orden.ExecuteScalar();
        return valor is DBNull ? null : valor;
    }

    /// <summary>Ejecuta una instruccion con sus parametros; los valores NUNCA se concatenan.</summary>
    private static void Ejecutar(
        SqliteConnection conexion, string instruccion, params (string Nombre, object Valor)[] parametros)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = instruccion;
        foreach (var (nombre, valor) in parametros)
        {
            orden.Parameters.AddWithValue(nombre, valor);
        }

        orden.ExecuteNonQuery();
    }
}
