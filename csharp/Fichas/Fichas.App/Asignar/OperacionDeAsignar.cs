using Fichas.App.Cascara;
using Fichas.Reportes.Reglas;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Asignar;

/// <summary>
/// La UNICA puerta de asignar del programa. La tarjeta de Revisar, la lista de Asignar y
/// la correccion llaman aqui y a ningun otro sitio (criterio C5-1).
/// </summary>
/// <remarks>
/// Existe por lo que dijo el dueno el 2026-09-04: «debe poder asignarse desde cualquier
/// lugar». Hoy en el programa viejo el desplegable de la tarjeta dice «todavia no esta
/// conectado, se asigna en la pantalla Asignar casos», y eso pasa cuando cada pantalla
/// tiene su propia media operacion. Aqui hay una sola, y ademas es la que deja los avisos
/// en la franja: los tres sitios hacen UNA llamada y no repiten el manejo del aviso.
///
/// ⚠️ Lo que esta clase NO decide: si un caso puede llevarlo mas de un companero a la vez.
/// Es la P-11, abierta y devuelta al dueno (Fichas.Contratos/Modelos/Asignacion.cs). Al
/// asignar un caso que ya lleva otro, el motor lo permite y avisa; aqui no se retira al
/// primero por nuestra cuenta, porque nada se quita sin preguntar.
/// </remarks>
public sealed class OperacionDeAsignar
{
    /// <summary>El motor de asignaciones; es quien de verdad escribe y quien decide si un caso ya lo lleva otro.</summary>
    private readonly IAsignaciones _asignaciones;
    /// <summary>Con qué instante se fecha cada asignación y cada retirada.</summary>
    private readonly IReloj _reloj;
    /// <summary>La franja de la cáscara: aquí se dejan los avisos del motor para que las tres pantallas no los repitan.</summary>
    private readonly BuzonDeAvisos _avisos;

    /// <summary>Ata la operacion al motor de asignaciones, al reloj y al buzon de la franja.</summary>
    /// <param name="asignaciones">El motor de asignaciones.</param>
    /// <param name="reloj">El reloj del programa.</param>
    /// <param name="avisos">El buzón de la franja.</param>
    public OperacionDeAsignar(IAsignaciones asignaciones, IReloj reloj, BuzonDeAvisos avisos)
    {
        _asignaciones = asignaciones;
        _reloj = reloj;
        _avisos = avisos;
    }

    /// <summary>Asigna un caso a un companero. Sin filtro de estado: cualquier caso vale (requisito 8).</summary>
    /// <param name="casoId">El documento.</param>
    /// <param name="companeroId">A quién se le da.</param>
    /// <returns>Lo que dijo el motor; sus avisos ya quedaron en la franja.</returns>
    public ResultadoDeEscritura Asignar(long casoId, long companeroId)
    {
        var resultado = _asignaciones.Asignar(casoId, companeroId, _reloj.Ahora());
        _avisos.Dejar(resultado.Avisos);
        return resultado;
    }

    /// <summary>Asigna varios casos de una vez y devuelve la cuenta de lo que entro y lo que no.</summary>
    /// <param name="casoIds">Los documentos.</param>
    /// <param name="companeroId">A quién se le dan.</param>
    /// <param name="nombreDelCompanero">Su nombre, para la línea del acuse.</param>
    public ResumenDeAsignacion AsignarVarios(IReadOnlyCollection<long> casoIds, long companeroId, string nombreDelCompanero)
    {
        var entraron = 0;
        var noEntraron = 0;
        foreach (var casoId in casoIds)
        {
            // Una a una y por la misma puerta: asi el lote no tiene reglas propias que
            // se puedan separar de las de un caso suelto.
            if (Asignar(casoId, companeroId).SeEscribio) entraron++;
            else noEntraron++;
        }
        return new ResumenDeAsignacion(entraron, noEntraron, nombreDelCompanero);
    }

    /// <summary>
    /// Cuantos casos se le darian a ese companero si se siguiera adelante, y la pregunta.
    /// </summary>
    /// <remarks>
    /// <para>Va separado de <see cref="AsignarVarios"/> a proposito y por lo mismo que
    /// <see cref="MirarLoQueSeLeQuitaria"/>: <b>mirar no escribe nada</b>. Asi la pantalla
    /// puede decir cuantos son, dejar que el dueno se eche atras, y no haber tocado la base
    /// mientras tanto.</para>
    ///
    /// <para>Existe desde el 2026-09-09, con el reparto por grupo: asignar el grupo del 12 de
    /// septiembre toca quince documentos de una vez, y todo lo que en este programa toca
    /// muchos a la vez dice cuantos antes de hacerlo. Hasta hoy los dos botones de bloque de
    /// <c>PaginaDeGrupo</c> asignaban directo y lo decian despues; eso NO se toca aqui —es
    /// terreno de otro— pero esta puerta ya esta abierta para cuando se unifique.</para>
    ///
    /// <para>No se cuenta contra la base: los casos ya vienen contados por quien armo el
    /// grupo, y volver a preguntar seria leer otra vez lo que ya esta en la mano.</para>
    /// </remarks>
    /// <param name="casoIds">Los documentos del grupo que se iba a repartir.</param>
    /// <param name="nombreDelCompanero">A quien se le darian, para poder nombrarlo.</param>
    /// <param name="deQueGrupo">De que grupo son, tal como se lee en el panel.</param>
    public LoQueSeVaAAsignar MirarLoQueSeVaAAsignar(
        IReadOnlyCollection<long> casoIds, string nombreDelCompanero, string deQueGrupo)
    {
        ArgumentNullException.ThrowIfNull(casoIds);
        return new(casoIds.Count, nombreDelCompanero, deQueGrupo);
    }

    /// <summary>
    /// Cuantos casos se le quitarian a ese companero, y la pregunta con el numero delante.
    /// </summary>
    /// <remarks>
    /// <para>Va separado de <see cref="QuitarleTodo"/> a proposito: <b>mirar no escribe nada</b>.
    /// Asi la pantalla puede decir cuantos son, dejar que el dueno se eche atras, y no haber
    /// tocado la base mientras tanto.</para>
    ///
    /// <para>Se cuenta con <c>Contar</c> y no trayendo la lista: para pintar la pregunta hace
    /// falta el numero, no las filas.</para>
    /// </remarks>
    /// <param name="companeroId">A quién se le quitaría.</param>
    /// <param name="nombreDelCompanero">Su nombre, para la pregunta.</param>
    public LoQueSeLeQuitaria MirarLoQueSeLeQuitaria(long companeroId, string nombreDelCompanero)
        => new(
            _asignaciones.Contar(new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: true)),
            nombreDelCompanero);

    /// <summary>
    /// Le quita de golpe TODOS los casos que lleva vivos; los desactiva, nunca los borra.
    /// </summary>
    /// <remarks>
    /// <para>Lo pidio el dueno el 2026-09-07: <i>«no hay un botón para quitarle todos los casos
    /// asignados a una persona»</i>. Hasta hoy habia que abrir caso por caso.</para>
    ///
    /// <para>⛔ <b>Esto no borra nada del caso.</b> Ni su estado, ni el motivo que escribio el
    /// agente, ni la firma de Miguel, ni sus personas. El caso vuelve a estar sin asignar y ya.
    /// Y la asignacion se desactiva con su fecha en vez de borrarse, que es lo que conserva
    /// quien llevo que caso.</para>
    ///
    /// <para>Va por <see cref="IAsignaciones.Retirar"/> una a una y por la misma puerta que
    /// <see cref="RetirarDelCaso"/>: asi el lote no tiene reglas propias que se puedan separar
    /// de las de una asignacion suelta.</para>
    /// </remarks>
    /// <param name="companeroId">A quién se le quita todo.</param>
    /// <param name="nombreDelCompanero">Su nombre, para la línea del acuse.</param>
    public ResumenDeRetirada QuitarleTodo(long companeroId, string nombreDelCompanero)
    {
        var suyas = _asignaciones
            .Listar(
                new FiltroDeAsignaciones(CompaneroId: companeroId, SoloActivas: true),
                Pagina.Primera(int.MaxValue))
            .Elementos;

        var retirados = 0;
        var noSePudieron = 0;
        foreach (var asignacion in suyas)
        {
            var resultado = _asignaciones.Retirar(asignacion.Id, _reloj.Ahora());
            _avisos.Dejar(resultado.Avisos);
            if (resultado.SeEscribio) retirados++;
            else noSePudieron++;
        }

        return new ResumenDeRetirada(retirados, noSePudieron, nombreDelCompanero);
    }

    /// <summary>
    /// Le retira a ESE companero las asignaciones vivas de esos casos, y a nadie mas.
    /// </summary>
    /// <remarks>
    /// <para>Lo pidio el dueno el 2026-09-07 para el paquete que vuelve completo: <i>«cuando el
    /// sube un paquete que completo, debe quitarle que ese caso esta asignado a el»</i>. Quien
    /// decide QUE casos son es <see cref="Fichas.App.Paquetes.LimpiezaAlVolver"/>; aqui solo se
    /// retiran.</para>
    ///
    /// <para>⚠️ <b>Se filtra por companero y esa es la diferencia con
    /// <see cref="RetirarDelCaso"/>.</b> Un caso puede llevarlo mas de uno a la vez (la P-11
    /// sigue abierta), y quitarselo a todos porque uno lo devolvio completo le borraria el
    /// trabajo al otro sin que nadie lo haya pedido.</para>
    ///
    /// <para>Va por <see cref="IAsignaciones.Retirar"/>, la misma puerta que las otras dos: la
    /// fila se desactiva con su fecha y nunca se borra, asi que quien llevo que caso se
    /// conserva.</para>
    /// </remarks>
    /// <param name="companeroId">A quién se le retiran; las asignaciones de otros sobre los mismos casos no se tocan.</param>
    /// <param name="casoIds">Los documentos que devolvió completos.</param>
    /// <param name="nombreDelCompanero">Su nombre, para la línea del acuse.</param>
    public ResumenDeRetirada RetirarleEstosCasos(
        long companeroId, IReadOnlyCollection<long> casoIds, string nombreDelCompanero)
    {
        ArgumentNullException.ThrowIfNull(casoIds);

        var retirados = 0;
        var noSePudieron = 0;
        foreach (var casoId in casoIds)
        {
            foreach (var asignacion in _asignaciones.VivasDeCaso(casoId).Where(una => una.CompaneroId == companeroId))
            {
                var resultado = _asignaciones.Retirar(asignacion.Id, _reloj.Ahora());
                _avisos.Dejar(resultado.Avisos);
                if (resultado.SeEscribio) retirados++;
                else noSePudieron++;
            }
        }

        return new ResumenDeRetirada(retirados, noSePudieron, nombreDelCompanero);
    }

    /// <summary>Retira todas las asignaciones vivas de un caso; las desactiva, nunca las borra (C5-3).</summary>
    /// <param name="casoId">El documento.</param>
    /// <returns>El resultado de la última retirada; «no se escribió» con aviso si nadie lo llevaba.</returns>
    public ResultadoDeEscritura RetirarDelCaso(long casoId)
    {
        var vivas = _asignaciones.VivasDeCaso(casoId);
        if (vivas.Count == 0)
        {
            var nadie = Aviso.Informa("Este caso no lo lleva nadie ahora mismo; no habia nada que retirar.");
            _avisos.Dejar(nadie);
            return ResultadoDeEscritura.NoSeEscribio(nadie);
        }

        var ultima = ResultadoDeEscritura.NoSeEscribio();
        foreach (var asignacion in vivas)
        {
            ultima = _asignaciones.Retirar(asignacion.Id, _reloj.Ahora());
            _avisos.Dejar(ultima.Avisos);
        }
        return ultima;
    }
}

/// <summary>
/// Lo que se le quitaria a un companero si se siguiera adelante. Mirarlo no escribe nada.
/// </summary>
/// <remarks>
/// Existe para poder decir el numero ANTES de tocar la base, que es como pregunta este
/// programa cuando algo deshace trabajo. Deshacer una asignacion no borra nada y se puede
/// volver a asignar, asi que la pregunta va en la propia pantalla y no en un cuadro que
/// detiene el trabajo: el unico autorizado a detenerlo es borrar de verdad
/// (<c>Fichas.App/Revisar/OperacionDeBorrar.cs</c>).
/// </remarks>
/// <param name="CasosQueLleva">Cuantos casos lleva vivos ahora mismo.</param>
/// <param name="NombreDelCompanero">A quien se le quitarian, para poder nombrarlo.</param>
public sealed record LoQueSeLeQuitaria(int CasosQueLleva, string NombreDelCompanero)
{
    /// <summary>Si hay algo que quitar; con cero no se enciende el boton.</summary>
    public bool HayAlgoQueQuitar => CasosQueLleva > 0;

    /// <summary>La pregunta con el numero delante y con lo que NO se toca dicho.</summary>
    /// <remarks>
    /// <para>Dice lo que NO pasa porque es lo que hace que la pregunta se pueda contestar: sin
    /// esa frase, «quitarle todos los casos» se lee como si se perdiera el trabajo del agente.</para>
    ///
    /// <para>Las formas del verbo se calculan ANTES y no dentro del texto: un condicional
    /// metido en medio de la frase la parte en trozos que ya no se leen como espanol, ni por
    /// quien mantiene el archivo ni por la comprobacion de tildes de la pantalla.</para>
    /// </remarks>
    public string Pregunta
    {
        get
        {
            if (CasosQueLleva == 0) return $"{NombreDelCompanero} no lleva ningún caso: no hay nada que quitarle.";

            var esUnoSolo = CasosQueLleva == 1;
            var vuelven = esUnoSolo ? "Vuelve" : "Vuelven";
            var sePuedenDar = esUnoSolo ? "se le puede dar" : "se les puede dar";

            return $"Se le van a quitar a {NombreDelCompanero} sus "
                 + Plural.Con(CasosQueLleva, "caso", "casos")
                 + $". {vuelven} a estar sin asignar y {sePuedenDar} a otra persona. "
                 + "No se borra nada del documento: su estado, lo que contestó el agente y las firmas "
                 + "se quedan como están.";
        }
    }
}

/// <summary>
/// Lo que se le daria a un companero si se siguiera adelante. Mirarlo no escribe nada.
/// </summary>
/// <remarks>
/// Existe para poder decir el numero ANTES de tocar la base, que es como pregunta este
/// programa cuando un gesto toca muchos documentos de una vez. Es el hermano de
/// <see cref="LoQueSeLeQuitaria"/> y la pregunta va en la propia pantalla, no en un cuadro
/// que detiene el trabajo: el unico autorizado a detenerlo es borrar de verdad
/// (<c>Fichas.App/Revisar/OperacionDeBorrar.cs</c>).
/// </remarks>
/// <param name="Cuantos">Cuantos documentos trae el grupo que se iba a repartir.</param>
/// <param name="NombreDelCompanero">A quien se le darian, para poder nombrarlo.</param>
/// <param name="DeQueGrupo">De que grupo son, tal como se lee en el panel.</param>
public sealed record LoQueSeVaAAsignar(int Cuantos, string NombreDelCompanero, string DeQueGrupo)
{
    /// <summary>Si hay algo que dar; con cero no se enciende el boton de confirmar.</summary>
    public bool HayAlgoQueAsignar => Cuantos > 0;

    /// <summary>La pregunta con el numero delante, el grupo nombrado y lo que NO pasa dicho.</summary>
    /// <remarks>
    /// <para>Nombra el grupo porque en esta pantalla hay muchos y el gesto es el mismo en
    /// todos: una pregunta que dijera solo «se van a asignar 15» no deja comprobar que son
    /// los 15 del dia que se queria repartir.</para>
    ///
    /// <para>Dice lo que NO pasa por lo mismo que la pregunta de quitar: sin esa frase,
    /// «asignar el grupo entero» se lee como si se cerrara el trabajo de esos documentos, y
    /// asignar no marca nada como verificado (regla permanente 5).</para>
    ///
    /// <para>Las formas del verbo se calculan ANTES y no dentro del texto: un condicional
    /// metido en medio de la frase la parte en trozos que ya no se leen como espanol.</para>
    /// </remarks>
    public string Pregunta
    {
        get
        {
            if (Cuantos == 0) return $"En {DeQueGrupo} no hay ningún documento que asignar.";

            var esUnoSolo = Cuantos == 1;
            var van = esUnoSolo ? "va" : "van";
            // ⚠️ La frase del final concuerda ENTERA, sujeto incluido. Con solo el verbo
            // cambiado se leia «los documentos se quedan como estaba», que se vio pintado en
            // la ventana el 2026-09-09: el sujeto en plural no se puede dejar fijo.
            var seQuedanComoEstaban = esUnoSolo
                ? "el documento se queda como estaba"
                : "los documentos se quedan como estaban";

            return $"Se {van} a asignar a {NombreDelCompanero} "
                 + Plural.Con(Cuantos, "documento", "documentos")
                 + $" de {DeQueGrupo}. "
                 + $"Si alguno ya lo llevaba otra persona, se {van} a quedar con los dos y se avisa. "
                 + $"No se marca nada como revisado: {seQuedanComoEstaban}.";
        }
    }
}

/// <summary>Lo que dejo quitarle a alguien todos sus casos, para decirlo en una linea.</summary>
/// <param name="Retirados">Cuantas asignaciones se desactivaron.</param>
/// <param name="NoSePudieron">Cuantas no; el motivo de cada una ya esta en la franja.</param>
/// <param name="NombreDelCompanero">A quien se le quitaron.</param>
public sealed record ResumenDeRetirada(int Retirados, int NoSePudieron, string NombreDelCompanero)
{
    /// <summary>La linea de una sola frase que se ensena en el acuse del pie.</summary>
    public string Linea
    {
        get
        {
            if (Retirados == 0 && NoSePudieron == 0)
                return $"{NombreDelCompanero} no llevaba ningún caso; no se quitó nada.";

            var vuelven = Plural.Palabra(Retirados, "vuelve", "vuelven");
            var cabeza = $"{Plural.Con(Retirados, "caso", "casos")} "
                + Plural.Palabra(Retirados, "quitado", "quitados")
                + $" a {NombreDelCompanero}; {vuelven} a estar sin asignar. "
                + "Los documentos no se tocaron.";

            return NoSePudieron == 0
                ? cabeza
                : $"{cabeza} {NoSePudieron} no "
                  + Plural.Palabra(NoSePudieron, "se pudo", "se pudieron") + " quitar (mira la franja).";
        }
    }
}

/// <summary>Lo que dejo un lote de asignaciones, para decirlo en una linea en el acuse.</summary>
/// <param name="Asignados">Cuantos entraron.</param>
/// <param name="NoSePudieron">Cuantos no; el motivo de cada uno ya esta en la franja.</param>
/// <param name="NombreDelCompanero">A quien se le dieron, para poder nombrarlo.</param>
public sealed record ResumenDeAsignacion(int Asignados, int NoSePudieron, string NombreDelCompanero)
{
    /// <summary>La linea de una sola frase que se ensena en el acuse del pie.</summary>
    public string Linea
    {
        get
        {
            var cuantos = Plural.Con(Asignados, "caso asignado", "casos asignados");
            return NoSePudieron == 0
                ? $"{cuantos} a {NombreDelCompanero}."
                : $"{cuantos} a {NombreDelCompanero}; {NoSePudieron} no "
                  + Plural.Palabra(NoSePudieron, "se pudo", "se pudieron") + " (mira la franja).";
        }
    }
}
