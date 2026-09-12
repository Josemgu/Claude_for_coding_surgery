using System.Globalization;
using ClosedXML.Excel;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Paquetes;

/// <summary>Una columna del espejo: como se llama en la base y de que clase es.</summary>
/// <param name="Nombre">El nombre de la columna en la tabla, tal cual.</param>
/// <param name="Clase">Como entra el valor en la celda.</param>
public sealed record ColumnaDelEspejo(string Nombre, ClaseDeColumna Clase);

/// <summary>Lo que quedo del intento de escribir el espejo.</summary>
/// <param name="Ruta">Donde se dejo, o donde se intento dejar.</param>
/// <param name="Escrito">Si llego a escribirse.</param>
/// <param name="Avisos">Lo que hay que decir; vacio cuando salio bien.</param>
public sealed record ResultadoDelEspejo(string Ruta, bool Escrito, IReadOnlyList<Aviso> Avisos);

/// <summary>
/// El Excel espejo de la base: una hoja por tabla, una columna por columna, sin calcular nada.
/// </summary>
/// <remarks>
/// Las columnas salen una a una del esquema. No se anade ninguna que la tabla no tenga ni se
/// calcula ninguna: un espejo que calcula deja de ser un espejo y pasa a ser un reporte, y
/// entonces hay dos verdades.
/// <para>
/// Sin celdas combinadas, sin color y sin negrita: no se escribe ni una sola instruccion de
/// estilo aparte del formato de numero. Es una base de datos, no un reporte, y un color no es
/// un dato. La fila 1 se distingue porque esta congelada y porque lleva el autofiltro, que son
/// dos marcas estructurales y no decorativas.
/// </para>
/// <para>
/// ⚠️ Los booleanos y los tres estados se escriben como 0, 1 o vacio y NO se traducen a «Sí» y
/// «No». Es deliberado y es lo mismo que hace el programa en Python: una casilla vacia
/// significa «nadie lo ha mirado», que no es lo mismo que «lo miro y dijo que no», y esa
/// diferencia se pierde en cuanto se traduce. Un espejo existe para poder comprobar la base sin
/// abrirla; si distingue peor que la base, no sirve.
/// </para>
/// <para>
/// ⚠️ <b>Lo que este espejo NO cubre hoy, y por que.</b> El espejo del programa en Python
/// vuelca CINCO tablas: casos, personas, asignaciones, contactos y documentos ilegibles. Aqui
/// se le pasan las filas ya leidas, asi que las cinco caben; pero <c>contactos</c> esta
/// declarada PROVISIONAL en el esquema y no hay puerto en Fichas.Contratos que devuelva todas
/// las filas de una tabla de golpe. Quien llame decide que listas le pasa: una hoja sin filas
/// sale con su cabecera, que dice «esta tabla existe y esta vacia», y eso NO es lo mismo que
/// no tener la hoja.
/// </para>
/// </remarks>
public static class Espejo
{
    /// <summary>El formato de número de Excel que fuerza texto: es lo que salva el cero de delante de un MRN.</summary>
    private const string FormatoDeTexto = "@";

    /// <summary>Cómo pinta Excel una fecha sola; en su sintaxis, con <c>mm</c> en minúscula.</summary>
    private const string FormatoDeFechaEnExcel = "yyyy-mm-dd";

    /// <summary>Cómo pinta Excel una marca de tiempo; en su sintaxis, no en la de .NET.</summary>
    private const string FormatoDeMarcaDeTiempoEnExcel = "yyyy-mm-dd hh:mm:ss";

    /// <summary>Cómo viene una fecha de la base (ISO-8601), en la sintaxis de <c>DateTime.TryParseExact</c>.</summary>
    private const string FormatoDeFecha = "yyyy-MM-dd";

    /// <summary>Cómo viene una marca de tiempo de la base, en la sintaxis de <c>DateTime.TryParseExact</c>.</summary>
    private const string FormatoDeMarcaDeTiempo = "yyyy-MM-dd HH:mm:ss";

    /// <summary>La fila 1 es la cabecera; los datos empiezan en la 2, en todas las hojas.</summary>
    private const int PrimeraFilaDeDatos = 2;

    /// <summary>Las columnas de <c>casos</c>, en el orden del esquema.</summary>
    /// <remarks><c>unidad_numero</c> va en texto: es un numero que puede empezar por cero y un entero se lo comeria.</remarks>
    public static IReadOnlyList<ColumnaDelEspejo> ColumnasDeCasos { get; } =
    [
        new("id", ClaseDeColumna.Crudo),
        new("numero_caso", ClaseDeColumna.Crudo),
        new("unidad_numero", ClaseDeColumna.Texto),
        new("unidad_nombre", ClaseDeColumna.Crudo),
        new("templo_nombre", ClaseDeColumna.Crudo),
        new("fecha_viaje", ClaseDeColumna.Temporal),
        new("pagina_pdf", ClaseDeColumna.Crudo),
        new("captura_manual", ClaseDeColumna.Crudo),
        new("archivado", ClaseDeColumna.Crudo),
        new("fecha_archivado", ClaseDeColumna.Temporal),
        new("ruta_pdf", ClaseDeColumna.Crudo),
        new("creado_en", ClaseDeColumna.Temporal),
        new("estado_recomendacion", ClaseDeColumna.Crudo),
        new("duplicado_de", ClaseDeColumna.Crudo),
        new("estado_marcado_por", ClaseDeColumna.Crudo),
        new("estado_marcado_en", ClaseDeColumna.Temporal),
        new("estado_marcado_origen", ClaseDeColumna.Crudo),
        new("estado_del_companero", ClaseDeColumna.Crudo),
        new("estado_del_companero_por", ClaseDeColumna.Crudo),
        new("estado_del_companero_en", ClaseDeColumna.Temporal),
        new("motivo_no_completa", ClaseDeColumna.Crudo),
        new("motivo_del_companero", ClaseDeColumna.Crudo),
    ];

    /// <summary>
    /// Las columnas de <c>personas</c>, en el orden del esquema.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Las tres ultimas —las de la migracion 19— NO salen de <see cref="Persona"/>.</b>
    /// <c>Fichas.Contratos/Modelos</c> esta congelado, asi que <c>pasos_por</c>,
    /// <c>pasos_en</c> y <c>pasos_origen</c> viven en <see cref="FirmaDeLosPasos"/> y llegan
    /// aqui aparte, en un diccionario por numero interno de persona. Es la misma forma en
    /// que las devuelve <c>IPersonas.FirmasDeLosPasosDelCaso</c>.
    /// <para>
    /// Y no son <c>propuesto_por</c> / <c>propuesto_en</c>: aquellas son de quien propuso el
    /// ESTADO desde su Excel, estas de quien contesto las seis preguntas del sistema del
    /// lider. Pueden ser dos personas distintas en la misma fila.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ColumnaDelEspejo> ColumnasDePersonas { get; } =
    [
        new("id", ClaseDeColumna.Crudo),
        new("caso_id", ClaseDeColumna.Crudo),
        new("mrn", ClaseDeColumna.Texto),
        new("nombre", ClaseDeColumna.Crudo),
        new("fila_formulario", ClaseDeColumna.Crudo),
        new("pagina_pdf", ClaseDeColumna.Crudo),
        new("ord_recibir_propias", ClaseDeColumna.Crudo),
        new("ord_observar_sellamiento", ClaseDeColumna.Crudo),
        new("ord_traductor", ClaseDeColumna.Crudo),
        new("ord_investidura", ClaseDeColumna.Crudo),
        new("ord_sellamiento_esposos", ClaseDeColumna.Crudo),
        new("ord_sellamiento_hijo_padres", ClaseDeColumna.Crudo),
        new("estado_propuesto", ClaseDeColumna.Crudo),
        new("nota_companero", ClaseDeColumna.Crudo),
        new("propuesto_por", ClaseDeColumna.Crudo),
        new("propuesto_en", ClaseDeColumna.Temporal),
        new("pudo_viajar", ClaseDeColumna.Crudo),
        new("motivo_no_viajo", ClaseDeColumna.Crudo),
        new("paso_preparacion", ClaseDeColumna.Crudo),
        new("paso_informacion", ClaseDeColumna.Crudo),
        new("paso_cita_del_templo", ClaseDeColumna.Crudo),
        new("paso_acciones_requeridas", ClaseDeColumna.Crudo),
        new("paso_entrevistas", ClaseDeColumna.Crudo),
        new("paso_listo_para_el_templo", ClaseDeColumna.Crudo),
        new("llamo_al_lider", ClaseDeColumna.Crudo),
        new("pasos_por", ClaseDeColumna.Crudo),
        new("pasos_en", ClaseDeColumna.Temporal),
        new("pasos_origen", ClaseDeColumna.Crudo),
    ];

    /// <summary>Las columnas de <c>asignaciones</c>, en el orden del esquema.</summary>
    public static IReadOnlyList<ColumnaDelEspejo> ColumnasDeAsignaciones { get; } =
    [
        new("id", ClaseDeColumna.Crudo),
        new("caso_id", ClaseDeColumna.Crudo),
        new("companero_id", ClaseDeColumna.Crudo),
        new("asignado_en", ClaseDeColumna.Temporal),
        new("activa", ClaseDeColumna.Crudo),
        new("desactivada_en", ClaseDeColumna.Temporal),
    ];

    /// <summary>Las columnas de <c>documentos_ilegibles</c>, en el orden del esquema.</summary>
    public static IReadOnlyList<ColumnaDelEspejo> ColumnasDeIlegibles { get; } =
    [
        new("id", ClaseDeColumna.Crudo),
        new("ruta_pdf", ClaseDeColumna.Crudo),
        new("pagina_pdf", ClaseDeColumna.Crudo),
        new("motivo", ClaseDeColumna.Crudo),
        new("detalle", ClaseDeColumna.Crudo),
        new("lineas_leidas", ClaseDeColumna.Crudo),
        new("caso_id", ClaseDeColumna.Crudo),
        new("registrado_en", ClaseDeColumna.Temporal),
    ];

    /// <summary>Las columnas de <c>filas_descartadas</c>, en el orden del esquema.</summary>
    public static IReadOnlyList<ColumnaDelEspejo> ColumnasDeDescartadas { get; } =
    [
        new("id", ClaseDeColumna.Crudo),
        new("companero_id", ClaseDeColumna.Crudo),
        new("ruta_excel", ClaseDeColumna.Crudo),
        new("fila_excel", ClaseDeColumna.Crudo),
        new("numero_caso", ClaseDeColumna.Crudo),
        new("mrn", ClaseDeColumna.Texto),
        new("nombre", ClaseDeColumna.Crudo),
        new("motivo", ClaseDeColumna.Crudo),
        new("registrado_en", ClaseDeColumna.Temporal),
    ];

    /// <summary>El libro entero, con una pestana por tabla y en el orden en que se declaran.</summary>
    /// <remarks>Quien lo recibe lo cierra. No toca el disco: escribirlo es <see cref="Regenerar"/>.</remarks>
    /// <param name="casos">Las filas de <c>casos</c>.</param>
    /// <param name="personas">Las filas de <c>personas</c>.</param>
    /// <param name="firmasDeLosPasos">
    /// Quien contesto las seis preguntas de cada persona, por su numero interno. La que no
    /// esta sale con las tres celdas vacias, que es lo que dice la base de quien no ha
    /// contestado; NO se rellena con nada.
    /// </param>
    /// <param name="asignaciones">Las filas de <c>asignaciones</c>.</param>
    /// <param name="ilegibles">Las filas de <c>documentos_ilegibles</c>.</param>
    /// <param name="descartadas">Las filas de <c>filas_descartadas</c>.</param>
    public static XLWorkbook Construir(
        IReadOnlyList<Caso> casos,
        IReadOnlyList<Persona> personas,
        IReadOnlyDictionary<long, FirmaDeLosPasos> firmasDeLosPasos,
        IReadOnlyList<Asignacion> asignaciones,
        IReadOnlyList<RenglonIlegible> ilegibles,
        IReadOnlyList<FilaDescartada> descartadas)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(firmasDeLosPasos);

        var libro = new XLWorkbook();
        EscribirHoja(libro, "casos", ColumnasDeCasos, casos, ValorDeCaso);
        EscribirHoja(libro, "personas", ColumnasDePersonas, personas,
            (persona, columna) => ValorDePersona(persona, columna, FirmaDe(firmasDeLosPasos, persona.Id)));
        EscribirHoja(libro, "asignaciones", ColumnasDeAsignaciones, asignaciones, ValorDeAsignacion);
        EscribirHoja(libro, "documentos_ilegibles", ColumnasDeIlegibles, ilegibles, ValorDeIlegible);
        EscribirHoja(libro, "filas_descartadas", ColumnasDeDescartadas, descartadas, ValorDeDescartada);
        return libro;
    }

    /// <summary>
    /// Vuelve a escribir el `.xlsx` entero desde lo que se le pasa. Devuelve que paso.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>El orden de escritura, y por que es ese.</b> Se escribe entero a un archivo
    /// <c>.parcial</c> al lado y solo al final se reemplaza el definitivo de un golpe. Asi,
    /// matar el proceso a mitad no puede dejar un `.xlsx` a medias: lo que esta a medias se
    /// llama `.parcial`. Y el parcial vive en la MISMA carpeta que el definitivo y no en la de
    /// temporales del sistema, porque un reemplazo entre dos discos distintos no es un
    /// reemplazo.
    /// <para>
    /// Con el definitivo abierto en Excel, el reemplazo falla: entonces el definitivo se queda
    /// INTACTO, el parcial se queda donde esta —su nombre es fijo, asi que la siguiente
    /// regeneracion lo pisa y no se acumulan restos— y se devuelve el aviso en espanol. Lo que
    /// NO se toca es la base: el dato ya esta guardado antes de que el espejo se intente
    /// siquiera. ⚠️ Esto esta escrito segun lo que hace <c>File.Move(overwrite: true)</c> y
    /// <b>NO lo he medido con un Excel de verdad teniendo el archivo abierto</b>: eso es de QA.
    /// </para>
    /// </remarks>
    /// <param name="rutaDelEspejo">Ruta del <c>.xlsx</c> definitivo; su carpeta se crea si no existe.</param>
    /// <param name="casos">Las filas de <c>casos</c>.</param>
    /// <param name="personas">Las filas de <c>personas</c>.</param>
    /// <param name="firmasDeLosPasos">Quién contestó las seis preguntas de cada persona, por su número interno.</param>
    /// <param name="asignaciones">Las filas de <c>asignaciones</c>.</param>
    /// <param name="ilegibles">Las filas de <c>documentos_ilegibles</c>.</param>
    /// <param name="descartadas">Las filas de <c>filas_descartadas</c>.</param>
    /// <returns>Escrito y sin avisos, o no escrito con un solo aviso de problema que dice que la base sí quedó guardada.</returns>
    public static ResultadoDelEspejo Regenerar(
        string rutaDelEspejo,
        IReadOnlyList<Caso> casos,
        IReadOnlyList<Persona> personas,
        IReadOnlyDictionary<long, FirmaDeLosPasos> firmasDeLosPasos,
        IReadOnlyList<Asignacion> asignaciones,
        IReadOnlyList<RenglonIlegible> ilegibles,
        IReadOnlyList<FilaDescartada> descartadas)
    {
        var parcial = rutaDelEspejo + ".parcial";
        try
        {
            var carpeta = Path.GetDirectoryName(rutaDelEspejo);
            if (!string.IsNullOrEmpty(carpeta))
                Directory.CreateDirectory(carpeta);

            // Se guarda contra un flujo y no contra la ruta: ClosedXML rechaza por extension
            // cualquier archivo que no acabe en .xlsx —medido: «Extension 'parcial' is not
            // supported»— y el parcial tiene que llamarse distinto del definitivo para que se
            // vea que esta a medias. El flujo no mira la extension.
            using (var libro = Construir(casos, personas, firmasDeLosPasos, asignaciones, ilegibles, descartadas))
            using (var flujo = new FileStream(parcial, FileMode.Create, FileAccess.Write, FileShare.None))
                libro.SaveAs(flujo);
            File.Move(parcial, rutaDelEspejo, overwrite: true);
        }
        catch (Exception causa) when (causa is IOException or UnauthorizedAccessException)
        {
            return new ResultadoDelEspejo(rutaDelEspejo, false, [Aviso.Problema(
                $"No se pudo actualizar el Excel espejo «{Path.GetFileName(rutaDelEspejo)}» porque otro programa lo tiene abierto.",
                string.Empty,
                "LOS DATOS SÍ QUEDARON GUARDADOS EN LA BASE: no se ha perdido nada y no hace falta volver "
                + "a escribirlos. Cierre el archivo en Excel y guarde cualquier caso otra vez; el espejo se "
                + $"pondrá al día solo. El sistema dijo: {causa.Message}")]);
        }
        return new ResultadoDelEspejo(rutaDelEspejo, true, []);
    }

    /// <summary>
    /// Añade al libro una pestaña con su cabecera en la fila 1, una fila por elemento, la
    /// fila 1 congelada y el autofiltro sobre la tabla. Sin filas, el autofiltro cubre solo la cabecera.
    /// </summary>
    /// <typeparam name="T">El modelo de la tabla (<see cref="Caso"/>, <see cref="Persona"/>…).</typeparam>
    /// <param name="libro">El libro al que se añade la pestaña.</param>
    /// <param name="nombre">El nombre de la pestaña: el de la tabla, tal cual.</param>
    /// <param name="columnas">Las columnas en el orden del esquema.</param>
    /// <param name="filas">Los elementos a volcar, uno por fila.</param>
    /// <param name="valorDe">Dado un elemento y el nombre de una columna, el valor que va en la celda.</param>
    private static void EscribirHoja<T>(
        XLWorkbook libro,
        string nombre,
        IReadOnlyList<ColumnaDelEspejo> columnas,
        IReadOnlyList<T> filas,
        Func<T, string, object?> valorDe)
    {
        var hoja = libro.AddWorksheet(nombre);
        for (var numero = 1; numero <= columnas.Count; numero++)
            hoja.Cell(1, numero).SetValue(columnas[numero - 1].Nombre);

        for (var indice = 0; indice < filas.Count; indice++)
            for (var numero = 1; numero <= columnas.Count; numero++)
                EscribirCelda(hoja.Cell(PrimeraFilaDeDatos + indice, numero),
                    valorDe(filas[indice], columnas[numero - 1].Nombre), columnas[numero - 1].Clase);

        hoja.SheetView.FreezeRows(1);
        hoja.Range(1, 1, Math.Max(PrimeraFilaDeDatos - 1 + filas.Count, 1), columnas.Count).SetAutoFilter();
    }

    /// <summary>
    /// Mete un valor en su celda segun la clase de su columna.
    /// </summary>
    /// <remarks>
    /// Lo que no se puede leer como fecha se deja tal cual: no se adivina, no se rellena y no se
    /// descarta. La regla permanente 1 prohibe inventar un dato, y una fecha inventada en un
    /// espejo es peor que una fecha fea. Y todo texto entra con <c>SetValue(string)</c>: un
    /// nombre leido por OCR que empiece por «=» no es una formula, y una celda que se calcula
    /// sola deja de ser un espejo.
    /// </remarks>
    /// <param name="celda">La celda de destino.</param>
    /// <param name="valor">Nulo (la celda se deja vacía), <c>bool</c>, <c>long</c>, <c>int</c> o <c>string</c>; ningún otro tipo llega aquí desde los modelos.</param>
    /// <param name="clase">Solo cambia algo para un <c>string</c>: texto forzado, o fecha si se puede leer como tal.</param>
    private static void EscribirCelda(IXLCell celda, object? valor, ClaseDeColumna clase)
    {
        switch (valor)
        {
            case null:
                return;
            case bool booleano:
                // 0/1 y no «Sí»/«No»: el vacio —«nadie lo ha mirado»— es el estado que se pierde al traducir.
                celda.Value = booleano ? 1 : 0;
                return;
            case long entero:
                celda.Value = entero;
                return;
            case int entero:
                celda.Value = entero;
                return;
        }

        var texto = (string)valor;
        if (clase == ClaseDeColumna.Texto)
        {
            celda.Style.NumberFormat.Format = FormatoDeTexto;
            celda.SetValue(texto);
            return;
        }
        if (clase == ClaseDeColumna.Temporal && ComoFecha(texto) is (DateTime fecha, string formato))
        {
            celda.Value = fecha;
            celda.Style.NumberFormat.Format = formato;
            return;
        }
        celda.SetValue(texto);
    }

    /// <summary>
    /// Una cadena ISO-8601 como fecha, con el formato con el que se escribe; nulo si no lo es.
    /// </summary>
    /// <remarks>
    /// El orden importa: se prueba primero la fecha sola. Si se probara antes la marca de
    /// tiempo, «2026-09-08» fallaria y caeria al camino de «no se pudo», cuando si se puede.
    /// </remarks>
    /// <param name="texto">El valor de una columna temporal tal como está en la base.</param>
    /// <returns>La fecha y el formato de Excel con el que se pinta, o nulo si no es ISO-8601 exacto.</returns>
    private static (DateTime, string)? ComoFecha(string texto)
    {
        if (DateTime.TryParseExact(texto, FormatoDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var soloFecha))
            return (soloFecha, FormatoDeFechaEnExcel);
        if (DateTime.TryParseExact(texto, FormatoDeMarcaDeTiempo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var marca))
            return (marca, FormatoDeMarcaDeTiempoEnExcel);
        return null;
    }

    /// <summary>El valor de una columna de <c>casos</c>, sacado de la propiedad del mismo nombre.</summary>
    /// <param name="caso">La fila.</param>
    /// <param name="columna">El nombre de la columna en la base.</param>
    /// <exception cref="KeyNotFoundException">La columna no está en <see cref="ColumnasDeCasos"/>: el esquema y este mapa se separaron.</exception>
    private static object? ValorDeCaso(Caso caso, string columna) => columna switch
    {
        "id" => caso.Id,
        "numero_caso" => caso.NumeroCaso,
        "unidad_numero" => caso.UnidadNumero,
        "unidad_nombre" => caso.UnidadNombre,
        "templo_nombre" => caso.TemploNombre,
        "fecha_viaje" => caso.FechaViaje,
        "pagina_pdf" => caso.PaginaPdf,
        "captura_manual" => caso.CapturaManual,
        "archivado" => caso.Archivado,
        "fecha_archivado" => caso.FechaArchivado,
        "ruta_pdf" => caso.RutaPdf,
        "creado_en" => caso.CreadoEn,
        "estado_recomendacion" => caso.EstadoRecomendacion,
        "duplicado_de" => caso.DuplicadoDe,
        "estado_marcado_por" => caso.EstadoMarcadoPor,
        "estado_marcado_en" => caso.EstadoMarcadoEn,
        "estado_marcado_origen" => caso.EstadoMarcadoOrigen,
        "estado_del_companero" => caso.EstadoDelCompanero,
        "estado_del_companero_por" => caso.EstadoDelCompaneroPor,
        "estado_del_companero_en" => caso.EstadoDelCompaneroEn,
        "motivo_no_completa" => caso.MotivoNoCompleta,
        "motivo_del_companero" => caso.MotivoDelCompanero,
        _ => throw new KeyNotFoundException($"La hoja «casos» del espejo no conoce la columna «{columna}»."),
    };

    /// <summary>
    /// El valor de una columna de <c>personas</c>; las tres de la firma salen aparte.
    /// </summary>
    /// <remarks>
    /// La firma llega por separado y no dentro de <see cref="Persona"/> porque
    /// <c>Fichas.Contratos/Modelos</c> esta congelado. Sin firma se devuelve nulo en las
    /// tres, y un nulo deja la celda vacia: es lo que dice la base de quien no ha contestado.
    /// </remarks>
    /// <param name="persona">La fila.</param>
    /// <param name="columna">El nombre de la columna en la base.</param>
    /// <param name="firma">Quién contestó los pasos de esta persona, o <see cref="FirmaDeLosPasos.SinFirmar"/>.</param>
    /// <exception cref="KeyNotFoundException">La columna no está en <see cref="ColumnasDePersonas"/>.</exception>
    private static object? ValorDePersona(Persona persona, string columna, FirmaDeLosPasos firma) => columna switch
    {
        "id" => persona.Id,
        "caso_id" => persona.CasoId,
        "mrn" => persona.Mrn,
        "nombre" => persona.Nombre,
        "fila_formulario" => persona.FilaFormulario,
        "pagina_pdf" => persona.PaginaPdf,
        "ord_recibir_propias" => persona.OrdRecibirPropias,
        "ord_observar_sellamiento" => persona.OrdObservarSellamiento,
        "ord_traductor" => persona.OrdTraductor,
        "ord_investidura" => persona.OrdInvestidura,
        "ord_sellamiento_esposos" => persona.OrdSellamientoEsposos,
        "ord_sellamiento_hijo_padres" => persona.OrdSellamientoHijoPadres,
        "estado_propuesto" => persona.EstadoPropuesto,
        "nota_companero" => persona.NotaCompanero,
        "propuesto_por" => persona.PropuestoPor,
        "propuesto_en" => persona.PropuestoEn,
        "pudo_viajar" => persona.PudoViajar,
        "motivo_no_viajo" => persona.MotivoNoViajo,
        "paso_preparacion" => persona.PasoPreparacion,
        "paso_informacion" => persona.PasoInformacion,
        "paso_cita_del_templo" => persona.PasoCitaDelTemplo,
        "paso_acciones_requeridas" => persona.PasoAccionesRequeridas,
        "paso_entrevistas" => persona.PasoEntrevistas,
        "paso_listo_para_el_templo" => persona.PasoListoParaElTemplo,
        "llamo_al_lider" => persona.LlamoAlLider,
        "pasos_por" => firma.Por,
        "pasos_en" => firma.En,
        "pasos_origen" => firma.Origen,
        _ => throw new KeyNotFoundException($"La hoja «personas» del espejo no conoce la columna «{columna}»."),
    };

    /// <summary>La firma de esa persona, o la de quien no ha contestado.</summary>
    /// <param name="firmas">Las firmas por número interno de persona.</param>
    /// <param name="personaId">El número interno de la persona.</param>
    private static FirmaDeLosPasos FirmaDe(IReadOnlyDictionary<long, FirmaDeLosPasos> firmas, long personaId)
        => firmas.TryGetValue(personaId, out var firma) ? firma : FirmaDeLosPasos.SinFirmar;

    /// <summary>El valor de una columna de <c>asignaciones</c>, sacado de la propiedad del mismo nombre.</summary>
    /// <param name="asignacion">La fila.</param>
    /// <param name="columna">El nombre de la columna en la base.</param>
    /// <exception cref="KeyNotFoundException">La columna no está en <see cref="ColumnasDeAsignaciones"/>.</exception>
    private static object? ValorDeAsignacion(Asignacion asignacion, string columna) => columna switch
    {
        "id" => asignacion.Id,
        "caso_id" => asignacion.CasoId,
        "companero_id" => asignacion.CompaneroId,
        "asignado_en" => asignacion.AsignadoEn,
        "activa" => asignacion.Activa,
        "desactivada_en" => asignacion.DesactivadaEn,
        _ => throw new KeyNotFoundException($"La hoja «asignaciones» del espejo no conoce la columna «{columna}»."),
    };

    /// <summary>El valor de una columna de <c>documentos_ilegibles</c>, sacado de la propiedad del mismo nombre.</summary>
    /// <param name="renglon">La fila.</param>
    /// <param name="columna">El nombre de la columna en la base.</param>
    /// <exception cref="KeyNotFoundException">La columna no está en <see cref="ColumnasDeIlegibles"/>.</exception>
    private static object? ValorDeIlegible(RenglonIlegible renglon, string columna) => columna switch
    {
        "id" => renglon.Id,
        "ruta_pdf" => renglon.RutaPdf,
        "pagina_pdf" => renglon.PaginaPdf,
        "motivo" => renglon.Motivo,
        "detalle" => renglon.Detalle,
        "lineas_leidas" => renglon.LineasLeidas,
        "caso_id" => renglon.CasoId,
        "registrado_en" => renglon.RegistradoEn,
        _ => throw new KeyNotFoundException($"La hoja «documentos_ilegibles» del espejo no conoce la columna «{columna}»."),
    };

    /// <summary>El valor de una columna de <c>filas_descartadas</c>, sacado de la propiedad del mismo nombre.</summary>
    /// <param name="fila">La fila.</param>
    /// <param name="columna">El nombre de la columna en la base.</param>
    /// <exception cref="KeyNotFoundException">La columna no está en <see cref="ColumnasDeDescartadas"/>.</exception>
    private static object? ValorDeDescartada(FilaDescartada fila, string columna) => columna switch
    {
        "id" => fila.Id,
        "companero_id" => fila.CompaneroId,
        "ruta_excel" => fila.RutaExcel,
        "fila_excel" => fila.FilaExcel,
        "numero_caso" => fila.NumeroCaso,
        "mrn" => fila.Mrn,
        "nombre" => fila.Nombre,
        "motivo" => fila.Motivo,
        "registrado_en" => fila.RegistradoEn,
        _ => throw new KeyNotFoundException($"La hoja «filas_descartadas» del espejo no conoce la columna «{columna}»."),
    };
}
