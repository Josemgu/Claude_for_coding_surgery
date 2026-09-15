using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Lectura;

namespace Fichas.App.Importar;

/// <summary>
/// Una hoja leida, guardada en la base con toda su procedencia.
/// </summary>
/// <remarks>
/// <para>Portado de <c>importacion/guardado.py</c>, que es la especificacion. Lo que
/// garantiza, y que es la razon de que exista en vez de repartir estas llamadas por la
/// pantalla: <b>no se guarda un valor sin guardar de donde salio</b>. Si eso se hiciera
/// desde la interfaz, bastaria una pantalla nueva que se olvidara de una llamada para que
/// un dato apareciera sin saber de donde vino.</para>
///
/// <para>⛔ <b>NINGUNA HOJA SE RECHAZA. Ni una.</b> Es la decision del 2026-09-03, medida
/// en la maquina del dueno: importo diez documentos y el programa contesto «6 caso ya
/// existente» —seis documentos enteros fuera— porque el numero de caso son cuatro letras
/// mas el ano y el mes, o sea que identifica una UNIDAD Y UN MES, no una familia.</para>
///
/// <para>⛔ <b>Nada se marca como verificado aqui</b> (regla permanente 5). Las filas de
/// procedencia nacen sin firma, y solo el boton que pulsa Miguel las cambia.</para>
///
/// <para><b>Cada hoja entra entera o no entra</b> (R-4, 2026-09-15). Sus filas —el caso, sus
/// personas, la procedencia de cada campo y su renglón de ilegibles— se escriben dentro de un
/// <see cref="IAmbitoDeGuardado"/> que se confirma al terminar la hoja: de 9 a 24
/// confirmaciones por hoja a UNA, y si algo revienta a mitad no queda ni el caso ni las
/// personas anteriores. Lo que el motor rechaza fila a fila (una cédula repetida) sigue
/// siendo un aviso y no deshace la hoja: «ninguna hoja se rechaza» no cambia.</para>
/// </remarks>
public sealed partial class GuardadoDeHojas
{
    /// <summary>Por donde se abren y se leen los casos.</summary>
    private readonly ICasos _casos;
    /// <summary>Por donde entran las personas de cada hoja.</summary>
    private readonly IPersonas _personas;
    /// <summary>Por donde se anota de qué banda y con qué confianza salió cada valor.</summary>
    private readonly IProcedencia _procedencia;
    /// <summary>Por donde se apunta el renglón de una hoja que no se pudo leer entera.</summary>
    private readonly IIlegibles _ilegibles;
    /// <summary>La fecha de hoy para la fila de ilegibles; inyectado para que las pruebas la fijen.</summary>
    private readonly IReloj _reloj;
    /// <summary>Quien contesta de qué caso guardado repite una hoja; se vacía al empezar cada tanda.</summary>
    private readonly BuscadorDeDuplicados _duplicados;
    /// <summary>Quien guarda la copia del escaneo que el caso apunta; ver <see cref="CopiaDelEscaneo"/>.</summary>
    private readonly CopiaDelEscaneo _copias;
    /// <summary>Quien abre el ámbito en el que entra cada hoja entera; ver <see cref="IAmbitoDeGuardado"/>.</summary>
    private readonly Func<IAmbitoDeGuardado> _abrirAmbito;

    /// <summary>Se ata a los cuatro repositorios, al reloj, a la copia y a quien abre el ámbito.</summary>
    /// <param name="casos">Repositorio de casos.</param>
    /// <param name="personas">Repositorio de personas.</param>
    /// <param name="procedencia">Repositorio de procedencia de cada campo.</param>
    /// <param name="ilegibles">Repositorio de renglones de lo que no se pudo leer.</param>
    /// <param name="reloj">De dónde sale la fecha de hoy.</param>
    /// <param name="copias">Quien guarda la copia de cada escaneo en la carpeta de datos; el caso apunta a esa copia.</param>
    /// <param name="abrirAmbito">Quien abre, por hoja, el ámbito en el que sus filas entran todas o ninguna; con la base de verdad, una transacción de SQLite.</param>
    public GuardadoDeHojas(
        ICasos casos, IPersonas personas, IProcedencia procedencia,
        IIlegibles ilegibles, IReloj reloj, CopiaDelEscaneo copias,
        Func<IAmbitoDeGuardado> abrirAmbito)
    {
        ArgumentNullException.ThrowIfNull(copias);
        ArgumentNullException.ThrowIfNull(abrirAmbito);

        _casos = casos;
        _personas = personas;
        _procedencia = procedencia;
        _ilegibles = ilegibles;
        _reloj = reloj;
        _copias = copias;
        _abrirAmbito = abrirAmbito;
        _duplicados = new BuscadorDeDuplicados(casos, personas);
    }

    /// <summary>
    /// Guarda las hojas de UN documento, uniendo las que son del mismo caso.
    /// </summary>
    /// <remarks>
    /// Es el unico sitio donde nace el diccionario del documento, y por eso es el unico
    /// sitio donde una hoja se puede unir a otra: empieza vacio en cada documento, asi que
    /// un caso que ya estaba en la base de antes jamas entra en el. Un formulario de grupo
    /// ocupa seis paginas con el MISMO numero; sin esto, `SURB2609` entraba con 1 persona
    /// de 12 —medido—.
    /// <para>⚠️ El dueño dijo el 2026-09-10 (DECISIONES.md, «Lo que el dueño vio probando el
    /// v9», punto 4) que las hojas de un PDF pueden ser documentos distintos y que agruparlas
    /// tiene que ser opción suya. Este método sigue uniéndolas por número de caso: esa
    /// decisión está abierta y no se programó aquí.</para>
    /// <para>⚠️ Desde el 2026-09-15 el caso NO guarda la ruta del archivo que se leyó sino la de
    /// su COPIA en la carpeta de datos (<see cref="CopiaDelEscaneo"/>), hecha una vez por
    /// documento aquí. Los renglones de ilegibles siguen llevando la ruta original: es la que el
    /// dueño busca en su carpeta para ir a mirar el papel.</para>
    /// </remarks>
    /// <param name="hojas">Las hojas del documento, en el orden del PDF.</param>
    /// <returns>Un resultado por hoja, en el mismo orden, aunque la hoja no dejara nada.</returns>
    public IReadOnlyList<ResultadoDeLaHoja> GuardarLasHojasDelDocumento(IReadOnlyList<HojaLeida> hojas)
    {
        ArgumentNullException.ThrowIfNull(hojas);

        var papel = PapelDelDocumento(hojas);
        var casosDeEsteDocumento = new Dictionary<string, long>(StringComparer.Ordinal);
        var salida = new List<ResultadoDeLaHoja>(hojas.Count);

        foreach (var hoja in hojas)
        {
            salida.Add(GuardarUnaHojaEntera(hoja, casosDeEsteDocumento, papel));
        }

        return ConLosAvisosDeLaCopia(salida, papel);
    }

    /// <summary>
    /// Una hoja dentro de su ámbito: sus filas y su renglón, y la confirmación al final.
    /// </summary>
    /// <remarks>
    /// El <c>using</c> es lo que deshace: si algo levanta entre abrir y confirmar, el ámbito se
    /// cierra sin confirmar y la base queda como estaba antes de esta hoja. El renglón de
    /// ilegibles va dentro también: un caso confirmado sin su renglón, o un renglón sin su
    /// caso, sería la misma media hoja que esto existe para impedir.
    /// </remarks>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="casosDeEsteDocumento">Número de caso → id del caso que abrió una hoja anterior de este mismo PDF.</param>
    /// <param name="papel">La copia del escaneo, perezosa.</param>
    private ResultadoDeLaHoja GuardarUnaHojaEntera(
        HojaLeida hoja, Dictionary<string, long> casosDeEsteDocumento, Lazy<CopiaGuardada> papel)
    {
        using var ambito = _abrirAmbito();
        var resultado = GuardarUnaHoja(hoja, casosDeEsteDocumento, papel);
        AnotarLosRenglones(hoja, resultado);
        ambito.Confirmar();
        return resultado;
    }

    /// <summary>Se olvida del indice de duplicados; se llama al empezar cada tanda.</summary>
    public void EmpezarUnaTanda() => _duplicados.Olvidar();

    /// <summary>Guarda una hoja y dice que paso con ella.</summary>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="casosDeEsteDocumento">Número de caso → id del caso que abrió una hoja anterior de este mismo PDF.</param>
    /// <param name="papel">La copia del escaneo, que se hace la primera vez que una hoja abre caso; el caso guarda su ruta.</param>
    private ResultadoDeLaHoja GuardarUnaHoja(
        HojaLeida hoja, Dictionary<string, long> casosDeEsteDocumento, Lazy<CopiaGuardada> papel)
    {
        var campos = new CamposDeLaHoja(hoja.Campos);

        // Una hoja de la que no se saco NI UN campo no tiene nada que guardar: deja su
        // renglon y se acabo. Con campos, entra igual aunque el lector la marcara ilegible:
        // lo leido se ensena, no se tira (requisito 9).
        if (hoja.Campos.Count == 0)
        {
            return HojaQueNoDejoNada(hoja);
        }

        // Y una hoja que no es un formulario de recomendacion tampoco abre caso: deja su
        // renglon con lo que se leyo. El motivo, medido, esta en GuardadoDeHojas.Papel.cs.
        if (EsUnFormularioDesconocido(hoja))
        {
            return HojaDeOtraClase(hoja, campos);
        }

        var numeroCaso = campos.ValorDe(CamposDeLaHoja.CampoNumeroCaso);
        var unida = UnirSiEsHojaDelMismoCaso(hoja, campos, numeroCaso, casosDeEsteDocumento);
        return unida ?? AbrirUnCasoParaEstaHoja(hoja, campos, numeroCaso, casosDeEsteDocumento, papel.Value.RutaDelPapel);
    }

    /// <summary>Lo que se devuelve de una hoja que no se pudo leer en absoluto.</summary>
    /// <param name="hoja">La hoja sin campos; si además no tiene líneas ni página, es que el PDF no se abrió.</param>
    private static ResultadoDeLaHoja HojaQueNoDejoNada(HojaLeida hoja) => new(
        Entro: false,
        CasoNuevo: false,
        CasoId: null,
        NumeroCaso: null,
        PaginaPdf: hoja.Pagina,
        Personas: 0,
        Avisos: hoja.Avisos,
        PendienteDeIdentificar: false,
        DuplicadoDe: null,
        Renglones: [hoja.LineasLeidas == 0 && hoja.Pagina == 0
            ? MotivosDeIlegible.NoSePudoAbrir
            : MotivosDeIlegible.SinTexto]);

    /// <summary>
    /// El resultado de unir esta hoja al caso que abrio otra hoja de ESTE documento, o
    /// nulo si no procede.
    /// </summary>
    /// <remarks>
    /// Nulo significa las tres cosas que llevan al mismo sitio —esta hoja abre caso
    /// propio—: que no traiga numero, que su numero no lo haya abierto ninguna hoja
    /// anterior de este documento, o que lo haya abierto pero esta hoja lo contradiga.
    /// </remarks>
    /// <param name="hoja">La hoja leída.</param>
    /// <param name="campos">Los mismos campos, ya repartidos entre caso y personas.</param>
    /// <param name="numeroCaso">El número de caso que leyó esta hoja, o nulo.</param>
    /// <param name="casosDeEsteDocumento">Número de caso → id del caso abierto por una hoja anterior de este PDF.</param>
    private ResultadoDeLaHoja? UnirSiEsHojaDelMismoCaso(
        HojaLeida hoja, CamposDeLaHoja campos, string? numeroCaso,
        Dictionary<string, long> casosDeEsteDocumento)
    {
        if (numeroCaso is null) return null;
        if (!casosDeEsteDocumento.TryGetValue(numeroCaso, out var casoId)) return null;

        // El caso se lee UNA vez para las dos preguntas que se le hacen —en que contradice a
        // esta hoja y en que hoja le puede aportar—: son las dos caras de la misma
        // comparacion y pedir la fila dos veces por cada hoja de cada documento no anade
        // nada.
        var guardado = _casos.Obtener(casoId);
        if (Discrepancias(guardado, campos).Count > 0) return null;

        var (personas, avisos) = GuardarLasPersonas(casoId, campos, hoja.Pagina);
        var huecos = HuecosQueEstaHojaLeyo(guardado, campos);

        return new ResultadoDeLaHoja(
            Entro: true,
            CasoNuevo: false,
            CasoId: casoId,
            NumeroCaso: numeroCaso,
            PaginaPdf: hoja.Pagina,
            Personas: personas,
            Avisos: [.. hoja.Avisos, .. avisos, .. AvisoDeLoQueLeyoEstaHoja(huecos, hoja.Pagina)],
            PendienteDeIdentificar: false,
            DuplicadoDe: null,
            Renglones: huecos.Count > 0 ? [MotivosDeIlegible.LoLeyoOtraHoja] : []);
    }

    /// <summary>
    /// En que campos del caso esta hoja lee OTRA cosa que la que abrio el caso.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>«No lo lei» NO es una discrepancia</b>, y esa es la mitad que hace util al
    /// aviso: las hojas 5 y 6 del grupo real del dueno entran sin fecha y sin unidad, y
    /// contar eso como contradiccion pondria un renglon en cada grupo de mas de cuatro
    /// hojas. Un aviso que sale siempre no lo lee nadie, y entonces el que si importa
    /// tampoco. Contradecir es decir otra cosa, no callarse.
    ///
    /// <para>El numero de caso NO se compara, y no es un olvido: dos hojas se unen
    /// precisamente porque lo leyeron igual.</para>
    /// </remarks>
    /// <param name="guardado">El caso que abrió la hoja anterior; nulo devuelve vacío.</param>
    /// <param name="campos">Lo que leyó esta hoja.</param>
    /// <returns>Una terna (etiqueta, lo guardado, lo de esta hoja) por campo que contradice; vacía si no contradice ninguno.</returns>
    private static IReadOnlyList<(string Etiqueta, string Guardado, string DeEstaHoja)> Discrepancias(
        Caso? guardado, CamposDeLaHoja campos)
    {
        if (guardado is null) return [];

        var comparaciones = new (string Campo, string Etiqueta, string? LoGuardado)[]
        {
            (CamposDeLaHoja.CampoFechaViaje, "Fecha de viaje", guardado.FechaViaje),
            (CamposDeLaHoja.CampoUnidadNumero, "N.º de unidad", guardado.UnidadNumero),
            (CamposDeLaHoja.CampoUnidadNombre, "Unidad", guardado.UnidadNombre),
        };

        var discrepancias = new List<(string, string, string)>();
        foreach (var (campo, etiqueta, loGuardado) in comparaciones)
        {
            var deEstaHoja = campos.ValorDe(campo);
            if (deEstaHoja is null || loGuardado is null) continue;
            if (!string.Equals(deEstaHoja, loGuardado, StringComparison.Ordinal))
            {
                discrepancias.Add((etiqueta, loGuardado, deEstaHoja));
            }
        }

        return discrepancias;
    }

    /// <summary>
    /// Que campos del caso leyo ESTA hoja y el caso no tiene.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Es la otra mitad de <see cref="Discrepancias"/> y NO es simetrica con ella.</b>
    /// Alli las dos hojas leyeron y dijeron cosas distintas: no pueden ser ciertas a la vez y
    /// el programa no elige, la hoja entra aparte. Aqui solo hay UNA lectura y nadie la
    /// contradice, asi que la hoja se une y el caso no se toca.
    ///
    /// <para><b>Se miran CUATRO campos y no los tres de <see cref="Discrepancias"/>: entra
    /// tambien el templo.</b> Y el motivo esta medido, no es simetria: el 2026-09-07, sobre
    /// los diez PDF reales, un documento leyo <c>«Panama City»</c> donde los otros seis
    /// leyeron <c>«Panama City, Panama»</c>. Meter el templo entre lo que SEPARA hojas
    /// partiria grupos por esa variacion del OCR; entre lo que una hoja puede APORTAR no
    /// rompe nada, porque aportar no separa a nadie.</para>
    ///
    /// <para>El numero de caso no se mira, igual que alla: dos hojas se unen precisamente
    /// porque lo leyeron igual.</para>
    /// </remarks>
    /// <param name="guardado">El caso que abrió la hoja anterior; nulo devuelve vacío.</param>
    /// <param name="campos">Lo que leyó esta hoja.</param>
    /// <returns>Un par (etiqueta, lo de esta hoja) por campo que el caso tiene vacío y esta hoja leyó.</returns>
    private static IReadOnlyList<(string Etiqueta, string DeEstaHoja)> HuecosQueEstaHojaLeyo(
        Caso? guardado, CamposDeLaHoja campos)
    {
        if (guardado is null) return [];

        var comparaciones = new (string Campo, string Etiqueta, string? LoGuardado)[]
        {
            (CamposDeLaHoja.CampoFechaViaje, "Fecha de viaje", guardado.FechaViaje),
            (CamposDeLaHoja.CampoUnidadNumero, "N.º de unidad", guardado.UnidadNumero),
            (CamposDeLaHoja.CampoUnidadNombre, "Unidad", guardado.UnidadNombre),
            (CamposDeLaHoja.CampoTemploNombre, "Templo", guardado.TemploNombre),
        };

        var huecos = new List<(string, string)>();
        foreach (var (campo, etiqueta, loGuardado) in comparaciones)
        {
            if (loGuardado is null && campos.ValorDe(campo) is string deEstaHoja)
            {
                huecos.Add((etiqueta, deEstaHoja));
            }
        }

        return huecos;
    }

    /// <summary>
    /// El aviso de la hoja que leyo algo que al caso le falta. UNO, no uno por campo.
    /// </summary>
    /// <remarks>
    /// La franja enseña UNA linea y la cuenta de las que hay detras. El dueño rechazo por
    /// escrito los avisos que ocupan media pantalla el 2026-09-04 —«las letras en amarillo
    /// toman todo el espacio»—, asi que cuatro campos de la misma hoja son UN hecho: «esta
    /// pagina leyo cosas que al caso le faltan».
    /// </remarks>
    /// <param name="huecos">Lo que devolvió <see cref="HuecosQueEstaHojaLeyo"/>; vacío no produce aviso.</param>
    /// <param name="pagina">La página del PDF, base 1, para que el dueño sepa cuál mirar.</param>
    private static Aviso[] AvisoDeLoQueLeyoEstaHoja(
        IReadOnlyList<(string Etiqueta, string DeEstaHoja)> huecos, int pagina)
    {
        if (huecos.Count == 0) return [];

        var cuantos = huecos.Count == 1
            ? "1 campo que al caso le falta: no se puso solo"
            : $"{huecos.Count} campos que al caso le faltan: no se pusieron solos";
        return
        [
            Aviso.Advierte(
                $"La página {pagina} leyó {cuantos}.",
                CamposDeLaHoja.CampoNumeroCaso,
                string.Join(" ", huecos.Select(uno => $"«{uno.Etiqueta}»: esta página leyó «{uno.DeEstaHoja}»."))
                + " El caso los tiene vacíos porque la hoja que lo abrió no los leyó, y la página "
                + "sigue unida al caso: no contradice a nadie. NO se ponen solos porque la pantalla "
                + "de corrección enseña los campos del caso sobre la hoja que lo abrió, y un valor "
                + "de otra página señalaría un recuadro que no es el suyo. Escríbalos mirando esta "
                + $"página {pagina} del PDF."),
        ];
    }

    /// <summary>El aviso de la hoja que abrio caso aparte, en una linea con su detalle.</summary>
    /// <param name="discrepancias">Lo que devolvió <see cref="Discrepancias"/>, con al menos una.</param>
    /// <param name="numeroCaso">El número de caso que las dos hojas leyeron igual.</param>
    private static Aviso AvisoDeLaHojaAparte(
        IReadOnlyList<(string Etiqueta, string Guardado, string DeEstaHoja)> discrepancias,
        string numeroCaso)
        => Aviso.Advierte(
            $"Una hoja dice ser del caso {numeroCaso} pero no coincide con la anterior: entró aparte.",
            CamposDeLaHoja.CampoNumeroCaso,
            TextoDeLasDiscrepancias(discrepancias) +
            " Las dos no pueden ser ciertas, así que esta página NO se unió: ENTRÓ COMO CASO " +
            "APARTE, con todo lo que se le leyó. Dos familias distintas de la misma unidad y " +
            "el mismo mes comparten número de caso por construcción.");

    /// <summary>Los campos que no coinciden, dichos uno detras de otro en una frase.</summary>
    /// <remarks>
    /// Se junta todo en UNA frase y no una por campo: es un solo hecho —«esta hoja no es
    /// de la misma familia que la que abrio el caso»— y tres renglones seguidos con la
    /// misma pagina se leen como tres problemas distintos.
    /// </remarks>
    /// <param name="discrepancias">Las ternas (etiqueta, lo guardado, lo de esta hoja).</param>
    private static string TextoDeLasDiscrepancias(
        IReadOnlyList<(string Etiqueta, string Guardado, string DeEstaHoja)> discrepancias)
        => string.Join(" ", discrepancias.Select(cual =>
            $"«{cual.Etiqueta}»: el caso guardó «{cual.Guardado}» de la hoja que lo abrió, " +
            $"y esta hoja leyó «{cual.DeEstaHoja}»."));
}
