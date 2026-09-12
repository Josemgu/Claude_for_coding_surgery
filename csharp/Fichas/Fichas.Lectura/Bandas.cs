using Fichas.Contratos.Lectura;

namespace Fichas.Lectura;

/// <summary>
/// Localizar una banda del formulario por su ANCLA de texto, nunca por coordenada.
/// </summary>
/// <remarks>
/// Portado de `extraccion/bandas.py`. Un escaneo nunca cae dos veces en el mismo sitio:
/// los margenes del escaner mueven todo unos milimetros, y una coordenada fija acaba
/// capturando el encabezado en vez del dato. Cada campo se encuentra por la etiqueta
/// impresa que lo nombra, y el valor se busca en la fila de debajo.
///
/// <para>El OCR se equivoca al leer esas etiquetas, y esta medido: sobre los cuatro
/// documentos de referencia salieron «Ward/Branch Name and Upjt Number» y «Date
/// traveling home fror the temple». Por eso el ancla se busca por PARECIDO y no por
/// igualdad. Y por eso hay una segunda regla, que es la que de verdad protege:</para>
///
/// <para><b>un ancla solo vale si se parece a SU etiqueta mas que a cualquier otra
/// etiqueta conocida del formulario.</b></para>
///
/// <para>Sin esa regla, «Date traveling home fror the temple» se parecia 0,77 a «Date
/// traveling to the temple» y el sistema habria leido la fecha de vuelta como fecha de
/// ida. Es el error que manda a alguien al templo el dia equivocado.</para>
/// </remarks>
public static class Bandas
{
    /// <summary>
    /// Parecido minimo para aceptar un ancla.
    /// </summary>
    /// <remarks>
    /// Medido sobre las 9 paginas reales: las anclas correctas dieron entre 0,94 y 1,00,
    /// y la unica confusion peligrosa dio 0,77. El corte en 0,85 las separa con holgura
    /// por los dos lados.
    /// </remarks>
    public const double ParecidoMinimoDelAncla = 0.85;

    /// <summary>
    /// Alto de la fila de valor, en multiplos del alto del ancla.
    /// </summary>
    /// <remarks>
    /// Medido: con 0,8 los cinco tachones rojos que caen sobre una banda de interes la
    /// anulan con fracciones de 0,52 a 0,72, y todos los valores del OCR siguen dentro
    /// con 0,82 a 1,00. Con 1,2 el tachon de la fecha baja a 0,43 y DEJA DE ANULAR: el
    /// sistema se quedaria con la fecha tachada. El numero no se eligio a ojo.
    /// </remarks>
    public const double FactorDeAltoDeLaBanda = 0.8;

    /// <summary>Cuanto se ensancha la banda a la derecha del ancla, en multiplos de su alto.</summary>
    public const double FactorDeAnchoALaDerecha = 6.0;

    /// <summary>Cuanto se ensancha a la izquierda: el valor a veces empieza antes que su rotulo.</summary>
    public const double FactorDeAnchoALaIzquierda = 0.5;

    /// <summary>Cuantas ediciones de un caracter separan dos cadenas.</summary>
    /// <remarks>
    /// Levenshtein con dos filas en vez de la matriz entera: las etiquetas tienen menos de
    /// 50 caracteres y se compara cada línea del OCR contra 18 formas, así que la memoria
    /// importa más que la claridad de la matriz.
    /// </remarks>
    /// <param name="cadenaA">Una cadena ya normalizada para comparar.</param>
    /// <param name="cadenaB">La otra; el orden no cambia el resultado.</param>
    /// <returns>Cuántas inserciones, borrados o sustituciones de un carácter convierten una en la otra.</returns>
    private static int DistanciaDeEdicion(string cadenaA, string cadenaB)
    {
        var filaPrevia = new int[cadenaB.Length + 1];
        var fila = new int[cadenaB.Length + 1];
        for (int i = 0; i <= cadenaB.Length; i++) filaPrevia[i] = i;

        for (int indiceA = 1; indiceA <= cadenaA.Length; indiceA++)
        {
            fila[0] = indiceA;
            for (int indiceB = 1; indiceB <= cadenaB.Length; indiceB++)
            {
                int coste = cadenaA[indiceA - 1] == cadenaB[indiceB - 1] ? 0 : 1;
                fila[indiceB] = Math.Min(
                    Math.Min(filaPrevia[indiceB] + 1, fila[indiceB - 1] + 1),
                    filaPrevia[indiceB - 1] + coste);
            }
            (filaPrevia, fila) = (fila, filaPrevia);
        }
        return filaPrevia[cadenaB.Length];
    }

    /// <summary>
    /// Entre 0,0 y 1,0. No distingue mayusculas ni tildes: el OCR las confunde.
    /// </summary>
    /// <remarks>
    /// Las tildes se ignoran SOLO aqui, para comparar. Lo que se guarda conserva las
    /// suyas: <see cref="Etiquetas.NormalizarParaComparar"/> lleva escrito el motivo.
    /// </remarks>
    /// <param name="cadenaA">Normalmente lo que leyó el OCR; nulo cuenta como vacío.</param>
    /// <param name="cadenaB">Normalmente la etiqueta impresa que se busca; nulo cuenta como vacío.</param>
    /// <returns>1,0 si son iguales (o las dos vacías), 0,0 si solo una está vacía, y en medio la distancia de edición partida por la más larga.</returns>
    public static double Parecido(string? cadenaA, string? cadenaB)
    {
        string izquierda = Etiquetas.NormalizarParaComparar(cadenaA);
        string derecha = Etiquetas.NormalizarParaComparar(cadenaB);

        if (izquierda.Length == 0 && derecha.Length == 0) return 1.0;
        if (izquierda.Length == 0 || derecha.Length == 0) return 0.0;

        return 1.0 - (double)DistanciaDeEdicion(izquierda, derecha) / Math.Max(izquierda.Length, derecha.Length);
    }

    /// <summary>
    /// La linea del OCR que es la etiqueta de ese campo, o nula si no hay ninguna clara.
    /// </summary>
    /// <remarks>
    /// Se prueban las dos formas del campo —la inglesa y la espanola— y gana la que mas
    /// se parezca. Las de los demas campos hacen de RIVALES: una linea que se parezca
    /// mas a una rival que a la pedida se descarta aunque supere el minimo, porque es la
    /// otra etiqueta mal leida.
    /// </remarks>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <param name="campo">Una de las claves <c>Etiquetas.CampoDe…</c>.</param>
    /// <param name="parecidoMinimo">Por debajo de esto no hay ancla; las pruebas lo bajan para medir el cruce entre idiomas.</param>
    /// <returns>La línea que mejor se parece, o nula si ninguna llega al mínimo o la mejor se parece más a una rival.</returns>
    public static LineaDeOcr? LocalizarAncla(
        IReadOnlyList<LineaDeOcr> lineas, string campo, double parecidoMinimo = ParecidoMinimoDelAncla)
    {
        var propias = Etiquetas.FormasDe(campo);
        LineaDeOcr? mejorLinea = null;
        double mejorParecido = 0.0;

        foreach (var linea in lineas)
        {
            double parecidoActual = 0.0;
            foreach (var forma in propias)
            {
                double p = Parecido(linea.Texto, forma);
                if (p > parecidoActual) parecidoActual = p;
            }
            if (parecidoActual > mejorParecido)
            {
                mejorLinea = linea;
                mejorParecido = parecidoActual;
            }
        }

        if (mejorLinea is null || mejorParecido < parecidoMinimo) return null;

        foreach (var otro in Etiquetas.CamposDelFormulario)
        {
            if (otro == campo) continue;
            foreach (var forma in Etiquetas.FormasDe(otro))
            {
                if (Parecido(mejorLinea.Texto, forma) > mejorParecido) return null;
            }
        }
        return mejorLinea;
    }

    /// <summary>Las anclas de los nueve campos del formulario; nula la que no aparecio.</summary>
    /// <remarks>
    /// Se buscan TODOS y no solo los que se extraen: los que no se extraen —la estaca, la
    /// fecha de regreso— hacen falta igual como rivales, y saber si aparecieron es lo que
    /// permite decir por que fallo una pagina.
    /// </remarks>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <returns>Un diccionario con las nueve claves siempre presentes; el valor es nulo donde no hubo ancla.</returns>
    public static IReadOnlyDictionary<string, LineaDeOcr?> LocalizarLasAnclas(IReadOnlyList<LineaDeOcr> lineas)
        => Etiquetas.CamposDelFormulario.ToDictionary(campo => campo, campo => LocalizarAncla(lineas, campo));

    /// <summary>
    /// La fila donde vive el valor: justo debajo del ancla, y de su mismo alto.
    /// </summary>
    /// <param name="ancla">El rectangulo de la etiqueta, en fracciones de pagina.</param>
    /// <param name="relacionDeAspecto">
    /// Ancho partido por alto de la pagina, en puntos. Hace falta porque la banda se
    /// ensancha HORIZONTALMENTE en multiplos del alto del ancla, y una fraccion de ancho
    /// y una de alto no miden lo mismo cuando la pagina no es cuadrada: en una carta de
    /// 612x792 el factor entre ejes es 1,29, asi que ignorarlo estrecharia la banda un
    /// 23% y podria dejar fuera un valor escrito muy a la derecha.
    /// </param>
    /// <returns>La banda de valor en fracciones de página; con relación de aspecto cero o negativa se ensancha sin corregir, no lanza.</returns>
    public static BandaDeLaPagina BandaDeValor(BandaDeLaPagina ancla, double relacionDeAspecto)
    {
        double altoDelAncla = ancla.Y1 - ancla.Y0;
        double altoEnAnchoDePagina = relacionDeAspecto <= 0.0 ? altoDelAncla : altoDelAncla / relacionDeAspecto;

        return new BandaDeLaPagina(
            X0: ancla.X0 - altoEnAnchoDePagina * FactorDeAnchoALaIzquierda,
            Y0: ancla.Y1,
            X1: ancla.X1 + altoEnAnchoDePagina * FactorDeAnchoALaDerecha,
            Y1: ancla.Y1 + altoDelAncla * FactorDeAltoDeLaBanda);
    }

    /// <summary>
    /// Cierto cuando el rectangulo cae en la banda por vertical y por horizontal.
    /// </summary>
    /// <remarks>
    /// La comprobacion horizontal no es un adorno: el formulario tiene dos columnas, y
    /// sin ella un tachon de la columna derecha anularia el campo de la izquierda solo
    /// por estar a su misma altura.
    /// </remarks>
    /// <param name="rectangulo">La caja de una línea del OCR o de una anotación.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    /// <param name="fraccionMinima">Qué parte del alto de la banda tiene que cubrir el rectángulo; se exige más que esto, no igual.</param>
    public static bool EstaEnLaBanda(BandaDeLaPagina rectangulo, BandaDeLaPagina banda, double fraccionMinima = 0.5)
        => Geometria.FraccionDeTraslapeVertical(rectangulo, banda) > fraccionMinima
           && Geometria.SeSolapanEnHorizontal(rectangulo, banda);

    /// <summary>
    /// Cuanto de una linea del OCR tiene que caber en la banda para que su texto sea de ahi.
    /// </summary>
    /// <remarks>
    /// <para><b>El corte no se eligio a ojo</b> (2026-09-07, sobre las 20 hojas reales de
    /// esta maquina, en las bandas de los tres campos del caso que se extraen): las lineas
    /// que de verdad son de esa fila del papel dan de <b>0,588 a 1,000</b>, y las que son de
    /// otra fila dan de <b>0,354 a 0,481</b>. El 0,5 cae en medio del hueco, con 0,019 de
    /// holgura por abajo y 0,088 por arriba. En las filas de personas la linea mas baja de
    /// las buenas dio 0,627, asi que el mismo corte tampoco se come ninguna.</para>
    /// </remarks>
    public const double FraccionMinimaDeLaLineaDentroDeLaBanda = 0.5;

    /// <summary>
    /// Cierto cuando la linea del OCR es de ESTA fila del papel y no de la de al lado.
    /// </summary>
    /// <remarks>
    /// Son dos preguntas y hacen falta las dos. La primera —la de siempre— es si la linea
    /// cubre la banda; sin ella entraria cualquier cosa de la pagina. La segunda es si la
    /// linea CABE en la banda, y es la que se anadio el 2026-09-07: una caja del OCR que se
    /// tragó tres renglones cubre el 100% de todas las bandas que cruza, asi que sin esta
    /// segunda pregunta su texto se le daba al campo de cada una de esas filas —con la
    /// confianza del OCR delante, como si la lectura fuera suya—.
    ///
    /// <para><b>Medido en las hojas 5 y 6 de los dos <c>SURB2609</c></b>, que son escaneos
    /// del reves: «ceniceq t zcench:» (0,354 dentro) y «WANLCA Bzeench» (0,469) salian como
    /// nombre Y como numero de unidad, y «Currency:» (0,481) se metia dentro de la fecha de
    /// viaje. Es el cruce que el dueño enseñó el 2026-09-07.</para>
    ///
    /// <para>⚠️ <b>Vale para lineas del OCR, NO para anotaciones.</b> El rectangulo de una
    /// linea ES el texto que se leyo, asi que su alto significa algo. El <c>/Rect</c> de una
    /// <c>/FreeText</c> es una caja que alguien dibujo a mano y puede ser mucho mas alta que
    /// el renglon que corrige: medir las anotaciones con esta regla tiraria las siete
    /// correcciones de fecha de los escaneos del dueño, que son las que evitan que alguien
    /// viaje el dia equivocado. Por eso <see cref="CorreccionesEnLaBanda"/> y
    /// <see cref="HayTachonEnLaBanda"/> siguen preguntando solo lo primero.</para>
    /// </remarks>
    /// <param name="linea">La caja de una línea del OCR, en fracciones de página.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    public static bool LaLineaEsDeLaBanda(BandaDeLaPagina linea, BandaDeLaPagina banda)
        => EstaEnLaBanda(linea, banda)
           && Geometria.FraccionDelRectanguloDentroDeLaBanda(linea, banda) > FraccionMinimaDeLaLineaDentroDeLaBanda;

    /// <summary>Las lineas del OCR que pertenecen a la banda, de izquierda a derecha.</summary>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    /// <returns>Las que cubren la banda y caben en ella, ordenadas por su borde izquierdo; vacía si ninguna.</returns>
    public static IReadOnlyList<LineaDeOcr> LineasEnLaBanda(IReadOnlyList<LineaDeOcr> lineas, BandaDeLaPagina banda)
        => lineas.Where(linea => LaLineaEsDeLaBanda(linea.Banda, banda))
                 .OrderBy(linea => linea.Banda.X0)
                 .ToArray();

    /// <summary>
    /// Las lineas que cruzan la banda pero no caben en ella: lo que se leyo cerca y no es suyo.
    /// </summary>
    /// <remarks>
    /// No se tiran, y ese es todo el motivo de que esta funcion exista. El requisito 9 dice
    /// que lo que el papel decia se ENSEÑA; lo que no se puede hacer es darlo por el valor
    /// del campo. Su texto viaja a <c>procedencia_campo.valor_ocr</c> y al aviso, con el
    /// campo vacio.
    /// </remarks>
    /// <param name="lineas">Todas las líneas del OCR de la página.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    /// <returns>Las que cubren la banda pero no caben en ella, ordenadas por su borde izquierdo; vacía si ninguna.</returns>
    public static IReadOnlyList<LineaDeOcr> LineasQueRozanLaBanda(
        IReadOnlyList<LineaDeOcr> lineas, BandaDeLaPagina banda)
        => lineas.Where(linea => EstaEnLaBanda(linea.Banda, banda) && !LaLineaEsDeLaBanda(linea.Banda, banda))
                 .OrderBy(linea => linea.Banda.X0)
                 .ToArray();

    /// <summary>Las correcciones escritas que caen en la banda.</summary>
    /// <remarks>
    /// Vive aqui, y no en cada sitio que las necesita, porque los campos del caso y los de
    /// las personas tienen que preguntarlo IGUAL. Cuando cada uno lo escribia por su cuenta,
    /// las personas se quedaron sin preguntarlo durante toda una fase.
    /// </remarks>
    /// <param name="anotaciones">Todas las anotaciones de la página, de cualquier subtipo.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    /// <returns>Las <c>/FreeText</c> y los campos tecleados con texto que cubren la banda, en el orden del PDF.</returns>
    public static IReadOnlyList<AnotacionDelPdf> CorreccionesEnLaBanda(
        IReadOnlyList<AnotacionDelPdf> anotaciones, BandaDeLaPagina banda)
        => anotaciones.Where(a => Anotaciones.EsCorreccionEscrita(a) && EstaEnLaBanda(a.Banda, banda)).ToArray();

    /// <summary>Cierto cuando un trazo rojo cruza la banda y por lo tanto la anula.</summary>
    /// <param name="anotaciones">Todas las anotaciones de la página, de cualquier subtipo.</param>
    /// <param name="banda">La fila de valor que cuelga del ancla.</param>
    public static bool HayTachonEnLaBanda(IReadOnlyList<AnotacionDelPdf> anotaciones, BandaDeLaPagina banda)
        => anotaciones.Any(a => Anotaciones.EsTachon(a) && EstaEnLaBanda(a.Banda, banda));
}
