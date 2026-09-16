using System.Globalization;
using Fichas.Reportes.Consultas;
using Fichas.Reportes.Modelo;
using Fichas.Reportes.Reglas;

namespace Fichas.Reportes.Armado;

/// <summary>
/// El resumen del periodo que va a la hoja unica del Excel, contado una sola vez.
/// </summary>
/// <remarks>
/// <para>Del dueno, 2026-09-16 (<c>DECISIONES.md</c>, «EL REPORTE EN EXCEL ES UNA SOLA HOJA»):
/// <i>«Lo importante son las unidades: quiénes viajaron de esa unidad con todo completo y
/// quiénes no; a qué agente se le asignó y si lo completó; gráfico de la cantidad de personas
/// que viajaron en unos meses sin problemas o dificultades; números fríos.»</i></para>
///
/// <para>⚠️ <b>No cuenta por su cuenta.</b> Parte de <see cref="Preparacion.Recontar"/>, que es
/// de donde salen las cifras de la portada del PDF, y todo lo demas es repartir ESAS personas
/// por unidad, por agente y por mes. Asi el Excel y el PDF del mismo periodo no pueden decir
/// cifras distintas: la suma de cualquier tabla de aqui es la cifra de arriba.</para>
///
/// <para><b>«Devolvió» no existe como dato</b> y el dueno lo dijo el mismo dia: se deduce de si
/// el agente contesto, o sea de si el caso lleva escrito <c>EstadoDelCompanero</c>, que es lo
/// que escribe la vuelta de su hoja. Por unidad es sí cuando TODOS sus casos con agente
/// volvieron; por agente se cuentan casos devueltos de casos a su cargo.</para>
///
/// <para><b>El templo va en la cabecera</b>, y con varios en el mismo periodo se nombran todos:
/// medido en la base inventada, un periodo trae tres templos distintos, y un reporte por templo
/// tocaria la pantalla y el contrato congelado. La lista de pendientes lleva el suyo en cada
/// fila.</para>
/// </remarks>
public static class ArmadoDelResumen
{
    /// <summary>Los meses en tres letras, en espanol, sin depender del idioma del sistema.</summary>
    private static readonly string[] Meses =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    /// <summary>La raya que se escribe donde no hay templo: «no consta» no es una respuesta (dueno, 16).</summary>
    public const string SinTemplo = "—";

    /// <summary>Arma el resumen de ese periodo. Devuelve el modelo, sin escribirlo.</summary>
    /// <param name="lectura">Todo lo leído de los puertos, sin filtrar; aquí se recorta al periodo.</param>
    /// <param name="periodo">El periodo que se reporta, ya validado.</param>
    /// <param name="generadoEn">La marca «AAAA-MM-DD HH:mm:ss» con la que se genera; de ella sale el «hoy».</param>
    public static ResumenDelPeriodo DelPeriodo(LecturaParaReportes lectura, Periodo periodo, string generadoEn)
    {
        ArgumentNullException.ThrowIfNull(lectura);
        ArgumentNullException.ThrowIfNull(periodo);

        var personas = lectura.PersonasDelPeriodo(periodo);
        var recuento = Preparacion.Recontar(personas, DiaDe(generadoEn));
        var agentesDe = lectura.CompanerosPorCaso;

        var porUnidad = PorUnidad(recuento.Viajaron, agentesDe);
        var porAgente = PorAgente(recuento.Viajaron, agentesDe);
        var casosConAgente = recuento.Viajaron.Select(f => f.Caso).DistinctBy(c => c.Id).Where(c => TieneAgente(c.Id, agentesDe)).ToList();
        var agentes = porAgente.Where(a => a.Nombre != Vocabulario.SinAgente).ToList();

        return new ResumenDelPeriodo(
            Titulo: Vocabulario.Titulo,
            PeriodoEnTexto: periodo.EnTexto(),
            Templo: TemploDe(personas),
            GeneradoEn: generadoEn,
            Viajaron: recuento.Viajaron.Count,
            Completos: recuento.Completas.Count,
            SinCompletar: recuento.SinCompletar.Count,
            Meses: MesesDe(periodo).Count,
            Unidades: porUnidad.Count,
            UnidadesConPendientes: porUnidad.Count(u => u.SinCompletar > 0),
            UnidadesSinAgente: porUnidad.Count(u => u.SinCompletar > 0 && u.Agente == Vocabulario.SinAgente),
            CasosDevueltos: casosConAgente.Count(Contesto),
            CasosConAgente: casosConAgente.Count,
            Agentes: agentes.Count,
            AgentesSinPendientes: agentes.Count(a => a.SinCompletar == 0),
            PorUnidad: porUnidad,
            PorAgente: porAgente,
            PorMes: PorMes(recuento, periodo),
            Pendientes: Pendientes(recuento.SinCompletar, agentesDe));
    }

    // ---- por unidad ---------------------------------------------------------

    /// <summary>Una fila por unidad de las que ya viajaron, con las que mas deben primero.</summary>
    /// <remarks>El mismo orden que «Unidades con preparaciones sin completar» del PDF: sin completar, nombre, numero.</remarks>
    /// <param name="viajaron">Las personas que ya viajaron.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static List<UnidadDelResumen> PorUnidad(
        IReadOnlyList<PersonaConSuCaso> viajaron,
        IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
        => viajaron
            .GroupBy(f => (Numero: Vocabulario.NumeroDeUnidad(f.Caso.UnidadNumero), Nombre: Vocabulario.NombreDeUnidad(f.Caso.UnidadNombre)))
            .Select(grupo => UnaUnidad(grupo.Key.Numero, grupo.Key.Nombre, grupo.ToList(), agentesDe))
            .OrderByDescending(u => u.SinCompletar)
            .ThenBy(u => u.Nombre, StringComparer.Ordinal)
            .ThenBy(u => u.Numero, StringComparer.Ordinal)
            .ToList();

    /// <summary>La fila de una unidad: cuenta a sus personas y deduce «devolvió» de sus casos.</summary>
    /// <param name="numero">El numero de la unidad, ya escrito.</param>
    /// <param name="nombre">El nombre de la unidad, ya escrito.</param>
    /// <param name="suyas">Las personas de esa unidad que ya viajaron.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static UnidadDelResumen UnaUnidad(
        string numero, string nombre, List<PersonaConSuCaso> suyas,
        IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
    {
        var casos = suyas.Select(f => f.Caso).DistinctBy(c => c.Id).ToList();
        var conAgente = casos.Where(c => TieneAgente(c.Id, agentesDe)).ToList();
        var nombres = casos.SelectMany(c => AgentesDe(c.Id, agentesDe)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var completos = suyas.Count(f => Preparacion.TieneLaPreparacionCompleta(f.Persona));

        return new UnidadDelResumen(
            numero,
            nombre,
            suyas.Count,
            completos,
            suyas.Count - completos,
            nombres.Count > 0 ? string.Join(", ", nombres) : Vocabulario.SinAgente,
            conAgente.Count == 0 ? null : conAgente.All(Contesto));
    }

    // ---- por agente ---------------------------------------------------------

    /// <summary>Una fila por agente, de mas a menos personas, y «sin asignar» siempre la ultima.</summary>
    /// <remarks>Un caso con dos agentes cuenta para los dos, como en «El equipo» del PDF: la pregunta es a quien preguntarle.</remarks>
    /// <param name="viajaron">Las personas que ya viajaron.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static List<AgenteDelResumen> PorAgente(
        IReadOnlyList<PersonaConSuCaso> viajaron,
        IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
    {
        var filas = viajaron
            .SelectMany(f => AgentesDe(f.CasoId, agentesDe).DefaultIfEmpty(Vocabulario.SinAgente).Select(agente => (Agente: agente, Persona: f)))
            .GroupBy(par => par.Agente, StringComparer.Ordinal)
            .Select(grupo => UnAgente(grupo.Key, grupo.Select(par => par.Persona).ToList()))
            .ToList();

        return filas
            .Where(a => a.Nombre != Vocabulario.SinAgente)
            .OrderByDescending(a => a.Asignados)
            .ThenBy(a => a.Nombre, StringComparer.Ordinal)
            .Concat(filas.Where(a => a.Nombre == Vocabulario.SinAgente))
            .ToList();
    }

    /// <summary>La fila de un agente: sus personas y cuantos de sus casos volvieron.</summary>
    /// <param name="nombre">El agente, o «sin asignar».</param>
    /// <param name="suyas">Las personas que ya viajaron con un caso suyo.</param>
    private static AgenteDelResumen UnAgente(string nombre, List<PersonaConSuCaso> suyas)
    {
        var casos = nombre == Vocabulario.SinAgente ? [] : suyas.Select(f => f.Caso).DistinctBy(c => c.Id).ToList();
        var completos = suyas.Count(f => Preparacion.TieneLaPreparacionCompleta(f.Persona));

        return new AgenteDelResumen(nombre, suyas.Count, completos, suyas.Count - completos, casos.Count(Contesto), casos.Count);
    }

    // ---- por mes ------------------------------------------------------------

    /// <summary>Una barra doble por mes del periodo, tenga viajes o no, para que el eje no salte meses.</summary>
    /// <param name="recuento">Las personas ya repartidas entre completas y sin completar.</param>
    /// <param name="periodo">El periodo, del que salen los meses.</param>
    private static List<MesDelResumen> PorMes(Recuento recuento, Periodo periodo)
    {
        var meses = MesesDe(periodo);
        var cruzaDeAno = meses[0].Year != meses[^1].Year;

        return meses
            .Select(mes => new MesDelResumen(
                RotuloDelMes(mes, cruzaDeAno),
                recuento.Completas.Count(f => CaeEnElMes(f.FechaViaje, mes)),
                recuento.SinCompletar.Count(f => CaeEnElMes(f.FechaViaje, mes))))
            .ToList();
    }

    /// <summary>El primer dia de cada mes que toca el periodo, en orden.</summary>
    /// <param name="periodo">El periodo ya validado.</param>
    private static List<DateOnly> MesesDe(Periodo periodo)
    {
        var desde = DateOnly.ParseExact(periodo.Desde, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var hasta = DateOnly.ParseExact(periodo.Hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var meses = new List<DateOnly>();
        for (var mes = new DateOnly(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
            meses.Add(mes);
        return meses;
    }

    /// <summary>«sep», o «sep 26» cuando el periodo cruza de ano y el mes solo no bastaria.</summary>
    /// <param name="mes">El primer dia del mes.</param>
    /// <param name="cruzaDeAno">Si el periodo toca mas de un ano.</param>
    private static string RotuloDelMes(DateOnly mes, bool cruzaDeAno)
        => cruzaDeAno ? $"{Meses[mes.Month - 1]} {mes.Year % 100:00}" : Meses[mes.Month - 1];

    /// <summary>Si esa fecha «AAAA-MM-DD» cae en ese mes; se compara el prefijo «AAAA-MM».</summary>
    /// <param name="fechaViaje">La fecha de viaje, o nula.</param>
    /// <param name="mes">El primer dia del mes.</param>
    private static bool CaeEnElMes(string? fechaViaje, DateOnly mes)
        => fechaViaje is not null && fechaViaje.Length >= 7
           && fechaViaje[..7] == mes.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // ---- los pendientes -----------------------------------------------------

    /// <summary>La lista de solo los pendientes, en el orden de lectura del recuento.</summary>
    /// <param name="sinCompletar">Las personas que viajaron sin completar.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static List<PendienteDelResumen> Pendientes(
        IReadOnlyList<PersonaConSuCaso> sinCompletar,
        IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
        => sinCompletar
            .Select(f => new PendienteDelResumen(
                Vocabulario.NumeroDeUnidad(f.Caso.UnidadNumero),
                Vocabulario.NombreDeUnidad(f.Caso.UnidadNombre),
                Vocabulario.PersonaOSinNombre(f.Persona.Nombre),
                QueLeFalta(f.Persona),
                AgentesEscritos(f.CasoId, agentesDe),
                f.FechaViaje,
                f.Caso.NumeroCaso,
                string.IsNullOrWhiteSpace(f.Caso.TemploNombre) ? SinTemplo : f.Caso.TemploNombre))
            .ToList();

    /// <summary>Solo los pasos que faltan, separados por coma; o «nadie la miró» si no consta ni uno.</summary>
    /// <remarks>
    /// Mas corto que <see cref="Preparacion.QuePaso"/>, que dice «no está completa: falta X y Y»
    /// y es lo que lleva el PDF. Aqui la columna ya se llama «Qué le falta», y repetirlo en cada
    /// fila son letras que el dueno pidio quitar. El caso raro —marcada que no sin ningun paso
    /// en no— sale como en el PDF.
    /// </remarks>
    /// <param name="persona">La persona que viajo sin completar.</param>
    private static string QueLeFalta(Contratos.Modelos.Persona persona)
    {
        if (Pasos.Estado(persona) != false) return Preparacion.NadieLaMiro;

        var trabados = Pasos.SinCompletar(persona);
        return trabados.Count == 0 ? Preparacion.NoEstaCompleta : string.Join(", ", trabados);
    }

    // ---- lo compartido ------------------------------------------------------

    /// <summary>El templo del periodo: el unico, o «N templos: A · B» si hay varios, o la raya si ninguno.</summary>
    /// <param name="personas">Todas las personas del periodo; el templo es de su caso.</param>
    private static string TemploDe(IReadOnlyList<PersonaConSuCaso> personas)
    {
        var templos = personas
            .Select(f => f.Caso.TemploNombre)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return templos.Count switch
        {
            0 => SinTemplo,
            1 => templos[0],
            _ => $"{templos.Count} templos: {string.Join(" · ", templos)}",
        };
    }

    /// <summary>El dia de la marca con la que se genera; el mismo corte que hace <see cref="ArmadoDelDocumento"/> para el PDF.</summary>
    /// <remarks>Del mismo «hoy» depende quien cuenta como «ya viajó»: si los dos armados cortaran distinto, el Excel y el PDF discreparian por un dia.</remarks>
    /// <param name="generadoEn">La marca completa; si es más corta que una fecha, se devuelve tal cual.</param>
    private static string DiaDe(string generadoEn)
        => generadoEn.Length >= 10 ? generadoEn[..10] : generadoEn;

    /// <summary>Si el caso lleva escrito el estado que dijo el companero: eso es «contestó».</summary>
    /// <param name="caso">El caso.</param>
    private static bool Contesto(Contratos.Modelos.Caso caso) => !string.IsNullOrWhiteSpace(caso.EstadoDelCompanero);

    /// <summary>Si ese caso tiene a alguien asignado.</summary>
    /// <param name="casoId">El id del caso.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static bool TieneAgente(long casoId, IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
        => AgentesDe(casoId, agentesDe).Count > 0;

    /// <summary>Los nombres de quien lleva ese caso; vacia si nadie.</summary>
    /// <param name="casoId">El id del caso.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static IReadOnlyList<string> AgentesDe(long casoId, IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
        => agentesDe.TryGetValue(casoId, out var nombres) ? nombres : [];

    /// <summary>Los agentes de un caso escritos en una celda, o «sin asignar».</summary>
    /// <param name="casoId">El id del caso.</param>
    /// <param name="agentesDe">Quien lleva cada caso, por id.</param>
    private static string AgentesEscritos(long casoId, IReadOnlyDictionary<long, IReadOnlyList<string>> agentesDe)
    {
        var nombres = AgentesDe(casoId, agentesDe);
        return nombres.Count > 0 ? string.Join(", ", nombres) : Vocabulario.SinAgente;
    }
}
