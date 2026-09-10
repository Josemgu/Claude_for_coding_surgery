namespace Fichas.App.Correccion;

/// <summary>
/// Quien entra en Correccion y quien sale: un documento entra cuando le falta algo y sale
/// solo cuando deja de faltarle.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> Palabras del dueno el 2026-09-07 (<c>DECISIONES.md</c>, «EL
/// DUENO DICTA EL FLUJO ENTERO», apartado 4): <i>«Lo que está listo para asignar no puede
/// mezclarse con lo que se debe organizar. […] Cuando yo lo corrija y le ponga la información,
/// debe salir de Corrección y pasar al grupo de su fecha. Y si voy a Corrección no debe estar
/// ahí, porque ya está todo listo, toda la información está correcta»</i>.</para>
///
/// <para><b>Lo que habia, medido antes de este pase.</b>
/// <c>PaginaDeCorreccion.LlenarLosGrupos</c> armaba los grupos con TODAS las tarjetas no
/// archivadas de <c>TableroDeRevisar.Cargadas</c> y no preguntaba ni una vez si a alguna le
/// faltaba algo. Corregir el ultimo campo de un documento le cambiaba la frase del renglon y
/// nada mas: seguia en la lista, mezclado con lo que ya estaba listo.</para>
///
/// <para>⛔ <b>El veredicto NO se decide aqui y no se copia.</b> Llega como una funcion, y
/// quien la pasa la saca de <see cref="LoQueLeFaltaACadaDocumento.LeFaltaAlgo"/>, que es el
/// mismo <c>LoQueLeFalta.EstaListo</c> de la cola de Completar y de la pantalla del grupo.
/// Escribir un segundo criterio volveria a abrir el agujero que <c>DECISIONES.md</c> midio el
/// 2026-09-06: seis documentos que se leian al reves segun la pantalla, y uno que se quedaba
/// fuera de todas las listas.</para>
///
/// <para>⚠️ <b>«Sale de Correccion» NO puede querer decir «desaparece».</b> Un documento
/// resuelto tiene por fuerza su fecha de viaje —si le faltara, <c>LoQueLeFalta</c> la contaria y
/// seguiria aqui—, asi que el grupo de su dia lo tiene. No hay un tercer sitio donde caerse, y
/// eso lo fija
/// <c>PruebasDeQueCorreccionEsUnSitioDePaso.NingunDocumentoSeQuedaFueraDeLasDosListas</c>.</para>
///
/// <para>⛔ De aqui no sale ni una escritura: es un filtro sobre lo ya leido. La regla
/// permanente 5 sigue entera.</para>
/// </remarks>
public static class QueEntraEnCorreccion
{
    /// <summary>
    /// Deja en cada grupo solo los documentos a los que les falta algo, y tira los grupos que
    /// se quedan sin ninguno.
    /// </summary>
    /// <remarks>
    /// <para>Se filtra DENTRO de los grupos ya armados en vez de agrupar solo lo que falta, y
    /// es a proposito: el orden y los nombres de los grupos salen del arbol de Revisar
    /// (<see cref="GruposParaCorregir"/>), asi que el «Grupo del 12 de septiembre · 325535» de
    /// esta pantalla es el mismo que el de las otras aunque aqui traiga menos documentos
    /// dentro.</para>
    ///
    /// <para>⚠️ <b>El documento que se esta mirando entra aunque este resuelto</b>, y sin eso el
    /// arreglo seria peor que el defecto: al guardar el ultimo dato, el documento se caeria del
    /// desplegable en el acto y el dueno se encontraria OTRO documento delante sin haber pedido
    /// cambiar. Se queda mientras lo tiene abierto —y la pantalla lo dice— y no vuelve a la
    /// lista la proxima vez que se entra, que es lo que el pidio.</para>
    /// </remarks>
    /// <param name="grupos">Los grupos ya armados, con todos sus documentos.</param>
    /// <param name="leFaltaAlgo">Si a ese documento le queda algo que hacer.</param>
    /// <param name="elQueSeEstaMirando">
    /// El documento abierto ahora mismo, que entra aunque este resuelto; 0 si no hay ninguno.
    /// </param>
    public static IReadOnlyList<GrupoParaCorregir> Filtrar(
        IReadOnlyList<GrupoParaCorregir> grupos,
        Func<long, bool> leFaltaAlgo,
        long elQueSeEstaMirando = 0)
    {
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(leFaltaAlgo);

        var deTrabajo = new List<GrupoParaCorregir>(grupos.Count);
        foreach (var grupo in grupos)
        {
            var suyos = grupo.Documentos
                .Where(documento => documento.CasoId == elQueSeEstaMirando
                                    || !SaleDeCorreccion(grupo, leFaltaAlgo(documento.CasoId)))
                .ToList();

            if (suyos.Count > 0) deTrabajo.Add(grupo with { Documentos = suyos });
        }

        return deTrabajo;
    }

    /// <summary>
    /// Si un documento de ese grupo puede salir de Correccion: cuando no le falta nada
    /// <b>y</b> hay un grupo de fecha que lo reciba.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>La segunda mitad NO es un veredicto nuevo, es la puerta de salida.</b> «Le
    /// falta algo» lo sigue contestando <see cref="LoQueLeFalta"/> y nadie mas. Lo que se anade
    /// es que no se sale por una puerta que no existe: un documento sin fecha de viaje no tiene
    /// grupo de dia al que pasar, asi que sacarlo de Correccion seria hacerlo desaparecer.</para>
    ///
    /// <para><b>Y no es un caso teorico: esta medido.</b> En la base inventada de 300
    /// documentos, el caso 202 —<c>CASD2608</c>— tiene la fecha de viaje VACIA y aun asi
    /// <c>LoQueLeFalta.EstaListo</c> dice que si, porque alguien marco esa fecha como que <b>no
    /// esta en el papel</b> y ahi ya no queda nada que buscar (ADR de la migracion 14). Sin esta
    /// regla, ese documento se caia de Correccion y no aparecia en ningun grupo de dia:
    /// desaparecia del programa entero. Lo encontro
    /// <c>PruebasDeQueCorreccionEsUnSitioDePaso.NingunDocumentoSeQuedaFueraDeLasDosListas</c> la
    /// primera vez que se ejecuto.</para>
    ///
    /// <para>⚠️ <b>Lo que esto deja abierto, y hay que decirlo:</b> un documento cuya fecha se
    /// marco como ausente del papel se queda en Correccion para siempre, en el grupo «sin fecha
    /// de viaje», hasta que el dueno le ponga una fecha a mano o lo archive. No es un almacen
    /// —ese grupo PIDE que lo miren, con las palabras del dueno— pero es una decision suya que
    /// nadie le ha preguntado todavia. Va nombrada en la entrega.</para>
    /// </remarks>
    /// <param name="grupo">El grupo en el que esta el documento.</param>
    /// <param name="leFaltaAlgo">Si a ese documento le queda algo que hacer.</param>
    public static bool SaleDeCorreccion(GrupoParaCorregir grupo, bool leFaltaAlgo)
    {
        ArgumentNullException.ThrowIfNull(grupo);
        return !leFaltaAlgo && !grupo.HayQueRevisarlo;
    }

    /// <summary>Lo mismo, buscando primero en que grupo esta ese documento.</summary>
    /// <param name="todosLosGrupos">Los grupos sin filtrar.</param>
    /// <param name="casoId">El documento.</param>
    /// <param name="leFaltaAlgo">Si a ese documento le queda algo que hacer.</param>
    public static bool SaleDeCorreccion(
        IReadOnlyList<GrupoParaCorregir> todosLosGrupos, long casoId, bool leFaltaAlgo)
    {
        ArgumentNullException.ThrowIfNull(todosLosGrupos);

        var donde = GruposParaCorregir.DondeEsta(todosLosGrupos, casoId);

        // Un documento que no esta en ningun grupo esta archivado, y un archivado ya salio de
        // todas las listas por su cuenta: no hay nada que anunciar de el.
        return donde >= 0 && SaleDeCorreccion(todosLosGrupos[donde], leFaltaAlgo);
    }

    /// <summary>
    /// De los que estan en la lista, cuantos siguen pidiendo algo: el invitado NO cuenta.
    /// </summary>
    /// <remarks>
    /// <para>Hace falta como DENOMINADOR (CLAUDE.md §8): «8 documentos en este grupo» no dice
    /// si queda algo por ver sin la cifra de todo lo que hay que corregir.</para>
    ///
    /// <para>⛔ <b>Y tiene que ser UNA sola cuenta para las dos cifras de la pantalla.</b>
    /// Medido con la ventana abierta el 2026-09-09: tras corregir el ultimo dato, la cabecera
    /// decia «2 con algo que falta, de 6» y el pie «queda 1 documento con algo que falta». Dos
    /// cifras de la misma pantalla que no encajan, y ninguna forma de saber cual creer. La
    /// diferencia era el invitado —el documento que acaba de resolverse y sigue delante—, y por
    /// eso se cuenta con el mismo <see cref="SaleDeCorreccion"/> que decide quien entra.</para>
    /// </remarks>
    /// <param name="grupos">Los grupos ya filtrados.</param>
    /// <param name="leFaltaAlgo">Si a ese documento le queda algo que hacer.</param>
    public static int CuantosPidenAlgo(
        IReadOnlyList<GrupoParaCorregir> grupos, Func<long, bool> leFaltaAlgo)
    {
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(leFaltaAlgo);

        return grupos.Sum(grupo => grupo.Documentos.Count(
            documento => !SaleDeCorreccion(grupo, leFaltaAlgo(documento.CasoId))));
    }

    /// <summary>
    /// A que grupo de fecha y unidad pasa ese documento; vacio si no esta en ninguno.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Es la mitad que impide perder el documento.</b> Sin esto, «sale de
    /// Correccion» seria una desaparicion: el dueno no tendria forma de saber a donde fue lo
    /// que acaba de corregir, y en este programa perder un documento significa que alguien
    /// viaje con la recomendacion mal.</para>
    ///
    /// <para>Se le pasan los grupos SIN filtrar, que es donde esta el destino: en los filtrados
    /// un documento resuelto ya no aparece, por definicion.</para>
    ///
    /// <para>El vacio no es un descuido: pasa con un documento archivado, que no entra en
    /// ningun grupo. Inventarle un destino seria afirmar lo que no consta.</para>
    /// </remarks>
    /// <param name="todosLosGrupos">Los grupos sin filtrar, tal como los arma <see cref="GruposParaCorregir"/>.</param>
    /// <param name="casoId">El documento que acaba de resolverse.</param>
    public static string ADondePasa(IReadOnlyList<GrupoParaCorregir> todosLosGrupos, long casoId)
    {
        ArgumentNullException.ThrowIfNull(todosLosGrupos);

        var donde = GruposParaCorregir.DondeEsta(todosLosGrupos, casoId);
        return donde < 0 ? string.Empty : todosLosGrupos[donde].Nombre;
    }

    /// <summary>La fecha en ISO del grupo al que pasa, para poder abrirlo; vacia si no hay.</summary>
    /// <remarks>
    /// Va aparte de <see cref="ADondePasa"/> porque son dos cosas distintas: una se lee y la
    /// otra se navega. Un documento sin fecha de viaje no puede pasar a ningun grupo de dia
    /// —y no llega aqui, porque sin fecha le falta algo y sigue en Correccion—.
    /// </remarks>
    /// <param name="todosLosGrupos">Los grupos sin filtrar.</param>
    /// <param name="casoId">El documento.</param>
    public static string FechaDelGrupoAlQuePasa(IReadOnlyList<GrupoParaCorregir> todosLosGrupos, long casoId)
    {
        ArgumentNullException.ThrowIfNull(todosLosGrupos);

        var donde = GruposParaCorregir.DondeEsta(todosLosGrupos, casoId);
        return donde < 0 ? string.Empty : todosLosGrupos[donde].FechaIso;
    }
}
