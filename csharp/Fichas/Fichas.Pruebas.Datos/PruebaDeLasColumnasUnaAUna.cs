using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Las 111 columnas por su NOMBRE y en su ORDEN, no solo contadas.
/// </summary>
/// <remarks>
/// <para>
/// Un recuento no ve una columna mal escrita ni dos cambiadas de sitio: <c>propuesto_en</c>
/// por <c>propuesta_en</c> suma igual y rompe cada consulta que la nombre. Esta clase
/// enumera las dos listas enteras y las compara elemento a elemento —lista blanca, no
/// busqueda de lo que falta—, que es la unica forma de que el detector no tenga falsos
/// negativos.
/// </para>
/// <para>
/// ⚠️ Las listas se copian de la medicion del Python de este mismo arbol, hecha el
/// 2026-09-04 con <c>PRAGMA table_info</c> sobre una base construida por
/// <c>datos.esquema.aplicar_esquema</c>. NO se copian del DDL de C# que prueban.
/// El orden importa: es el orden en que el motor las devuelve, y en las tablas que
/// crecieron por <c>ALTER TABLE</c> las columnas nuevas van pegadas al final.
/// </para>
/// <para>
/// ⚠️ A las 104 del Python se les suman las CUATRO de la migracion 18 y las TRES de la
/// 19, que el Python
/// no tiene: <c>casos.motivo_no_completa</c> y <c>casos.motivo_del_companero</c>,
/// <c>companeros.rol</c> y <c>companeros.categoria</c>. Van al final de su tabla porque
/// la 18 es un <c>ADD COLUMN</c> y no una reconstruccion.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLasColumnasUnaAUna
{
    /// <summary>Las nueve tablas con sus columnas por nombre y en orden, copiadas de la medición del Python más las siete de la 18 y la 19.</summary>
    private static readonly (string Tabla, string[] Columnas)[] EsquemaEsperado =
    [
        ("asignaciones", [
            "id", "caso_id", "companero_id", "asignado_en", "activa", "desactivada_en"]),

        ("casos", [
            "id", "numero_caso", "unidad_numero", "fecha_viaje", "captura_manual",
            "archivado", "fecha_archivado", "ruta_pdf", "creado_en",
            "estado_recomendacion", "pagina_pdf", "unidad_nombre", "templo_nombre",
            "duplicado_de", "estado_marcado_por", "estado_marcado_en",
            "estado_marcado_origen", "estado_del_companero", "estado_del_companero_por",
            "estado_del_companero_en", "motivo_no_completa", "motivo_del_companero"]),

        ("companeros", [
            "id", "nombre", "activo", "desactivado_en", "creado_en", "rol", "categoria"]),

        ("contactos", [
            "id", "caso_id", "fecha", "medio", "con_quien", "resultado", "anulado",
            "motivo_anulacion", "anulado_en", "registrado_en", "contactado_por",
            "respondio"]),

        ("documentos_ilegibles", [
            "id", "ruta_pdf", "pagina_pdf", "motivo", "detalle", "lineas_leidas",
            "caso_id", "registrado_en"]),

        ("filas_descartadas", [
            "id", "companero_id", "ruta_excel", "fila_excel", "numero_caso", "mrn",
            "nombre", "motivo", "registrado_en"]),

        ("personas", [
            "id", "caso_id", "mrn", "nombre", "fila_formulario", "ord_recibir_propias",
            "ord_observar_sellamiento", "ord_traductor", "ord_investidura",
            "ord_sellamiento_esposos", "ord_sellamiento_hijo_padres", "pagina_pdf",
            "estado_propuesto", "nota_companero", "propuesto_por", "propuesto_en",
            "motivo_no_viajo", "pudo_viajar", "paso_preparacion", "paso_informacion",
            "paso_cita_del_templo", "paso_acciones_requeridas", "paso_entrevistas",
            "paso_listo_para_el_templo", "llamo_al_lider", "pasos_por", "pasos_en",
            "pasos_origen"]),

        ("procedencia_campo", [
            "id", "tabla", "registro_id", "campo", "origen", "confianza", "valor_ocr",
            "verificado", "verificado_por", "verificado_en", "banda_x0", "banda_y0",
            "banda_x1", "banda_y1", "anulado_por_tachon", "ausente_en_el_papel"]),

        ("version_esquema", [
            "version", "aplicada_en", "descripcion"]),
    ];

    /// <summary>Vigila que cada tabla de una base nueva devuelve exactamente la lista esperada, elemento a elemento.</summary>
    [TestMethod]
    public void CadaTablaTieneSusColumnasConSuNombreYEnSuOrden()
    {
        using var baseDePrueba = BaseDePrueba.Nueva();

        foreach (var (tabla, esperadas) in EsquemaEsperado)
        {
            var reales = LeerColumnasDe(baseDePrueba.Conexion, tabla);

            CollectionAssert.AreEqual(
                esperadas,
                reales,
                $"Las columnas de '{tabla}' no son las que midio el Python.\n" +
                $"  Se esperaban ({esperadas.Length}): {string.Join(", ", esperadas)}\n" +
                $"  Se encontraron ({reales.Length}): {string.Join(", ", reales)}");
        }
    }

    /// <summary>El denominador de la prueba de arriba: la lista de esta clase no se ha recortado.</summary>
    [TestMethod]
    public void LaListaEsperadaSumaCientoOnceColumnas()
    {
        // El denominador de la prueba de arriba, comprobado aparte: si alguien recorta
        // una lista, esta prueba lo dice en vez de dejar que la comparacion pase con
        // menos columnas de las que hay.
        Assert.AreEqual(
            111,
            EsquemaEsperado.Sum(t => t.Columnas.Length),
            "La lista esperada de esta clase ya no suma 111 columnas: las 104 que midio " +
            "el Python, mas las cuatro de la migracion 18 y las tres de la 19.");
    }

    /// <summary>Vigila en el DDL real que <c>casos.numero_caso</c> no lleva GLOB ni UNIQUE (decisión del dueño, 2026-09-04).</summary>
    [TestMethod]
    public void ElNumeroDeCasoNoLlevaElCheckDeCuatroLetrasYCuatroDigitos()
    {
        // La UNICA desviacion deliberada respecto del Python (DECISIONES.md 2026-09-04,
        // «El CHECK del numero de caso»). Se comprueba leyendo el DDL que el motor
        // guarda, y no confiando en que la constante diga lo que creemos.
        using var baseDePrueba = BaseDePrueba.Nueva();

        var ddl = LeerDdlDe(baseDePrueba.Conexion, "casos");

        Assert.IsFalse(
            ddl.Contains("numero_caso GLOB", StringComparison.Ordinal),
            "`casos.numero_caso` conserva un CHECK de forma, y el dueno decidio que no " +
            "lo lleve: se guarda lo que venga y quien avisa es la pantalla.\n" + ddl);

        Assert.IsFalse(
            ddl.Contains("numero_caso          TEXT    NOT NULL UNIQUE", StringComparison.Ordinal),
            "`casos.numero_caso` sigue siendo UNIQUE, y la version 12 se lo quito.");
    }

    /// <summary>El control de las dos anteriores: el esquema sigue teniendo 51 CHECK y quitar dos no se llevó el resto.</summary>
    [TestMethod]
    public void LosDemasCheckDeCoherenciaSiSiguenPuestos()
    {
        // El control de las dos pruebas de arriba: quitar DOS no puede haberse llevado el
        // resto por delante. Medido sobre el Python el 2026-09-04: 48 apariciones de
        // la palabra CHECK en el DDL de las nueve tablas; sin la de `numero_caso`
        // (migracion 16) y sin la de `mrn` (migracion 17), quedan 46; con los CUATRO que
        // trae la migracion 18 —los dos motivos, el rol y la categoria—, 50; y con el UNO
        // de la 19 —`pasos_en`, que exige que quien contesto y cuando vayan juntos—, 51.
        //
        // ⚠️ Este numero NO estaba en la lista de sitios que la FASE C10 daba por
        // corregir: son cinco alli y este es el sexto del arbol de pruebas.
        using var baseDePrueba = BaseDePrueba.Nueva();

        var total = EsquemaEsperado.Sum(
            t => ContarApariciones(LeerDdlDe(baseDePrueba.Conexion, t.Tabla), "CHECK"));

        Assert.AreEqual(
            51,
            total,
            "El numero de CHECK del esquema no es 51. Son los 48 que mide el Python " +
            "menos los DOS que el dueno mando quitar —`numero_caso` y `mrn`—, mas los " +
            "CUATRO de la migracion 18 y el UNO de la 19.");
    }

    /// <summary>Vigila en el DDL real que <c>personas.mrn</c> no lleva GLOB (migración 17).</summary>
    [TestMethod]
    public void LaCedulaNoLlevaNingunCheckDeForma()
    {
        // ~~La version 15 admitia letra en el ultimo caracter.~~ Sustituido el 2026-09-04
        // por la migracion 17: el CHECK entero se va. QA midio que con el de la 15 puesto
        // `123` y `''` devolvian «CHECK constraint failed», y lo que un CHECK rechaza aqui
        // no es un dato suelto: es la fila de una PERSONA que iba a viajar. Se guarda como
        // venga y quien avisa es la pantalla (ReglasDeFormato.RevisarMrn).
        using var baseDePrueba = BaseDePrueba.Nueva();

        var ddl = LeerDdlDe(baseDePrueba.Conexion, "personas");

        Assert.IsFalse(
            ddl.Contains("mrn GLOB", StringComparison.Ordinal),
            "`personas.mrn` conserva un CHECK de forma, y el dueno decidio que no lo " +
            "lleve: una cedula mal leida se guarda y se senala.\n" + ddl);
    }

    /// <summary>Los nombres de las columnas en el orden del motor, con el identificador entrecomillado al modo de SQLite.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="tabla">El nombre de la tabla; sale de las listas literales de esta clase, nunca de fuera.</param>
    private static string[] LeerColumnasDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = $"PRAGMA table_info(\"{tabla.Replace("\"", "\"\"", StringComparison.Ordinal)}\")";
        using var lector = orden.ExecuteReader();

        var columnas = new List<string>();
        while (lector.Read())
        {
            columnas.Add(lector.GetString(1));
        }

        return [.. columnas];
    }

    /// <summary>El <c>CREATE TABLE</c> guardado en <c>sqlite_master</c>, o vacío si no existe.</summary>
    /// <param name="conexion">La conexión de la base de prueba.</param>
    /// <param name="tabla">El nombre de la tabla; sale de las listas literales de esta clase, nunca de fuera.</param>
    private static string LeerDdlDe(SqliteConnection conexion, string tabla)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $tabla";
        orden.Parameters.AddWithValue("$tabla", tabla);
        return Convert.ToString(orden.ExecuteScalar()) ?? string.Empty;
    }

    /// <summary>Cuántas veces aparece la aguja en el texto, sin solaparse.</summary>
    /// <param name="texto">Dónde se busca.</param>
    /// <param name="aguja">Qué se busca.</param>
    private static int ContarApariciones(string texto, string aguja)
    {
        var apariciones = 0;
        var desde = 0;
        while ((desde = texto.IndexOf(aguja, desde, StringComparison.Ordinal)) >= 0)
        {
            apariciones++;
            desde += aguja.Length;
        }

        return apariciones;
    }
}
