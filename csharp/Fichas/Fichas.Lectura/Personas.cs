using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.Lectura;

/// <summary>Una persona leida de una fila del formulario.</summary>
/// <param name="FilaFormulario">En que fila del papel venia, base 1.</param>
/// <param name="Nombre">Su nombre tal como lo leyo el OCR.</param>
/// <param name="Cedula">Su cedula de miembro, con su procedencia.</param>
/// <param name="Banda">La fila entera del escaneo, para poder ensenarla al lado del campo.</param>
public sealed record PersonaExtraida(
    int FilaFormulario,
    CampoExtraido Nombre,
    CampoExtraido Cedula,
    BandaDeLaPagina Banda);

/// <summary>
/// Las filas de personas del formulario: quien viaja y con que cedula.
/// </summary>
/// <remarks>
/// Portado de `extraccion/personas.py`. El formulario trae seis filas y casi nunca vienen
/// todas llenas. Una fila sin nombre y sin cedula se descarta: no se guarda una persona
/// en blanco.
///
/// <para><b>Por que las filas se buscan por el NOMBRE y no por la cedula</b>, que seria
/// lo comodo: esta medido que el OCR falla la cedula mas veces que el nombre. En los
/// formularios de referencia hay una persona cuya cedula salio ilegible del todo. Si las
/// filas se buscaran por cedula, esa persona desapareceria del caso sin que nadie se
/// entere. Buscada por nombre, aparece con la cedula vacia y marcada para revision, que
/// es un problema visible y por lo tanto arreglable.</para>
/// </remarks>
public static class Personas
{
    /// <summary>
    /// El punto medio entre las dos cabeceras: a su izquierda estan los nombres.
    /// </summary>
    /// <remarks>
    /// No es una coordenada fija: sale de donde el OCR encontro las dos etiquetas en ESTA
    /// pagina, asi que se mueve con el escaneo. Hace falta porque el formulario lleva
    /// notas de revision —«Verified for endowment and sealing»— escritas a la izquierda de
    /// la columna de cedulas. Sin este limite, cada nota se contaria como una persona mas.
    /// </remarks>
    /// <param name="anclaNombres">La caja de la cabecera «Full Name(s)» tal como la leyó el OCR.</param>
    /// <param name="anclaCedula">La caja de la cabecera «Membership Record Number».</param>
    /// <returns>Una <c>x</c> en fracción de página: la media de los bordes izquierdos de las dos cabeceras.</returns>
    public static double LimiteDeLaColumnaDeNombres(BandaDeLaPagina anclaNombres, BandaDeLaPagina anclaCedula)
        => (anclaNombres.X0 + anclaCedula.X0) / 2.0;

    /// <summary>
    /// Desde debajo de las cabeceras hasta la siguiente seccion del formulario.
    /// </summary>
    /// <remarks>
    /// El borde de abajo es nulo cuando NINGUNA de las etiquetas que cierran el bloque
    /// aparecio. Ese nulo no es un detalle: sin cierre el bloque llegaria hasta el pie de
    /// la pagina y cada linea del formulario —los costes, las firmas, la letra pequena— se
    /// contaria como una persona. Medido el 2026-09-02: en las dos paginas peor escaneadas
    /// de los cuatro documentos, cerrar el bloque con el borde de la imagen producia 35 y
    /// 31 personas donde hay una.
    /// </remarks>
    /// <param name="anclaNombres">La caja de la cabecera de nombres.</param>
    /// <param name="anclaCedula">La caja de la cabecera de cédulas.</param>
    /// <param name="cierres">Las cajas de las etiquetas que cierran el bloque, nulas las que no aparecieron.</param>
    /// <returns>El borde de abajo de la cabecera más baja, y el borde de arriba del primer cierre por debajo de él, o nulo si no hay ninguno.</returns>
    private static (double Arriba, double? Abajo) BloqueDePersonas(
        BandaDeLaPagina anclaNombres, BandaDeLaPagina anclaCedula, IReadOnlyList<BandaDeLaPagina?> cierres)
    {
        double arriba = Math.Max(anclaNombres.Y1, anclaCedula.Y1);
        double? abajo = cierres
            .Where(cierre => cierre is not null && cierre.Value.Y0 > arriba)
            .Select(cierre => (double?)cierre!.Value.Y0)
            .DefaultIfEmpty(null)
            .Min();
        return (arriba, abajo);
    }

    /// <summary>Las lineas que pueden ser el nombre de una persona, de arriba abajo.</summary>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <param name="arriba">Borde superior del bloque de personas: la línea tiene que empezar en él o más abajo.</param>
    /// <param name="abajo">Borde inferior del bloque: la línea tiene que empezar por encima.</param>
    /// <param name="limiteDerecho">La <c>x</c> de <see cref="LimiteDeLaColumnaDeNombres"/>: la línea tiene que empezar a su izquierda.</param>
    private static IReadOnlyList<LineaDeOcr> LineasDeNombre(
        IReadOnlyList<LineaDeOcr> lineas, double arriba, double abajo, double limiteDerecho)
        => lineas
            .Where(l => l.Banda.Y0 >= arriba && l.Banda.Y0 < abajo && l.Banda.X0 < limiteDerecho)
            .OrderBy(l => l.Banda.Y0)
            .ToArray();

    /// <summary>
    /// Corta la lista en el primer hueco mayor que una fila.
    /// </summary>
    /// <remarks>
    /// Las filas de personas van pegadas una debajo de otra: medido sobre los formularios
    /// de referencia con varias personas, la separacion va de 0 a 7 pixeles sobre filas de
    /// 46 a 53 de alto.
    ///
    /// <para>Debajo de la ultima persona ya no hay personas, pero sigue habiendo texto: el
    /// nombre del templo, la moneda, la tabla de costes. En una pagina mal escaneada la
    /// etiqueta que deberia cerrar el bloque no se lee y todo eso entraba como personas.
    /// Medido: producia 4 y 2 personas en dos paginas que tienen una cada una.</para>
    ///
    /// <para>Se corta, no se filtra por contenido: decidir si «BEM BRASIL» es el nombre de
    /// alguien seria interpretar, y eso no se hace aqui.</para>
    /// </remarks>
    /// <param name="candidatas">Las líneas de nombre ya ordenadas de arriba abajo.</param>
    /// <returns>Desde la primera hasta la última antes de un hueco mayor que el alto de la fila anterior; vacía si no hay candidatas.</returns>
    private static IReadOnlyList<LineaDeOcr> SoloLasFilasSeguidas(IReadOnlyList<LineaDeOcr> candidatas)
    {
        if (candidatas.Count == 0) return [];

        var seguidas = new List<LineaDeOcr> { candidatas[0] };
        for (int i = 1; i < candidatas.Count; i++)
        {
            var anterior = seguidas[^1].Banda;
            double altoDeLaFilaAnterior = anterior.Y1 - anterior.Y0;
            if (candidatas[i].Banda.Y0 - anterior.Y1 > altoDeLaFilaAnterior) break;
            seguidas.Add(candidatas[i]);
        }
        return seguidas;
    }

    /// <summary>
    /// La cedula que comparte fila con el nombre, o un campo vacio si no hay UNA sola.
    /// </summary>
    /// <remarks>
    /// Se miran TODAS las lineas de la fila, tambien la del nombre, y no solo las de la
    /// columna de cedulas. Motivo medido: en uno de los formularios reales el OCR junto el
    /// nombre y la cedula en una sola linea, y buscando solo a la derecha esa cedula se
    /// perdia entera.
    ///
    /// <para>⛔ <b>Pero el valor sale de UNA linea, la que tiene forma de cedula, y no del
    /// renglon junto.</b> Lo segundo es lo que estaba mal y lo que el dueño señalo el
    /// 2026-09-07: medido sobre las 20 hojas reales, en <b>20 de 20</b> filas el valor
    /// propuesto de <c>mrn</c> era el renglon entero —«Wendell Wilfred Rink 000-0000-000A
    /// Verified»—, y en la hoja 5 de los dos <c>SURB2609</c>, donde el OCR leyo un cero como
    /// letra O, el valor de <c>mrn</c> era literalmente <b>el nombre de la persona</b>. Sus
    /// palabras: «siempre debe ser el nombre y debajo la cedula; si no pudo leerla la deja
    /// vacia».</para>
    ///
    /// <para><b>Y si en el renglon hay DOS textos con forma de cedula, no se elige uno.</b>
    /// Quedarse con el de mas a la izquierda seria inventarse de quien es. Pasa cuando el
    /// OCR devuelve una caja que se traga varias filas —medido: hasta 0,043 de alto en las
    /// hojas escaneadas del reves, casi tres renglones—, que es justo el caso en que la
    /// cedula de la fila de al lado se colaria. El campo va vacio, avisado, y con lo leido
    /// entero conservado para poder enseñarlo (requisito 9).</para>
    ///
    /// <para>⚠️ <b>La banda de BUSQUEDA y la de TRAZOS no son la misma, y no pueden serlo.</b>
    /// La de busqueda mide la pagina entera a proposito —ver
    /// <see cref="BandaDeBusquedaDeLaFila"/>—, y preguntarle a ella por el tachon haria que
    /// un trazo sobre las casillas de ordenanza, que estan en esta misma fila a la derecha,
    /// anulara la cedula. Por eso el tachon y la correccion se preguntan sobre la COLUMNA.</para>
    /// </remarks>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <param name="anotaciones">Todas las anotaciones de la página.</param>
    /// <param name="bandaDeBusqueda">La franja de la fila a lo ancho de toda la página: decide qué líneas comparten renglón.</param>
    /// <param name="columnaDeLaCedula">La franja estrecha de la columna: decide tachón y corrección.</param>
    /// <returns>La cédula con su procedencia; vacía y con el renglón entero en <c>ValorOcr</c> si no hay exactamente una con forma.</returns>
    private static CampoExtraido CedulaDeLaFila(
        IReadOnlyList<LineaDeOcr> lineas,
        IReadOnlyList<AnotacionDelPdf> anotaciones,
        BandaDeLaPagina bandaDeBusqueda,
        BandaDeLaPagina columnaDeLaCedula)
    {
        var deLaFila = lineas
            .Where(l => Bandas.LaLineaEsDeLaBanda(l.Banda, bandaDeBusqueda))
            .OrderBy(l => l.Banda.X0)
            .ToArray();

        var conFormaDeCedula = deLaFila
            .Where(l => Normalizacion.NormalizarCedula(l.Texto) is not null)
            .ToArray();

        return Campos.ResolverCampo(
            conFormaDeCedula.Length == 1 ? conFormaDeCedula : [],
            Bandas.CorreccionesEnLaBanda(anotaciones, columnaDeLaCedula),
            Bandas.HayTachonEnLaBanda(anotaciones, columnaDeLaCedula),
            Normalizacion.NormalizarCedula,
            lineasQueNoSonDeEsteCampo: conFormaDeCedula.Length == 1 ? [] : deLaFila);
    }

    /// <summary>
    /// El nombre tal como lo leyo el OCR. No se limpia ni se corrige la ortografia.
    /// </summary>
    /// <remarks>
    /// Pasa por <see cref="Campos.ResolverCampo"/> como todos los demas campos, y no se
    /// construye a mano: <c>Extraccion.Anadir</c> da por hecho que un campo con valor Y con
    /// la marca de tachon es un campo CORREGIDO —y le escribe «alguien escribió el valor
    /// bueno al lado»—, invariante que solo se sostiene si la precedencia la aplica siempre
    /// la misma funcion.
    ///
    /// <para><b>Se le pasa el tachon pero NO las correcciones, y es una decision.</b> Para
    /// la cedula, <see cref="Normalizacion.NormalizarCedula"/> decide si una anotacion es de
    /// verdad una cedula, y una que no lo sea se descarta sola. Para un nombre no hay
    /// ninguna regla de forma que decida eso —cualquier texto tiene forma de nombre—, asi
    /// que CUALQUIER anotacion que cayera en la fila sustituiria en silencio el nombre de la
    /// persona. Es el fallo medido que <c>Campos.MejorCorreccion</c> lleva escrito con el
    /// nombre de la unidad. En los siete escaneos no hay ni un nombre corregido a mano con
    /// el que calibrar esto, asi que queda abierto en `PENDIENTES.md` y no se adivina.</para>
    ///
    /// <para>⛔ <b>Y si la linea trae la cedula pegada, la cedula NO forma parte del
    /// nombre.</b> Es un caso medido —«Ejemplo, Daniel Jr. Damian Dorian |055-1111-3853
    /// Verified √» salio como una sola caja del OCR— y es la otra mitad de lo que el dueño
    /// pidio el 2026-09-07. Quien la quita es <see cref="Normalizacion.NombreSinLaCedula"/>,
    /// que no corrige ni una letra: solo aparta un trozo que se va a su propia columna.</para>
    ///
    /// <para>⚠️ El nombre y la cedula de ese ejemplo van SUSTITUIDOS desde el 2026-09-07:
    /// lo que se midio fue la forma —apellido, coma, «Jr.», dos nombres mas y una cedula
    /// pegada con una barra en medio—, y la forma esta entera. Ver `EN-CURSO.md`, «Los
    /// datos de personas reales salen del repositorio».</para>
    /// </remarks>
    /// <param name="lineaDeNombre">La línea del OCR que hace de fila de esta persona.</param>
    /// <param name="hayTachon">Cierto si un trazo rojo cruza la columna del nombre.</param>
    private static CampoExtraido NombreDeLaFila(LineaDeOcr lineaDeNombre, bool hayTachon)
        => Campos.ResolverCampo(
            [lineaDeNombre],
            [],
            hayTachon,
            Normalizacion.NombreSinLaCedula);

    /// <summary>
    /// La franja donde se busca la cedula de esta fila. NO es lo que se dibuja.
    /// </summary>
    /// <remarks>
    /// Es deliberadamente exagerada de ancha —toda la pagina— porque solo sirve para
    /// preguntar «¿esta linea comparte renglon con esta persona?», y la respuesta la
    /// decide el traslape VERTICAL. Ensancharla no cambia ninguna respuesta y garantiza
    /// que no se escape una cedula escrita muy a la derecha.
    /// </remarks>
    /// <param name="lineaDeNombre">La línea del OCR que hace de fila; solo se usan su <c>Y0</c> y su <c>Y1</c>.</param>
    private static BandaDeLaPagina BandaDeBusquedaDeLaFila(LineaDeOcr lineaDeNombre)
        => new(0.0, lineaDeNombre.Banda.Y0, 1.0, lineaDeNombre.Banda.Y1);

    /// <summary>
    /// La franja de la fila donde un trazo cuenta, estrechada por el factor ya medido.
    /// </summary>
    /// <remarks>
    /// El 0,8 NO se elige aqui: es <see cref="Bandas.FactorDeAltoDeLaBanda"/>, el mismo con
    /// el que se estrechan las bandas de los campos del caso, y existe justo por esto. La
    /// regla de `DECISIONES.md` es que un tachon anula cuando cubre mas del 50% del ALTO DE
    /// LA BANDA, y un trazo de boligrafo es delgado por naturaleza: contra una banda del alto
    /// entero, casi ningun tachon real llega al 50%.
    ///
    /// <para><b>La aritmetica, medida sobre los siete escaneos el 2026-09-06.</b> Las filas
    /// de personas miden de 0,0137 a 0,0180 de alto —media 0,0160—. De los 29 trazos rojos de
    /// los siete, 22 son finos: de 0,0062 a 0,0093 de alto, con mediana 0,0078. Contra la fila
    /// entera ese trazo mediano da <b>0,486</b> y NO anularia —la regla pide superar el 50%—;
    /// contra la fila por 0,8 da <b>0,608</b> y si. Es la misma franja de 0,52 a 0,72 con la
    /// que los campos del caso ya anulan hoy.</para>
    ///
    /// <para>⚠️ <b>Es un margen estrecho y hay que decirlo:</b> ninguno de los siete tiene un
    /// tachon sobre una cedula, asi que estos numeros salen de trazos medidos en OTRAS filas
    /// del mismo papel. Un trazo mas fino que la mediana sobre una fila alta —0,0062 sobre
    /// 0,0180— da 0,431 y se escaparia. Bajar el corte seria inventarse un umbral; lo que hace
    /// falta es un escaneo real con la cedula tachada, y hoy no existe.</para>
    ///
    /// <para>Se centra en la fila, y no cuelga de arriba como <see cref="Bandas.BandaDeValor"/>,
    /// porque alli la banda cuelga de un ROTULO y el valor esta debajo, mientras que aqui la
    /// fila ya es el texto y el trazo lo cruza por el medio.</para>
    /// </remarks>
    /// <param name="fila">La caja de la fila; de ella salen el centro y el alto.</param>
    /// <param name="x0">Borde izquierdo de la franja, en fracción de página.</param>
    /// <param name="x1">Borde derecho de la franja.</param>
    /// <returns>Una banda centrada en la fila, del 80 % de su alto, entre <paramref name="x0"/> y <paramref name="x1"/>.</returns>
    internal static BandaDeLaPagina FranjaDeTrazos(BandaDeLaPagina fila, double x0, double x1)
    {
        double centro = (fila.Y0 + fila.Y1) / 2.0;
        double mitadDelAlto = (fila.Y1 - fila.Y0) * Bandas.FactorDeAltoDeLaBanda / 2.0;
        return new BandaDeLaPagina(x0, centro - mitadDelAlto, x1, centro + mitadDelAlto);
    }

    /// <summary>La columna del NOMBRE en esta fila: exactamente donde el nombre esta escrito.</summary>
    /// <remarks>
    /// Se usa el rectangulo del propio renglon y no «desde el margen hasta
    /// <see cref="LimiteDeLaColumnaDeNombres"/>» porque hay nombres que se pasan del limite:
    /// medido, un nombre de la forma «Ejemplo, Daniel Jr. Damian Dorian» —cuatro
    /// componentes y una coma— llega a x 0,406 con el limite en 0,3455. El nombre concreto
    /// que se midio va sustituido; lo medido fue el ancho de esa forma.
    /// </remarks>
    /// <param name="lineaDeNombre">La línea del OCR que hace de fila de esta persona.</param>
    private static BandaDeLaPagina ColumnaDelNombre(LineaDeOcr lineaDeNombre)
        => FranjaDeTrazos(lineaDeNombre.Banda, lineaDeNombre.Banda.X0, lineaDeNombre.Banda.X1);

    /// <summary>
    /// La columna de la CEDULA en esta fila, sin llegar a las casillas de ordenanza.
    /// </summary>
    /// <remarks>
    /// <b>Por la derecha</b> acaba en el borde del rotulo impreso «Membership Record Number»
    /// de ESTA pagina, asi que se mueve con el escaneo en vez de ser una coordenada fija.
    /// Medido en los siete: el rotulo acaba en x 0,6536, el texto de la cedula acaba como
    /// mucho en x 0,625, y las marcas de ordenanza que el OCR devuelve empiezan en x 0,775.
    /// El corte separa las dos cosas con holgura por los dos lados. Sin el, tachar una
    /// ordenanza mal marcada —que es lo que alguien hace con un boligrafo rojo— borraria la
    /// cedula, que es la mitad de la clave con la que se reconcilia todo el programa.
    ///
    /// <para><b>Por la izquierda</b> empieza en el limite entre columnas, o donde acabe el
    /// nombre si el nombre se paso de largo. Asi las dos columnas nunca se solapan y un mismo
    /// trazo no marca los dos campos.</para>
    ///
    /// <para>⚠️ Si un nombre fuera tan largo que pasara del rotulo de las cedulas, la franja
    /// saldria vacia y no se detectaria ningun tachon. Es el lado seguro del error —no marca
    /// de mas— y no se ha visto en ningun escaneo, pero queda dicho.</para>
    /// </remarks>
    /// <param name="lineaDeNombre">La línea del OCR que hace de fila de esta persona.</param>
    /// <param name="anclaCedula">La caja de la cabecera de cédulas: su borde derecho cierra la columna.</param>
    /// <param name="limiteDeLosNombres">La <c>x</c> de <see cref="LimiteDeLaColumnaDeNombres"/>.</param>
    private static BandaDeLaPagina ColumnaDeLaCedula(
        LineaDeOcr lineaDeNombre, BandaDeLaPagina anclaCedula, double limiteDeLosNombres)
        => FranjaDeTrazos(
            lineaDeNombre.Banda,
            Math.Max(limiteDeLosNombres, lineaDeNombre.Banda.X1),
            anclaCedula.X1);

    /// <summary>
    /// El trozo del escaneo que se ensena de esta fila. Va del nombre a la cedula.
    /// </summary>
    /// <remarks>
    /// Va aparte de la banda de busqueda por una razon concreta: aquella mide la pagina
    /// entera a proposito, y recortar la imagen con ella daria una tira casi toda en
    /// blanco con el dato perdido en la esquina.
    ///
    /// <para>El respiro sale del alto de la propia fila, que es lo que escala con el
    /// tamano del escaneo; un numero fijo valdria para una resolucion y no para otra.</para>
    /// </remarks>
    /// <param name="lineaDeNombre">La línea del OCR que hace de fila de esta persona.</param>
    /// <param name="anclaCedula">La caja de la cabecera de cédulas, para llegar hasta su columna.</param>
    /// <param name="relacionDeAspecto">Ancho partido por alto de la página, para que el respiro horizontal mida lo mismo que el alto de la fila.</param>
    /// <returns>Una banda de la altura de la fila, del nombre a la cédula con un respiro a cada lado, recortada a la página.</returns>
    private static BandaDeLaPagina BandaVisibleDeLaFila(
        LineaDeOcr lineaDeNombre, BandaDeLaPagina anclaCedula, double relacionDeAspecto)
    {
        var fila = lineaDeNombre.Banda;
        double altoDeLaFila = fila.Y1 - fila.Y0;
        double respiro = relacionDeAspecto <= 0.0 ? altoDeLaFila : altoDeLaFila / relacionDeAspecto;

        return new BandaDeLaPagina(
            X0: Math.Max(0.0, Math.Min(fila.X0, anclaCedula.X0) - respiro),
            Y0: fila.Y0,
            X1: Math.Min(1.0, Math.Max(fila.X1, anclaCedula.X1) + respiro),
            Y1: fila.Y1);
    }

    /// <summary>
    /// Las personas de la pagina, numeradas por su fila, sin las filas vacias.
    /// </summary>
    /// <remarks>
    /// Sin las dos cabeceras, o sin ninguna etiqueta que cierre el bloque por abajo,
    /// devuelve cero personas. Es a proposito: de esa pagina no se sabe donde empieza ni
    /// donde acaba la lista, y adivinarlo produce personas que no existen. El numero de
    /// filas descartadas se informa: una fila que se descarta en silencio es una persona
    /// que puede haberse perdido.
    ///
    /// <para><b>Dos clases de documento, y se decide aqui cual es</b> (2026-09-10). Si el
    /// bloque trae campos de texto del formulario con algo tecleado, la hoja es un formulario
    /// rellenado a maquina y las personas salen de esos campos, por su rectangulo; las lineas
    /// del OCR de esas filas son la pintura de los mismos campos y no se usan, para no contar
    /// a cada persona dos veces. Si los campos estan todos vacios —o no hay ninguno, que es
    /// el caso de los escaneos—, se sigue por el OCR como hasta ahora.</para>
    /// </remarks>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <param name="anotaciones">Todas las anotaciones de la página: tachones, correcciones y campos del formulario.</param>
    /// <param name="anclaNombres">La caja de la cabecera de nombres, o nula si no se encontró.</param>
    /// <param name="anclaCedula">La caja de la cabecera de cédulas, o nula si no se encontró.</param>
    /// <param name="cierresDelBloque">Las cajas de las etiquetas que cierran el bloque por abajo, nulas las que faltan.</param>
    /// <param name="relacionDeAspecto">Ancho partido por alto de la página, para la banda que se enseña.</param>
    /// <returns>Las personas con algo leído y cuántas filas seguidas se descartaron por venir en blanco; sin cabeceras o sin cierre, ninguna y cero.</returns>
    public static (IReadOnlyList<PersonaExtraida> Personas, int Descartadas) Extraer(
        IReadOnlyList<LineaDeOcr> lineas,
        IReadOnlyList<AnotacionDelPdf> anotaciones,
        BandaDeLaPagina? anclaNombres,
        BandaDeLaPagina? anclaCedula,
        IReadOnlyList<BandaDeLaPagina?> cierresDelBloque,
        double relacionDeAspecto)
    {
        if (anclaNombres is null || anclaCedula is null) return ([], 0);

        double limite = LimiteDeLaColumnaDeNombres(anclaNombres.Value, anclaCedula.Value);
        var (arriba, abajo) = BloqueDePersonas(anclaNombres.Value, anclaCedula.Value, cierresDelBloque);
        if (abajo is null) return ([], 0);

        var filasDelFormulario = PersonasDelFormulario.FilasDeCampos(anotaciones, arriba, abajo.Value);
        if (filasDelFormulario.Any(fila => fila.Any(Anotaciones.EsCampoTecleado)))
        {
            return (PersonasDelFormulario.Extraer(filasDelFormulario, anotaciones, anclaCedula.Value, limite), 0);
        }

        var candidatas = SoloLasFilasSeguidas(LineasDeNombre(lineas, arriba, abajo.Value, limite));

        var personas = new List<PersonaExtraida>();
        int descartadas = 0;
        int numeroDeFila = 0;
        foreach (var lineaDeNombre in candidatas)
        {
            numeroDeFila++;
            var nombre = NombreDeLaFila(
                lineaDeNombre, Bandas.HayTachonEnLaBanda(anotaciones, ColumnaDelNombre(lineaDeNombre)));
            var cedula = CedulaDeLaFila(
                lineas,
                anotaciones,
                BandaDeBusquedaDeLaFila(lineaDeNombre),
                ColumnaDeLaCedula(lineaDeNombre, anclaCedula.Value, limite));

            // Una fila sin NADA leido ni en el nombre ni en la cedula no es una persona.
            // Ojo: lo que se leyo pero no encaja SI cuenta, porque su texto vive en
            // `ValorOcr` y perderlo seria vaciarlo en silencio (requisito 9). Desde que el
            // nombre pasa por la precedencia, un nombre TACHADO tambien sale con origen
            // vacio y su texto en `ValorOcr`: mirar solo el origen descartaria como «fila en
            // blanco» justo la fila que alguien marco a mano.
            bool nombreSinRastro = nombre.Origen == OrigenDeCampo.Vacio && nombre.ValorOcr is null;
            bool cedulaSinRastro = cedula.Origen == OrigenDeCampo.Vacio && cedula.ValorOcr is null;
            if (nombreSinRastro && cedulaSinRastro)
            {
                descartadas++;
                continue;
            }

            personas.Add(new PersonaExtraida(
                FilaFormulario: numeroDeFila,
                Nombre: nombre,
                Cedula: cedula,
                Banda: BandaVisibleDeLaFila(lineaDeNombre, anclaCedula.Value, relacionDeAspecto)));
        }
        return (personas, descartadas);
    }
}
