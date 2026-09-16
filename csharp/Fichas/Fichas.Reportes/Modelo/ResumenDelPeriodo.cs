namespace Fichas.Reportes.Modelo;

/// <summary>Una fila de la tabla «Por unidad» de la hoja del periodo.</summary>
/// <param name="Numero">El numero de la unidad tal cual, o «sin número».</param>
/// <param name="Nombre">El nombre de la unidad, o «unidad sin nombre».</param>
/// <param name="Viajaron">Cuantas personas de esa unidad ya viajaron en el periodo.</param>
/// <param name="Completos">De esas, cuantas con los seis pasos en sí.</param>
/// <param name="SinCompletar">De esas, cuantas sin ellos.</param>
/// <param name="Agente">Quien lleva sus casos, separados por coma; «sin asignar» si nadie.</param>
/// <param name="Devolvio">Si todos sus casos con agente volvieron contestados; nulo si ninguno tiene agente.</param>
public sealed record UnidadDelResumen(
    string Numero,
    string Nombre,
    int Viajaron,
    int Completos,
    int SinCompletar,
    string Agente,
    bool? Devolvio);

/// <summary>Una fila de la tabla «Por agente» de la hoja del periodo.</summary>
/// <param name="Nombre">El agente, o «sin asignar» en la ultima fila.</param>
/// <param name="Asignados">Cuantas personas que ya viajaron llevan un caso suyo.</param>
/// <param name="Completos">De esas, cuantas con los seis pasos en sí.</param>
/// <param name="SinCompletar">De esas, cuantas sin ellos.</param>
/// <param name="CasosDevueltos">Cuantos de sus casos volvieron contestados.</param>
/// <param name="CasosACargo">Cuantos casos suyos viajaron en el periodo; cero en «sin asignar».</param>
public sealed record AgenteDelResumen(
    string Nombre,
    int Asignados,
    int Completos,
    int SinCompletar,
    int CasosDevueltos,
    int CasosACargo);

/// <summary>Una barra doble del grafico: un mes del periodo.</summary>
/// <param name="Rotulo">El mes en tres letras, con el año detras solo si el periodo cruza de año.</param>
/// <param name="Completos">Cuantas personas viajaron ese mes con todo completo.</param>
/// <param name="ConDificultades">Cuantas viajaron ese mes sin completar.</param>
public sealed record MesDelResumen(string Rotulo, int Completos, int ConDificultades);

/// <summary>Una persona que viajo sin completar: la lista de debajo de la hoja.</summary>
/// <param name="NumeroDeUnidad">El numero de su unidad, tal cual.</param>
/// <param name="Unidad">El nombre de su unidad.</param>
/// <param name="Persona">Su nombre, o «nombre sin leer».</param>
/// <param name="QueLeFalta">En que se quedo, con las palabras de <c>Preparacion.QuePaso</c>.</param>
/// <param name="Agente">Quien lleva su caso, o «sin asignar».</param>
/// <param name="ViajoEl">La fecha de viaje del caso.</param>
/// <param name="Caso">El numero de caso, o nulo si no se leyo.</param>
/// <param name="Templo">El templo del caso, o una raya si no consta.</param>
public sealed record PendienteDelResumen(
    string NumeroDeUnidad,
    string Unidad,
    string Persona,
    string QueLeFalta,
    string Agente,
    string? ViajoEl,
    string? Caso,
    string Templo);

/// <summary>
/// Todo lo que lleva la hoja unica del Excel del periodo, ya contado y con sus palabras.
/// </summary>
/// <remarks>
/// Es lo que el dueno aprobo el 2026-09-16 en el mockup v3: cabecera, cinco tarjetas, la tabla
/// por unidad, la tabla por agente, el grafico por mes y la lista de solo los pendientes.
/// <b>Aqui no hay ni un parrafo:</b> titulos, cifras y tablas.
/// </remarks>
/// <param name="Titulo">El titulo del documento, el mismo del PDF.</param>
/// <param name="PeriodoEnTexto">El periodo escrito como se lee.</param>
/// <param name="Templo">El templo del periodo, o cuantos y cuales si hay varios.</param>
/// <param name="GeneradoEn">La marca de tiempo con la que se genero.</param>
/// <param name="Viajaron">Cuantas personas ya viajaron en el periodo.</param>
/// <param name="Completos">De esas, cuantas con los seis pasos en sí.</param>
/// <param name="SinCompletar">De esas, cuantas sin ellos.</param>
/// <param name="Meses">Cuantos meses abarca el periodo.</param>
/// <param name="Unidades">Cuantas unidades tienen a alguien que ya viajo.</param>
/// <param name="UnidadesConPendientes">De esas, cuantas con alguien sin completar.</param>
/// <param name="UnidadesSinAgente">De las que tienen pendientes, cuantas no tienen a nadie asignado.</param>
/// <param name="CasosDevueltos">Cuantos casos con agente volvieron contestados.</param>
/// <param name="CasosConAgente">Cuantos casos que viajaron tienen a alguien asignado.</param>
/// <param name="Agentes">Cuantos agentes llevan algo del periodo; «sin asignar» no cuenta.</param>
/// <param name="AgentesSinPendientes">De esos, cuantos no tienen a nadie sin completar.</param>
/// <param name="PorUnidad">La tabla por unidad, con las que mas deben primero.</param>
/// <param name="PorAgente">La tabla por agente, con «sin asignar» al final si hace falta.</param>
/// <param name="PorMes">Una barra doble por mes del periodo, en orden.</param>
/// <param name="Pendientes">Solo los que viajaron sin completar, en orden de lectura.</param>
public sealed record ResumenDelPeriodo(
    string Titulo,
    string PeriodoEnTexto,
    string Templo,
    string GeneradoEn,
    int Viajaron,
    int Completos,
    int SinCompletar,
    int Meses,
    int Unidades,
    int UnidadesConPendientes,
    int UnidadesSinAgente,
    int CasosDevueltos,
    int CasosConAgente,
    int Agentes,
    int AgentesSinPendientes,
    IReadOnlyList<UnidadDelResumen> PorUnidad,
    IReadOnlyList<AgenteDelResumen> PorAgente,
    IReadOnlyList<MesDelResumen> PorMes,
    IReadOnlyList<PendienteDelResumen> Pendientes);
