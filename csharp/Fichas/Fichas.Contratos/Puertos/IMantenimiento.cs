using System.Globalization;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;

namespace Fichas.Contratos.Puertos;

/// <summary>
/// Lo unico del programa que BORRA de verdad, y por eso lo unico que se para a preguntar.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Este archivo pasa de las 300 lineas y lleva dentro sus tres registros</b>
/// (<see cref="PlanDeBorrado"/>, <see cref="ResultadoDeBorrado"/> y
/// <see cref="ConteoDeTabla"/>), que por la forma de esta carpeta irian en
/// <c>Fichas.Contratos/Consultas/</c>. Es a proposito y tiene motivo escrito:
/// <c>Fichas.Contratos</c> esta congelada archivo por archivo
/// (<c>.claude/congelados.txt</c>, commit 82644c3) y el unico nombre autorizado para este
/// pase es este. Partirlo exige otro desbloqueo del supervisor; hasta entonces, un archivo
/// largo y coherente es mejor que un puerto a medias.
/// </para>
/// <para>
/// ⛔ <b>La excepcion declarada.</b> El proyecto dice «avisar, nunca impedir»
/// (requisito 9) y ningun otro puerto abre un cuadro: se escribe y se deja la linea en la
/// franja. Aqui no. DECISIONES.md, 2026-09-04, «Requisitos del dueno para el programa
/// nuevo», punto 9: las dos unicas excepciones son la firma de Miguel y <b>borrar sin
/// preguntar</b>. Archivar no borra —el caso sigue contando en los reportes— y por eso
/// archivar no pregunta; esto si.
/// </para>
/// <para>
/// <b>El orden es copia → pregunta → borrado</b>, y no es indiferente. La copia se hace
/// en <c>Planear…</c>, ANTES de que nadie conteste, para que la pregunta pueda decir
/// donde quedo: preguntar primero y copiar despues dejaria al dueno contestando «si» sin
/// saber si habia red debajo. Si la copia no se pudo hacer, el plan vuelve con
/// <see cref="PlanDeBorrado.SePuedeBorrar"/> en falso y su motivo: no se borra a ciegas.
/// </para>
/// <para>
/// Por que existe: el dueno, 2026-09-05, «necesito un boton para dejar tambien todo en
/// limpio y eliminar todo, porque me dejaste muchos documentos de prueba que no se como
/// borrar», y «tampoco tengo la opcion de eliminar o quitar agentes del sistema». Hasta
/// hoy solo se podia archivar, y archivar no quita nada de la base.
/// </para>
/// <para>
/// <b>Quién lo implementa:</b> solo
/// <c>Fichas.Datos.Mantenimiento.RepositorioDeMantenimiento</c>, con la copia en
/// <c>RespaldoAntesDeBorrar</c>. <b>No hay doble en <c>Fichas.Datos.Falso</c></b>: sin base
/// en un archivo no hay nada que copiar, y la app deja este servicio a nulo con
/// <c>--falso</c>. <b>Quién lo consume:</b> Revisar (<c>PaginaDeRevisar</c> planea,
/// <c>OperacionDeBorrar</c> ejecuta), Importar (los PDF que entraron sin información) y el
/// equipo de Asignar (<c>PanelDelEquipo</c>: carga, quitar y reactivar a un compañero).
/// </para>
/// <para>
/// ⚠️ <b>El orden real es cuenta → copia → plan</b>, no copia → cuenta: se cuenta primero
/// para no copiar la base cuando no hay nada que borrar (medido en el código el 2026-09-11).
/// Lo que el párrafo de arriba fija, y sigue siendo cierto, es que la copia va ANTES de
/// preguntar.
/// </para>
/// </remarks>
public interface IMantenimiento
{
    /// <summary>Prepara el borrado de los documentos marcados: copia la base y cuenta.</summary>
    /// <remarks>
    /// Cuenta lo que cuelga de esos casos tabla por tabla (personas, procedencia, contactos,
    /// asignaciones, renglones ilegibles y los propios casos), hace la copia y devuelve el
    /// plan. No escribe nada permanente: usa una tabla temporal para los ids. ⚠️ No comprueba
    /// que los ids existan: con ids que no están, copia igual y da permiso sobre conteos a 0;
    /// entonces <see cref="PlanDeBorrado.NoHayNadaQueBorrar"/> es lo que tiene que mirar la
    /// pantalla antes de preguntar.
    /// </remarks>
    /// <param name="casoIds">Los numeros internos de los documentos marcados; vacía da un plan sin permiso y sin copia.</param>
    /// <returns>Un plan con alcance <see cref="AlcanceDelBorrado.Documentos"/>; sin permiso si la copia no se pudo hacer, con el motivo en sus avisos.</returns>
    PlanDeBorrado PlanearDocumentos(IReadOnlyCollection<long> casoIds);

    /// <summary>
    /// Prepara «empezar de cero»: la base sin ningun documento y CON los companeros.
    /// </summary>
    /// <remarks>
    /// Los companeros se quedan a proposito (el dueno: «dejar todo en limpio»): son el
    /// equipo, no datos de prueba, y volver a teclearlos seria trabajo perdido. Lo que si
    /// se va es todo lo que cuelga de un documento, incluidos los renglones de lo que no
    /// se pudo leer y las filas del Excel que no entraron: son restos de importaciones que
    /// ya no tienen documento al que apuntar.
    /// </remarks>
    /// <returns>Un plan con alcance <see cref="AlcanceDelBorrado.TodoEnLimpio"/>. Si la base ya está vacía, vuelve sin permiso, sin copia y con un aviso informativo que lo dice; si la copia falló, sin permiso con su motivo.</returns>
    PlanDeBorrado PlanearEmpezarDeCero();

    /// <summary>
    /// Prepara quitar a un companero del equipo; solo sale adelante si no lleva nada.
    /// </summary>
    /// <remarks>
    /// Si lleva algo a su nombre —una asignacion, una firma, una fila devuelta, un estado
    /// marcado— el plan vuelve SIN permiso de borrar y diciendo cuantas cosas lleva. Lo que
    /// se hace entonces es DESACTIVARLO (<see cref="ICompaneros.Desactivar"/>), que le quita
    /// el trabajo nuevo y conserva su nombre en todo lo que ya hizo. Nunca se pierde el
    /// rastro de quien hizo que.
    /// </remarks>
    /// <param name="companeroId">A quién se quiere quitar del equipo.</param>
    /// <returns>Un plan con alcance <see cref="AlcanceDelBorrado.Companero"/> y su nombre. Sin permiso —y sin copia, porque se comprueba antes de copiar— si no está en la base o si lleva algo; con permiso y su copia si no lleva nada.</returns>
    PlanDeBorrado PlanearCompanero(long companeroId);

    /// <summary>
    /// Ejecuta un plan que el dueno ya contesto que si. Todo o nada.
    /// </summary>
    /// <remarks>
    /// Quien llama tiene que haber preguntado antes: este puerto no puede saber si se
    /// pregunto. Lo que si hace es negarse a ejecutar un plan que ya venia sin permiso, y
    /// volver a contar antes de borrar: entre la pregunta y el «si» pudo cambiar algo.
    /// <para>
    /// Lo que se vuelve a comprobar, en este orden: que no sea un plan de renglones
    /// ilegibles (ese va por <see cref="IIlegibles.BorrarRenglonesSinCaso"/>), que tenga
    /// permiso, que tenga copia, y —solo para un compañero— que siga sin llevar nada. Para
    /// documentos y «todo en limpio» se borra en una transacción, hijos antes que padres, y
    /// los casos que señalaban a un borrado como duplicado se quedan con
    /// <see cref="Caso.DuplicadoDe"/> a nulo. Si el motor rechaza algo, nada cambió.
    /// </para>
    /// </remarks>
    /// <param name="plan">El que devolvió uno de los tres <c>Planear…</c>, tal cual.</param>
    /// <returns>Borrado con las cifras que devolvió el motor tabla por tabla, o no borrado con su motivo. ⚠️ Para documentos, <see cref="ResultadoDeBorrado.SeBorro"/> vuelve verdadero aunque las cifras sean 0.</returns>
    ResultadoDeBorrado Borrar(PlanDeBorrado plan);

    /// <summary>Cuenta lo que un companero lleva a su nombre, columna por columna.</summary>
    /// <param name="companeroId">De quién.</param>
    /// <returns>Una entrada por columna que apunta a <c>companeros</c>, con su cifra, también las que dan 0; o <see cref="CargaDeUnCompanero.NoEsta"/> si no está en la base. No escribe nada.</returns>
    CargaDeUnCompanero Carga(long companeroId);

    /// <summary>
    /// Vuelve a poner activo a un companero desactivado, y le quita la fecha de baja.
    /// </summary>
    /// <remarks>
    /// Es la vuelta atras de <see cref="ICompaneros.Desactivar"/>, y por eso NO pregunta:
    /// lo que se puede deshacer con un boton no necesita un cuadro (requisito 9).
    /// <b>Escribe</b> <c>activo = 1</c> y <c>desactivado_en = NULL</c>. Es idempotente:
    /// reactivar a uno ya activo deja lo mismo y no avisa.
    /// </remarks>
    /// <param name="companeroId">A quién.</param>
    /// <returns>El id del compañero, o no escrito si no está en la base.</returns>
    ResultadoDeEscritura Reactivar(long companeroId);

    /// <summary>
    /// Prepara unificar un duplicado con su original: qué pasaría al original, copia la base y da permiso.
    /// </summary>
    /// <remarks>
    /// <para>Del dueño, 2026-09-14: <i>«agrega la función de unificar el caso duplicado con el
    /// caso original, para que se elimine el duplicado»</i>. Hasta ese día un duplicado solo se
    /// avisaba (2026-09-03, «nada se fusiona solo»); esto sigue sin fusionar solo: se planea, se
    /// pregunta con lo que va a pasar delante, y solo con el «sí» se ejecuta.</para>
    ///
    /// <para><b>Lo que decide, y por qué así.</b> Pasan al original las personas del duplicado
    /// que el original no tiene —misma cédula es la misma persona; sin cédula, mismo nombre
    /// exacto— y, de los cinco campos del caso (<see cref="PlanDeUnificacion.LosCincoCampos"/>),
    /// los que el original tiene vacíos y el duplicado trae. <b>Nunca se pisa lo que el original
    /// ya tiene</b>: es la regla del 2026-09-03 («el caso que ya estaba, con sus correcciones a
    /// mano y sus firmas, no se toca»). El estado y el motivo NO pasan: son la firma de un
    /// compañero sobre ese papel y la regla permanente 5 dice que nada se marca solo.</para>
    ///
    /// <para>El orden es cuenta → copia → plan, como en <see cref="PlanearDocumentos"/>: sin nada
    /// que unificar no se copia; sin copia, no hay permiso.</para>
    /// </remarks>
    /// <param name="duplicadoId">El documento marcado como duplicado.</param>
    /// <param name="hoy">La fecha de hoy en ISO-8601; con ella se retiran las asignaciones vivas.</param>
    /// <returns>Un plan con permiso y copia; o sin permiso, con el motivo en sus avisos: no está, no es duplicado, su original ya no está, o la copia no se pudo hacer.</returns>
    PlanDeUnificacion PlanearUnificacion(long duplicadoId, string hoy);

    /// <summary>
    /// Ejecuta un plan de unificación que el dueño ya contestó que sí. Todo o nada, en una transacción.
    /// </summary>
    /// <remarks>
    /// <para>Vuelve a leer los dos documentos antes de tocar nada: entre la pregunta y el «sí»
    /// pudo cambiar algo, y lo que se pasa se decide sobre lo que hay ahora, no sobre lo que se
    /// dijo. En este orden: las personas que pasan cambian de caso (sus filas de procedencia y su
    /// hoja viajan con ellas porque cuelgan de su id); los campos vacíos del original se rellenan y
    /// la fila de procedencia de ese campo pasa del duplicado al original (la del original, si la
    /// había, se retira: era la del vacío); las asignaciones del duplicado pasan al original y las
    /// vivas se desactivan con la fecha del plan, como al archivar; los contactos pasan; los
    /// documentos que señalaban al duplicado pasan a señalar al original; y el duplicado se borra
    /// por el camino de siempre, hijos antes que padres.</para>
    ///
    /// <para>Se niega, sin tocar nada, con un plan sin permiso, sin copia, o si alguno de los dos
    /// documentos ya no está o el duplicado ya no señala a ese original.</para>
    /// </remarks>
    /// <param name="plan">El que devolvió <see cref="PlanearUnificacion"/>, tal cual.</param>
    /// <returns>Unificado con las cifras de lo que pasó y de lo que cayó, o no unificado con su motivo.</returns>
    ResultadoDeUnificacion Unificar(PlanDeUnificacion plan);

    /// <summary>
    /// Le quita a un documento la marca de duplicado: pasa a ser un documento normal.
    /// </summary>
    /// <remarks>
    /// Es para el duplicado cuyo original ya no está en la base —la marca dice «duplicado de un
    /// documento que ya no está»— y con el que no hay nada con lo que unificar. Escribe
    /// <c>duplicado_de = NULL</c> y nada más: no borra, no mueve, no firma.
    /// </remarks>
    /// <param name="casoId">El documento.</param>
    /// <returns>El id del documento, o no escrito si no está en la base.</returns>
    ResultadoDeEscritura QuitarLaMarcaDeDuplicado(long casoId);
}

/// <summary>Uno de los cinco campos del caso que pueden pasar de un duplicado a su original.</summary>
/// <param name="Columna">La columna, tal como está en <c>casos</c>.</param>
/// <param name="Rotulo">Cómo se le dice al dueño en la pregunta.</param>
public sealed record CampoUnificable(string Columna, string Rotulo);

/// <summary>Un campo que va a pasar al original, con el valor que se va a escribir.</summary>
/// <param name="Columna">La columna, tal como está en <c>casos</c>.</param>
/// <param name="Rotulo">Cómo se le dice al dueño.</param>
/// <param name="Valor">Lo que trae el duplicado y se va a escribir en el original.</param>
public sealed record CampoQuePasaAlOriginal(string Columna, string Rotulo, string Valor)
{
    /// <summary>«fecha de viaje: 2026-10-08».</summary>
    public string Dicho => $"{Rotulo}: {Valor}";
}

/// <summary>Una persona del duplicado que el original no tiene y va a pasar a él.</summary>
/// <param name="Id">Su número interno; es la fila que cambia de caso.</param>
/// <param name="Nombre">Su nombre, o vacío si no se leyó.</param>
/// <param name="Mrn">Su cédula, o nula si no se leyó.</param>
/// <param name="Hoja">La hoja del PDF de la que salió, o nula; viaja con ella.</param>
public sealed record PersonaQuePasaAlOriginal(long Id, string Nombre, string? Mrn, int? Hoja)
{
    /// <summary>«Carla Prueba (1234567890003)», o lo que haya de las dos cosas.</summary>
    public string Dicho => (Nombre.Length, Mrn) switch
    {
        (0, null) => "una persona sin nombre ni cédula",
        (0, _) => $"cédula {Mrn}",
        (_, null) => Nombre,
        _ => $"{Nombre} ({Mrn})",
    };
}

/// <summary>
/// Lo que va a pasar al unificar, ya decidido y con la copia previa hecha. Todavía no se tocó nada.
/// </summary>
/// <remarks>
/// Como <see cref="PlanDeBorrado"/>: aquí está lo que hace falta para PREGUNTAR bien —qué pasa
/// al original, qué se retira, dónde quedó la copia— y ningún método que escriba. Escribir es
/// <see cref="IMantenimiento.Unificar"/>.
/// </remarks>
public sealed record PlanDeUnificacion
{
    /// <summary>Los cinco campos del caso que pueden pasar, en el orden en que se dicen.</summary>
    /// <remarks>
    /// Son las cinco columnas que Corrección dibuja y que «listo para asignar» mira. No entran el
    /// estado ni el motivo (firmas), ni la ruta y la hoja del PDF (identidad del documento), ni
    /// captura manual ni archivado ni fechas del programa.
    /// </remarks>
    public static IReadOnlyList<CampoUnificable> LosCincoCampos { get; } =
    [
        new("numero_caso", "número de caso"),
        new("unidad_numero", "número de unidad"),
        new("unidad_nombre", "unidad"),
        new("fecha_viaje", "fecha de viaje"),
        new("templo_nombre", "templo"),
    ];

    /// <summary>Lo que va en el botón que NO unifica.</summary>
    public const string TextoDelBotonQueNoUnifica = "No unificar";

    /// <summary>El documento marcado como duplicado, que es el que se va a borrar.</summary>
    public long DuplicadoId { get; init; }

    /// <summary>El original, que es el que se queda.</summary>
    public long OriginalId { get; init; }

    /// <summary>«CASP2609_Ana_Prueba.pdf hoja 2»: cómo se nombra el duplicado.</summary>
    public string DuplicadoDicho { get; init; } = string.Empty;

    /// <summary>«CASP2609_Ana_Prueba.pdf hoja 1»: cómo se nombra el original.</summary>
    public string OriginalDicho { get; init; } = string.Empty;

    /// <summary>Si el original está archivado; entonces lo que pase quedará archivado con él.</summary>
    public bool OriginalArchivado { get; init; }

    /// <summary>Las personas del duplicado que el original no tiene.</summary>
    public IReadOnlyList<PersonaQuePasaAlOriginal> PersonasQuePasan { get; init; } = Array.Empty<PersonaQuePasaAlOriginal>();

    /// <summary>Cuántas personas del duplicado ya estaban en el original y se van con él.</summary>
    public int PersonasQueYaEstaban { get; init; }

    /// <summary>Los campos vacíos en el original que el duplicado trae.</summary>
    public IReadOnlyList<CampoQuePasaAlOriginal> CamposQuePasan { get; init; } = Array.Empty<CampoQuePasaAlOriginal>();

    /// <summary>Cuántas asignaciones vivas tiene el duplicado; se retiran con fecha.</summary>
    public int AsignacionesVivasQueSeRetiran { get; init; }

    /// <summary>La fecha con la que se retiran, ISO-8601.</summary>
    public string Hoy { get; init; } = string.Empty;

    /// <summary>Dónde quedó la copia de la base, o nulo si no se llegó a hacer.</summary>
    public string? RutaDeLaCopia { get; init; }

    /// <summary>Si se puede seguir adelante; falso deja el motivo en <see cref="Avisos"/>.</summary>
    public bool SePuedeUnificar { get; init; }

    /// <summary>Lo que hay que decir en la franja cuando el plan no sale adelante.</summary>
    public IReadOnlyList<Aviso> Avisos { get; init; } = Array.Empty<Aviso>();

    /// <summary>Si no hay nada que pasar al original: entonces unificar solo borra el duplicado.</summary>
    public bool NoHayNadaQuePasar => PersonasQuePasan.Count == 0 && CamposQuePasan.Count == 0;

    /// <summary>El título del cuadro: la acción con el original delante, nunca un «¿seguro?».</summary>
    public string Titulo => $"Unificar con {OriginalDicho}";

    /// <summary>Lo que va en el botón que unifica; dice también que borra.</summary>
    public string TextoDelBoton => "Unificar y borrar el duplicado";

    /// <summary>
    /// El cuerpo de la pregunta: qué pasa al original, qué se retira, dónde quedó la copia y que
    /// el duplicado no vuelve.
    /// </summary>
    public string Pregunta
    {
        get
        {
            var lineas = new List<string>
            {
                $"El duplicado {DuplicadoDicho} se va a unificar con el original {OriginalDicho}.",
                string.Empty,
            };

            if (NoHayNadaQuePasar)
            {
                lineas.Add("No hay nada que pasar al original: ya tiene todo lo que trae el duplicado.");
            }
            else
            {
                lineas.Add("Pasa al original lo que le falta:");
                if (PersonasQuePasan.Count > 0)
                {
                    lineas.Add($"    {Cuenta(PersonasQuePasan.Count, "persona", "personas")}: "
                               + string.Join(", ", PersonasQuePasan.Select(p => p.Dicho)));
                }

                foreach (var campo in CamposQuePasan) lineas.Add("    " + campo.Dicho);
            }

            if (PersonasQueYaEstaban > 0)
            {
                lineas.Add($"{Cuenta(PersonasQueYaEstaban, "persona", "personas")} del duplicado ya "
                           + (PersonasQueYaEstaban == 1 ? "estaba" : "estaban")
                           + " en el original y se van con él.");
            }

            if (AsignacionesVivasQueSeRetiran > 0)
            {
                lineas.Add($"Se retira {Cuenta(AsignacionesVivasQueSeRetiran, "asignación viva", "asignaciones vivas")} "
                           + "del duplicado; quien lo llevaba conserva el rastro en su informe.");
            }

            if (OriginalArchivado)
            {
                lineas.Add("El original está archivado: lo que pase quedará archivado con él.");
            }

            lineas.Add(string.Empty);
            lineas.Add(RutaDeLaCopia is null
                ? "NO se pudo hacer copia previa de la base."
                : "Antes de unificar se hizo una copia de la base en:"
                  + Environment.NewLine + "    " + RutaDeLaCopia);
            lineas.Add(string.Empty);
            lineas.Add("El duplicado se borra de la base y no vuelve. Desde el programa esto no se puede deshacer.");

            return string.Join(Environment.NewLine, lineas);
        }
    }

    /// <summary>Un plan que no sale adelante, con el motivo ya escrito.</summary>
    /// <param name="duplicadoId">El documento que se quería unificar.</param>
    /// <param name="rutaDeLaCopia">Dónde quedó la copia si llegó a hacerse, o nulo.</param>
    /// <param name="avisos">Por qué no se puede; es lo que va a la franja.</param>
    /// <returns>Sin permiso y sin nada que listar.</returns>
    public static PlanDeUnificacion NoSePuede(long duplicadoId, string? rutaDeLaCopia, params Aviso[] avisos)
        => new() { DuplicadoId = duplicadoId, RutaDeLaCopia = rutaDeLaCopia, SePuedeUnificar = false, Avisos = avisos };

    /// <summary>«1 persona», «3 personas»: una cifra con su palabra.</summary>
    /// <param name="cuantas">La cifra.</param>
    /// <param name="singular">La palabra para una.</param>
    /// <param name="plural">La palabra para varias.</param>
    internal static string Cuenta(int cuantas, string singular, string plural)
        => cuantas.ToString(CultureInfo.InvariantCulture) + " " + (cuantas == 1 ? singular : plural);
}

/// <summary>Lo que quedó después de unificar, para decirlo en una línea y anotarlo.</summary>
public sealed record ResultadoDeUnificacion
{
    /// <summary>Si de verdad se unificó.</summary>
    public bool SeUnifico { get; init; }

    /// <summary>El original, que es el que queda.</summary>
    public long OriginalId { get; init; }

    /// <summary>El duplicado, que ya no está.</summary>
    public long DuplicadoId { get; init; }

    /// <summary>Cómo se nombra el original.</summary>
    public string OriginalDicho { get; init; } = string.Empty;

    /// <summary>Cómo se nombraba el duplicado; es el rastro que queda de qué PDF y hoja venía.</summary>
    public string DuplicadoDicho { get; init; } = string.Empty;

    /// <summary>Cuántas personas pasaron al original.</summary>
    public int PersonasQuePasaron { get; init; }

    /// <summary>Cuántos campos del caso se rellenaron en el original.</summary>
    public int CamposQuePasaron { get; init; }

    /// <summary>Cuántas asignaciones vivas se retiraron.</summary>
    public int AsignacionesRetiradas { get; init; }

    /// <summary>Qué cayó con el duplicado, tabla por tabla, con las cifras del motor.</summary>
    public IReadOnlyList<ConteoDeTabla> Borradas { get; init; } = Array.Empty<ConteoDeTabla>();

    /// <summary>Dónde quedó la copia previa.</summary>
    public string? RutaDeLaCopia { get; init; }

    /// <summary>Lo que hay que decir en la franja; vacío si todo fue bien.</summary>
    public IReadOnlyList<Aviso> Avisos { get; init; } = Array.Empty<Aviso>();

    /// <summary>
    /// La línea del acuse: «Unificado con CASP2609_… hoja 1: 1 persona y 2 campos pasaron al
    /// original; el duplicado se borró (copia en …).»
    /// </summary>
    public string Linea
    {
        get
        {
            if (!SeUnifico) return "No se unificó nada.";

            var paso = PersonasQuePasaron == 0 && CamposQuePasaron == 0
                ? "nada tenía que pasar al original"
                : $"{PlanDeUnificacion.Cuenta(PersonasQuePasaron, "persona", "personas")} y "
                  + $"{PlanDeUnificacion.Cuenta(CamposQuePasaron, "campo", "campos")} pasaron al original";
            var copia = RutaDeLaCopia is null ? string.Empty : $" (copia en {RutaDeLaCopia})";
            return $"Unificado con {OriginalDicho}: {paso}; el duplicado se borró{copia}.";
        }
    }

    /// <summary>La línea que va a <c>fichas.log</c>: ids y cifras, sin nombres ni cédulas.</summary>
    public string LineaDelRegistro
    {
        get
        {
            var borrado = string.Join(
                ", ",
                Borradas.Select(c => c.Tabla + "=" + c.Filas.ToString(CultureInfo.InvariantCulture)));
            var copia = RutaDeLaCopia is null ? "sin copia" : $"copia previa en «{RutaDeLaCopia}»";
            return (SeUnifico ? "UNIFICADO  " : "UNIFICACION NO EJECUTADA  ")
                   + $"caso {DuplicadoId.ToString(CultureInfo.InvariantCulture)} -> caso {OriginalId.ToString(CultureInfo.InvariantCulture)}  "
                   + $"personas={PersonasQuePasaron.ToString(CultureInfo.InvariantCulture)} "
                   + $"campos={CamposQuePasaron.ToString(CultureInfo.InvariantCulture)} "
                   + $"asignaciones_retiradas={AsignacionesRetiradas.ToString(CultureInfo.InvariantCulture)}  "
                   + $"borrado: {borrado}  {copia}";
        }
    }

    /// <summary>El resultado de un plan que no salió adelante, con su motivo.</summary>
    /// <param name="rutaDeLaCopia">La copia del plan, si la había.</param>
    /// <param name="avisos">Por qué no se unificó; es lo que va a la franja.</param>
    /// <returns><see cref="SeUnifico"/> en falso y sin cifras.</returns>
    public static ResultadoDeUnificacion NoSeUnifico(string? rutaDeLaCopia, params Aviso[] avisos)
        => new() { SeUnifico = false, RutaDeLaCopia = rutaDeLaCopia, Avisos = avisos };
}

/// <summary>Que se va a borrar: unos documentos, todos, o un companero.</summary>
public enum AlcanceDelBorrado
{
    /// <summary>Los documentos marcados y todo lo que cuelga de ellos.</summary>
    Documentos,

    /// <summary>Todos los documentos; los companeros se quedan.</summary>
    TodoEnLimpio,

    /// <summary>Un companero que no lleva nada a su nombre.</summary>
    Companero,
}

/// <summary>
/// Cuantas cosas lleva un companero a su nombre, y por donde.
/// </summary>
/// <remarks>
/// Se cuenta preguntandole al esquema por sus claves foraneas
/// (<c>PRAGMA foreign_key_list</c>) y NO con una lista escrita a mano: hoy hay siete
/// columnas que apuntan a <c>companeros</c> —en <c>asignaciones</c>, <c>procedencia_campo</c>,
/// <c>filas_descartadas</c>, <c>casos</c> (dos), <c>personas</c> y <c>contactos</c>— y la
/// migracion 18 puede anadir otra. Una lista escrita a mano se queda corta ese dia, y
/// entonces el programa diria «no lleva nada» de alguien que si llevaba.
/// </remarks>
/// <param name="CompaneroId">De quien se cuenta.</param>
/// <param name="Nombre">Como se llama, para poder decirlo en la pregunta.</param>
/// <param name="EstaActivo">Si ahora mismo puede recibir trabajo nuevo.</param>
/// <param name="Detalle">Una entrada por columna que apunta a el, con su cifra.</param>
public sealed record CargaDeUnCompanero(
    long CompaneroId,
    string Nombre,
    bool EstaActivo,
    IReadOnlyList<ConteoDeTabla> Detalle)
{
    /// <summary>Cuantas cosas lleva en total.</summary>
    public int Total => Detalle.Sum(d => d.Filas);

    /// <summary>Si no lleva nada, que es la unica condicion para poder borrarlo de verdad.</summary>
    public bool NoLlevaNada => Total == 0;

    /// <summary>Lo que lleva, dicho en una linea: «3 asignaciones, 12 firmas».</summary>
    public string Dicho
    {
        get
        {
            var conAlgo = Detalle.Where(d => d.Filas > 0).Select(d => d.Dicho).ToList();
            return conAlgo.Count == 0 ? "nada a su nombre" : string.Join(", ", conAlgo);
        }
    }

    /// <summary>El companero que no esta en la base.</summary>
    /// <param name="companeroId">El id que se preguntó, para poder decirlo.</param>
    /// <returns>Sin nombre, inactivo y sin detalle; <see cref="NoLlevaNada"/> da verdadero, así que quien lo use tiene que mirar el nombre vacío antes de creerlo.</returns>
    public static CargaDeUnCompanero NoEsta(long companeroId)
        => new(companeroId, string.Empty, false, Array.Empty<ConteoDeTabla>());
}

/// <summary>Cuantas filas de una tabla entran en el borrado, y como se dicen en espanol.</summary>
/// <remarks>
/// Las dos formas se escriben a mano y no las adivina un pluralizador, por la misma razon
/// que en <c>ResumenDeLote</c>: acertaria con «documento» y fallaria con cualquier palabra
/// que no haga el plural en «-s», y un programa que inventa una palabra en espanol delante
/// de quien lo usa es peor que uno repetitivo.
/// </remarks>
/// <param name="Tabla">El nombre de la tabla, tal cual esta en el esquema.</param>
/// <param name="Filas">Cuantas filas.</param>
/// <param name="Singular">Como se dice una: «documento».</param>
/// <param name="Plural">Como se dicen varias: «documentos».</param>
public sealed record ConteoDeTabla(string Tabla, int Filas, string Singular, string Plural)
{
    /// <summary>«7 documentos», «1 persona», «0 contactos».</summary>
    public string Dicho =>
        Filas.ToString(CultureInfo.InvariantCulture) + " " + (Filas == 1 ? Singular : Plural);
}

/// <summary>
/// Lo que se va a borrar, ya contado y con la copia previa hecha. Todavia no se borro nada.
/// </summary>
/// <remarks>
/// Lo que hay aqui es lo que hace falta para PREGUNTAR bien: las cifras de verdad, la
/// ruta de la copia y el permiso. No hay ningun metodo que borre: borrar es
/// <see cref="IMantenimiento.Borrar"/>, y asi un plan no puede ejecutarse solo.
/// </remarks>
public sealed record PlanDeBorrado
{
    /// <summary>Que clase de borrado es.</summary>
    public AlcanceDelBorrado Alcance { get; init; }

    /// <summary>Los ids afectados: los casos marcados, o el unico companero.</summary>
    public IReadOnlyList<long> Ids { get; init; } = Array.Empty<long>();

    /// <summary>El nombre del companero, cuando el borrado es de uno; vacio si no.</summary>
    public string Nombre { get; init; } = string.Empty;

    /// <summary>Cuantas filas caen, tabla por tabla, en el orden en que se dicen.</summary>
    public IReadOnlyList<ConteoDeTabla> Conteos { get; init; } = Array.Empty<ConteoDeTabla>();

    /// <summary>Donde quedo la copia de la base, o nulo si no se llego a hacer.</summary>
    public string? RutaDeLaCopia { get; init; }

    /// <summary>Si se puede seguir adelante; falso deja el motivo en <see cref="Avisos"/>.</summary>
    public bool SePuedeBorrar { get; init; }

    /// <summary>Lo que hay que decir en la franja cuando el plan no sale adelante.</summary>
    public IReadOnlyList<Aviso> Avisos { get; init; } = Array.Empty<Aviso>();

    /// <summary>Cuantos documentos caen.</summary>
    public int Documentos => Cuantas("casos");

    /// <summary>Cuantas personas caen.</summary>
    public int Personas => Cuantas("personas");

    /// <summary>Si el plan no toca ni una fila; entonces no hay nada que preguntar.</summary>
    public bool NoHayNadaQueBorrar => Conteos.Count == 0 || Conteos.All(c => c.Filas == 0);

    /// <summary>Lo que va en el boton que NO borra.</summary>
    public const string TextoDelBotonQueNoBorra = "No borrar nada";

    /// <summary>
    /// El titulo del cuadro: la accion CON su numero delante, nunca un «¿seguro?».
    /// </summary>
    /// <remarks>
    /// Del pase del dueno: «una confirmacion con el numero delante». Un «¿seguro?» se
    /// contesta que si sin leerlo; «Borrar 7 documentos» no.
    /// </remarks>
    public string Titulo => Alcance switch
    {
        AlcanceDelBorrado.Companero => $"Borrar a {Nombre} del equipo",
        AlcanceDelBorrado.TodoEnLimpio => $"Dejar todo en limpio: borrar {Cuenta("casos")}",
        _ => "Borrar " + Cuenta("casos"),
    };

    /// <summary>Lo que va en el boton que borra; tambien con el numero delante.</summary>
    public string TextoDelBoton => Alcance == AlcanceDelBorrado.Companero
        ? $"Borrar a {Nombre}"
        : "Borrar " + Cuenta("casos");

    /// <summary>
    /// El cuerpo de la pregunta: cuanto cae, donde quedo la copia, y que no hay vuelta atras.
    /// </summary>
    public string Pregunta
    {
        get
        {
            var lineas = new List<string>();

            if (Alcance == AlcanceDelBorrado.Companero)
            {
                lineas.Add(
                    $"{Nombre} no lleva nada a su nombre, así que se puede quitar del equipo " +
                    "sin borrar el rastro de nadie.");
            }
            else
            {
                lineas.Add("Se van a borrar de la base, y no solo archivar:");
                lineas.Add(string.Empty);
                foreach (var conteo in Conteos)
                {
                    lineas.Add("    " + conteo.Dicho);
                }
            }

            lineas.Add(string.Empty);
            lineas.Add(RutaDeLaCopia is null
                ? "NO se pudo hacer copia previa de la base."
                : "Antes de borrar se hizo una copia de la base en:"
                  + Environment.NewLine + "    " + RutaDeLaCopia
                  + Environment.NewLine
                  + "Esa copia se abre con este mismo programa si hace falta volver atrás.");

            lineas.Add(string.Empty);
            lineas.Add("Desde el programa esto no se puede deshacer.");

            return string.Join(Environment.NewLine, lineas);
        }
    }

    /// <summary>Cuantas filas caen de esa tabla; 0 si la tabla no entra en el plan.</summary>
    /// <param name="tabla">El nombre de la tabla tal como está en el esquema (<c>casos</c>, <c>personas</c>).</param>
    public int Cuantas(string tabla)
        => Conteos.FirstOrDefault(c => c.Tabla == tabla)?.Filas ?? 0;

    /// <summary>Un plan que no sale adelante, con el motivo ya escrito.</summary>
    /// <param name="alcance">Qué clase de borrado se intentaba.</param>
    /// <param name="rutaDeLaCopia">Dónde quedó la copia si llegó a hacerse, o nulo; se conserva para que la pregunta pueda nombrarla.</param>
    /// <param name="avisos">Por qué no se puede; es lo que va a la franja.</param>
    /// <returns>Sin permiso, sin ids y sin conteos: no hay nada que preguntar.</returns>
    public static PlanDeBorrado NoSePuede(
        AlcanceDelBorrado alcance, string? rutaDeLaCopia, params Aviso[] avisos)
        => new()
        {
            Alcance = alcance,
            RutaDeLaCopia = rutaDeLaCopia,
            SePuedeBorrar = false,
            Avisos = avisos,
        };

    /// <summary>La cifra de esa tabla ya dicha en español, para el título y el botón.</summary>
    /// <param name="tabla">El nombre de la tabla tal como está en el esquema.</param>
    /// <returns>«7 documentos», «1 persona»; y «0 documentos» cuando la tabla no entra en el plan, que es por lo que un plan de renglones da un título que no sirve.</returns>
    private string Cuenta(string tabla)
        => Conteos.FirstOrDefault(c => c.Tabla == tabla)?.Dicho ?? "0 documentos";
}

/// <summary>Lo que quedo despues de borrar, para decirlo en una linea y anotarlo.</summary>
public sealed record ResultadoDeBorrado
{
    /// <summary>Si de verdad se borro algo.</summary>
    public bool SeBorro { get; init; }

    /// <summary>Que se borro, tabla por tabla, con las cifras que devolvio el motor.</summary>
    public IReadOnlyList<ConteoDeTabla> Borradas { get; init; } = Array.Empty<ConteoDeTabla>();

    /// <summary>Donde quedo la copia previa.</summary>
    public string? RutaDeLaCopia { get; init; }

    /// <summary>Lo que hay que decir en la franja; vacio si todo fue bien.</summary>
    public IReadOnlyList<Aviso> Avisos { get; init; } = Array.Empty<Aviso>();

    /// <summary>La linea del acuse del pie: que se borro, con su numero (punto 5 del pase).</summary>
    public string Linea
    {
        get
        {
            if (!SeBorro) return "No se borró nada.";

            var conAlgo = Borradas.Where(c => c.Filas > 0).Select(c => c.Dicho).ToList();
            var loQueCayo = conAlgo.Count == 0 ? "nada" : string.Join(", ", conAlgo);
            return $"Borrado: {loQueCayo}.";
        }
    }

    /// <summary>La linea que va a <c>fichas.log</c>, con la copia nombrada dentro.</summary>
    /// <remarks>
    /// Sin nombres ni MRN: el cuaderno de tiempos no es sitio para datos de personas. Van
    /// las tablas y sus cifras, que es lo que hace falta para saber que paso y cuando.
    /// </remarks>
    public string LineaDelRegistro
    {
        get
        {
            var detalle = string.Join(
                ", ",
                Borradas.Select(c => c.Tabla + "=" + c.Filas.ToString(CultureInfo.InvariantCulture)));
            var copia = RutaDeLaCopia is null ? "sin copia" : $"copia previa en «{RutaDeLaCopia}»";
            return (SeBorro ? "BORRADO  " : "BORRADO NO EJECUTADO  ") + detalle + "  " + copia;
        }
    }

    /// <summary>El resultado de un plan que no salio adelante, con su motivo.</summary>
    /// <param name="rutaDeLaCopia">La copia del plan, si la había, para que el acuse la nombre igual.</param>
    /// <param name="avisos">Por qué no se borró; es lo que va a la franja.</param>
    /// <returns><see cref="SeBorro"/> en falso y sin cifras.</returns>
    public static ResultadoDeBorrado NoSeBorro(string? rutaDeLaCopia, params Aviso[] avisos)
        => new() { SeBorro = false, RutaDeLaCopia = rutaDeLaCopia, Avisos = avisos };
}
