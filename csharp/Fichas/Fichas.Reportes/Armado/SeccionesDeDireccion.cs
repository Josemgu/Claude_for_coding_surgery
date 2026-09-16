using System.Globalization;
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
/// rotulos. Hasta el 2026-09-16 la aclaracion iba dicha DENTRO del informe, en una nota bajo
/// «Quiénes viajaron sin verificar»; ese dia el dueno pidio los reportes sin parrafos
/// —<i>«se están colocando muchas letras; debe explicarse sin leer una sola palabra»</i>— y la
/// nota se fue con todas las demas. El riesgo sigue siendo real y queda escrito aqui.
///
/// ⚠️ <b>Ninguna seccion lleva notas desde el 2026-09-16.</b> Las dieciseis que habia —2 500
/// caracteres, 26 lineas del PDF sobre la base falsa de 300— eran los parrafos explicativos que
/// el dueno mando quitar: titulos, cifras y tablas. Lo que una nota decia con numeros (quien
/// no trae casilla en «A qué van»; los cubos de la metrica 3) se movio a su tabla; lo que decia
/// con palabras se quito.
///
/// ⚠️ <b>Y tampoco lleva la columna «País»</b>, que salia siempre como «no consta»: el dueno,
/// el 2026-09-16, <i>«"no consta" no es una respuesta; a dónde viajarán es el templo»</i>. Los
/// viajes quedan con siete columnas y «Templo» donde estaba.
///
/// <b>Aqui no se cuenta nada</b>: las cuentas estan en <see cref="Preparacion"/>. Esto las
/// escribe en espanol y las coloca en columnas. Y <b>sin dinero, en ninguna de las seis</b>.
/// </remarks>
public static class SeccionesDeDireccion
{
    /// <summary>Las seis columnas de la sección que abre el informe; «Qué pasó» es la que dice en qué paso se quedó.</summary>
    private static readonly Columna[] ColumnasDeQuienViajoSinVerificar =
    [
        new("Persona", ClaseDeColumna.Crudo, 30),
        // ⚠️ 2026-09-08: la unidad en DOS columnas, el numero delante y como Texto. Ver el
        // motivo entero en Vocabulario, donde vivia la funcion que las pegaba.
        new(Vocabulario.RotuloDelNumeroDeUnidad, ClaseDeColumna.Texto, 16),
        new("Barrio o rama", ClaseDeColumna.Crudo, 24),
        new("Caso", ClaseDeColumna.Texto, 12),
        new("Viajó el", ClaseDeColumna.Temporal, 14),
        new("Qué pasó", ClaseDeColumna.Crudo, 40),
    ];

    /// <summary>Las siete columnas de «Los viajes»: las del informe viejo menos «País» (dueno, 2026-09-16).</summary>
    private static readonly Columna[] ColumnasDeLosViajes =
    [
        new("Caso", ClaseDeColumna.Texto, 12),
        new("Templo", ClaseDeColumna.Crudo, 20),
        new("Sale", ClaseDeColumna.Temporal, 14),
        new("Asignado a", ClaseDeColumna.Crudo, 24),
        new("Viajan", ClaseDeColumna.Crudo, 9),
        new(Vocabulario.ConLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
        new(Vocabulario.SinLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
    ];

    /// <summary>Las dos columnas de «A qué van al templo».</summary>
    private static readonly Columna[] ColumnasDeLasOrdenanzas =
    [
        new("A qué va al templo", ClaseDeColumna.Crudo, 34),
        new("Personas", ClaseDeColumna.Crudo, 12),
    ];

    /// <summary>Las dos columnas de «Dónde se traban las preparaciones».</summary>
    private static readonly Columna[] ColumnasDeLosPasosTrabados =
    [
        new("Paso del sistema del líder", ClaseDeColumna.Crudo, 34),
        new("Veces sin completar", ClaseDeColumna.Crudo, 20),
    ];

    /// <summary>Las cuatro columnas de «Unidades con preparaciones sin completar»; el número de unidad va delante y como texto.</summary>
    private static readonly Columna[] ColumnasDeLasUnidades =
    [
        // ⚠️ 2026-09-08: la unidad en DOS columnas, el numero delante y como Texto. Aqui ademas
        // cambio LA CLAVE del agrupado: ver <see cref="LasUnidades"/>.
        new(Vocabulario.RotuloDelNumeroDeUnidad, ClaseDeColumna.Texto, 16),
        new("Barrio o rama", ClaseDeColumna.Crudo, 34),
        new("Personas", ClaseDeColumna.Crudo, 12),
        new(Vocabulario.SinLaPreparacionCompleta, ClaseDeColumna.Crudo, 18),
    ];

    /// <summary>Las cuatro columnas de «El equipo»; «Sin mirar» es la que habla del reparto de trabajo.</summary>
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
    /// <param name="recuento">Las cuatro cifras ya contadas por <see cref="Preparacion.Recontar"/>.</param>
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
    /// <remarks>Es la seccion que ABRE el informe. Sin notas desde el 2026-09-16: ver la cabecera de la clase.</remarks>
    /// <param name="sinCompletar">Las personas que ya viajaron sin los seis pasos en sí; una por fila.</param>
    /// <returns>La sección, con tabla vacía y resumen «0 personas viajaron sin verificar» si no hay ninguna.</returns>
    public static Seccion QuienViajoSinVerificar(IReadOnlyList<PersonaConSuCaso> sinCompletar)
    {
        var filas = sinCompletar
            .Select(f => (IReadOnlyList<string?>)new string?[]
            {
                Vocabulario.PersonaOSinNombre(f.Persona.Nombre),
                Vocabulario.NumeroDeUnidad(f.Caso.UnidadNumero),
                Vocabulario.NombreDeUnidad(f.Caso.UnidadNombre),
                f.Caso.NumeroCaso,
                f.FechaViaje,
                Preparacion.QuePaso(f.Persona),
            })
            .ToList();

        return new Seccion(
            Vocabulario.QuienesViajaronSinVerificar,
            [],
            ColumnasDeQuienViajoSinVerificar,
            filas,
            Plural.Con(filas.Count, "persona viajó", "personas viajaron") + " sin verificar");
    }

    /// <summary>Un renglon por caso, con quien lo lleva en la misma fila.</summary>
    /// <param name="renglones">Los viajes ya contados por <see cref="Preparacion.ResumenPorCaso"/>, en su orden.</param>
    /// <returns>La sección; un caso que aún no salió lleva una raya en «Sin verificar».</returns>
    public static Seccion LosViajes(IReadOnlyList<RenglonDeViaje> renglones)
    {
        var filas = renglones
            .Select(r => (IReadOnlyList<string?>)new string?[]
            {
                r.NumeroCaso,
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
            [],
            ColumnasDeLosViajes,
            filas,
            Plural.Con(filas.Count, "caso en el período", "casos en el período"));
    }

    /// <summary>A que van al templo las personas del periodo, contado por ordenanza.</summary>
    /// <param name="personas">Todas las personas del periodo; se cuentan sus casillas <c>Ord*</c>.</param>
    /// <returns>La sección, sin resumen; con una fila más, <see cref="SinNingunaCasillaMarcada"/>, si alguien no trae ninguna.</returns>
    public static Seccion AQueVan(IReadOnlyList<PersonaConSuCaso> personas)
    {
        var suyas = personas.Select(f => f.Persona).ToList();
        var sinMarcar = suyas.Count(Ordenanzas.SinNingunaMarcada);

        var filas = Ordenanzas.Contar(suyas)
            .Select(c => (IReadOnlyList<string?>)new string?[] { c.Rotulo, Numero(c.Personas) })
            .ToList();
        // Hasta el 2026-09-16 esto era una nota; la cifra sigue, ahora como fila de la tabla.
        if (sinMarcar > 0) filas.Add([SinNingunaCasillaMarcada, Numero(sinMarcar)]);

        return new Seccion("A qué van al templo", [], ColumnasDeLasOrdenanzas, filas, null);
    }

    /// <summary>La fila de «A qué van» que cuenta a quien no trae ninguna casilla marcada en el papel.</summary>
    public const string SinNingunaCasillaMarcada = "Sin ninguna casilla marcada";

    /// <summary>En que paso del sistema del lider se quedan las preparaciones.</summary>
    /// <remarks>
    /// Es la seccion accionable del informe: dice donde hay que acompanar mas a las unidades.
    /// Sale SOLO si hay algun paso marcado que no; una tabla de ceros no dice nada y ocupa el
    /// sitio de lo que si.
    /// </remarks>
    /// <param name="personas">Todas las personas del periodo; se cuentan sus pasos marcados que no.</param>
    /// <returns>La sección ordenada de más a menos veces, o nulo si ningún paso está marcado que no.</returns>
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
            [],
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
    /// <param name="personas">Todas las personas del periodo; se agrupan por el par (número, nombre) de su unidad.</param>
    /// <returns>Solo las unidades con alguien sin la preparación completa, o nulo si no hay ninguna.</returns>
    public static Seccion? LasUnidades(IReadOnlyList<PersonaConSuCaso> personas)
    {
        // ⚠️ 2026-09-08: la clave es el PAR (número, nombre) y ya no la cadena pegada.
        // Agrupar por la cadena metia el numero dentro de la clave sin querer; ahora entra
        // declarado, cada dato sale en su columna y el orden sigue siendo el mismo porque se
        // desempata por los dos.
        var porUnidad = new Dictionary<(string Numero, string Nombre), (int Personas, int SinCompletar)>();
        foreach (var fila in personas)
        {
            var unidad = (
                Vocabulario.NumeroDeUnidad(fila.Caso.UnidadNumero),
                Vocabulario.NombreDeUnidad(fila.Caso.UnidadNombre));

            var cuenta = porUnidad.GetValueOrDefault(unidad);
            porUnidad[unidad] = (
                cuenta.Personas + 1,
                cuenta.SinCompletar + (Preparacion.TieneLaPreparacionCompleta(fila.Persona) ? 0 : 1));
        }

        var conFalta = porUnidad
            .Where(par => par.Value.SinCompletar > 0)
            .OrderByDescending(par => par.Value.SinCompletar)
            .ThenBy(par => par.Key.Nombre, StringComparer.Ordinal)
            .ThenBy(par => par.Key.Numero, StringComparer.Ordinal)
            .ToList();
        if (conFalta.Count == 0) return null;

        return new Seccion(
            "Unidades con preparaciones sin completar",
            [],
            ColumnasDeLasUnidades,
            conFalta
                .Select(par => (IReadOnlyList<string?>)new string?[]
                {
                    par.Key.Numero, par.Key.Nombre,
                    Numero(par.Value.Personas), Numero(par.Value.SinCompletar),
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
    /// <param name="personas">Todas las personas del periodo.</param>
    /// <param name="companerosPorCaso">Los nombres de quien lleva cada caso, por id; un caso ausente cuenta como <see cref="Vocabulario.SinAgente"/>.</param>
    /// <returns>Una fila por agente, de más a menos personas; nunca nula.</returns>
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
            [],
            ColumnasDelEquipo,
            filas,
            Plural.Con(filas.Count, "agente con trabajo en el período", "agentes con trabajo en el período"));
    }

    /// <summary>Un numero escrito siempre igual, sin depender del idioma del sistema.</summary>
    /// <param name="valor">La cifra.</param>
    internal static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);
}
