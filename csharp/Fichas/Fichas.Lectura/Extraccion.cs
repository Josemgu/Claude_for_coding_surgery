using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.Lectura;

/// <summary>
/// De las lineas que devolvio el OCR a los campos que se proponen.
/// </summary>
/// <remarks>
/// ⛔ Regla permanente 1: aqui se aplican anclas y reglas deterministas, nunca un modelo
/// que adivine. ⛔ Regla permanente 5: propone, no firma.
///
/// <para><b>Requisito 9, y es el criterio C3-L5.</b> Un valor que el OCR leyo pero que no
/// tiene la forma que el campo pide NO se pierde: sale en <see cref="CampoPropuesto.Valor"/>
/// tal como se leyo, acompanado de un <see cref="Aviso"/> de advertencia que nombra el
/// campo. Es el caso de las dos cedulas terminadas en letra que el programa viejo dejaba
/// vacias en silencio. Solo sale nulo lo que de verdad no se leyo, y entonces el origen
/// es <see cref="OrigenDeCampo.Vacio"/>.</para>
/// </remarks>
public sealed class Extraccion : IExtraccion
{
    // Los nombres de columna de `procedencia_campo.campo`, que es lo que viaja en
    // `CampoPropuesto.Campo`. Van en snake_case porque son columnas de la base, no
    // identificadores de C#.
    /// <summary>La columna <c>casos.numero_caso</c>.</summary>
    public const string CampoNumeroDeCaso = "numero_caso";

    /// <summary>La columna <c>casos.fecha_viaje</c>.</summary>
    public const string CampoFechaDeViaje = "fecha_viaje";

    /// <summary>La columna <c>casos.unidad_numero</c>.</summary>
    public const string CampoUnidadNumero = "unidad_numero";

    /// <summary>La columna <c>casos.unidad_nombre</c>.</summary>
    public const string CampoUnidadNombre = "unidad_nombre";

    /// <summary>La columna <c>casos.templo_nombre</c>.</summary>
    public const string CampoTemploNombre = "templo_nombre";

    /// <summary>La columna <c>personas.nombre</c>.</summary>
    public const string CampoNombreDePersona = "nombre";

    /// <summary>La columna <c>personas.mrn</c>, que es la cedula de miembro.</summary>
    public const string CampoCedula = "mrn";

    /// <summary>
    /// Confianza por debajo de la cual un campo se considera flojo.
    /// </summary>
    /// <remarks>Umbral de `DECISIONES.md`, portado de `extraccion/formulario.py`.</remarks>
    public const double ConfianzaQueSeConsideraBaja = 0.6;

    /// <summary>
    /// Si mas de esta proporcion de campos vuelve floja, la pagina se abre a mano.
    /// </summary>
    public const double ProporcionDeCamposFlojosQueMarcaCapturaManual = 0.6;

    private readonly double _relacionDeAspecto;

    /// <summary>Crea la extraccion para paginas de una forma dada.</summary>
    /// <param name="relacionDeAspecto">
    /// Ancho partido por alto de la pagina, en puntos. Hace falta porque las bandas se
    /// ensanchan horizontalmente en multiplos del ALTO del ancla, y una fraccion de ancho
    /// no mide lo mismo que una de alto cuando la pagina no es cuadrada. El valor por
    /// defecto es el de las siete hojas reales del dueno, medido: 612 x 792 puntos, que
    /// es una carta. Quien lea una hoja de otro tamano pasa el suyo.
    /// </param>
    public Extraccion(double relacionDeAspecto = 612.0 / 792.0) => _relacionDeAspecto = relacionDeAspecto;

    // --- Los campos del caso -----------------------------------------------------

    /// <inheritdoc />
    public ResultadoDeExtraccion ProponerCamposDelCaso(
        IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(anotaciones);

        var anclas = Bandas.LocalizarLasAnclas(lineas);
        var campos = new List<CampoPropuesto>();
        var avisos = new List<Aviso>();

        Anadir(campos, avisos, TablaDeProcedencia.Casos, CampoNumeroDeCaso,
            NumeroDeCaso(lineas, anotaciones), banda: null);

        var (fecha, bandaDeLaFecha) = CampoDeLaBanda(
            lineas, anotaciones, anclas[Etiquetas.CampoDeLaFechaDeViaje], Normalizacion.NormalizarFecha);
        Anadir(campos, avisos, TablaDeProcedencia.Casos, CampoFechaDeViaje, fecha, bandaDeLaFecha);

        var (unidadNumero, bandaDeLaUnidad) = CampoDeLaBanda(
            lineas, anotaciones, anclas[Etiquetas.CampoDeLaUnidad], Normalizacion.NormalizarNumeroDeUnidad);
        Anadir(campos, avisos, TablaDeProcedencia.Casos, CampoUnidadNumero, unidadNumero, bandaDeLaUnidad);

        // El nombre y el numero de la unidad salen de la MISMA banda del papel —«Ward/Branch
        // Name and Unit Number» es una sola etiqueta con las dos cosas debajo—, pero se
        // resuelven por separado: en un formulario real hay una anotacion que corrige el
        // NOMBRE y no dice nada del numero, y tratandola como correccion del conjunto se
        // perdia el numero que el OCR habia leido bien.
        var (unidadNombre, _) = CampoDeLaBanda(
            lineas, anotaciones, anclas[Etiquetas.CampoDeLaUnidad], Normalizacion.NormalizarNombreDeUnidad);
        Anadir(campos, avisos, TablaDeProcedencia.Casos, CampoUnidadNombre,
            SinRepetirElNumeroDeLaUnidad(unidadNombre), bandaDeLaUnidad);

        var (templo, bandaDelTemplo) = CampoDeLaBanda(
            lineas, anotaciones, anclas[Etiquetas.CampoDelNombreDelTemplo], Normalizacion.NormalizarNombreDelTemplo);
        Anadir(campos, avisos, TablaDeProcedencia.Casos, CampoTemploNombre, templo, bandaDelTemplo);

        foreach (var campo in anclas.Where(par => par.Value is null).Select(par => par.Key))
        {
            avisos.Add(Aviso.Advierte(
                $"No se encontró en el papel la etiqueta «{Etiquetas.FormaEspanolaDe(campo)}».",
                campo,
                "Se busca por su texto y no por su posición, así que un escaneo torcido no la mueve: "
                + "o no está impresa en esta hoja, o el OCR no la leyó."));
        }
        return new ResultadoDeExtraccion(campos, avisos);
    }

    /// <summary>
    /// El numero de caso, que en estos formularios va siempre en una anotacion.
    /// </summary>
    /// <remarks>
    /// Se busca PRIMERO en las anotaciones porque ahi el texto es exacto y no pasa por el
    /// OCR, y solo si no aparece se recurre a lo leido. No lleva banda: el patron —cuatro
    /// letras mayusculas y cuatro digitos— es especifico de sobra para encontrarlo en toda
    /// la pagina, asi que no hay una fila del papel que ensenar.
    ///
    /// <para>⚠️ <b>Medido el 2026-09-04, y corrige lo que decia `DECISIONES.md`.</b> Los
    /// siete escaneos del dueno traen el numero en una <c>/FreeText</c>, y el escaneo por
    /// debajo NO trae ninguno: renderizada una hoja sin anotaciones, el OCR devuelve 78
    /// lineas y <b>cero</b> con la forma de un numero de caso. De los siete, seis dicen
    /// <c>CASP2609</c> y uno dice literalmente <c>CASD2609</c> —texto exacto del PDF, sin
    /// OCR de por medio—. <b>No fue una P leida como D:</b> esta escrito asi dentro del
    /// documento, y el unico sitio donde pone `CASP` en ese archivo es su nombre. Ningun
    /// motor arregla eso; se guarda tal cual y se corrige a mano (ADR-0004 §6ter).</para>
    /// </remarks>
    private static CampoExtraido NumeroDeCaso(
        IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        var enAnotacion = anotaciones
            .Where(a => Anotaciones.EsCorreccionEscrita(a) && Normalizacion.NormalizarNumeroDeCaso(a.Texto) is not null)
            .ToArray();
        if (enAnotacion.Length > 0)
        {
            return Campos.ResolverCampo([], enAnotacion, hayTachon: false, Normalizacion.NormalizarNumeroDeCaso);
        }

        var enLineas = lineas.Where(l => Normalizacion.NormalizarNumeroDeCaso(l.Texto) is not null).ToArray();
        return enLineas.Length > 0
            ? Campos.ResolverCampo([enLineas[0]], [], hayTachon: false, Normalizacion.NormalizarNumeroDeCaso)
            : Campos.CampoVacio();
    }

    /// <summary>
    /// El nombre de la unidad, sin el respaldo del texto crudo cuando ese texto ES el numero.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Medido el 2026-09-05 sobre los dos formularios de grupo del dueno</b> (los
    /// <c>SURB2609</c>, seis hojas cada uno): <c>unidad_nombre</c> salia valiendo
    /// <c>«7000011»</c> en 8 de sus 12 hojas, o sea el NUMERO de la unidad. En la pantalla se
    /// leia una cifra donde va el nombre del barrio.</para>
    ///
    /// <para><b>De donde salia.</b> El nombre y el numero cuelgan de la MISMA banda —«Ward/Branch
    /// Name and Unit Number» es una sola etiqueta con las dos cosas debajo—, y cuando esa banda
    /// trae solo la cifra, <see cref="Normalizacion.NormalizarNombreDeUnidad"/> devuelve nulo
    /// con razon: ahi no hay nombre. Entonces entraba el respaldo del requisito 9 —«se deja lo
    /// que decia el papel»— y metia el texto crudo de la banda, que era la cifra.</para>
    ///
    /// <para><b>Por que quitarlo NO rompe el requisito 9.</b> Ese respaldo existe para que no se
    /// pierda lo que el papel decia. Aqui no se pierde nada: lo que la banda decia esta entero en
    /// <c>unidad_numero</c>, que sale de esta misma banda dos lineas mas arriba. La condicion es
    /// justo esa y ninguna mas —hay texto crudo, no hay nombre, y el crudo da un numero de
    /// unidad valido—; una banda con basura dentro sigue devolviendo su basura para que se vea.</para>
    /// </remarks>
    /// <remarks>
    /// ⚠️ <b>Y solo cuando lo leido es de ESTA banda</b> (añadido el 2026-09-07). Toda la
    /// justificacion de arriba —«no se pierde nada, porque lo que la banda decia esta entero
    /// en <c>unidad_numero</c>»— se cae si el texto NO era de la banda: entonces
    /// <c>unidad_numero</c> tambien se quedo vacio, y borrarlo aqui lo perderia del todo.
    /// </remarks>
    private static CampoExtraido SinRepetirElNumeroDeLaUnidad(CampoExtraido nombre)
        => nombre.Valor is null
           && nombre.LoLeidoEsDeEsteCampo
           && Normalizacion.NormalizarNumeroDeUnidad(nombre.ValorOcr) is not null
            ? nombre with { ValorOcr = null }
            : nombre;

    /// <summary>Aplica la precedencia sobre la banda que cuelga de un ancla.</summary>
    private (CampoExtraido Campo, BandaDeLaPagina? Banda) CampoDeLaBanda(
        IReadOnlyList<LineaDeOcr> lineas,
        IReadOnlyList<AnotacionDelPdf> anotaciones,
        LineaDeOcr? ancla,
        Func<string?, string?> normalizar)
    {
        if (ancla is null) return (Campos.CampoVacio(), null);

        var banda = Bandas.BandaDeValor(ancla.Banda, _relacionDeAspecto);

        return (Campos.ResolverCampo(
            Bandas.LineasEnLaBanda(lineas, banda),
            Bandas.CorreccionesEnLaBanda(anotaciones, banda),
            Bandas.HayTachonEnLaBanda(anotaciones, banda),
            normalizar,
            Bandas.LineasQueRozanLaBanda(lineas, banda)), banda);
    }

    // --- Los campos de las personas ----------------------------------------------

    /// <inheritdoc />
    public ResultadoDeExtraccion ProponerCamposDePersonas(
        IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(anotaciones);

        var (personas, descartadas) = ExtraerPersonas(lineas, anotaciones);
        var campos = new List<CampoPropuesto>();
        var avisos = new List<Aviso>();

        foreach (var persona in personas)
        {
            Anadir(campos, avisos, TablaDeProcedencia.Personas, CampoNombreDePersona,
                persona.Nombre, persona.Banda, persona.FilaFormulario);
            Anadir(campos, avisos, TablaDeProcedencia.Personas, CampoCedula,
                persona.Cedula, persona.Banda, persona.FilaFormulario);
        }

        if (personas.Count == 0)
        {
            avisos.Add(Aviso.Advierte(
                "No se pudo leer ninguna persona en esta hoja.",
                CampoNombreDePersona,
                "Hace falta encontrar las dos cabeceras del bloque y una etiqueta que lo cierre por abajo. "
                + "Sin eso no se sabe dónde empieza ni dónde acaba la lista, y adivinarlo inventa personas."));
        }
        if (descartadas > 0)
        {
            avisos.Add(Aviso.Advierte(
                $"Se descartaron {descartadas} filas del bloque de personas por venir en blanco.",
                CampoNombreDePersona));
        }
        return new ResultadoDeExtraccion(campos, avisos);
    }

    /// <summary>Las personas de la hoja, con las anclas ya localizadas. Para reutilizar el trabajo.</summary>
    /// <remarks>
    /// ⚠️ <b>Las anotaciones son obligatorias y no tienen valor por defecto</b>, aunque
    /// pasarlas vacias compilara igual. Hasta el 2026-09-06 esta funcion no las recibia y las
    /// personas NUNCA detectaban un tachon: una cedula tachada entraba a la base
    /// indistinguible de una lectura limpia, y la cedula es la mitad del par
    /// <c>numero_caso</c> + <c>mrn</c> con el que se reconcilia todo el programa.
    /// </remarks>
    public (IReadOnlyList<PersonaExtraida> Personas, int Descartadas) ExtraerPersonas(
        IReadOnlyList<LineaDeOcr> lineas, IReadOnlyList<AnotacionDelPdf> anotaciones)
    {
        var anclas = Bandas.LocalizarLasAnclas(lineas);
        var cierres = Etiquetas.CamposQueCierranLasPersonas
            .Select(campo => anclas[campo]?.Banda)
            .ToArray();

        return Personas.Extraer(
            lineas,
            anotaciones,
            anclas[Etiquetas.CampoDeLosNombres]?.Banda,
            anclas[Etiquetas.CampoDeLaCedula]?.Banda,
            cierres,
            _relacionDeAspecto);
    }

    // --- Normalizar un valor suelto ----------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    /// Nunca lanza por una forma rara. Un valor que no encaja vuelve <b>tal como venia</b>
    /// con su aviso: es el requisito 9, y vaciarlo en silencio es lo unico que no vale.
    /// </remarks>
    public ResultadoDeExtraccion Normalizar(string campo, string? valorLeido)
    {
        var darForma = NormalizadorDe(campo);
        if (darForma is null)
        {
            return ResultadoDeExtraccion.Nada(Aviso.Advierte(
                $"No hay regla de formato para el campo «{campo}»; se deja tal como se leyó.", campo));
        }

        var resuelto = string.IsNullOrWhiteSpace(valorLeido)
            ? Campos.CampoVacio()
            : Campos.ResolverCampo(
                [new LineaDeOcr(valorLeido, null, new BandaDeLaPagina(0, 0, 0, 0))],
                [], hayTachon: false, darForma);

        var campos = new List<CampoPropuesto>();
        var avisos = new List<Aviso>();
        Anadir(campos, avisos, TablaDeLa(campo), campo, resuelto, banda: null);
        return new ResultadoDeExtraccion(campos, avisos);
    }

    /// <summary>La regla de formato de ese campo, o nula si el campo no tiene ninguna.</summary>
    public static Func<string?, string?>? NormalizadorDe(string campo) => campo switch
    {
        CampoNumeroDeCaso => Normalizacion.NormalizarNumeroDeCaso,
        CampoFechaDeViaje => Normalizacion.NormalizarFecha,
        CampoUnidadNumero => Normalizacion.NormalizarNumeroDeUnidad,
        CampoUnidadNombre => Normalizacion.NormalizarNombreDeUnidad,
        CampoTemploNombre => Normalizacion.NormalizarNombreDelTemplo,
        CampoCedula => Normalizacion.NormalizarCedula,
        CampoNombreDePersona => texto => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim(),
        _ => null,
    };

    private static TablaDeProcedencia TablaDeLa(string campo)
        => campo is CampoNombreDePersona or CampoCedula ? TablaDeProcedencia.Personas : TablaDeProcedencia.Casos;

    // --- El puente entre lo resuelto y lo que viaja por el contrato ---------------

    /// <summary>
    /// Mete el campo en la lista, y con el el aviso que haga falta.
    /// </summary>
    /// <remarks>
    /// Aqui vive el requisito 9 entero, y son tres casos:
    /// <list type="number">
    ///   <item>El valor tiene su forma: viaja normalizado y sin aviso.</item>
    ///   <item>Se leyo algo que no tiene esa forma: viaja <b>lo leido, tal cual</b>, con
    ///   un aviso de advertencia que nombra el campo. <b>Jamas vacio en silencio.</b></item>
    ///   <item>No se leyo nada: viaja nulo con origen vacio.</item>
    /// </list>
    ///
    /// <para>⚠️ <b>Y la marca de tachon viaja CON el dato, no solo en el aviso.</b> Hasta
    /// el 2026-09-06 se construia el <see cref="CampoPropuesto"/> con siete argumentos
    /// posicionales y <c>AnuladoPorTachon</c> —el noveno— se quedaba en su
    /// <c>false</c> por defecto. Un comentario de aqui mismo decia que
    /// <see cref="CampoPropuesto"/> «no tiene donde ponerlo»: era verdad cuando se
    /// escribio y dejo de serlo el 2026-09-04, cuando se anadio el sitio. Mientras tanto,
    /// un valor tachado llegaba a la base indistinguible de una lectura limpia, y si por
    /// casualidad tenia la forma correcta —once digitos, una fecha bien formada— el
    /// programa lo daba por bueno: justo el dato que alguien tacho por estar mal.</para>
    ///
    /// <para><b>Que significa la marca, y por que las dos ramas del tachon no se marcan
    /// igual.</b> En el contrato quiere decir «el valor que viaja aqui esta anulado», no
    /// «hubo un trazo rojo en esta fila». Lo fija el codigo que ya la lee:
    /// <c>Fichas.App/Importar/CamposDeLaHoja.cs:106</c> devuelve <c>null</c> en cuanto la
    /// ve, y <c>Fichas.App/Correccion/EstadosDeCampo.cs:71</c> la mira ANTES que el
    /// origen y le pone la palabra «tachado, sin correccion». Asi que un campo que SI
    /// tiene correccion escrita —donde el valor que viaja es el bueno, el que escribio la
    /// persona— no se marca: marcarlo tiraria ese dato correcto y ademas lo describiria
    /// con una frase falsa. Que hubo un trazo rojo sobre el se sigue diciendo en su
    /// aviso, que es donde ya se decia, y lo tachado sigue entero en
    /// <see cref="CampoExtraido.ValorOcr"/>.</para>
    /// </remarks>
    private static void Anadir(
        List<CampoPropuesto> campos,
        List<Aviso> avisos,
        TablaDeProcedencia tabla,
        string nombreDelCampo,
        CampoExtraido campo,
        BandaDeLaPagina? banda,
        int? filaFormulario = null)
    {
        if (campo.Valor is not null)
        {
            // Lo que viaja aqui NO sale de debajo del trazo rojo: o lo escribio una
            // persona en la anotacion, o no hubo tachon ninguno. Por eso no se anula.
            campos.Add(new CampoPropuesto(
                tabla, nombreDelCampo, campo.Valor, campo.Origen, campo.Confianza, banda, filaFormulario,
                AnuladoPorTachon: false));
            if (campo.AnuladoPorTachon)
            {
                avisos.Add(Aviso.Advierte(
                    $"«{nombreDelCampo}» venía tachado en el papel y alguien escribió el valor bueno al lado.",
                    nombreDelCampo, $"Lo tachado decía: {campo.ValorOcr}"));
            }
            return;
        }

        if (campo.ValorOcr is not null && campo.LoLeidoEsDeEsteCampo)
        {
            // Se leyo algo EN EL SITIO DE ESTE CAMPO. No tiene la forma que la columna
            // pide, pero el papel lo decia ahi y por lo tanto se devuelve. Es el criterio
            // C3-L5, y es la rama que salva las cedulas terminadas en letra.
            //
            // Y si lo anulo un tachon, lo que viaja ES el texto de debajo del trazo: va
            // marcado, para que nadie lo confunda con una lectura limpia.
            campos.Add(new CampoPropuesto(
                tabla, nombreDelCampo, campo.ValorOcr, OrigenDeCampo.Ocr, campo.Confianza, banda, filaFormulario,
                ValorOcr: campo.ValorOcr,
                AnuladoPorTachon: campo.AnuladoPorTachon));
            avisos.Add(Aviso.Advierte(
                campo.AnuladoPorTachon
                    ? $"«{nombreDelCampo}» venía tachado en el papel y nadie escribió el valor bueno."
                    : $"«{nombreDelCampo}» no tiene la forma esperada; se deja lo que decía el papel para que lo revise.",
                nombreDelCampo,
                $"Se leyó: {campo.ValorOcr}"));
            return;
        }

        // Nada que poner aqui. Son tres motivos distintos y hay que decir cual, porque a
        // cada uno le toca una cosa distinta de quien corrige la hoja.
        //
        // ⛔ El tercero es el que el dueño señalo el 2026-09-07: se leyo algo cerca —en la
        // fila de al lado, o en el renglon de la persona pero fuera de la casilla de la
        // cedula— y NO se puede afirmar que sea de este campo. Rellenar el campo con eso es
        // cruzar los datos, y hacerlo con la confianza del OCR delante es peor todavia:
        // un dato cruzado con confianza alta pasa por bueno y no lo mira nadie. Asi que el
        // campo va VACIO y SIN CONFIANZA, y lo leido se conserva en `ValorOcr` —la columna
        // `procedencia_campo.valor_ocr`— que `Fichas.App/Correccion/EstadosDeCampo.cs:136`
        // ya pinta como «El lector leyó aquí «…» y no encajó: no se guardó». Vacio en el
        // dato, dicho en el aviso, y lo leido enseñado donde no se confunde con el valor.
        campos.Add(new CampoPropuesto(
            tabla, nombreDelCampo, null, OrigenDeCampo.Vacio, null, banda, filaFormulario,
            ValorOcr: campo.ValorOcr,
            AnuladoPorTachon: campo.AnuladoPorTachon,
            NecesitaRevision: true));
        avisos.Add(Aviso.Advierte(
            campo.AnuladoPorTachon
                ? $"«{nombreDelCampo}» venía tachado en el papel y debajo no se leyó nada."
                : campo.ValorOcr is not null
                    ? $"«{nombreDelCampo}» se queda vacío: lo que se leyó cerca no es de este campo."
                    : $"«{nombreDelCampo}» no se pudo leer del papel.",
            nombreDelCampo,
            campo.ValorOcr is null
                ? null
                : $"Se leyó cerca: {campo.ValorOcr}. No se guardó porque no se puede afirmar que sea de aquí."));
    }
}
