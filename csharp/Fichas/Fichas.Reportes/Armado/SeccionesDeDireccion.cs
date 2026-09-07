using System.Globalization;
using Fichas.Contratos.Modelos;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Armado;

/// <summary>
/// Las seis secciones del informe a la direccion, y su portada.
/// </summary>
/// <remarks>
/// Son las del informe del proyecto viejo (<c>salida/informe.py</c>), el que el dueno aprobo:
/// quienes viajaron sin la preparacion completa, los viajes, a que van al templo, donde se
/// traban las preparaciones, que unidades acumulan pendientes y como esta repartido el trabajo
/// del equipo. Van DELANTE de las tres secciones de la FASE 8.
///
/// ⚠️ <b>El viejo tenia SIETE.</b> Falta «De qué país viajan», y no es un olvido: el pais no
/// esta impreso en el formulario y sale de un catalogo que espera al ADR-0002. Va dicho en la
/// entrega. (Medido en el proyecto viejo: esa tabla salia en la practica con una sola fila
/// «sin país», porque ningun grupo rellenaba el campo.)
///
/// ⚠️ <b>Estas tablas dicen «Caso» donde las de la FASE 8 dicen «N.º de caso»</b>, y no es un
/// descuido: son las columnas del informe que el dueno aprobo, y se copian tal como estaban.
/// Unificar el rotulo cambiaria el documento que el ya dio por bueno para ganar una coherencia
/// que nadie pidio.
///
/// ⚠️ <b>«Verificada» volvio a estas tablas, y con ella el riesgo que el criterio C8-2 queria
/// evitar.</b> La palabra significa dos cosas en el mismo programa: en la pantalla de
/// correccion es la FIRMA de Miguel sobre un campo leido, y aqui son los seis pasos que
/// contesta un companero. El C8-2 las habia separado llamando a estas «con la preparación
/// completa»; el dueno pidio el informe del viejo el 2026-09-04 y con el volvieron sus
/// rotulos. <b>Lo que se conserva del C8-2 es la aclaracion</b>, dicha dentro del informe en
/// la nota de <see cref="QuienViajoSinVerificar"/>: la palabra es la que el pidio y el lector
/// se encuentra la explicacion antes que ninguna tabla que la use.
///
/// <b>Aqui no se cuenta nada</b>: las cuentas estan en <see cref="Preparacion"/>. Esto las
/// escribe en espanol y las coloca en columnas. Y <b>sin dinero, en ninguna de las seis</b>.
/// </remarks>
public static class SeccionesDeDireccion
{
    private static readonly Columna[] ColumnasDeQuienViajoSinVerificar =
    [
        new("Persona", ClaseDeColumna.Crudo, 30),
        new("Barrio o rama", ClaseDeColumna.Crudo, 24),
        new("Caso", ClaseDeColumna.Texto, 12),
        new("Viajó el", ClaseDeColumna.Temporal, 14),
        new("Qué pasó", ClaseDeColumna.Crudo, 40),
    ];

    private static readonly Columna[] ColumnasDeLosViajes =
    [
        new("Caso", ClaseDeColumna.Texto, 12),
        new("País", ClaseDeColumna.Crudo, 16),
        new("Templo", ClaseDeColumna.Crudo, 20),
        new("Sale", ClaseDeColumna.Temporal, 14),
        new("Asignado a", ClaseDeColumna.Crudo, 24),
        new("Viajan", ClaseDeColumna.Crudo, 9),
        new(Vocabulario.ConLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
        new(Vocabulario.SinLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
    ];

    private static readonly Columna[] ColumnasDeLasOrdenanzas =
    [
        new("A qué va al templo", ClaseDeColumna.Crudo, 34),
        new("Personas", ClaseDeColumna.Crudo, 12),
    ];

    private static readonly Columna[] ColumnasDeLosPasosTrabados =
    [
        new("Paso del sistema del líder", ClaseDeColumna.Crudo, 34),
        new("Veces sin completar", ClaseDeColumna.Crudo, 20),
    ];

    private static readonly Columna[] ColumnasDeLasUnidades =
    [
        new("Barrio o rama", ClaseDeColumna.Crudo, 34),
        new("Personas", ClaseDeColumna.Crudo, 12),
        new(Vocabulario.SinLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
    ];

    private static readonly Columna[] ColumnasDelEquipo =
    [
        new("Agente", ClaseDeColumna.Crudo, 26),
        new("Personas a su cargo", ClaseDeColumna.Crudo, 16),
        new(Vocabulario.ConLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
        new("Sin mirar", ClaseDeColumna.Crudo, 12),
    ];

    /// <summary>El titular y las cuatro cifras grandes. Sin porcentajes y sin dinero.</summary>
    /// <remarks>
    /// Las cuatro son las del proyecto viejo y en su orden: las dos primeras parten a las que ya
    /// viajaron, la tercera dice cuantas quedan por delante —que es sobre las que todavia se
    /// puede hacer algo— y la cuarta da el tamano del periodo.
    /// </remarks>
    public static Portada Portada(Recuento recuento)
        => new(
            Vocabulario.TitularDeLaPortada,
            Preparacion.Titular(recuento),
            [
                new Cifra(
                    recuento.SinCompletar.Count,
                    "viajaron sin verificar",
                    recuento.SinCompletar.Count > 0 ? TonoDeCifra.Malo : TonoDeCifra.Bueno),
                new Cifra(recuento.Completas.Count, "viajaron verificadas", TonoDeCifra.Bueno),
                new Cifra(recuento.PorViajar.Count, "por viajar todavía", TonoDeCifra.Neutro),
                new Cifra(recuento.Casos.Count, "casos en el período", TonoDeCifra.Neutro),
            ]);

    /// <summary>Con nombre y unidad, porque cada renglon es una persona que no recibio nada.</summary>
    /// <remarks>
    /// Es la seccion que ABRE el informe y por eso lleva ella la aclaracion de la palabra: quien
    /// lo lee se la encuentra antes que ninguna tabla que la use.
    /// </remarks>
    public static Seccion QuienViajoSinVerificar(IReadOnlyList<PersonaConSuCaso> sinCompletar)
    {
        var filas = sinCompletar
            .Select(f => (IReadOnlyList<string?>)new string?[]
            {
                Vocabulario.PersonaOSinNombre(f.Persona.Nombre),
                Vocabulario.UnidadConSuNumero(f.Caso.UnidadNombre, f.Caso.UnidadNumero),
                f.Caso.NumeroCaso,
                f.FechaViaje,
                Preparacion.QuePaso(f.Persona),
            })
            .ToList();

        return new Seccion(
            Vocabulario.QuienesViajaronSinVerificar,
            [
                "Con nombre y unidad, porque cada uno de estos renglones es una persona que fue "
                + "al templo y no recibió su ordenanza.",
                // La aclaracion que se conserva del criterio C8-2. Sin ella, «Verificadas» se
                // lee como la firma de Miguel sobre un campo, y son dos hechos distintos que
                // conviven en este mismo documento: las metricas del final SI hablan de la firma.
                "«Verificada» quiere decir aquí los seis pasos del sistema del líder contestados "
                + "que sí, y NO la firma de Miguel sobre un campo del formulario. Las dos cosas "
                + "salen en este informe: las métricas del final son las de la firma.",
                "«Nadie la miró» y «no está completa» no son lo mismo y piden dos remedios "
                + "distintos: la primera es trabajo que no se hizo, la segunda es trabajo que se "
                + "hizo y encontró algo.",
            ],
            ColumnasDeQuienViajoSinVerificar,
            filas,
            Plural.Con(filas.Count, "persona viajó", "personas viajaron") + " sin verificar");
    }

    /// <summary>Un renglon por caso, con quien lo lleva en la misma fila.</summary>
    public static Seccion LosViajes(IReadOnlyList<RenglonDeViaje> renglones)
    {
        var filas = renglones
            .Select(r => (IReadOnlyList<string?>)new string?[]
            {
                r.NumeroCaso,
                Vocabulario.ElPaisNoSeGuarda,
                string.IsNullOrWhiteSpace(r.Templo) ? Vocabulario.SinDato : r.Templo,
                r.FechaViaje,
                r.AsignadoA,
                Numero(r.Viajan),
                Numero(r.Completas),
                r.SinCompletar is null ? "—" : Numero(r.SinCompletar.Value),
            })
            .ToList();

        return new Seccion(
            "Los viajes",
            [
                "«Sin verificar» sale con una raya en los casos que todavía no han "
                + "salido: ahí lo que falta no es un fallo, es trabajo por hacer.",
                // Copiada palabra por palabra del Python (secciones_de_direccion.py). La fecha va
                // dentro a proposito: la nota le dice a quien lee el informe POR QUE un caso
                // viejo sale sin templo, y quitarla convierte una explicacion en una excusa.
                $"«País» sale como «{Vocabulario.ElPaisNoSeGuarda}» en todas las filas porque no "
                + "está impreso en el formulario y su catálogo todavía no se ha decidido. «Templo» "
                + "sí se lee del papel desde el 2026-09-03, y sale igual cuando el caso se importó "
                + "antes de esa fecha o cuando el lector no lo encontró.",
            ],
            ColumnasDeLosViajes,
            filas,
            Plural.Con(filas.Count, "caso en el período", "casos en el período"));
    }

    /// <summary>A que van al templo las personas del periodo, contado por ordenanza.</summary>
    public static Seccion AQueVan(IReadOnlyList<PersonaConSuCaso> personas)
    {
        var suyas = personas.Select(f => f.Persona).ToList();
        var sinMarcar = suyas.Count(Ordenanzas.SinNingunaMarcada);

        var notas = new List<string>
        {
            "Una persona puede ir a varias ordenanzas, así que estos números suman más que el "
            + "total de personas. No es un error de cuenta.",
        };
        if (sinMarcar > 0)
        {
            notas.Add(
                Plural.Con(sinMarcar, "persona no trae", "personas no traen")
                + " ninguna casilla marcada. Esas casillas del formulario son marcas de tilde y "
                + "hoy se ponen a mano.");
        }

        return new Seccion(
            "A qué van al templo",
            notas,
            ColumnasDeLasOrdenanzas,
            Ordenanzas.Contar(suyas)
                .Select(c => (IReadOnlyList<string?>)new string?[] { c.Rotulo, Numero(c.Personas) })
                .ToList(),
            null);
    }

    /// <summary>En que paso del sistema del lider se quedan las preparaciones.</summary>
    /// <remarks>
    /// Es la seccion accionable del informe: dice donde hay que acompanar mas a las unidades.
    /// Sale SOLO si hay algun paso marcado que no; una tabla de ceros no dice nada y ocupa el
    /// sitio de lo que si.
    /// </remarks>
    public static Seccion? DondeSeTraban(IReadOnlyList<PersonaConSuCaso> personas)
    {
        var cuentas = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var fila in personas)
        {
            foreach (var trabado in Pasos.SinCompletar(fila.Persona))
            {
                cuentas[trabado] = cuentas.GetValueOrDefault(trabado) + 1;
            }
        }
        if (cuentas.Count == 0) return null;

        return new Seccion(
            "Dónde se traban las preparaciones",
            [
                "De los seis pasos del sistema del líder, estos son los que quedaron sin "
                + "completar. Es donde hay que acompañar más a las unidades.",
            ],
            ColumnasDeLosPasosTrabados,
            cuentas
                .OrderByDescending(par => par.Value)
                .ThenBy(par => par.Key, StringComparer.Ordinal)
                .Select(par => (IReadOnlyList<string?>)new string?[] { par.Key, Numero(par.Value) })
                .ToList(),
            null);
    }

    /// <summary>Que unidades acumulan preparaciones sin completar.</summary>
    /// <remarks>
    /// Es la misma cuenta que «Dónde se traban» mirada por el otro lado, y por eso valen las
    /// dos: aquella dice QUE paso falla y esta dice EN QUE UNIDAD. Con las dos se sabe a quien
    /// llamar y de que hablarle; con una sola, solo la mitad.
    ///
    /// Ordena por las que mas deben, y a igualdad por nombre: sin el segundo criterio, dos
    /// informes del mismo periodo podrian listar las mismas unidades en distinto orden y leerse
    /// como si algo hubiera cambiado.
    /// </remarks>
    public static Seccion? LasUnidades(IReadOnlyList<PersonaConSuCaso> personas)
    {
        var porUnidad = new Dictionary<string, (int Personas, int SinCompletar)>(StringComparer.Ordinal);
        foreach (var fila in personas)
        {
            var unidad = Vocabulario.UnidadConSuNumero(fila.Caso.UnidadNombre, fila.Caso.UnidadNumero);
            if (string.IsNullOrWhiteSpace(unidad)) unidad = Vocabulario.SinUnidad;

            var cuenta = porUnidad.GetValueOrDefault(unidad);
            porUnidad[unidad] = (
                cuenta.Personas + 1,
                cuenta.SinCompletar + (Preparacion.TieneLaPreparacionCompleta(fila.Persona) ? 0 : 1));
        }

        var conFalta = porUnidad
            .Where(par => par.Value.SinCompletar > 0)
            .OrderByDescending(par => par.Value.SinCompletar)
            .ThenBy(par => par.Key, StringComparer.Ordinal)
            .ToList();
        if (conFalta.Count == 0) return null;

        return new Seccion(
            "Unidades con preparaciones sin completar",
            [
                "Es «Dónde se traban» mirado por el otro lado: aquella tabla dice qué paso falla "
                + "y esta dice en qué unidad. Con las dos se sabe a quién llamar y de qué hablarle.",
                "Solo salen las unidades que tienen a alguien sin verificar. Las "
                + "que están al día no ocupan sitio.",
            ],
            ColumnasDeLasUnidades,
            conFalta
                .Select(par => (IReadOnlyList<string?>)new string?[]
                {
                    par.Key, Numero(par.Value.Personas), Numero(par.Value.SinCompletar),
                })
                .ToList(),
            Plural.Con(conFalta.Count, "unidad con algo pendiente", "unidades con algo pendiente"));
    }

    /// <summary>Cuanto lleva cada agente y cuanto de eso esta mirado.</summary>
    /// <remarks>
    /// Sale SIEMPRE, incluso con una sola fila de «sin asignar», y ahi se separa de «Unidades» y
    /// de «Dónde se traban»: que nadie tenga nada asignado es justamente lo que la direccion
    /// tiene que ver, y una tabla que desaparece cuando el trabajo no esta repartido esconde el
    /// peor de los casos.
    ///
    /// Un caso asignado a varios companeros hace que esa persona cuente para los dos: el informe
    /// contesta «¿a quién le pregunto por esta persona?», y la respuesta son los dos.
    /// </remarks>
    public static Seccion ElEquipo(
        IReadOnlyList<PersonaConSuCaso> personas,
        IReadOnlyDictionary<long, IReadOnlyList<string>> companerosPorCaso)
    {
        var porAgente = new Dictionary<string, (int Personas, int Completas, int SinMirar)>(StringComparer.Ordinal);

        foreach (var fila in personas)
        {
            var asignados = companerosPorCaso.TryGetValue(fila.CasoId, out var nombres) && nombres.Count > 0
                ? nombres
                : [Vocabulario.SinAgente];

            foreach (var agente in asignados)
            {
                var cuenta = porAgente.GetValueOrDefault(agente);
                var estado = Pasos.Estado(fila.Persona);
                porAgente[agente] = (
                    cuenta.Personas + 1,
                    cuenta.Completas + (estado == true ? 1 : 0),
                    // Ni completa ni incompleta: NADIE contesto ni un paso. Es la columna que de
                    // verdad informa a la direccion, porque no habla de las unidades sino del
                    // reparto de trabajo del equipo.
                    cuenta.SinMirar + (estado is null ? 1 : 0));
            }
        }

        var filas = porAgente
            .OrderByDescending(par => par.Value.Personas)
            .ThenBy(par => par.Key, StringComparer.Ordinal)
            .Select(par => (IReadOnlyList<string?>)new string?[]
            {
                par.Key, Numero(par.Value.Personas), Numero(par.Value.Completas), Numero(par.Value.SinMirar),
            })
            .ToList();

        return new Seccion(
            "El equipo",
            [
                "«Sin mirar» no es lo mismo que «sin completar»: son las personas de las que no "
                + "consta NI UN paso contestado, o sea trabajo que no se ha empezado.",
                "Un caso asignado a dos compañeros cuenta para los dos, así que estas cifras "
                + "pueden sumar más que el total de personas. No es un error de cuenta: la "
                + "pregunta que contesta la tabla es a quién preguntarle por cada persona.",
            ],
            ColumnasDelEquipo,
            filas,
            Plural.Con(filas.Count, "agente con trabajo en el período", "agentes con trabajo en el período"));
    }

    /// <summary>Un numero escrito siempre igual, sin depender del idioma del sistema.</summary>
    internal static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);
}
