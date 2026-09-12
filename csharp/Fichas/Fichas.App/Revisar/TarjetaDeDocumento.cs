using Fichas.App.Asignar;
using Fichas.App.Grupo;
using Fichas.App.Vocabulario;
using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Revisar;

/// <summary>
/// Un documento tal como se ve en su tarjeta de Revisar. Datos ya calculados, sin ventana.
/// </summary>
/// <remarks>
/// Es el equivalente de <c>_documento_de_la_fila</c> de <c>datos/revision.py</c>, y las
/// palabras son las del mockup aprobado (<c>mockups/mockup-v2-revisar.html</c>): el color
/// nunca va solo, cada estado lleva su palabra en espanol.
///
/// **La firma y la marca del estado son dos cosas y no se mezclan** (CLAUDE.md §1.5, tal
/// como lo preciso el dueno el 2026-09-03): <see cref="Firma"/> dice quien puso el estado
/// —el companero cuyo Excel volvio, «Completada por Sandy · fecha»—, y NO es la firma de
/// campos de Miguel, que vive en <c>procedencia_campo.verificado_por</c> y no se toca aqui.
/// </remarks>
public sealed record TarjetaDeDocumento
{
    /// <summary>Numero interno del caso.</summary>
    public long CasoId { get; init; }

    /// <summary>El numero del papel, o «sin numero de caso». Nunca se inventa.</summary>
    public string NumeroDeCaso { get; init; } = RenglonParaAsignar.SinNumero;

    /// <summary>Si el numero se leyo de verdad.</summary>
    public bool TieneNumeroDeCaso { get; init; }

    /// <summary>Nombre del archivo del que salio, sin la ruta entera.</summary>
    public string Archivo { get; init; } = string.Empty;

    /// <summary>«hoja 3», o vacio si no se sabe de que hoja salio.</summary>
    public string Hoja { get; init; } = string.Empty;

    /// <summary>Cuantas personas van en el formulario.</summary>
    public int Personas { get; init; }

    /// <summary>La fecha de viaje, o «sin fecha de viaje».</summary>
    public string FechaDeViaje { get; init; } = RenglonParaAsignar.SinFecha;

    /// <summary>
    /// La fecha de viaje TAL COMO esta en la base, ISO-8601, o vacia si no la hay.
    /// </summary>
    /// <remarks>
    /// Va aparte de <see cref="FechaDeViaje"/>, que es texto para leer y con «sin fecha de
    /// viaje» dentro cuando no la hay. Agrupar por mes necesita el dato, no la frase: el
    /// 2026-09-05 el dueno pidio ver esto «por mes, luego por fecha de viaje, luego por
    /// unidad», y de una frase no sale un mes.
    /// </remarks>
    public string FechaDeViajeIso { get; init; } = string.Empty;

    /// <summary>Si la fecha de viaje ya paso; lleva su aviso y sus dos salidas.</summary>
    public bool FechaYaPasada { get; init; }

    /// <summary>Si de esta tarjeta sale una fecha de viaje de verdad.</summary>
    /// <remarks>
    /// Lo decide <see cref="ArbolDeRevisar.EsFechaLegible"/>, que es quien decide tambien en
    /// que carpeta cae. Una fecha ilegible cuenta como que NO la tiene, que es lo que el
    /// dueno pidio ver: «lo que no se reconozca la fecha».
    /// </remarks>
    public bool TieneFechaDeViaje => ArbolDeRevisar.EsFechaLegible(FechaDeViajeIso);

    /// <summary>
    /// Lo que dice el boton que abre el cuadro de la fecha: poner la que falta o cambiarla.
    /// </summary>
    /// <remarks>
    /// Son dos palabras y no una porque son dos gestos distintos para quien mira: en la
    /// seccion de «revisar este documento» el boton PIDE algo, y en el resto de las tarjetas
    /// solo ofrece corregir lo que ya hay.
    /// </remarks>
    public string PalabraDelBotonDeFecha => TieneFechaDeViaje ? "Cambiar fecha…" : "Poner fecha…";

    /// <summary>Como se llama ese boton para quien no ve la pantalla; nombra su documento.</summary>
    public string NombreDelBotonDeFecha => $"{PalabraDelBotonDeFecha.TrimEnd('…')} de viaje de {Archivo}";

    /// <summary>
    /// Como se llama, para quien no ve la pantalla, el boton que abre las seis preguntas.
    /// </summary>
    /// <remarks>
    /// Nombra su documento porque en la rejilla hay uno por tarjeta: un lector de pantalla
    /// que dijera «las seis preguntas» treinta veces seguidas no diria de cual.
    /// </remarks>
    public string NombreDelBotonDeLasPreguntas => $"Las seis preguntas de las personas de {Archivo}";

    /// <summary>Lo que dice <see cref="Unidad"/> cuando el papel no traia nombre de unidad.</summary>
    /// <remarks>
    /// Es una constante y no un literal suelto porque <see cref="ArbolDeRevisar"/> tiene que
    /// distinguir «no hay unidad» de un nombre de verdad para no crear una carpeta llamada
    /// «sin unidad» como si lo fuera.
    /// </remarks>
    public const string SinUnidad = "sin unidad";

    /// <summary>Nombre de la unidad, o «sin unidad».</summary>
    public string Unidad { get; init; } = SinUnidad;

    /// <summary>
    /// Numero de la unidad, 6 o 7 digitos, o vacio si no se leyo.
    /// </summary>
    /// <remarks>
    /// El dueno pidio la carpeta con las dos cosas —«el nombre y numero de la unidad»— y
    /// hace falta: dos unidades pueden llamarse igual y el numero es lo unico que las
    /// distingue.
    /// </remarks>
    public string UnidadNumero { get; init; } = string.Empty;

    /// <summary>El nombre de la unidad, o vacío: «sin unidad» no es un nombre.</summary>
    /// <remarks>
    /// <see cref="Unidad"/> lleva «sin unidad» dentro porque eso es lo que se lee bien en una
    /// tarjeta. Aquí eso NO es un nombre: tomarlo por uno crearía una carpeta llamada «sin
    /// unidad» junto a las de verdad, como si esa unidad existiera.
    /// </remarks>
    public string NombreDeLaUnidad
        => string.Equals(Unidad, SinUnidad, StringComparison.Ordinal) ? string.Empty : Unidad.Trim();

    /// <summary>
    /// La unidad tal como se lee en la tarjeta: «7000011 · Castries Branch».
    /// </summary>
    /// <remarks>
    /// <para><b>La queja del dueño que cierra</b>, con sus palabras: «muchos no tenían unidad;
    /// cuando entré al documento, la unidad sí estaba». La tarjeta componía su unidad solo con
    /// <c>unidad_nombre</c> y decía «sin unidad» cuando ese campo venía vacío, <b>aunque
    /// <c>unidad_numero</c> estuviera lleno</b>. El documento abierto sí enseña los dos campos:
    /// la lista decía una cosa y el documento otra sobre el mismo caso.</para>
    ///
    /// <para>Con los dos se dicen los dos, igual que en la carpeta del árbol y por el mismo
    /// motivo: dos unidades pueden llamarse igual y el número es lo único que las distingue.</para>
    /// </remarks>
    public string UnidadQueSeLee => ComponerUnidad(UnidadNumero, NombreDeLaUnidad, SinUnidad);

    /// <summary>
    /// El número y el nombre de una unidad en un renglón, con lo que se diga cuando no hay nada.
    /// </summary>
    /// <remarks>
    /// Lo usan la tarjeta y la carpeta del árbol, y por eso vive en un solo sitio: leen el MISMO
    /// caso, así que si compusieran cada una por su cuenta podrían acabar diciendo cosas
    /// distintas de la misma unidad — que es exactamente el defecto que esto cierra.
    /// Lo único que cambia entre las dos es cómo se llama el vacío: «sin unidad» se lee en una
    /// tarjeta y «Sin unidad» es el nombre de una carpeta del disco.
    /// </remarks>
    /// <param name="numero">El número de la unidad, o vacío.</param>
    /// <param name="nombre">El nombre de la unidad, o vacío.</param>
    /// <param name="sinUnidad">Lo que se dice cuando no hay ni número ni nombre.</param>
    public static string ComponerUnidad(string numero, string nombre, string sinUnidad)
        => (numero.Length, nombre.Length) switch
        {
            (0, 0) => sinUnidad,
            (0, _) => nombre,
            (_, 0) => numero,
            _ => $"{numero} · {nombre}",
        };

    /// <summary>
    /// La ruta entera del PDF del que salio este documento, o vacia si no se sabe.
    /// </summary>
    /// <remarks>
    /// <see cref="Archivo"/> es solo el nombre, que es lo que cabe en la tarjeta. Para
    /// volcar el mes a carpetas hace falta la ruta entera, y puede que el archivo ya no este
    /// ahi: <see cref="VolcadoDeCarpetas"/> lo cuenta y lo dice en vez de callarlo.
    /// </remarks>
    public string RutaDelPdf { get; init; } = string.Empty;

    /// <summary>El estado tal como lo entiende el programa.</summary>
    public EstadoDeRecomendacion Estado { get; init; }

    /// <summary>
    /// De que estado se ve esta tarjeta: el de la base, salvo el archivado que ya viajo.
    /// </summary>
    /// <remarks>
    /// ⚠️ NO es <see cref="Estado"/> y no lo sustituye: la base sigue diciendo lo que decia y
    /// los reportes la siguen leyendo. Esto es lo que se LEE en la tarjeta, y son cuatro cosas
    /// y no tres (<see cref="EstadosQueSeVen.DeLaTarjeta"/>).
    /// </remarks>
    public EstadoQueSeVe EstadoQueSeVe => EstadosQueSeVen.DeLaTarjeta(Estado, Archivado, FechaYaPasada);

    /// <summary>
    /// Lo que se lee de este documento: una de las dos palabras, y detras que falta y a quien
    /// le toca.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Pasa por <see cref="LoQueSeLeeDeUnDocumento"/> y no por
    /// <see cref="EstadoQueSeVe"/></b>, y la diferencia es un caso real: un archivado que
    /// TODAVIA no ha viajado. Para el enum es su estado de siempre, porque aquel se invento
    /// para la frase «fecha pasada completada»; para el dueno esta resuelto, porque archivar es
    /// el gesto con el que el dice que ya no tiene nada que hacer con eso. La palabra sale de
    /// donde esta esa regla escrita.</para>
    ///
    /// <para><b>Cuantos campos le faltan no se pregunta aqui</b> y va en cero a proposito: eso
    /// exige leer <c>procedencia_campo</c>, y esta pantalla se carga de una sola pasada sobre
    /// 3 000 documentos (criterio C13-5). La cuenta de campos vive en Inicio y en Completar,
    /// que si la leen; lo que Revisar contesta es lo que dijo el companero.</para>
    /// </remarks>
    public LoQueSeLeeDeUnDocumento Lectura => LoQueSeLeeDeUnDocumento.De(
        Estado,
        Archivado,
        cuantoLeFalta: 0,
        sinNingunaPersonaLeida: Personas == 0,
        quienLoLleva: SinAsignar ? string.Empty : AsignadoA,
        firma: Firma,
        fechaDeArchivado: FechaDeArchivado,
        motivo: MotivoParaElDetalle);

    /// <summary>
    /// La palabra del estado: «resuelto» o «me falta», y no hay una tercera.
    /// </summary>
    /// <remarks>
    /// ⛔ Hasta el 2026-09-07 decia una de CUATRO —«sin revisar», «completa», «no esta
    /// completa» o «fecha pasada completada»—. Lo que decian no se pierde: esta en
    /// <see cref="DetalleDelEstado"/>, que se abre cuando el dueno lo pide.
    /// </remarks>
    public string PalabraDelEstado => Lectura.Palabra;

    /// <summary>Lo que lee en voz alta un lector de pantalla sobre la pastilla del estado.</summary>
    /// <remarks>
    /// Lleva la palabra Y el detalle, aunque en pantalla el detalle este a un clic: quien no ve
    /// la pantalla no puede pulsar para enterarse de que le falta. Es la misma regla que ya
    /// sigue <c>RenglonDeCaso.ParaElLector</c>.
    /// </remarks>
    public string NombreDeLaPastillaParaElLector => $"{NumeroDeCaso}: {Lectura.ParaElLector}. Pulse para verlo.";

    /// <summary>Que falta y a quien le toca, o quien lo dio por bueno y cuando.</summary>
    /// <remarks>
    /// Es la otra mitad de la decision del dueno: <i>«me falta siempre puede decir que falta y
    /// a quien le toca, y lo dice cuando el lo pide, no de entrada»</i>. Se lee al pulsar la
    /// pastilla, no en la tarjeta: el ya rechazo por escrito los avisos que ocupan media
    /// pantalla.
    /// </remarks>
    public string DetalleDelEstado => Lectura.Detalle;

    /// <summary>Si esta tarjeta se lee «resuelto»; enciende su pastilla y no la otra.</summary>
    /// <remarks>
    /// <para>Son dos banderas y no una propiedad de color porque el color tiene que resolverse
    /// con el tema del ELEMENTO y no con el de la aplicacion. Es la causa medida del 1,70:1 del
    /// 2026-09-05 en la franja de avisos: un pincel sacado de
    /// <c>Application.Current.Resources</c> se resuelve con el tema de la APLICACION, que se fija
    /// al arrancar, mientras la letra de al lado sigue el del elemento — y con los dos temas
    /// distintos el fondo y la letra se acercan hasta perderse.</para>
    ///
    /// <para>Con una pastilla por lectura, cada una con su <c>ThemeResource</c> fijo, XAML vuelve
    /// a resolver los colores cuando el dueno cambia el tema y no hay pincel que viaje en un
    /// dato. Lo que decide cual se ve es el dato; lo que decide de que color es, el tema.</para>
    /// </remarks>
    public bool SeVeResuelto => Lectura.EsResuelto;

    /// <summary>Si esta tarjeta se lee «me falta».</summary>
    public bool SeVeMeFalta => Lectura.EsMeFalta;

    /// <summary>El motivo que va DENTRO del detalle, sin la frase de quien lo dijo delante.</summary>
    /// <remarks>
    /// <see cref="LineaDelMotivo"/> compone la frase entera para la tarjeta; aqui hace falta
    /// solo el motivo, porque <see cref="LoQueSeLeeDeUnDocumento"/> le pone delante «el
    /// compañero dijo:» y con las dos saldria dicho dos veces.
    /// </remarks>
    private string MotivoParaElDetalle
    {
        get
        {
            if (Motivo != MotivoDeNoCompletar.SinMotivo) return PalabrasDelEstado.DecirElMotivo(Motivo);
            return MotivoQueDijoElCompanero == MotivoDeNoCompletar.SinMotivo
                ? string.Empty
                : PalabrasDelEstado.DecirElMotivo(MotivoQueDijoElCompanero);
        }
    }

    /// <summary>
    /// El motivo VIGENTE, que es el que puso Miguel (<c>casos.motivo_no_completa</c>).
    /// </summary>
    public MotivoDeNoCompletar Motivo { get; init; }

    /// <summary>Lo que dijo la hoja del companero (<c>casos.motivo_del_companero</c>).</summary>
    /// <remarks>
    /// Va aparte y no se mezcla con <see cref="Motivo"/> a proposito: son las dos columnas que
    /// la migracion 18 desdoblo para que la correccion de Miguel no borre lo que dijo el
    /// companero, y para que se pueda ver que discreparon (ADR-0005 §3.3).
    /// </remarks>
    public MotivoDeNoCompletar MotivoQueDijoElCompanero { get; init; }

    /// <summary>
    /// Por que no esta completa, tal como se lee en la tarjeta; vacio si nadie lo ha dicho.
    /// </summary>
    /// <remarks>
    /// <para>Existe para que el dueno vea el motivo <b>sin abrir el documento</b>, que fue lo
    /// que pidio el 2026-09-05 al nombrar sus tres estados: «no completado, no se pudo
    /// comunicar con el lider, o el lider no lo hizo».</para>
    ///
    /// <para>El de Miguel se dice a secas y el del companero <b>dice de quien es</b>. No es
    /// adorno: son dos afirmaciones distintas, y ensenarlas con la misma frase haria creer que
    /// Miguel dijo algo que no dijo.</para>
    ///
    /// <para>«No completado» a secas NO produce linea: un documento sin motivo dicho no lleva
    /// aqui una palabra inventada.</para>
    ///
    /// <para>⚠️ Un archivado que ya viajo tampoco la lleva, y por eso la guarda mira
    /// <see cref="EstadoQueSeVe"/> y no <see cref="Estado"/>: la tarjeta que dice «fecha pasada
    /// completada» no puede llevar debajo por que no se completo. Serian dos frases del mismo
    /// documento diciendose que no, y la de abajo hablaria de un trabajo que el dueno ya hizo a
    /// mano antes de que existiera el programa.</para>
    /// </remarks>
    public string LineaDelMotivo
    {
        get
        {
            if (EstadoQueSeVe != EstadoQueSeVe.NoCompleta) return string.Empty;
            if (Motivo != MotivoDeNoCompletar.SinMotivo) return PalabrasDelEstado.DecirElMotivo(Motivo);
            return MotivoQueDijoElCompanero == MotivoDeNoCompletar.SinMotivo
                ? string.Empty
                : $"el compañero dijo: {PalabrasDelEstado.DecirElMotivo(MotivoQueDijoElCompanero)}";
        }
    }

    /// <summary>Si el caso esta archivado.</summary>
    public bool Archivado { get; init; }

    /// <summary>Cuando se archivo, o vacio.</summary>
    public string FechaDeArchivado { get; init; } = string.Empty;

    /// <summary>Si este documento repite a otro que ya estaba. Entra igual y no pisa nada.</summary>
    public bool EsDuplicado { get; init; }

    /// <summary>
    /// «duplicado de CASP2609_Ana_Prueba.pdf hoja 1», o vacio si no repite a nadie.
    /// </summary>
    /// <remarks>
    /// <para>⛔ Existe porque el aviso se quedaba a medias. Medido por QA sobre el paquete
    /// publicado el 2026-09-04: la base guardaba <c>duplicado_de</c> y el resumen de la tanda
    /// decia «7 duplicados avisados», pero en Revisar las tarjetas repetidas no llevaban NADA.
    /// El dueno pidio «si hay documentos duplicados debe decirlo y no rechazarlo»: no se
    /// rechazaban, pero tampoco se decia donde el trabaja.</para>
    ///
    /// <para>Nombra el ARCHIVO y la hoja, y no solo el numero de caso, porque el numero no
    /// distingue: los siete escaneos del dueno comparten <c>CASP2609</c> y son de siete
    /// familias distintas (<see cref="Importar.BuscadorDeDuplicados"/>).</para>
    /// </remarks>
    public string MarcaDeDuplicado { get; init; } = string.Empty;

    /// <summary>
    /// Cuantos documentos a la vista llevan este mismo numero de caso, contandose a si mismo.
    /// </summary>
    /// <remarks>
    /// <para>Del dueno, 2026-09-05, peticion 11: «documentos que tienen el numero de caso
    /// iguales puede significar que viajaran en el mismo grupo; es importante saber esto».
    /// Sus siete escaneos comparten <c>CASP2609</c>, asi que aqui vale 7 en cada uno.</para>
    ///
    /// <para>⛔ <b>Esto NO deshace la decision del 2026-09-03.</b> El numero son cuatro
    /// letras de unidad mas el ano y el mes, asi que <b>no identifica a una familia</b> y por
    /// eso se le quito la unicidad y los seis documentos del dueno dejaron de rechazarse.
    /// Sigue sin ser identidad: esto solo lo <b>ensena</b>. No junta documentos, no marca
    /// duplicados —eso es <see cref="EsDuplicado"/>, otra cosa— y no cambia el arbol de
    /// carpetas, que se sigue haciendo por fecha y unidad.</para>
    ///
    /// <para>Cuenta sobre lo que hay <b>a la vista</b>: los archivados no entran, porque «cuando
    /// yo archive, debe salir del sistema visible». Con el buscador puesto sigue siendo la
    /// cifra de la base y no la de lo filtrado (<see cref="TableroDeRevisar"/>): decir «1
    /// documento con este numero» de siete que hay seria un numero falso en pantalla.</para>
    /// </remarks>
    public int CuantosCompartenElNumero { get; init; } = 1;

    /// <summary>Si hay al menos otro documento con el mismo numero de caso.</summary>
    public bool CompartenElNumero => CuantosCompartenElNumero > 1;

    /// <summary>
    /// «7 documentos con este numero: puede que viajen en el mismo grupo», o vacio.
    /// </summary>
    /// <remarks>
    /// Dice «puede que» porque es lo que es: una pista, no un hecho. El numero no identifica
    /// a nadie, y afirmar que viajan juntos seria inventarlo.
    /// </remarks>
    public string LineaDelNumeroCompartido
        => CompartenElNumero
            ? $"{Plural.Con(CuantosCompartenElNumero, "documento", "documentos")} con este número: "
              + "puede que viajen en el mismo grupo"
            : string.Empty;

    /// <summary>Si no lo lleva nadie ahora mismo.</summary>
    public bool SinAsignar { get; init; }

    /// <summary>Quien lo lleva, o «sin asignar». Con dos vivos, los dos nombres.</summary>
    public string AsignadoA { get; init; } = RenglonParaAsignar.SinAsignar;

    /// <summary>
    /// Quien puso el estado y cuando: «Completada por Sandy · 2026-08-30». Vacio si nadie
    /// lo ha dicho todavia.
    /// </summary>
    public string Firma { get; init; } = string.Empty;

    /// <summary>La linea de datos de la tarjeta, en un renglon (requisito 4: ni un parrafo).</summary>
    public string Datos
    {
        get
        {
            var hoja = string.IsNullOrEmpty(Hoja) ? string.Empty : $" · {Hoja}";
            return $"{Plural.Con(Personas, "persona", "personas")} · {FechaDeViaje} · {UnidadQueSeLee}{hoja}";
        }
    }

    /// <summary>La linea del pie de la tarjeta: la firma y, si lo esta, el archivado.</summary>
    public string PieDeLaTarjeta
    {
        get
        {
            var archivado = Archivado
                ? (string.IsNullOrEmpty(FechaDeArchivado) ? "archivado" : $"archivado el {FechaDeArchivado}")
                : string.Empty;
            if (Firma.Length == 0) return archivado;
            return archivado.Length == 0 ? Firma : $"{Firma} · {archivado}";
        }
    }

    /// <summary>El nombre del archivo sin la ruta; la ruta entera no cabe en la tarjeta.</summary>
    /// <param name="ruta">La ruta entera del PDF, con barras de cualquiera de los dos tipos; nula o vacía da «sin archivo».</param>
    public static string NombreDelArchivo(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return "sin archivo";
        var corte = ruta.LastIndexOfAny(['\\', '/']);
        return corte < 0 ? ruta : ruta[(corte + 1)..];
    }

    /// <summary>
    /// La marca que lleva un duplicado, apuntando al documento del que repite.
    /// </summary>
    /// <remarks>
    /// El nulo NO se calla: un duplicado cuyo original ya no se puede leer sigue siendo un
    /// duplicado, y taparlo devolveria el defecto que esto cierra. Se dice lo que se sabe.
    /// </remarks>
    /// <param name="original">El caso del que repite, o nulo si no se pudo leer.</param>
    public static string ComponerMarcaDeDuplicado(Caso? original)
    {
        if (original is null) return "duplicado de un documento que ya no está en la base";

        var hoja = original.PaginaPdf is int pagina ? $" hoja {pagina}" : string.Empty;
        return $"duplicado de {NombreDelArchivo(original.RutaPdf)}{hoja}";
    }

    /// <summary>
    /// Compone la firma del estado con el nombre de quien lo marco y la fecha.
    /// </summary>
    /// <remarks>
    /// «Y dice completado por Sandy», dicho por el dueno el 2026-09-03. El nombre sale de
    /// <c>casos.estado_marcado_por</c>, que es el companero cuyo Excel volvio; si esa fila
    /// no esta, se dice «por alguien que ya no esta» en vez de callar el hecho de que
    /// alguien lo marco.
    /// </remarks>
    /// <param name="caso">El documento cuyo estado se firma.</param>
    /// <param name="nombres">El nombre de cada compañero por su número.</param>
    /// <returns>«Completada por Sandy · 2026-08-30», o vacío si el estado está sin marcar.</returns>
    public static string ComponerFirma(Caso caso, IReadOnlyDictionary<long, string> nombres)
    {
        if (caso.Estado == EstadoDeRecomendacion.SinMarcar) return string.Empty;

        var quien = caso.EstadoMarcadoPor is long id && nombres.TryGetValue(id, out var nombre)
            ? nombre
            : "alguien que ya no está en el equipo";
        var cuando = string.IsNullOrWhiteSpace(caso.EstadoMarcadoEn) ? "sin fecha" : caso.EstadoMarcadoEn;
        var verbo = caso.Estado == EstadoDeRecomendacion.Completa ? "Completada" : "Marcada no completa";
        return $"{verbo} por {quien} · {cuando}";
    }
}
