using Fichas.Contratos.Modelos;
using Fichas.Reportes.Consultas;

namespace Fichas.Reportes.Reglas;

/// <summary>Las cuatro cifras del encabezado, contadas una sola vez.</summary>
/// <remarks>
/// Los tres primeros grupos son excluyentes y <see cref="Viajaron"/> es la suma de los dos
/// primeros: eso permite comprobar el informe SUMANDO, que es lo que hace que un numero mal
/// contado se vea en vez de creerse.
/// </remarks>
/// <param name="Viajaron">Las personas cuyo caso ya viajo.</param>
/// <param name="SinCompletar">De esas, las que salieron sin la preparacion completa.</param>
/// <param name="Completas">De esas, las que salieron con la preparacion completa.</param>
/// <param name="PorViajar">Las que todavia no han salido.</param>
/// <param name="Casos">Los ids de los casos del periodo. Ids, no numeros de caso.</param>
public sealed record Recuento(
    IReadOnlyList<PersonaConSuCaso> Viajaron,
    IReadOnlyList<PersonaConSuCaso> SinCompletar,
    IReadOnlyList<PersonaConSuCaso> Completas,
    IReadOnlyList<PersonaConSuCaso> PorViajar,
    IReadOnlyList<long> Casos);

/// <summary>Un renglon de la tabla «Los viajes»: un viaje, con quien lo lleva.</summary>
/// <param name="NumeroCaso">El numero del caso, o nulo si no se pudo leer.</param>
/// <param name="FechaViaje">Cuando sale.</param>
/// <param name="Templo">A que templo, o nulo si no consta.</param>
/// <param name="AsignadoA">Quien lo lleva, ya escrito; nunca vacio.</param>
/// <param name="Viajan">Cuantas personas van en ese caso.</param>
/// <param name="Completas">Cuantas de ellas con la preparacion completa.</param>
/// <param name="SinCompletar">Cuantas sin ella; NULO si el caso todavia no ha salido.</param>
public sealed record RenglonDeViaje(
    string? NumeroCaso,
    string? FechaViaje,
    string? Templo,
    string AsignadoA,
    int Viajan,
    int Completas,
    int? SinCompletar);

/// <summary>
/// El numero por el que existe este programa: cuantos viajaron sin estar listos.
/// </summary>
/// <remarks>
/// Portado de <c>reportes/preparacion.py</c>, que a su vez viene del informe del proyecto
/// viejo (<c>salida/informe.py</c>), el que el dueno aprobo:
/// «N de las M personas que ya viajaron lo hicieron SIN la preparación completa».
///
/// Una persona que viaja sin la preparacion completa no hace la ordenanza: hizo el viaje y
/// volvio como se fue. Todo lo demas del informe esta para explicar ese numero.
///
/// <b>Aqui solo hay aritmetica: ni una consulta.</b> Lo que llega es la lista de personas del
/// periodo, y de ella salen todas las cifras, para que dos numeros del mismo documento no
/// puedan contradecirse por haberse calculado de dos maneras.
///
/// ⚠️ <b>«Con la preparación completa» quiere decir los seis pasos en «Sí», y nada mas.</b> No
/// se mira <c>estado_recomendacion</c>: ese campo es del caso entero y la preparacion es de
/// cada persona. Una persona sin ningun paso contestado cuenta como SIN la preparacion
/// completa, no como desconocida, y es el lado seguro del error (criterio C8-2).
///
/// <b>Sin dinero, a proposito.</b> Citado del viejo: el presupuesto lo lleva otro
/// departamento. Ninguna funcion de aqui cuenta un importe.
/// </remarks>
public static class Preparacion
{
    /// <summary>Lo que se escribe cuando nadie contesto ni un paso de esa persona.</summary>
    public const string NadieLaMiro = "nadie la miró";

    /// <summary>Lo que se escribe cuando alguien la miro y encontro algo.</summary>
    public const string NoEstaCompleta = "no está completa";

    /// <summary>Si esa persona ya hizo el viaje. Sin fecha, todavia no viajo.</summary>
    /// <remarks>
    /// El dia del viaje NO cuenta como viajado: mientras el dia no termina, la preparacion
    /// todavia se puede arreglar, y contar a esa persona entre las perdidas seria darla por
    /// perdida antes de tiempo.
    /// </remarks>
    /// <param name="fechaViaje">La fecha «AAAA-MM-DD» del caso, o nula si no consta.</param>
    /// <param name="diaDeHoy">El día de hoy en «AAAA-MM-DD»; entra desde fuera, aquí no se mira el reloj.</param>
    public static bool YaViajo(string? fechaViaje, string diaDeHoy)
        => fechaViaje is not null && string.CompareOrdinal(fechaViaje, diaDeHoy) < 0;

    /// <summary>Si los seis pasos de esa persona constan en «Sí». Ni uno menos.</summary>
    /// <param name="persona">La persona con sus seis casillas <c>Paso*</c>; una en blanco ya cuenta como no completa.</param>
    public static bool TieneLaPreparacionCompleta(Persona persona) => Pasos.Estado(persona) == true;

    /// <summary>Por que esa persona viajo sin la preparacion completa, y en que se quedo.</summary>
    /// <remarks>
    /// Distingue las dos formas de llegar al mismo sitio, que piden dos remedios distintos:
    /// «no está completa» es un trabajo que alguien hizo y salio mal, y «nadie la miró» es un
    /// trabajo que no se hizo. Cuando se sabe en que paso se quedo, se dice: es lo que hay que
    /// hablar con el lider.
    /// </remarks>
    /// <param name="persona">La persona que viajó sin la preparación completa.</param>
    /// <returns><see cref="NadieLaMiro"/>, <see cref="NoEstaCompleta"/>, o esta última con los pasos que faltan.</returns>
    public static string QuePaso(Persona persona)
    {
        if (Pasos.Estado(persona) != false) return NadieLaMiro;

        var trabados = Pasos.SinCompletar(persona);
        return trabados.Count == 0
            ? NoEstaCompleta
            : $"{NoEstaCompleta}: falta {string.Join(" y ", trabados)}";
    }

    /// <summary>Las cuatro cifras del encabezado, contadas una sola vez.</summary>
    /// <param name="personas">Todas las personas cuyo caso cae en el periodo.</param>
    /// <param name="diaDeHoy">El día de hoy en «AAAA-MM-DD», para partir entre viajaron y por viajar.</param>
    public static Recuento Recontar(IReadOnlyList<PersonaConSuCaso> personas, string diaDeHoy)
    {
        var viajaron = personas.Where(f => YaViajo(f.FechaViaje, diaDeHoy)).ToList();

        return new Recuento(
            Viajaron: viajaron,
            SinCompletar: viajaron.Where(f => !TieneLaPreparacionCompleta(f.Persona)).ToList(),
            Completas: viajaron.Where(f => TieneLaPreparacionCompleta(f.Persona)).ToList(),
            PorViajar: personas.Where(f => !YaViajo(f.FechaViaje, diaDeHoy)).ToList(),
            // ⚠️ Se cuentan CASOS y no numeros de caso: desde la migracion 12 dos casos pueden
            // llevar el mismo numero, y contar numeros distintos diria «1 caso en el período»
            // donde hay dos. Ademas, un caso sin numero no puede colarse en una ordenacion
            // mezclada con textos, que era un TypeError esperando a un caso sin numero.
            Casos: personas.Select(f => f.CasoId).Distinct().Order().ToList());
    }

    /// <summary>La frase con la que abre el informe. Nunca lleva porcentajes.</summary>
    /// <remarks>
    /// Sobre ocho personas un porcentaje engana mas de lo que informa, y es la razon que da el
    /// proyecto viejo para no ponerlos en ningun sitio de este documento.
    /// </remarks>
    /// <param name="recuento">Las cuatro cifras ya contadas por <see cref="Recontar"/>.</param>
    /// <returns>Una de tres frases: nadie viajó, todos salieron verificados, o N de M salieron sin verificar.</returns>
    public static string Titular(Recuento recuento)
    {
        if (recuento.Viajaron.Count == 0)
        {
            return "Todavía no ha viajado nadie de los que hay cargados en el período.";
        }
        if (recuento.SinCompletar.Count == 0)
        {
            return recuento.Viajaron.Count == 1
                ? "La única persona que viajó en este período salió verificada. "
                  + "No se perdió ninguna ordenanza."
                : $"Las {recuento.Viajaron.Count} personas que viajaron en este período salieron "
                  + "verificadas. No se perdió ninguna ordenanza.";
        }
        return $"{recuento.SinCompletar.Count} de las {recuento.Viajaron.Count} personas que ya "
               + "viajaron lo hicieron SIN verificar. Esas ordenanzas no se hicieron: "
               + "el viaje se hizo igual y la persona volvió como se fue.";
    }

    /// <summary>Un renglon por viaje: quien lo lleva, cuantos van y cuantos sin completar.</summary>
    /// <remarks>
    /// ⚠️ <b>Quien lo tiene va en la MISMA fila que el viaje.</b> Un informe que dice que un
    /// caso salio con gente sin la preparacion completa y no dice de quien era obliga a ir a
    /// buscarlo a otra parte para poder preguntar, y entonces no se pregunta.
    ///
    /// «Sin la preparación completa» solo se cuenta en los casos que YA viajaron. En uno que
    /// todavia no ha salido, lo que falta no es un fallo: es trabajo por hacer, y sale con una
    /// raya.
    ///
    /// El orden es por fecha y luego por NUMERO —no por el id— porque es lo que Miguel lee. El
    /// id entra solo al final para desempatar dos casos que comparten numero: sin el, el orden
    /// entre esos dos dependeria del azar y el informe saldria distinto cada vez.
    /// </remarks>
    /// <param name="personas">Todas las personas del periodo; se agrupan por caso.</param>
    /// <param name="diaDeHoy">El día de hoy en «AAAA-MM-DD».</param>
    /// <param name="companerosPorCaso">Los nombres de quien lleva cada caso, por id de caso; un caso ausente sale como <see cref="Vocabulario.SinAgente"/>.</param>
    /// <returns>Un renglón por caso, ordenados por fecha, número e id.</returns>
    public static IReadOnlyList<RenglonDeViaje> ResumenPorCaso(
        IReadOnlyList<PersonaConSuCaso> personas,
        string diaDeHoy,
        IReadOnlyDictionary<long, IReadOnlyList<string>> companerosPorCaso)
    {
        return personas
            .GroupBy(f => f.CasoId)
            .OrderBy(g => g.First().FechaViaje ?? "9999-99-99", StringComparer.Ordinal)
            .ThenBy(g => g.First().Caso.NumeroCaso ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(g => g.Key)
            .Select(grupo =>
            {
                var suyas = grupo.ToList();
                var una = suyas[0];
                var viajado = YaViajo(una.FechaViaje, diaDeHoy);
                var asignados = companerosPorCaso.TryGetValue(grupo.Key, out var nombres) ? nombres : [];
                var completas = suyas.Count(f => TieneLaPreparacionCompleta(f.Persona));

                return new RenglonDeViaje(
                    NumeroCaso: una.Caso.NumeroCaso,
                    FechaViaje: una.FechaViaje,
                    // El templo es un dato DEL caso —lo trae `casos.templo_nombre` desde la
                    // version 10 del esquema— y las personas de un caso lo llevan todas igual:
                    // viajan juntas al mismo sitio.
                    Templo: una.Caso.TemploNombre,
                    AsignadoA: asignados.Count > 0 ? string.Join(", ", asignados) : Vocabulario.SinAgente,
                    Viajan: suyas.Count,
                    Completas: completas,
                    SinCompletar: viajado ? suyas.Count - completas : null);
            })
            .ToList();
    }
}
