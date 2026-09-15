using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;
using Fichas.Lectura;

namespace Fichas.App.Correccion;

/// <summary>
/// Todo lo que decide la pantalla de correccion, SIN una sola linea de XAML.
/// </summary>
/// <remarks>
/// Vive aparte del control por el mismo motivo que <c>interfaz/encuadre.py</c> vive aparte
/// del visor: se puede probar sin abrir una ventana, y la prueba da el mismo numero en
/// cualquier maquina. Los criterios C4-7, C4-10, C4-11 y C4-13 se miden aqui con un comando.
/// <para>
/// De datos solo conoce <c>Fichas.Contratos</c>: enchufado a <c>Fichas.Datos</c> (fase C2)
/// no cambia ni una linea. De <c>Fichas.Lectura</c> conoce SOLO los siete nombres de
/// columna, que son constantes de compilacion: no monta ningun OCR ni abre ningun PDF, y se
/// sigue probando sin base y sin ventana. El motivo de que esos nombres esten alli y no en
/// <c>Fichas.Contratos</c> —que es donde les toca— esta escrito en
/// <c>ModeloDeCorreccion.Guardado.cs</c>.
/// </para>
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>Por donde se lee y se escribe el caso: sus cinco campos van en UNA fila.</summary>
    private readonly ICasos _casos;

    /// <summary>Por donde se leen y se escriben las personas del caso, una a una.</summary>
    private readonly IPersonas _personas;

    /// <summary>De donde salio cada campo, y el UNICO camino por el que se firma (regla permanente 5).</summary>
    private readonly IProcedencia _procedencia;

    /// <summary>Rasteriza y lee el escaneo; solo se usa para situar las bandas, nunca para cambiar un valor.</summary>
    private readonly ILecturaDePdf _lecturaDePdf;

    /// <summary>Propone campos sobre una hoja leida; de lo propuesto se toma SOLO la banda.</summary>
    private readonly IExtraccion _extraccion;

    /// <summary>La hora del programa, para que una prueba pueda decir que hora es al firmar y al acusar.</summary>
    private readonly IReloj _reloj;

    /// <summary>Para poner nombre a quien contesto en el Excel de vuelta y a quien puso la marca.</summary>
    private readonly ICompaneros _companeros;

    /// <summary>
    /// Lo tecleado y todavia no guardado, por clave de campo. Una clave ausente significa
    /// «no se ha tocado»; una clave con nulo significa «se vacio».
    /// </summary>
    private readonly Dictionary<string, string?> _tecleado = new(StringComparer.Ordinal);

    /// <summary>Todos los campos del caso abierto, en orden de lectura; se vacia y se rehace al cargar.</summary>
    private readonly List<CampoEnPantalla> _campos = [];

    /// <summary>Lo que salio mal al cargar; se suma a los avisos de la cabecera mientras dure el caso.</summary>
    private readonly List<Aviso> _avisosDeCarga = [];

    /// <summary>
    /// ⚠️ <b>Se REEMPLAZA en cada carga; no se vacia y se vuelve a llenar.</b>
    /// </summary>
    /// <remarks>
    /// Medido con la ventana abierta el 2026-09-05 sobre los siete escaneos reales: con una
    /// lista que se vaciaba y se rellenaba, el repetidor de la pantalla seguia ensenando
    /// <b>«Ana Prueba»</b> con el documento de <b>Jonas Ficticio</b> delante. Un
    /// <c>List&lt;T&gt;</c> no avisa de que cambio por dentro, y el repetidor recibia la
    /// MISMA referencia, asi que no volvia a leerla. Con otra lista cada vez, la referencia
    /// cambia y se repinta.
    /// <para>
    /// Enseñar la respuesta del companero de OTRA persona es peor que no enseñar ninguna: se
    /// lee como si esa persona estuviera lista cuando no lo esta. Es el mismo trato que ya
    /// tiene <c>_personasDelCaso</c>, dos lineas mas abajo.
    /// </para>
    /// </remarks>
    private IReadOnlyList<RespuestaDelCompanero> _respuestas = [];

    /// <summary>El caso abierto tal como esta en la base; nulo hasta que <see cref="Cargar"/> lo encuentre.</summary>
    /// <remarks>
    /// Se relee del almacen despues de cada escritura: la pantalla no se adelanta a la base.
    /// </remarks>
    private Caso? _caso;

    /// <summary>Las personas del caso abierto. Se REEMPLAZA en cada carga, por lo mismo que <see cref="_respuestas"/>.</summary>
    private IReadOnlyList<Persona> _personasDelCaso = [];

    /// <summary>Monta el modelo con las siete puertas que necesita, y nada mas.</summary>
    /// <remarks>
    /// La septima —<see cref="ICompaneros"/>— entro el 2026-09-05 y solo se usa para poner
    /// NOMBRE a quien contesto en el Excel de vuelta. Sin ella la respuesta se veria igual
    /// pero firmada por «el companero n.º 7», que no dice nada a quien mira la pantalla.
    /// </remarks>
    /// <param name="casos">El almacen de casos.</param>
    /// <param name="personas">El almacen de personas.</param>
    /// <param name="procedencia">El almacen de procedencia, por donde se firma.</param>
    /// <param name="lecturaDePdf">Quien rasteriza y lee el escaneo.</param>
    /// <param name="extraccion">Quien propone campos sobre lo leido.</param>
    /// <param name="reloj">La hora del programa.</param>
    /// <param name="companeros">El almacen de companeros, para ponerles nombre.</param>
    public ModeloDeCorreccion(
        ICasos casos,
        IPersonas personas,
        IProcedencia procedencia,
        ILecturaDePdf lecturaDePdf,
        IExtraccion extraccion,
        IReloj reloj,
        ICompaneros companeros)
    {
        _casos = casos;
        _personas = personas;
        _procedencia = procedencia;
        _lecturaDePdf = lecturaDePdf;
        _extraccion = extraccion;
        _reloj = reloj;
        _companeros = companeros;
    }

    /// <summary>El caso que se esta corrigiendo, o nulo si todavia no se abrio ninguno.</summary>
    public Caso? Caso => _caso;

    /// <summary>Las personas de ese caso, en el orden en que venian en el formulario.</summary>
    public IReadOnlyList<Persona> Personas => _personasDelCaso;

    /// <summary>Todos los campos que se pintan, en orden de lectura.</summary>
    public IReadOnlyList<CampoEnPantalla> Campos => _campos;

    /// <summary>
    /// Lo que los companeros contestaron de las personas de este caso, una por persona.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Esto es del companero y NO firma nada de Miguel.</b> Son dos cosas distintas y
    /// no se mezclan (regla permanente 5): el estado de la recomendacion lo escribe el Excel
    /// que devuelve el companero, con su nombre; la firma de campos es de Miguel.
    /// <para>
    /// Sale de la queja del dueno del 2026-09-05. El Excel de vuelta ya escribia —7 casos y
    /// 7 personas cambiaban sobre sus siete escaneos reales, medido— y <b>ninguna pantalla
    /// lo leia</b>: cero coincidencias de <c>EstadoPropuesto</c>, <c>PropuestoPor</c>,
    /// <c>NotaCompanero</c> ni los seis <c>Paso*</c> en todo <c>Fichas.App</c>.
    /// </para>
    /// <para>
    /// Las personas por las que nadie contesto NO salen: una ficha vacia por cada una
    /// enterraria las que si traen algo, que son las que hay que mirar.
    /// </para>
    /// </remarks>
    public IReadOnlyList<RespuestaDelCompanero> RespuestasDeLosCompaneros => _respuestas;

    /// <summary>
    /// Como esta la recomendacion de CADA persona de este documento en el sistema del obispo.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Salen TODAS, tambien las que nadie miro</b>, y ahi esta la diferencia con
    /// <see cref="RespuestasDeLosCompaneros"/>: aquella contesta «¿que dijo el companero?» y
    /// las que no traen respuesta se callan a proposito; esta contesta «¿esta confirmada su
    /// recomendacion?», y para la que nadie miro la respuesta es «sin mirar», que no es lo
    /// mismo que «no» y no se puede omitir. Criterio C17-3.
    /// <para>
    /// Solo se lee: no hay por donde escribir. Contestar las seis dentro del programa es la
    /// FASE C19.
    /// </para>
    /// </remarks>
    public IReadOnlyList<RecomendacionDeUnaPersona> LaRecomendacionDeCadaPersona
        => LaRecomendacionEnElSistemaDelObispo.DeCadaPersona(_personasDelCaso);

    /// <summary>Cuantas personas de este documento tienen la recomendacion confirmada.</summary>
    public int CuantasPersonasConfirmadas
        => LaRecomendacionEnElSistemaDelObispo.CuantasConfirmadas(_personasDelCaso);

    /// <summary>Cuantas no: las que estan en «no» y las que nadie miro.</summary>
    public int CuantasPersonasSinConfirmar => _personasDelCaso.Count - CuantasPersonasConfirmadas;

    /// <summary>«1 de 3 personas con la recomendación confirmada en el sistema del obispo».</summary>
    /// <remarks>El denominador va siempre: una cifra sin el no se puede comprobar (C1-1).</remarks>
    public string LineaDeLaRecomendacion => LaRecomendacionEnElSistemaDelObispo.Linea(_personasDelCaso);

    /// <summary>Los que hay que mirar primero: vacios, de poca confianza, o que no valen.</summary>
    public IReadOnlyList<CampoEnPantalla> Dudosos => [.. _campos.Where(EsDudoso)];

    /// <summary>Los demas, que ya se leyeron bien y no piden atencion.</summary>
    public IReadOnlyList<CampoEnPantalla> Resto => [.. _campos.Where(campo => !EsDudoso(campo))];

    /// <summary>Cuantos campos de este caso llevan la firma de Miguel, leidos del almacen.</summary>
    /// <remarks>
    /// Se cuenta consultando y no llevando la cuenta en memoria: un contador de pantalla que
    /// dijera otra cosa que el almacen seria justo la mentira que hay que cazar.
    /// </remarks>
    public int CuantosFirmados
    {
        get
        {
            if (_caso is null) return 0;
            var total = _procedencia.ContarVerificados(TablaDeProcedencia.Casos, _caso.Id);
            foreach (var persona in _personasDelCaso)
                total += _procedencia.ContarVerificados(TablaDeProcedencia.Personas, persona.Id);
            return total;
        }
    }

    /// <summary>Abre un caso. Devuelve falso y deja su aviso si no se pudo; NUNCA lanza.</summary>
    /// <remarks>
    /// Tira lo tecleado del caso anterior: lo que no se guardo antes de cambiar de caso no se
    /// conserva, y la pantalla lo pregunta antes de llegar aqui. Abrir un caso no escribe ni
    /// una fila (regla permanente 5): lo vigila <c>PruebasDelModeloDeCorreccion.AbrirUnCasoNoFirmaNada</c>.
    /// </remarks>
    /// <param name="casoId">El numero interno del caso; si ya no esta en la base, se dice y se devuelve falso.</param>
    /// <returns>Verdadero si el caso se abrio con sus campos y sus personas; falso si ya no esta.</returns>
    public bool Cargar(long casoId)
    {
        _tecleado.Clear();
        _campos.Clear();
        _avisosDeCarga.Clear();
        _respuestas = [];

        _caso = _casos.Obtener(casoId);
        VolverALaHojaDelCaso();
        if (_caso is null)
        {
            _personasDelCaso = [];
            _avisosDeCarga.Add(Aviso.Problema(
                "ese caso ya no está en la base: no se puede corregir",
                string.Empty,
                $"No hay ningún caso con el número interno {casoId}. Puede que se archivara o que se "
                + "abriera desde una lista vieja. Vuelva a la lista y elija otro."));
            return false;
        }

        _personasDelCaso = _personas.DeCaso(casoId);
        ArmarLosCampos(_caso, _personasDelCaso);
        ArmarLasRespuestas(casoId, _personasDelCaso);
        return true;
    }

    /// <summary>
    /// Reune lo que los companeros contestaron de estas personas, con su nombre delante.
    /// </summary>
    /// <remarks>
    /// Se pregunta por el companero UNA vez por cada id distinto y no una por persona: en un
    /// formulario de grupo, doce personas suelen traer el mismo companero detras.
    /// <para>
    /// ⛔ De aqui no sale ninguna escritura. Leer lo que dijo el companero no da por bueno
    /// ningun campo (regla permanente 5).
    /// </para>
    /// </remarks>
    /// <param name="casoId">El caso cuyas firmas de los seis pasos se leen de una vez.</param>
    /// <param name="personas">Las personas del caso; las que no traen respuesta no salen.</param>
    private void ArmarLasRespuestas(long casoId, IReadOnlyList<Persona> personas)
    {
        var firmas = _personas.FirmasDeLosPasosDelCaso(casoId);
        var porId = ElEquipoQueHaceFalta(personas, firmas);
        var equipo = porId.Values.OfType<Companero>().ToList();
        var reunidas = new List<RespuestaDelCompanero>();

        foreach (var persona in personas)
        {
            var quien = persona.PropuestoPor is long id ? porId.GetValueOrDefault(id) : null;
            var firma = firmas.TryGetValue(persona.Id, out var suya) ? suya : FirmaDeLosPasos.SinFirmar;

            if (LoQueContestoElCompanero.De(persona, quien, firma, equipo) is RespuestaDelCompanero respuesta)
                reunidas.Add(respuesta);
        }

        _respuestas = reunidas;
    }

    /// <summary>
    /// Los compañeros que hay que nombrar en este documento, buscados UNA vez cada uno.
    /// </summary>
    /// <remarks>
    /// Se juntan los dos orígenes —<c>propuesto_por</c>, que propuso el estado desde su
    /// Excel, y <c>pasos_por</c>, que contestó las seis— porque en la misma fila pueden ser
    /// personas distintas y las dos hay que poder nombrarlas.
    /// <para>
    /// ⚠️ Se pregunta por <see cref="ICompaneros.Obtener"/> y no por <c>Activos()</c>: un
    /// compañero desactivado sigue siendo quien contestó, y sustituir su nombre por un
    /// número al darle de baja borraría el rastro de lo que hizo.
    /// </para>
    /// </remarks>
    /// <param name="personas">Las personas del caso, de las que se toma <c>propuesto_por</c>.</param>
    /// <param name="firmas">La firma de los seis pasos de cada persona, de la que se toma <c>pasos_por</c>.</param>
    /// <returns>Cada id que hizo falta con su compañero, o nulo si ese id ya no está en la base.</returns>
    private Dictionary<long, Companero?> ElEquipoQueHaceFalta(
        IReadOnlyList<Persona> personas, IReadOnlyDictionary<long, FirmaDeLosPasos> firmas)
    {
        var porId = new Dictionary<long, Companero?>();

        foreach (var persona in personas)
        {
            long?[] losDos = [persona.PropuestoPor, firmas.GetValueOrDefault(persona.Id)?.Por];
            foreach (var cual in losDos)
            {
                if (cual is long id && !porId.ContainsKey(id)) porId[id] = _companeros.Obtener(id);
            }
        }

        return porId;
    }

    /// <summary>Apunta lo que hay escrito ahora en un campo, y con qué hoja delante; se revalida en el acto.</summary>
    /// <remarks>
    /// Se revalida al teclear y no al guardar: un campo que se pone rojo media hora despues
    /// obliga a volver a buscar donde estaba el error (<c>interfaz/campo.py</c>).
    /// <para>
    /// La hoja se apunta aquí y no al guardar, y es a propósito: de dónde salió lo escrito se
    /// sabe en el momento de escribirlo (<c>ModeloDeCorreccion.Hoja.cs</c>).
    /// </para>
    /// </remarks>
    /// <param name="clave">La clave del campo, la que compone <see cref="CampoEnPantalla.ClaveDe"/>.</param>
    /// <param name="valor">Lo que hay en el cuadro ahora; nulo o vacio significa «se vacio».</param>
    public void Teclear(string clave, string? valor)
    {
        _tecleado[clave] = valor;
        ApuntarLaHojaDeLoTecleado(clave);
    }

    /// <summary>Lo que hay escrito ahora mismo en ese campo; vacio devuelve nulo.</summary>
    /// <remarks>
    /// Manda lo tecleado si lo hay, y si no lo guardado. Los dos pasan por
    /// <see cref="ReglasDeCampo.Limpiar"/>, asi que un campo con solo espacios es un campo vacio.
    /// </remarks>
    /// <param name="campo">El campo que se pregunta.</param>
    public string? ValorDe(CampoEnPantalla campo)
        => ReglasDeCampo.Limpiar(_tecleado.TryGetValue(campo.Clave, out var tecleado) ? tecleado : campo.ValorGuardado);

    /// <summary>Por que no vale lo que hay escrito, o nulo si vale.</summary>
    /// <param name="campo">El campo que se pregunta; su columna decide que regla se aplica.</param>
    public string? MotivoDe(CampoEnPantalla campo) => ReglasDeCampo.MotivoDe(campo.Campo, ValorDe(campo));

    /// <summary>En cual de los seis estados esta el campo ahora mismo, con lo tecleado incluido.</summary>
    /// <param name="campo">El campo que se pregunta.</param>
    public EstadoDeCampo EstadoDe(CampoEnPantalla campo)
        => EstadosDeCampo.Decidir(campo.Procedencia, MotivoDe(campo) is null);

    /// <summary>Si lo escrito es distinto de lo guardado.</summary>
    /// <remarks>
    /// Se compara ya limpio por los dos lados: escribir un espacio de mas no es un cambio.
    /// </remarks>
    /// <param name="campo">El campo que se pregunta.</param>
    public bool Cambio(CampoEnPantalla campo)
        => !string.Equals(ValorDe(campo), ReglasDeCampo.Limpiar(campo.ValorGuardado), StringComparison.Ordinal);

    /// <summary>Si hay algo tecleado que todavia no se ha guardado.</summary>
    public bool HayCambiosSinGuardar => _campos.Any(Cambio);

    /// <summary>Si este campo es de los que hay que mirar primero (criterio C4-13).</summary>
    /// <param name="campo">El campo que se pregunta.</param>
    private bool EsDudoso(CampoEnPantalla campo)
        => EstadosDeCampo.EsDudoso(ValorDe(campo), campo.Procedencia, MotivoDe(campo) is null);

    /// <summary>
    /// Los avisos de la cabecera, calculados con lo que hay en pantalla AHORA.
    /// </summary>
    /// <remarks>
    /// El del mes cruzado tiene que aparecer y desaparecer mientras se teclea, sin esperar a
    /// Guardar: por eso se recalcula al pedirlo y no se guarda en una lista.
    /// Cada linea cabe en un renglon; lo largo va en el detalle, que solo se ve al pulsar «ver».
    /// </remarks>
    public IReadOnlyList<Aviso> AvisosDeLaCabecera
    {
        get
        {
            if (_caso is null) return _avisosDeCarga;

            var avisos = new List<Aviso>(_avisosDeCarga);
            var numero = ValorDelCampo(Extraccion.CampoNumeroDeCaso) ?? _caso.NumeroCaso;
            var fecha = ValorDelCampo(Extraccion.CampoFechaDeViaje);

            if (ReglasDeCampo.Limpiar(numero) is null)
            {
                avisos.Add(Aviso.Advierte(
                    "caso sin número: mírelo en el escaneo y escríbalo arriba",
                    Extraccion.CampoNumeroDeCaso,
                    "Este caso entró SIN número porque no se pudo leer, y todo lo demás que traía la "
                    + "página SÍ se guardó: no se ha perdido nada. Hasta que lo tenga, este caso no se "
                    + "puede cruzar con el Excel que devuelven los compañeros."));
            }

            if (_caso.CapturaManual)
            {
                avisos.Add(Aviso.Advierte(
                    "formulario ilegible: los campos vienen vacíos, teclee mirando el escaneo",
                    string.Empty,
                    "Sobre un escaneo que no se lee el sistema no insiste ni adivina (regla permanente 1). "
                    + "Los campos vienen VACÍOS a propósito, no se perdieron."));
            }

            if (ReglasDeCampo.AvisoDelMesCruzado(numero, fecha) is string mesCruzado)
            {
                avisos.Add(Aviso.Advierte(
                    "fecha de viaje que no cuadra con el mes del número de caso",
                    Extraccion.CampoFechaDeViaje,
                    mesCruzado + " Puede ser un viaje reprogramado: el dato se guarda igual y hay que "
                    + "confirmarlo a mano. Es un aviso, no una pared."));
            }

            return avisos;
        }
    }

    /// <summary>Lo que hay escrito ahora en un campo del caso, por su nombre de columna.</summary>
    /// <param name="campo">El nombre de la columna; una que no se dibuja devuelve nulo.</param>
    private string? ValorDelCampo(string campo)
    {
        var ficha = _campos.FirstOrDefault(c => c.Tabla == TablaDeProcedencia.Casos && c.Campo == campo);
        return ficha is null ? null : ValorDe(ficha);
    }
}
