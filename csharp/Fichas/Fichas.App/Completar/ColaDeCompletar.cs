using Fichas.App.Grupo;
using Fichas.App.Inicio;

namespace Fichas.App.Completar;

/// <summary>
/// La cola de trabajo de la pestana «Completar»: los documentos a los que les falta un
/// dato, en el orden en que hay que resolverlos, y quien toca cuando uno se resuelve.
/// </summary>
/// <remarks>
/// <para><b>Por que existe.</b> Palabras del dueno el 2026-09-06: <i>«Cuando algo no leído
/// se lee y yo lo reviso y le doy guardar, debe pasar a otro renglón de "listo para
/// asignar" y me envía a mí a la pantalla donde está todo lo que no está completo, para yo
/// seguir trabajando. […] un flujo de trabajo donde yo vaya resolviendo casos de manera
/// automática, vaya donde tenga que ir»</i>. Lo que faltaba no era el veredicto —ya estaba
/// en <c>ModeloDeCorreccion.ListoParaAsignar</c> desde el 5— sino el ENCADENADO.</para>
///
/// <para>⛔ <b>Esta clase encadena y nada mas. No firma, no marca y no escribe una sola fila
/// en ninguna parte</b>, y eso es la regla permanente 5 de <c>CLAUDE.md</c>: la firma de
/// campos —<c>procedencia_campo.verificado</c>— es de Miguel y nunca es automatica. «Listo
/// para asignar» es otra cosa: una LECTURA del estado, la respuesta a «¿le falta algo a este
/// documento?», que el dueno dijo el 2026-09-05 que contesta el programa solo. Que no
/// escriba nada se vigila en <c>PruebasDeQueLaColaNoFirmaNada</c>, contando filas del
/// almacen y no mirando la pantalla.</para>
///
/// <para><b>Que entra en la cola, y por que es menos de lo que hay en «lo que no esta
/// completo».</b> <see cref="LectorDeIncompletos"/> junta dos poblaciones: los que tienen
/// huecos en nuestros campos y los que el Excel del companero devolvio marcados
/// <c>no_completa</c>. Aqui solo entran los primeros. La pestana es, con sus palabras,
/// <i>«completar información de documentos que faltan»</i>, y un documento sin ningun hueco
/// no se arregla escribiendo: se arregla hablando con el lider (<i>«yo debo llamar al
/// obispo»</i>, 2026-09-05). Si entrara, se corregiria, se guardaria y no saldria nunca de
/// la cola. Los <c>no_completa</c> siguen enteros donde ya estaban: en la ventana de
/// incompletos de Inicio y en el tablero «No completas» de Revisar.</para>
///
/// <para><b>El orden no se inventa aqui.</b> Es el que ya fija y prueba
/// <see cref="LectorDeIncompletos"/>: primero lo que todavia se puede salvar, del viaje mas
/// cercano al mas lejano; detras lo vencido; al final lo que no tiene fecha. Sale de sus
/// palabras del 2026-09-05: <i>«la prioridad son de gente que viajará pronto»</i> y <i>«no
/// quiero revisar de gente que viaja en noviembre estando en septiembre»</i>. Aplanar los
/// grupos conserva ese orden, asi que la cola tiene UNA sola verdad de orden y no dos.</para>
///
/// <para>⚠️ <b>La cola se lee una vez y se lleva en la mano.</b> Volver a leer la base
/// entera despues de cada guardado costaria una pasada por los 3 000 documentos por cada
/// pulsacion. El precio es que la lista puede quedar vieja —el mismo documento se pudo
/// corregir desde el desplegable de Correccion—, y por eso al avanzar se pregunta por cada
/// candidato si TODAVIA le falta algo: dos consultas cortas por salto, no una pasada.</para>
/// </remarks>
public sealed class ColaDeCompletar
{
    private readonly List<RenglonDeCaso> _pendientes;

    /// <summary>Monta la cola con sus documentos ya en orden y con el denominador de la base.</summary>
    /// <param name="pendientes">Los documentos con huecos, del viaje mas cercano al mas lejano.</param>
    /// <param name="casosNoArchivados">Cuantos casos quedan tras quitar los archivados.</param>
    /// <param name="casosEnLaBase">Cuantos casos hay en la base, archivados incluidos.</param>
    private ColaDeCompletar(List<RenglonDeCaso> pendientes, int casosNoArchivados, int casosEnLaBase)
    {
        _pendientes = pendientes;
        CasosNoArchivados = casosNoArchivados;
        CasosEnLaBase = casosEnLaBase;
    }

    /// <summary>
    /// Arma la cola a partir de lo que ya leyo <see cref="LectorDeIncompletos"/>.
    /// </summary>
    /// <remarks>
    /// Se toma el resumen ya leido en vez de leer aqui dentro: asi la cola no conoce ningun
    /// puerto, se prueba sin base y no hay una segunda forma de leer lo incompleto que se
    /// pueda separar de la primera.
    /// </remarks>
    /// <param name="resumen">Lo que devolvio el lector de incompletos.</param>
    public static ColaDeCompletar Desde(ResumenDeIncompletos resumen)
    {
        ArgumentNullException.ThrowIfNull(resumen);

        var pendientes = resumen.Grupos
            .SelectMany(grupo => grupo.Documentos)
            .Where(documento => documento.CuantoLeFalta > 0)
            .ToList();

        return new ColaDeCompletar(pendientes, resumen.CasosNoArchivados, resumen.CasosEnLaBase);
    }

    /// <summary>Los documentos que quedan por resolver, en el orden en que hay que hacerlo.</summary>
    public IReadOnlyList<RenglonDeCaso> Documentos => _pendientes;

    /// <summary>Cuantos quedan en la cola ahora mismo.</summary>
    public int Quedan => _pendientes.Count;

    /// <summary>Si ya no queda ninguno; la pantalla lo dice en vez de quedarse en blanco.</summary>
    public bool EstaVacia => _pendientes.Count == 0;

    /// <summary>Cuantos casos hay sin archivar; es la mitad del denominador.</summary>
    public int CasosNoArchivados { get; }

    /// <summary>Cuantos casos hay en la base, archivados incluidos; la otra mitad.</summary>
    public int CasosEnLaBase { get; }

    /// <summary>El primero de la cola, o nulo si esta vacia.</summary>
    public long? Primero => _pendientes.Count == 0 ? null : _pendientes[0].CasoId;

    /// <summary>La linea del denominador de la cabecera, ya compuesta.</summary>
    public string LineaDelDenominador
        => TextoDeLaCola.LineaDelDenominador(Quedan, CasosNoArchivados, CasosEnLaBase);

    /// <summary>«3 de 8», o vacio si ese documento no esta en la cola.</summary>
    public string PosicionDe(long casoId)
    {
        var donde = _pendientes.FindIndex(documento => documento.CasoId == casoId);
        return donde < 0 ? string.Empty : $"{donde + 1} de {_pendientes.Count}";
    }

    /// <summary>El renglon de ese documento, o nulo si ya no esta en la cola.</summary>
    public RenglonDeCaso? RenglonDe(long casoId)
        => _pendientes.Find(documento => documento.CasoId == casoId);

    /// <summary>
    /// Ese documento se resolvio: sale de la cola y se dice cual toca ahora.
    /// </summary>
    /// <remarks>
    /// <para>El siguiente es el que pasa a ocupar SU sitio, que es el que viaja despues de
    /// el. Si era el ultimo, toca el que quedo delante: dejar la pantalla en blanco con
    /// documentos aun por completar seria decirle que ha terminado cuando no ha terminado.</para>
    ///
    /// <para>De camino se saltan los que ya no tengan huecos —corregidos por otra via
    /// mientras la cola estaba en la mano—, y se sacan tambien: llevarlos delante seria
    /// abrir un documento que no tiene nada que arreglar.</para>
    /// </remarks>
    /// <param name="casoId">El que se acaba de resolver.</param>
    /// <param name="todaviaLeFalta">Pregunta si a un documento le sigue faltando algun dato.</param>
    /// <returns>El documento que toca abrir, o nulo si la cola quedo vacia.</returns>
    public long? SacarYDecirCualToca(long casoId, Func<long, bool> todaviaLeFalta)
    {
        ArgumentNullException.ThrowIfNull(todaviaLeFalta);

        var donde = _pendientes.FindIndex(documento => documento.CasoId == casoId);
        if (donde < 0) return null;

        _pendientes.RemoveAt(donde);
        return ElQueTocaDesde(donde, todaviaLeFalta);
    }

    /// <summary>
    /// Ese documento se guardo pero SIGUE incompleto: no sale de la cola y sigue tocando el.
    /// </summary>
    /// <remarks>
    /// La cola no adelanta a nadie por haber pulsado Guardar: adelanta por haber resuelto.
    /// Palabras del pase: <i>«Si no quedó completo, se queda en la cola con lo que le falta a
    /// la vista»</i>.
    /// </remarks>
    /// <param name="casoId">El que se acaba de guardar sin terminar de completarse.</param>
    /// <returns>El mismo documento, o nulo si ya no estaba en la cola.</returns>
    public long? DejarYDecirCualToca(long casoId)
        => _pendientes.Exists(documento => documento.CasoId == casoId) ? casoId : null;

    /// <summary>
    /// Desde esa posicion, el primer documento al que TODAVIA le falta algo; saca los demas.
    /// </summary>
    /// <remarks>
    /// Se mira hacia delante desde el hueco que dejo el resuelto y, si no queda nada
    /// delante, hacia atras. Los que ya no tienen huecos se quitan de la cola segun se
    /// encuentran, para no volver a tropezar con ellos en la siguiente vuelta.
    /// </remarks>
    private long? ElQueTocaDesde(int desde, Func<long, bool> todaviaLeFalta)
    {
        var donde = Math.Min(desde, _pendientes.Count - 1);

        while (donde >= 0 && _pendientes.Count > 0)
        {
            var candidato = _pendientes[donde].CasoId;
            if (todaviaLeFalta(candidato)) return candidato;

            _pendientes.RemoveAt(donde);
            donde = Math.Min(donde, _pendientes.Count - 1);
        }

        return null;
    }
}
