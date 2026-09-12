using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Lectura;

namespace Fichas.App.Correccion;

/// <summary>
/// Lo que devuelve guardar: cuanto entro, que hay que mirar, y lo que dice el pie.
/// </summary>
/// <param name="CamposGuardados">Cuantos campos llegaron a escribirse.</param>
/// <param name="CamposSenalados">
/// Las etiquetas de los que no cumplen su forma, en orden de lectura. <b>Estan
/// guardados</b>: la lista dice que hay que mirarlos, no que se hayan perdido.
/// </param>
/// <param name="CamposQueElAlmacenNoAdmitio">
/// Las etiquetas de los que el almacen se nego a escribir. Casi siempre vacia: lo unico
/// que queda rechazando en <c>personas</c> es <c>UNIQUE (caso_id, mrn)</c>.
/// </param>
/// <param name="Avisos">Lo que hay que ensenar en la franja; puede estar vacia.</param>
/// <param name="FirmasRetiradas">Cuantas firmas se cayeron porque su valor cambio.</param>
/// <param name="LineaDelAcuse">La linea del pie, ya compuesta.</param>
/// <param name="CamposQueLeFaltan">
/// Cuantos campos le faltan al documento para poder asignarse: los vacios, los de poca
/// confianza, los tachados sin corregir y los que no cumplen su forma.
/// </param>
/// <param name="ListoParaAsignar">
/// Si el PROGRAMA lleno todos los campos y no queda ninguno dudoso. <b>Es una lectura del
/// estado, no un estado nuevo, y NO es la firma de Miguel</b>: no hay columna en la base
/// para esto y no se escribe en ninguna.
/// </param>
/// <param name="SinNingunaPersonaLeida">
/// Si el documento no trae ni una persona. Va aparte de <paramref name="CamposQueLeFaltan"/>
/// porque no es un campo que falte: es que <b>no hay a quien recomendar</b>, y decirlo como
/// «faltan 0 campos» dejaria al dueno mirando un documento que no esta listo sin una sola
/// palabra que diga por que.
/// </param>
public sealed record ResultadoDeGuardado(
    int CamposGuardados,
    IReadOnlyList<string> CamposSenalados,
    IReadOnlyList<string> CamposQueElAlmacenNoAdmitio,
    IReadOnlyList<Aviso> Avisos,
    int FirmasRetiradas,
    string LineaDelAcuse,
    int CamposQueLeFaltan,
    bool ListoParaAsignar,
    bool SinNingunaPersonaLeida = false);

/// <summary>
/// La parte del modelo que ESCRIBE: guardar lo tecleado, firmar un campo, marcar que no
/// esta en el papel, y el veredicto de si al documento le falta algo.
/// </summary>
/// <remarks>
/// Los tres actos son distintos y no se mezclan (regla permanente 5): guardar apunta lo que
/// dice el papel y nunca pone <c>verificado</c>; firmar es el unico camino a
/// <c>verificado = 1</c> y lo pulsa Miguel; marcar «no esta en el papel» deja dicho que ahi
/// no hay dato que buscar y tampoco es una firma. El veredicto —<see cref="ListoParaAsignar"/>—
/// es una lectura, no una escritura.
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>
    /// Los cinco campos del caso que se corrigen: su columna, su nombre en pantalla y el del papel.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>El primero de los tres es el nombre de la COLUMNA, no el de la propiedad de C#.</b>
    /// Es lo que se escribe en <c>procedencia_campo.campo</c>, y la clave de esa tabla es
    /// (tabla, registro_id, campo). Hasta el 2026-09-04 aqui iba el <c>nameof</c> de la
    /// propiedad —«UnidadNombre»— y la importacion escribia «unidad_nombre»: no se pisaban, quedaban DOS
    /// filas del mismo campo, y esta pantalla no encontraba ni la banda del papel, ni lo que
    /// leyo el OCR, ni la confianza. Ademas «UnidadNombre» no es espanol ni es una columna de
    /// nada (regla permanente 4).
    /// <para>
    /// ⚠️ <b>Y por eso este archivo escribe <c>using Fichas.Lectura</c>, que es una desviacion
    /// de lo que dice Fichas.App.csproj</b> («las pantallas solo conocen Fichas.Contratos»).
    /// Las siete constantes viven hoy en <see cref="Extraccion"/> y desde alli las usa la
    /// importacion; declararlas otra vez aqui seria dos verdades que se separan el dia que
    /// alguien toque una. Su sitio de verdad es <c>Fichas.Contratos</c>, junto a
    /// <see cref="Caso"/>, y ahi NO se tocan: esta congelado y su titular es el supervisor.
    /// Va nombrado en la entrega; el dia que suban, este <c>using</c> se borra y no cambia
    /// nada mas.
    /// </para>
    /// </remarks>
    private static readonly (string Campo, string Etiqueta, string EnElPapel)[] CamposDelCaso =
    [
        (Extraccion.CampoNumeroDeCaso, "N.º de caso", "Case Number"),
        (Extraccion.CampoUnidadNumero, "N.º de unidad", "Unit Number"),
        (Extraccion.CampoUnidadNombre, "Unidad", "Ward/Branch Name"),
        (Extraccion.CampoFechaDeViaje, "Fecha de viaje", "Travel Date"),
        (Extraccion.CampoTemploNombre, "Templo", "Temple Name"),
    ];

    /// <summary>Los dos campos de texto de cada persona; las ordenanzas son casillas, no texto.</summary>
    /// <remarks>El primero de los tres es la columna, por lo mismo que en el caso.</remarks>
    private static readonly (string Campo, string Etiqueta, string EnElPapel)[] CamposDeLaPersona =
    [
        (Extraccion.CampoCedula, "Cédula", "Membership Record Number"),
        (Extraccion.CampoNombreDePersona, "Nombre", "Name"),
    ];

    /// <summary>Las columnas del caso que esta pantalla dibuja, para que otro las pueda comparar.</summary>
    /// <remarks>
    /// Existe por un defecto concreto: la importacion llevaba su propia lista de campos y
    /// esta pantalla la suya, se separaron sin que nadie lo viera, y <c>templo_nombre</c>
    /// acabo dibujado aqui y sin fila de procedencia alli —0 de 7 casos, medido sobre los
    /// siete escaneos reales—. Un campo sin fila NO se puede firmar. Con esto, la prueba
    /// <c>LaPantallaYLaImportacionHablanDeLosMismosCampos</c> compara las dos listas y se
    /// pone roja el mismo dia que vuelvan a separarse.
    /// </remarks>
    public static IReadOnlyList<string> CamposDelCasoQueSeDibujan { get; } =
        [.. CamposDelCaso.Select(uno => uno.Campo)];

    /// <summary>Las columnas de cada persona que esta pantalla dibuja, por lo mismo.</summary>
    public static IReadOnlyList<string> CamposDeLaPersonaQueSeDibujan { get; } =
        [.. CamposDeLaPersona.Select(uno => uno.Campo)];

    /// <summary>
    /// Guarda TODO lo que se tecleo —valga o no— y deja senalado lo que hay que mirar.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Un valor raro se guarda. Siempre.</b> Es el requisito 9 del dueno —«avisar,
    /// nunca impedir»— y es lo mismo que dice la migracion 17 al quitarle el <c>CHECK</c>
    /// a <c>personas.mrn</c>: «una cedula mal leida se guarda y se senala en la pantalla,
    /// no se rechaza; lo que un CHECK tira aqui no es un dato, es la fila de una persona».
    /// <para>
    /// ⚠️ Hasta el 2026-09-04 esta lista llevaba <c>&amp;&amp; MotivoDe(campo) is null</c>, y
    /// con eso la pantalla era la ultima pared que quedaba: QA tecleo <c>123</c> en una
    /// cedula, pulso Guardar, el pie dijo «1 campo sin guardar» y en la base el MRN seguia
    /// siendo el de antes. La base ya lo admitia, la capa de datos ya contestaba «se guardo
    /// igual», y era esta linea la que lo tiraba.
    /// </para>
    /// <para>
    /// Lo que NO se relaja es la firma: <see cref="Firmar"/> sigue negandose sobre un campo
    /// que no vale. Guardar es apuntar lo que dice el papel; firmar es darlo por bueno, y
    /// son dos actos distintos (regla permanente 5).
    /// </para>
    /// <para>Y guarde lo que guarde, <b>lo dice en el pie</b>: un boton que hace su trabajo en
    /// silencio es, para quien lo mira, un boton roto.</para>
    /// <para>
    /// ⚠️ <b>Guardar puede retirar firmas, y se cuenta.</b> Es la decision del dueno del
    /// 2026-09-03 (<c>DECISIONES.md</c>, «Cambiar o borrar un campo firmado le retira la
    /// firma»): una firma dice «di por bueno ESE valor», y con otro valor debajo deja de valer.
    /// La retira el almacen al anotar el valor nuevo; aqui se mide la diferencia de firmas
    /// antes y despues y se dice en un aviso, para que no se pierda ninguna en silencio.
    /// </para>
    /// </remarks>
    /// <param name="tecleado">Lo que se acaba de escribir; nulo usa lo que ya se apunto con <see cref="Teclear"/>.</param>
    /// <returns>Cuanto entro, que hay que mirar y la linea del pie; nunca lanza por un valor raro.</returns>
    public ResultadoDeGuardado Guardar(IReadOnlyDictionary<string, string?>? tecleado = null)
    {
        if (_caso is null)
        {
            var problema = Aviso.Problema("no hay ningún caso abierto: no hay nada que guardar");
            return new ResultadoDeGuardado(0, [], [], [problema], 0, "No hay ningún caso abierto.", 0, false);
        }

        if (tecleado is not null)
            foreach (var (clave, valor) in tecleado) _tecleado[clave] = valor;

        var firmadosAntes = CuantosFirmados;
        var avisos = new List<Aviso>();

        var porGuardar = _campos.Where(campo => !campo.SoloLectura && Cambio(campo)).ToList();
        var (escritos, noAdmitidos) = EscribirEstosCampos(porGuardar, avisos);

        var retiradas = Math.Max(0, firmadosAntes - CuantosFirmados);
        if (retiradas > 0) avisos.Add(AvisoDeLasFirmasRetiradas(retiradas));

        // Se senala lo que hay en pantalla AHORA, ya guardado: es lo que Miguel tiene que
        // mirar, no lo que se acaba de teclear en esta pasada.
        var senalados = _campos.Where(campo => MotivoDe(campo) is not null).ToList();
        var leFaltan = CamposQueLeFaltan;
        var listo = ListoParaAsignar;

        var linea = TextoDelAcuse.Componer(
            TextoDelAcuse.HoraDe(_reloj.Ahora()),
            escritos.Count,
            [.. senalados.Select(campo => campo.EtiquetaCompleta)],
            [.. noAdmitidos.Select(campo => campo.EtiquetaCompleta)],
            TextoDelAcuse.FraseDelDocumento(listo, leFaltan, SinNingunaPersonaLeida));

        return new ResultadoDeGuardado(
            escritos.Count,
            [.. senalados.Select(campo => campo.EtiquetaCompleta)],
            [.. noAdmitidos.Select(campo => campo.EtiquetaCompleta)],
            avisos,
            retiradas,
            linea,
            leFaltan,
            listo,
            SinNingunaPersonaLeida);
    }

    /// <summary>
    /// Escribe esos campos, anota que los tecleo una mano y los da por guardados.
    /// </summary>
    /// <remarks>
    /// Sale de <see cref="Guardar"/> porque <see cref="Firmar"/> necesita lo mismo para UN
    /// solo campo: dar por bueno lo que se acaba de escribir tiene que escribirlo antes,
    /// y hacerlo con otro camino seria dos formas de guardar que se separan.
    /// </remarks>
    /// <param name="porGuardar">Los campos con un cambio pendiente; vacio no toca nada.</param>
    /// <param name="avisos">Donde se van sumando los avisos del almacen.</param>
    /// <returns>Los que entraron y los que el almacen no admitio; juntos son los que se pidieron.</returns>
    private (List<CampoEnPantalla> Escritos, List<CampoEnPantalla> NoAdmitidos) EscribirEstosCampos(
        List<CampoEnPantalla> porGuardar, List<Aviso> avisos)
    {
        var noAdmitidos = new List<CampoEnPantalla>();
        if (_caso is null || porGuardar.Count == 0) return ([], noAdmitidos);

        EscribirElCaso(_caso, porGuardar, avisos, noAdmitidos);
        EscribirLasPersonas(porGuardar, avisos, noAdmitidos);

        var escritos = porGuardar.Where(campo => !noAdmitidos.Contains(campo)).ToList();
        AnotarComoManual(escritos);
        DarPorGuardados(escritos);

        _caso = _casos.Obtener(_caso.Id) ?? _caso;
        _personasDelCaso = _personas.DeCaso(_caso.Id);
        return (escritos, noAdmitidos);
    }

    /// <summary>
    /// Firma un campo como bueno. El UNICO camino a verificado, y lo pulsa Miguel.
    /// </summary>
    /// <remarks>
    /// Regla permanente 5: esto solo ocurre porque Miguel lo pulso, y nunca solo.
    /// <para>
    /// ⚠️ <b>Firmar escribe primero lo que se acaba de teclear en ESE campo.</b> Hasta el
    /// 2026-09-05 se negaba con «guarde X antes de darlo por bueno», y el dueno lo dijo asi:
    /// «cuando esta en blanco no me deja firmar si coloco la informacion que esta
    /// pidiendome». Medido con la ventana abierta sobre sus siete escaneos: rellenar y dar
    /// por bueno un campo costaba dos avisos de error, un Guardar y cuatro pulsaciones.
    /// Rellenar un campo y decir que esta bien es UN acto suyo, no dos con un error en medio,
    /// y la regla 5 no se toca: sigue firmando el porque el lo pulso.
    /// </para>
    /// <para>
    /// ⛔ <b>Lo que NO se relaja:</b> un valor que no cumple su forma no se firma —seria dar
    /// por bueno un formato que ya se sabe malo—, y un campo VACIO tampoco. Firmar un hueco
    /// dejaba <c>verificado = 1</c> sobre nada; lo que se quiere decir ahi es «no está en el
    /// papel», y para eso esta <see cref="MarcarQueNoEstaEnElPapel"/>.
    /// </para>
    /// </remarks>
    /// <param name="campo">El campo que se da por bueno, con lo que tenga tecleado encima.</param>
    /// <param name="companeroId">Quien firma; el almacen se niega si no esta en la base.</param>
    /// <returns>Lo que contesto el almacen, con los avisos del guardado previo delante si lo hubo.</returns>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public ResultadoDeEscritura Firmar(CampoEnPantalla campo, long companeroId)
    {
        ArgumentNullException.ThrowIfNull(campo);

        if (MotivoDe(campo) is string motivo)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                $"«{campo.EtiquetaCompleta}» no se puede dar por bueno mientras no valga",
                campo.Campo,
                motivo));
        }

        if (ValorDe(campo) is null)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                $"«{campo.EtiquetaCompleta}» está vacío: no hay nada que dar por bueno",
                campo.Campo,
                "Escriba el dato mirando el escaneo y vuelva a pulsar. Si el formulario no trae este "
                + "campo, marque la casilla «no está en el papel»: eso NO es una firma, y deja dicho "
                + "que ahí no hay nada que buscar."));
        }

        var avisos = new List<Aviso>();
        if (Cambio(campo))
        {
            var (escritos, noAdmitidos) = EscribirEstosCampos([campo], avisos);
            if (escritos.Count == 0)
            {
                avisos.Add(Aviso.Problema(
                    $"«{campo.EtiquetaCompleta}» no se pudo guardar, así que tampoco se dio por bueno",
                    campo.Campo,
                    "Una firma dice que usted dio por bueno ESE valor, y ese valor no llegó a entrar en "
                    + "la base. Mire el aviso de al lado para saber por qué."));
                return new ResultadoDeEscritura(false, 0, avisos);
            }

            _ = noAdmitidos;
        }

        // Sin fila de procedencia, el almacen de verdad no tiene que actualizar y contesta
        // «No se encontro la fila que se queria cambiar», que no dice ni de que campo habla.
        // Es el caso de `templo_nombre`, que no la tiene en ninguno de los nueve casos de la
        // base del dueno. Se anota lo unico que se sabe —que no hay lectura guardada— y NO
        // se inventa un origen: `Anotar` jamas pone verificado, asi que la regla 5 sigue.
        if (campo.Procedencia is null) AnotarQueNoHayLecturaGuardada(campo);

        var firma = _procedencia.Firmar(campo.Tabla, campo.RegistroId, campo.Campo, companeroId, _reloj.Ahora());
        if (firma.SeEscribio) RefrescarLaProcedencia(campo);

        return avisos.Count == 0
            ? firma
            : new ResultadoDeEscritura(firma.SeEscribio, firma.Id, [.. avisos, .. firma.Avisos]);
    }

    /// <summary>
    /// Deja dicho que el formulario NO trae este campo, o quita esa marca.
    /// </summary>
    /// <remarks>
    /// Palabras del dueno el 2026-09-05: «falta el boton de "esta informacion no es
    /// necesaria" para guardar el documento».
    /// <para>
    /// ⛔ <b>Esto NO es una firma.</b> Escribe <c>ausente_en_el_papel</c> y deja
    /// <c>verificado</c> en cero, que es lo que hace <c>IProcedencia.Anotar</c> siempre. La
    /// columna es de la version 14 del esquema y su motivo es literal: «sin ella, "no esta
    /// en el papel" se ve exactamente igual que "el OCR no supo leerlo"».
    /// </para>
    /// <para>
    /// Se pide que el campo este VACIO en el almacen y sin nada tecleado encima. Marcar «el
    /// papel no trae este campo» sobre un campo con un dato dentro son dos afirmaciones que
    /// se contradicen, y la unica forma de resolverlo aqui seria borrar el dato en silencio.
    /// </para>
    /// <para>
    /// Marcarlo sobre un campo firmado le retira la firma: <c>Anotar</c> pisa la fila entera y
    /// la deja sin <c>verificado</c>, que es lo que tiene que pasar —«Miguel dio por bueno este
    /// dato» y «este campo no existe en el papel» no pueden convivir—.
    /// </para>
    /// </remarks>
    /// <param name="campo">El campo que se marca o se desmarca.</param>
    /// <param name="marcado">Verdadero para decir que el papel no lo trae; falso para quitar esa marca.</param>
    /// <returns>Lo que contesto el almacen; con el motivo en una linea si no se escribio.</returns>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public ResultadoDeEscritura MarcarQueNoEstaEnElPapel(CampoEnPantalla campo, bool marcado)
    {
        ArgumentNullException.ThrowIfNull(campo);

        if (marcado && Cambio(campo))
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                $"«{campo.EtiquetaCompleta}» tiene algo escrito sin guardar",
                campo.Campo,
                "Un campo no puede estar a la vez escrito y ausente del papel. Borre lo que escribió y "
                + "pulse Guardar, o guárdelo y dé el dato por bueno."));
        }

        if (marcado && ReglasDeCampo.Limpiar(campo.ValorGuardado) is not null)
        {
            return ResultadoDeEscritura.NoSeEscribio(Aviso.Advierte(
                $"«{campo.EtiquetaCompleta}» tiene un dato guardado: no se puede marcar como ausente",
                campo.Campo,
                "Decir que el papel no trae este campo mientras hay un dato dentro son dos cosas que se "
                + "contradicen. Si el dato sobra, bórrelo y pulse Guardar; después márquelo."));
        }

        var anterior = campo.Procedencia;
        var resultado = _procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = campo.Tabla,
            RegistroId = campo.RegistroId,
            Campo = campo.Campo,
            Origen = anterior?.Origen ?? OrigenDeCampo.Vacio,
            Confianza = anterior?.Confianza,
            ValorOcr = anterior?.ValorOcr,
            BandaX0 = anterior?.BandaX0,
            BandaY0 = anterior?.BandaY0,
            BandaX1 = anterior?.BandaX1,
            BandaY1 = anterior?.BandaY1,
            AnuladoPorTachon = anterior?.AnuladoPorTachon ?? false,
            AusenteEnElPapel = marcado,
        });

        if (resultado.SeEscribio) RefrescarLaProcedencia(campo);
        return resultado;
    }

    /// <summary>Si ese campo esta marcado como que el formulario no lo trae.</summary>
    /// <remarks>Se lee de la procedencia que trae el campo, que es la ultima releida del almacen.</remarks>
    /// <param name="campo">El campo que se pregunta.</param>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public bool EstaMarcadoComoAusente(CampoEnPantalla campo)
    {
        ArgumentNullException.ThrowIfNull(campo);
        return campo.Procedencia?.AusenteEnElPapel == true;
    }

    /// <summary>Un campo esta resuelto si Miguel lo firmo o marco que no está en el papel.</summary>
    /// <remarks>
    /// ⚠️ Esto cuenta el trabajo de MIGUEL, y desde el 2026-09-05 ya no es lo que decide
    /// «listo para asignar»: eso lo decide <see cref="CamposQueLeFaltan"/>, que mira lo que
    /// leyo el PROGRAMA. Se queda porque siguen siendo dos preguntas distintas y las dos
    /// tienen respuesta; va nombrado en la entrega para que se decida al cerrar la fase.
    /// </remarks>
    /// <param name="campo">El campo que se pregunta; sin fila de procedencia no esta resuelto.</param>
    /// <exception cref="ArgumentNullException">Si el campo es nulo.</exception>
    public bool EstaResuelto(CampoEnPantalla campo)
    {
        ArgumentNullException.ThrowIfNull(campo);
        return campo.Procedencia is ProcedenciaDeCampo fila && (fila.Verificado || fila.AusenteEnElPapel);
    }

    /// <summary>Cuantos campos de este caso no estan ni firmados ni marcados como ausentes.</summary>
    public int CamposSinResolver => _campos.Count(campo => !EstaResuelto(campo));

    /// <summary>
    /// Cuantos campos le faltan a este documento para poder asignarse.
    /// </summary>
    /// <remarks>
    /// Es la cuenta de los dudosos: vacios, de poca confianza, tachados sin corregir, o con
    /// un valor que no cumple su forma. <b>Lo firmado y lo marcado como que no está en el
    /// papel ya no cuentan</b>, porque ahi no queda nada que buscar.
    /// <para>
    /// Es la misma cuenta que la pantalla ya ensena arriba —«Quedan N campos por
    /// comprobar»—, y es a proposito: dos cifras distintas para la misma pregunta en la
    /// misma pantalla es el defecto que se midio el 2026-09-05, y no se repite.
    /// </para>
    /// </remarks>
    public int CamposQueLeFaltan => Dudosos.Count;

    /// <summary>
    /// Si este documento se puede pasar a asignar. <b>Lo dice el PROGRAMA, no la firma.</b>
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Corregido por el dueno el 2026-09-05, con estas palabras:</b> <i>«Yo doy ese
    /// veredicto cuando el sistema no completa todos los campos. Si el sistema escanea y
    /// verifica todos los campos sin mi intervencion, debe decir "listo para asignar": el
    /// sistema lleno todos los campos y a mi solo me deberia dejar verificarlo.»</i>
    /// <para>
    /// Lo que estaba mal hasta esa fecha: esto exigia <c>CamposSinResolver == 0</c>, o sea
    /// la firma de Miguel campo por campo. Eso le obliga a pulsar en documentos que el
    /// programa leyo enteros y bien; con 3 000 documentos son miles de pulsaciones para
    /// confirmar lo que ya estaba. Su firma es para lo que el sistema NO pudo, no un peaje
    /// para trabajar.
    /// </para>
    /// <para>
    /// ⛔ <b>Y esto NO rompe la regla permanente 5.</b> La regla dice que nada se marca como
    /// <c>verificado</c> automaticamente, y eso sigue igual: ni un campo pasa a
    /// <c>verificado = 1</c> sin que el lo firme, y el unico camino sigue siendo
    /// <see cref="Firmar"/>. «Listo para asignar» es otra cosa: es la respuesta a «¿le falta
    /// algo a este documento?», y esa pregunta la puede contestar el programa porque solo
    /// mira si hay huecos. No hay columna en la base para esto y no se escribe en ninguna.
    /// </para>
    /// <para>
    /// ⚠️ <b>Y desde el 2026-09-06 exige ademas que haya alguien a quien recomendar.</b> Es
    /// la sexta divergencia de <c>DECISIONES.md</c> y va al reves que las otras: esto solo
    /// miraba los cinco campos del CASO, asi que un documento del que no se leyo ni una
    /// persona salia como listo para asignar. Mandarselo a un companero es mandarle una hoja
    /// sin nadie dentro. La cola siempre lo dijo —«sin ninguna persona leída»—; era esta
    /// pantalla la que no lo contaba.
    /// </para>
    /// <para>
    /// ⚠️ <b>Desde el 2026-09-07 la palabra «listo para asignar» ya no se le ensena al
    /// dueno</b> (<c>DECISIONES.md</c>, «DOS ESTADOS Y NO CUATRO»): a la vista van «resuelto» y
    /// «me falta», y esto pasa a ser el DETALLE de «me falta» —«te toca a ti: repartirlo»—.
    /// La propiedad se queda con su nombre porque es lo que mide: si el programa dejo el
    /// documento sin huecos. Lo que cambio es como se dice, no lo que se calcula.
    /// </para>
    /// </remarks>
    public bool ListoParaAsignar
        => _caso is not null && _campos.Count > 0 && CamposQueLeFaltan == 0 && !SinNingunaPersonaLeida;

    /// <summary>Si este documento no trae ni una persona: no hay a quien recomendar.</summary>
    /// <remarks>
    /// Se dice aparte de <see cref="CamposQueLeFaltan"/> a proposito: no es un campo que
    /// falte, y sumarlo a esa cuenta haria que el pie dijera «falta 1 campo por revisar»
    /// sobre un campo que no existe.
    /// </remarks>
    public bool SinNingunaPersonaLeida => _caso is not null && _personasDelCaso.Count == 0;

    /// <summary>Vuelve a leer del almacen la procedencia de ese campo.</summary>
    /// <remarks>
    /// Se pide la del registro entero y se busca la columna: <c>IProcedencia</c> no ofrece
    /// leer una sola fila, y son unas pocas por registro.
    /// </remarks>
    /// <param name="campo">El campo cuya procedencia se pone al dia; queda nula si el almacen no tiene fila.</param>
    private void RefrescarLaProcedencia(CampoEnPantalla campo)
        => campo.Procedencia = _procedencia
            .DeRegistro(campo.Tabla, campo.RegistroId)
            .FirstOrDefault(fila => fila.Campo == campo.Campo);

    /// <summary>
    /// Anota la fila que falta diciendo lo unico que se sabe: que no hay lectura guardada.
    /// </summary>
    /// <remarks>
    /// Origen «vacio» y sin <c>valor_ocr</c> porque es la verdad: de ese campo no hay
    /// ninguna lectura. Poner «ocr» afirmaria que una maquina lo leyo y poner «manual» que
    /// alguien lo tecleo, y ninguna de las dos consta. La causa de fondo —que la importacion
    /// no deje fila para <c>templo_nombre</c>— es de <c>Fichas.App/Importar</c> y va
    /// nombrada en la entrega, no arreglada aqui.
    /// </remarks>
    /// <param name="campo">El campo sin fila de procedencia; al volver ya la tiene.</param>
    private void AnotarQueNoHayLecturaGuardada(CampoEnPantalla campo)
    {
        _procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = campo.Tabla,
            RegistroId = campo.RegistroId,
            Campo = campo.Campo,
            Origen = OrigenDeCampo.Vacio,
        });
        RefrescarLaProcedencia(campo);
    }

    /// <summary>Arma la lista de campos de un caso y sus personas, en orden de lectura.</summary>
    /// <remarks>
    /// Primero los cinco del caso y despues los dos de cada persona, en el orden en que
    /// venian en el formulario. La procedencia se pide una vez por registro, no por campo:
    /// son 1 + N consultas cortas, y N son las personas del caso.
    /// </remarks>
    /// <param name="caso">El caso abierto.</param>
    /// <param name="personas">Sus personas, en el orden del formulario.</param>
    private void ArmarLosCampos(Caso caso, IReadOnlyList<Persona> personas)
    {
        var deCaso = _procedencia.DeRegistro(TablaDeProcedencia.Casos, caso.Id)
            .ToDictionary(p => p.Campo, StringComparer.Ordinal);

        foreach (var (campo, etiqueta, enElPapel) in CamposDelCaso)
        {
            var procedencia = deCaso.GetValueOrDefault(campo);
            _campos.Add(new CampoEnPantalla
            {
                Tabla = TablaDeProcedencia.Casos,
                RegistroId = caso.Id,
                Campo = campo,
                Etiqueta = etiqueta,
                EtiquetaDelPapel = enElPapel,
                ValorGuardado = ValorDelCaso(caso, campo),
                Procedencia = procedencia,
                Banda = CampoEnPantalla.BandaDe(procedencia),
                PaginaPdf = caso.PaginaPdf,
                // El numero de caso solo se teclea cuando NO se pudo leer: esa puerta abre en
                // un sentido, de «no se sabe» a un numero, y nunca al reves. Es la mitad de la
                // clave con la que se cruza el Excel que devuelven los companeros.
                SoloLectura = campo == Extraccion.CampoNumeroDeCaso && caso.NumeroCaso is not null,
            });
        }

        foreach (var persona in personas)
        {
            var deLaPersona = _procedencia.DeRegistro(TablaDeProcedencia.Personas, persona.Id)
                .ToDictionary(p => p.Campo, StringComparer.Ordinal);

            foreach (var (campo, etiqueta, enElPapel) in CamposDeLaPersona)
            {
                var procedencia = deLaPersona.GetValueOrDefault(campo);
                _campos.Add(new CampoEnPantalla
                {
                    Tabla = TablaDeProcedencia.Personas,
                    RegistroId = persona.Id,
                    Campo = campo,
                    Etiqueta = etiqueta,
                    EtiquetaDelPapel = enElPapel,
                    ValorGuardado = campo == Extraccion.CampoCedula ? persona.Mrn : persona.Nombre,
                    Procedencia = procedencia,
                    Banda = CampoEnPantalla.BandaDe(procedencia),
                    PaginaPdf = persona.PaginaPdf ?? caso.PaginaPdf,
                    FilaFormulario = persona.FilaFormulario,
                    NombreDeLaPersona = persona.Nombre ?? string.Empty,
                });
            }
        }
    }

    /// <summary>
    /// Escribe los campos del caso, valgan o no. Lo que el almacen no admita se apunta.
    /// </summary>
    /// <remarks>
    /// Los cinco campos del caso van en UNA escritura, asi que o entran los cinco o no
    /// entra ninguno: es como esta hecho <c>ICasos.Guardar</c>, que recibe la fila entera.
    /// Por eso, cuando no entra, se apuntan los cinco y no se adivina cual fue.
    /// </remarks>
    /// <param name="caso">El caso tal como esta; se le cambian solo las columnas tecleadas.</param>
    /// <param name="porGuardar">Todos los campos con cambio; aqui se toman los del caso.</param>
    /// <param name="avisos">Donde se suman los avisos del almacen.</param>
    /// <param name="noAdmitidos">Donde se apuntan los campos que no entraron.</param>
    private void EscribirElCaso(
        Caso caso, List<CampoEnPantalla> porGuardar, List<Aviso> avisos, List<CampoEnPantalla> noAdmitidos)
    {
        var delCaso = porGuardar.Where(campo => campo.Tabla == TablaDeProcedencia.Casos).ToList();
        if (delCaso.Count == 0) return;

        var nuevo = caso;
        foreach (var campo in delCaso) nuevo = ConElCampo(nuevo, campo.Campo, ValorDe(campo));

        var resultado = _casos.Guardar(nuevo);
        avisos.AddRange(resultado.Avisos);
        if (!resultado.SeEscribio) noAdmitidos.AddRange(delCaso);
    }

    /// <summary>
    /// Escribe los campos de cada persona, valgan o no, persona por persona.
    /// </summary>
    /// <remarks>
    /// ⚠️ Una persona que ya no esta en la base NO se calla: sus campos entran en la lista
    /// de lo que no se admitio y salen nombrados en el pie. Antes se saltaba con un
    /// <c>continue</c> y lo tecleado desaparecia sin que nada lo dijera.
    /// </remarks>
    /// <param name="porGuardar">Todos los campos con cambio; aqui se toman los de personas, agrupados por persona.</param>
    /// <param name="avisos">Donde se suman los avisos del almacen.</param>
    /// <param name="noAdmitidos">Donde se apuntan los campos que no entraron.</param>
    private void EscribirLasPersonas(
        List<CampoEnPantalla> porGuardar, List<Aviso> avisos, List<CampoEnPantalla> noAdmitidos)
    {
        var porPersona = porGuardar
            .Where(campo => campo.Tabla == TablaDeProcedencia.Personas)
            .GroupBy(campo => campo.RegistroId);

        foreach (var grupo in porPersona)
        {
            var persona = _personas.Obtener(grupo.Key);
            if (persona is null)
            {
                noAdmitidos.AddRange(grupo);
                avisos.Add(Aviso.Problema(
                    "esa persona ya no está en la base: lo que tecleó en ella no se guardó",
                    string.Empty,
                    $"No hay ninguna persona con el número interno {grupo.Key}. Vuelva a abrir el caso "
                    + "para ver cómo está ahora."));
                continue;
            }

            foreach (var campo in grupo) persona = ConElCampo(persona, campo.Campo, ValorDe(campo));

            var resultado = _personas.Guardar(persona);
            avisos.AddRange(resultado.Avisos);
            if (!resultado.SeEscribio) noAdmitidos.AddRange(grupo);
        }
    }

    /// <summary>Deja anotado que ese valor lo tecleo una mano. NUNCA pone verificado.</summary>
    /// <remarks>
    /// Se conserva de la fila anterior todo lo que no cambia por teclear —lo que leyo el OCR,
    /// la banda, el tachon, la marca de ausente— y se pisa el origen y la confianza. Anotar
    /// es lo que le retira la firma al campo si la tenia (dueno, 2026-09-03).
    /// </remarks>
    /// <param name="porGuardar">Los campos que acaban de entrar en el almacen.</param>
    private void AnotarComoManual(List<CampoEnPantalla> porGuardar)
    {
        foreach (var campo in porGuardar)
        {
            var anterior = campo.Procedencia;
            _procedencia.Anotar(new ProcedenciaDeCampo
            {
                Tabla = campo.Tabla,
                RegistroId = campo.RegistroId,
                Campo = campo.Campo,
                Origen = OrigenDeCampo.Manual,
                Confianza = null,
                // Lo que leyo el OCR se conserva: es la unica prueba de que decia el papel.
                ValorOcr = anterior?.ValorOcr,
                BandaX0 = anterior?.BandaX0,
                BandaY0 = anterior?.BandaY0,
                BandaX1 = anterior?.BandaX1,
                BandaY1 = anterior?.BandaY1,
                AnuladoPorTachon = anterior?.AnuladoPorTachon ?? false,
                AusenteEnElPapel = anterior?.AusenteEnElPapel ?? false,
            });
        }
    }

    /// <summary>Lo escrito deja de ser un cambio pendiente; lo que no valio se queda en pantalla.</summary>
    /// <remarks>
    /// El valor guardado pasa a ser lo tecleado, lo tecleado se olvida y la procedencia se
    /// relee: sin esto, Guardar se quedaria pulsandose para siempre sobre lo mismo.
    /// </remarks>
    /// <param name="porGuardar">Los campos que entraron; los no admitidos NO pasan por aqui.</param>
    private void DarPorGuardados(List<CampoEnPantalla> porGuardar)
    {
        foreach (var campo in porGuardar)
        {
            campo.ValorGuardado = ValorDe(campo);
            _tecleado.Remove(campo.Clave);
            campo.Procedencia = _procedencia
                .DeRegistro(campo.Tabla, campo.RegistroId)
                .FirstOrDefault(p => p.Campo == campo.Campo);
        }
    }

    /// <summary>El aviso de las firmas que se cayeron al cambiar su valor.</summary>
    /// <param name="retiradas">Cuantas firmas menos hay que antes de guardar; siempre mayor que cero aqui.</param>
    private static Aviso AvisoDeLasFirmasRetiradas(int retiradas) => Aviso.Advierte(
        $"se retiró la firma de {retiradas} campo{(retiradas == 1 ? string.Empty : "s")}: su valor cambió",
        string.Empty,
        "Una firma dice que usted dio por bueno ESE valor, así que al cambiarlo deja de valer. El caso "
        + "vuelve a salir como pendiente hasta que lo confirme otra vez.");

    /// <summary>El valor guardado de un campo del caso, por su nombre de columna.</summary>
    /// <param name="caso">El caso tal como esta en la base.</param>
    /// <param name="campo">El nombre de la columna; una que no se conoce devuelve nulo.</param>
    private static string? ValorDelCaso(Caso caso, string campo) => campo switch
    {
        Extraccion.CampoNumeroDeCaso => caso.NumeroCaso,
        Extraccion.CampoUnidadNumero => caso.UnidadNumero,
        Extraccion.CampoUnidadNombre => caso.UnidadNombre,
        Extraccion.CampoFechaDeViaje => caso.FechaViaje,
        Extraccion.CampoTemploNombre => caso.TemploNombre,
        _ => null,
    };

    /// <summary>El caso con ese campo cambiado; un campo que no conoce lo deja igual.</summary>
    /// <param name="caso">El caso de partida; no se toca, se devuelve una copia.</param>
    /// <param name="campo">El nombre de la columna que cambia.</param>
    /// <param name="valor">El valor nuevo, ya limpio; nulo vacia la columna.</param>
    private static Caso ConElCampo(Caso caso, string campo, string? valor) => campo switch
    {
        Extraccion.CampoNumeroDeCaso => caso with { NumeroCaso = valor },
        Extraccion.CampoUnidadNumero => caso with { UnidadNumero = valor },
        Extraccion.CampoUnidadNombre => caso with { UnidadNombre = valor },
        Extraccion.CampoFechaDeViaje => caso with { FechaViaje = valor },
        Extraccion.CampoTemploNombre => caso with { TemploNombre = valor },
        _ => caso,
    };

    /// <summary>La persona con ese campo cambiado; un campo que no conoce la deja igual.</summary>
    /// <param name="persona">La persona de partida; no se toca, se devuelve una copia.</param>
    /// <param name="campo">El nombre de la columna que cambia: la cedula o el nombre.</param>
    /// <param name="valor">El valor nuevo, ya limpio; nulo vacia la columna.</param>
    private static Persona ConElCampo(Persona persona, string campo, string? valor) => campo switch
    {
        Extraccion.CampoCedula => persona with { Mrn = valor },
        Extraccion.CampoNombreDePersona => persona with { Nombre = valor },
        _ => persona,
    };
}
