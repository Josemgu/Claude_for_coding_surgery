using Fichas.Reportes.Reglas;

namespace Fichas.App.Inicio;

/// <summary>
/// El grupo que viene: el proximo dia con fecha en el que viaja alguien, contado en PERSONAS.
/// </summary>
/// <remarks>
/// ⛔ <b>Es la peticion 7 del dueno, con sus palabras</b> (2026-09-05): <i>«En el Home debe
/// decirme: las personas del grupo del 17 de septiembre, faltan 3, 4 o 5 personas que la
/// recomendación para el templo no está confirmada. Ahí yo puedo verificarlos y ver en el
/// sistema de la Iglesia»</i>.
/// <para>
/// Cuenta personas y no documentos porque esa es su unidad de trabajo: <i>«Es por persona
/// que se revisa la información»</i>. Un documento con diez personas son diez tickets.
/// </para>
/// </remarks>
/// <param name="Fecha">El dia en que viaja ese grupo.</param>
/// <param name="CuantasPersonas">Cuantas personas viajan ese dia; es el denominador.</param>
/// <param name="Confirmadas">Cuantas tienen sus seis preguntas en si.</param>
/// <param name="NoListas">Cuantas tienen alguna pregunta en no.</param>
/// <param name="SinMirar">Cuantas tienen alguna en blanco y ninguna en no.</param>
public sealed record GrupoQueViene(
    DateOnly Fecha,
    int CuantasPersonas,
    int Confirmadas,
    int NoListas,
    int SinMirar)
{
    /// <summary>Las que le quedan por verificar en el sistema del obispo.</summary>
    public int SinConfirmar => NoListas + SinMirar;

    /// <summary>«17 de septiembre», como el lo nombra.</summary>
    public string CuandoViaja => FechasEnEspanol.DecirElDiaYElMes(Fecha);

    /// <summary>La fecha en ISO-8601, que es lo que se le pasa a la pantalla del grupo.</summary>
    public string FechaIso => FechasEnEspanol.Escribir(Fecha);

    /// <summary>
    /// La frase entera, con su denominador siempre a la vista (C20-1 y C20-4).
    /// </summary>
    /// <remarks>
    /// Un dia sin ninguna persona sin confirmar lo dice con palabras y no deja un hueco ni
    /// un cero mudo (C20-6): un cero que no se explica se lee como «no hay datos».
    /// </remarks>
    public string Frase
    {
        get
        {
            var cuando = $"Del grupo del {CuandoViaja}";

            if (CuantasPersonas == 0) return $"{cuando} todavía no se ha leído ninguna persona.";

            if (SinConfirmar == 0)
            {
                var todas = CuantasPersonas == 1
                    ? "la única persona (1) está confirmada"
                    : $"las {CuantasPersonas} están confirmadas";
                return $"{cuando} no falta ninguna persona por confirmar: {todas}.";
            }

            return $"{cuando} "
                   + Plural.Palabra(SinConfirmar, "falta", "faltan") + " "
                   + Plural.Con(SinConfirmar, "persona", "personas")
                   + $" por confirmar, de {CuantasPersonas}.";
        }
    }
}

/// <summary>
/// Una persona con un ticket abierto: su recomendacion no esta confirmada y su viaje ya paso
/// o esta encima.
/// </summary>
/// <remarks>
/// ⛔ <b>El ticket es la persona</b> (ADR-0006): <i>«Es por persona que se revisa la
/// información»</i>. Una familia de cinco donde fallo uno es UNA persona, no un documento.
/// </remarks>
/// <param name="CasoId">El documento del que sale; es lo que se abre al pulsar.</param>
/// <param name="Nombre">Como se llama, o lo que se sepa decir de ella.</param>
/// <param name="NumeroCaso">El numero del documento.</param>
/// <param name="Unidad">Su unidad, que es con quien hay que hablar.</param>
/// <param name="FechaViaje">Su fecha de viaje en ISO-8601.</param>
/// <param name="DiasHastaElViaje">Cuantos dias faltan; negativo si ya paso.</param>
/// <param name="Recomendacion">Lo que dicen sus seis preguntas; nunca es «si» aqui.</param>
/// <param name="SeQuedoEn">Los pasos marcados que NO; vacio si nadie la miro.</param>
/// <remarks>
/// ⛔ <b>Un documento archivado no llega a esta lista</b> (2026-09-06). Hasta ese dia llegaba
/// con la palabra ARCHIVADO al lado, por el criterio C21-2 —«archivar no es resolver»—, que
/// el dueno deshizo: <i>«si ya resolví un archivo y lo archivo, no debe aparecer en
/// notificaciones»</i>. Por eso este registro ya no tiene ni <c>Archivado</c> ni su marca:
/// aqui todo lo que hay esta abierto.
/// </remarks>
public sealed record PersonaConTicket(
    long CasoId,
    string Nombre,
    string NumeroCaso,
    string Unidad,
    string FechaViaje,
    int DiasHastaElViaje,
    bool? Recomendacion,
    IReadOnlyList<string> SeQuedoEn)
{
    /// <summary>«hace 15 días», «hoy», «en 3 días».</summary>
    public string CuandoViaja => FechasEnEspanol.DecirLosDias(DiasHastaElViaje);

    /// <summary>La linea de debajo del nombre: su documento, su unidad, cuando y en que paso se quedo.</summary>
    public string Detalle
        => $"{NumeroCaso} · {Unidad} · {CuandoViaja} · "
           + Fichas.App.Grupo.LasDosPreguntas.FraseDeUnaPersona(Recomendacion, SeQuedoEn);

    /// <summary>Lo que lee en voz alta un lector de pantalla.</summary>
    public string ParaElLector => $"{Nombre}. {Detalle}. Pulse para abrir este documento.";
}

/// <summary>
/// Lo que hay que verificar en el sistema del obispo: el grupo que viene, lo que ya viajo sin
/// confirmar y lo que viaja dentro de la ventana.
/// </summary>
/// <remarks>
/// <para>Es la tercera cosa de Inicio, y <b>no toca las dos que el dueno pidio el
/// 2026-09-05</b> —lo listo para asignar y lo asignado— (C20-2). Aquellas hablan de nuestros
/// campos; esta habla de otro sistema, el de la Iglesia, que el programa no ve y que solo
/// una persona puede mirar.</para>
///
/// <para>⛔ <b>No hay ningun aviso que salga solo</b> (C21-5): ni correo, ni sonido, ni
/// servicio. Regla permanente 2 y nadie lo ha pedido. El aviso es esta lista, que se ve al
/// abrir.</para>
/// </remarks>
/// <param name="ProximoGrupo">El proximo dia con fecha en el que viaja alguien, o nulo si no hay.</param>
/// <param name="Vencidas">Las personas que ya viajaron sin la recomendacion confirmada.</param>
/// <param name="QueApremian">Las que viajan dentro de la ventana y siguen sin confirmar.</param>
/// <param name="DiasDeLaVentana">Los dias de «pronto»; son los MISMOS que sombrea el calendario.</param>
public sealed record LoDelSistemaDelObispo(
    GrupoQueViene? ProximoGrupo,
    IReadOnlyList<PersonaConTicket> Vencidas,
    IReadOnlyList<PersonaConTicket> QueApremian,
    int DiasDeLaVentana)
{
    /// <summary>La frase del grupo que viene, o la que dice que no hay ninguno.</summary>
    public string FraseDelProximoGrupo
        => ProximoGrupo?.Frase ?? "No hay ningún grupo con fecha de viaje por delante.";

    /// <summary>
    /// «3 personas viajaron sin la recomendación confirmada», o que no hay ninguna.
    /// </summary>
    /// <remarks>
    /// Se cuentan PERSONAS y en ningun caso documentos (C21-3): una familia de cinco donde
    /// fallo uno es una persona, y decir «1 documento» le escondería que las otras cuatro
    /// estaban bien.
    /// </remarks>
    public string FraseDeLoVencido
        => Vencidas.Count == 0
            ? "Ninguna persona viajó sin la recomendación confirmada."
            : Plural.Con(Vencidas.Count, "persona viajó", "personas viajaron")
              + " sin la recomendación confirmada.";

    /// <summary>«5 personas viajan en los próximos 7 días con la recomendación sin confirmar».</summary>
    /// <remarks>
    /// ⛔ La ventana es la MISMA que sombrea el calendario (C21-4). Dos numeros distintos
    /// para «pronto» en la misma pantalla es otra vez el programa diciendo dos cosas.
    /// </remarks>
    public string FraseDeLoQueApremia
        => QueApremian.Count == 0
            ? $"En los próximos {DiasDeLaVentana} días no viaja nadie con la recomendación sin confirmar."
            : Plural.Con(QueApremian.Count, "persona viaja", "personas viajan")
              + $" en los próximos {DiasDeLaVentana} días con la recomendación sin confirmar.";

    /// <summary>Si hay algo que mirar; lo lee la pantalla para no ensenar una lista vacia.</summary>
    public bool HayTicketsAbiertos => Vencidas.Count > 0 || QueApremian.Count > 0;
}

