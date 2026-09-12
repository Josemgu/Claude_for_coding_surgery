using Fichas.Contratos.Modelos;

namespace Fichas.Datos.Falso;

/// <summary>
/// De dónde salió cada valor de la base inventada: la procedencia de cada campo de cada caso
/// y de cada persona.
/// </summary>
/// <remarks>
/// <para>⛔ <b>Hasta el 2026-09-06 esto no existía y el generador no escribía NI UNA fila de
/// procedencia</b> —<c>grep Procedencias GeneradorFalso.cs</c> daba cero coincidencias—. Con
/// eso, el modo <c>--falso</c> no servía para medir nada que dependa de de dónde salió un
/// valor: ni el estado de un campo en Corrección, ni «listo para asignar», ni la cola de
/// Completar. Toda la base inventada se veía como si el lector no hubiera leído nada.</para>
///
/// <para><b>La mezcla no se elige a ojo.</b> Se copia lo medido el 2026-09-05 sobre los siete
/// escaneos reales del dueño, más la regla de los manuscritos que ya estaba escrita en
/// <c>DECISIONES.md</c>. Cada número de abajo dice de dónde sale.</para>
///
/// <para>⚠️ Esto NO son datos de nadie: los valores ya venían inventados de
/// <see cref="GeneradorFalso"/> y aquí solo se dice con qué confianza se «leyeron».</para>
/// </remarks>
public static class ProcedenciaInventada
{
    /// <summary>Las cinco columnas del caso que llevan procedencia, en el orden de la pantalla.</summary>
    /// <remarks>
    /// ⚠️ Se escriben aquí como texto y NO se importan de <c>Fichas.Lectura.Extraccion</c>
    /// porque este proyecto solo referencia <c>Fichas.Contratos</c>, que es justo lo que le
    /// permite sustituirse por <c>Fichas.Datos</c> cambiando una línea. Que no se separen de
    /// las de verdad lo vigila <c>PruebasDeLaProcedenciaInventada</c>.
    /// </remarks>
    public static IReadOnlyList<string> CamposDelCaso { get; } =
        ["numero_caso", "unidad_numero", "unidad_nombre", "fecha_viaje", "templo_nombre"];

    /// <summary>Las dos columnas de texto de cada persona; las casillas no llevan procedencia.</summary>
    public static IReadOnlyList<string> CamposDeLaPersona { get; } = ["mrn", "nombre"];

    /// <summary>Las dos columnas que en los escaneos de verdad vienen de una anotación del PDF.</summary>
    /// <remarks>
    /// Medido el 2026-09-05 sobre los siete escaneos reales: <c>numero_caso</c> y
    /// <c>fecha_viaje</c> salieron de un <c>/FreeText</c> del propio PDF con confianza 1,00 en
    /// 7 de 7; <c>unidad_numero</c> y <c>unidad_nombre</c>, del OCR con 0,99.
    /// </remarks>
    private static readonly HashSet<string> CamposQueVienenDeUnaAnotacion =
        new(["numero_caso", "fecha_viaje"], StringComparer.Ordinal);

    /// <summary>Uno de cada cuatro campos vacíos ya lo cerró Miguel diciendo que no está en el papel.</summary>
    /// <remarks>
    /// Sin ninguno así, la exención de <c>AusenteEnElPapel</c> no se ejerce nunca corriendo
    /// con <c>--falso</c>, y es justo la que decide si un hueco cerrado vuelve a la cola.
    /// </remarks>
    private const int DeCadaCienVaciosQueMiguelYaCerro = 25;

    /// <summary>
    /// Siete de cada diez campos de un formulario escrito a mano vuelven por debajo del umbral.
    /// </summary>
    /// <remarks>
    /// Es la regla de <c>DECISIONES.md</c> puesta del derecho: «si más del 60% de los campos
    /// vuelven con confianza bajo 0.6, el formulario probablemente está escrito a mano».
    /// <see cref="GeneradorFalso"/> ya marcaba <c>CapturaManual</c> en el 5% de los casos, y
    /// hasta hoy esa marca no se notaba en ningún sitio.
    /// </remarks>
    private const int DeCadaCienCamposManuscritosMalLeidos = 70;

    /// <summary>Sembra la procedencia de todos los campos de todos los casos y personas.</summary>
    /// <remarks>
    /// ⛔ <b>Ni una fila nace firmada</b> (regla permanente 5): <c>verificado</c> se queda en
    /// falso en las 29 784, porque firmar es de Miguel y ninguna base inventada puede decir
    /// que él dio algo por bueno.
    /// <para>
    /// Se escribe directo en el almacén y no por <c>RepositorioDeProcedenciaFalso.Anotar</c> a
    /// propósito: <c>Anotar</c> tiene que buscar si la fila ya existe, y sembrar 29 784 filas
    /// pasando por ahí haría el trabajo dos veces.
    /// </para>
    /// </remarks>
    /// <param name="almacen">Donde se escribe.</param>
    /// <param name="sorteo">El sorteo ya empezado; la misma semilla da la misma procedencia.</param>
    public static void Sembrar(AlmacenFalso almacen, SorteoDeterminista sorteo)
    {
        ArgumentNullException.ThrowIfNull(almacen);
        ArgumentNullException.ThrowIfNull(sorteo);

        foreach (var caso in almacen.Casos.Values.OrderBy(uno => uno.Id))
        {
            string?[] valores =
                [caso.NumeroCaso, caso.UnidadNumero, caso.UnidadNombre, caso.FechaViaje, caso.TemploNombre];
            SembrarLosDe(almacen, sorteo, TablaDeProcedencia.Casos, caso.Id,
                         CamposDelCaso, valores, caso.CapturaManual);
        }

        foreach (var persona in almacen.Personas.Values.OrderBy(una => una.Id))
        {
            var manual = almacen.Casos.TryGetValue(persona.CasoId, out var suCaso) && suCaso.CapturaManual;
            string?[] valores = [persona.Mrn, persona.Nombre];
            SembrarLosDe(almacen, sorteo, TablaDeProcedencia.Personas, persona.Id,
                         CamposDeLaPersona, valores, manual);
        }
    }

    /// <summary>Siembra los campos de UN registro, cada uno con el valor que ya tenía.</summary>
    /// <param name="almacen">Dónde se escriben las filas.</param>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="campos">Los nombres de columna, en orden.</param>
    /// <param name="valores">El valor de cada columna, en el mismo orden; nulo o en blanco es un campo vacío.</param>
    /// <param name="capturaManual">Si el documento está escrito a mano, que baja las confianzas.</param>
    private static void SembrarLosDe(
        AlmacenFalso almacen,
        SorteoDeterminista sorteo,
        TablaDeProcedencia tabla,
        long registroId,
        IReadOnlyList<string> campos,
        IReadOnlyList<string?> valores,
        bool capturaManual)
    {
        for (var i = 0; i < campos.Count; i++)
        {
            var fila = Componer(sorteo, tabla, registroId, campos[i], valores[i], capturaManual);
            if (fila is null) continue;

            var id = almacen.SiguienteId();
            almacen.Procedencias[id] = fila with { Id = id };
        }
    }

    /// <summary>
    /// La fila que le toca a un campo, o nula cuando le toca <b>no tener ninguna</b>.
    /// </summary>
    /// <remarks>
    /// El reparto, de arriba abajo y con lo que cuesta cada rama:
    /// <list type="table">
    ///   <item><term>El campo está vacío</term><description>Origen «vacío» y sin confianza, que
    ///   es lo que deja «no se pudo leer». Uno de cada cuatro va además marcado como que no
    ///   está en el papel: Miguel ya lo miró y cerró el hueco.</description></item>
    ///   <item><term>Documento escrito a mano</term><description>7 de cada 10 de sus campos por
    ///   debajo de 0,6.</description></item>
    ///   <item><term>1 de cada 100</term><description><b>Sin fila ninguna.</b> Pasa de verdad:
    ///   hasta el 2026-09-05 la importación no escribía fila para <c>templo_nombre</c>, y eran
    ///   0 de 7 casos. Un campo sin fila no dice de dónde salió su valor y encima no se puede
    ///   firmar, porque <c>Firmar</c> es un <c>UPDATE</c> y cambia cero filas.</description></item>
    ///   <item><term>1 de cada 100</term><description>Tachado en el papel, con el valor
    ///   debajo.</description></item>
    ///   <item><term>2 de cada 100</term><description>Confianza baja con la forma bien. Es el
    ///   caso que abrió todo esto: un MRN que la máquina leyó mal y que por casualidad sale con
    ///   la forma correcta.</description></item>
    ///   <item><term>El resto</term><description>Anotación del PDF con 1,00 en las dos columnas
    ///   que así vienen; OCR entre 0,90 y 0,99 en las demás.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    /// <param name="tabla">Si el registro es un caso o una persona.</param>
    /// <param name="registroId">El id del caso o de la persona.</param>
    /// <param name="campo">El nombre de la columna.</param>
    /// <param name="valor">Lo que ya tiene la columna; nulo o en blanco da origen vacío.</param>
    /// <param name="capturaManual">Si el documento está escrito a mano.</param>
    /// <returns>La fila sin id —el que llama se lo pone—, o nulo cuando al campo no le toca ninguna.</returns>
    private static ProcedenciaDeCampo? Componer(
        SorteoDeterminista sorteo,
        TablaDeProcedencia tabla,
        long registroId,
        string campo,
        string? valor,
        bool capturaManual)
    {
        var fila = new ProcedenciaDeCampo { Tabla = tabla, RegistroId = registroId, Campo = campo };

        // Un campo sin valor no tiene confianza que contar: o Miguel ya dijo que el papel no lo
        // trae, o sigue siendo un hueco de los que hay que ir a buscar.
        if (string.IsNullOrWhiteSpace(valor))
        {
            return fila with
            {
                Origen = OrigenDeCampo.Vacio,
                AusenteEnElPapel = sorteo.ConProbabilidad(DeCadaCienVaciosQueMiguelYaCerro),
            };
        }

        if (capturaManual && sorteo.ConProbabilidad(DeCadaCienCamposManuscritosMalLeidos))
            return fila with { Origen = OrigenDeCampo.Ocr, Confianza = ConfianzaBaja(sorteo) };

        var dado = sorteo.Hasta(100);
        if (dado < 1) return null;
        if (dado < 2)
            return fila with { Origen = OrigenDeCampo.Ocr, Confianza = ConfianzaAlta(sorteo), AnuladoPorTachon = true };
        if (dado < 4)
            return fila with { Origen = OrigenDeCampo.Ocr, Confianza = ConfianzaBaja(sorteo) };

        return CamposQueVienenDeUnaAnotacion.Contains(campo)
            ? fila with { Origen = OrigenDeCampo.Anotacion, Confianza = 1.0 }
            : fila with { Origen = OrigenDeCampo.Ocr, Confianza = ConfianzaAlta(sorteo) };
    }

    /// <summary>De 0,30 a 0,59: por debajo del umbral, o sea un dato que hay que mirar.</summary>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    private static double ConfianzaBaja(SorteoDeterminista sorteo) => sorteo.Entre(30, 60) / 100.0;

    /// <summary>De 0,90 a 0,99, que es lo que dieron los siete escaneos de verdad.</summary>
    /// <param name="sorteo">El sorteo ya empezado.</param>
    private static double ConfianzaAlta(SorteoDeterminista sorteo) => sorteo.Entre(90, 100) / 100.0;
}
