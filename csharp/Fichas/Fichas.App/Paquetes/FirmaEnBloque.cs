using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>
/// Lo que el boton va a firmar, contado ANTES de firmar nada.
/// </summary>
/// <remarks>
/// La cuenta se ensena antes porque el dueno tiene que poder no seguir. Un boton que firma
/// y luego dice cuantas firmo no da esa opcion.
/// </remarks>
/// <param name="SeFirman">Cuantos campos pasarian a verificado.</param>
/// <param name="YaFirmados">Cuantos ya lo estaban; no se vuelven a firmar ni se cuentan.</param>
/// <param name="VaciosOMalos">Cuantos se quedan fuera por estar vacios o no cumplir su forma.</param>
/// <param name="Documentos">De cuantos documentos son.</param>
/// <param name="Personas">De cuantas personas son.</param>
public sealed record CuentaDeLaFirma(
    int SeFirman, int YaFirmados, int VaciosOMalos, int Documentos, int Personas)
{
    /// <summary>Una cuenta de una vuelta en la que no hay nada que firmar.</summary>
    public static CuentaDeLaFirma Nada { get; } = new(0, 0, 0, 0, 0);

    /// <summary>Si el boton tiene algo que hacer.</summary>
    public bool HayAlgoQueFirmar => SeFirman > 0;

    /// <summary>La frase que se lee ANTES de pulsar, con el numero delante.</summary>
    public string Linea
    {
        get
        {
            if (!HayAlgoQueFirmar)
                return "No queda ningún campo por dar por bueno en lo que trajo este paquete.";

            var cola = new List<string>();
            if (YaFirmados > 0) cola.Add($"{YaFirmados} ya {(YaFirmados == 1 ? "estaba" : "estaban")}");
            if (VaciosOMalos > 0) cola.Add($"{VaciosOMalos} se {(VaciosOMalos == 1 ? "queda" : "quedan")} fuera");

            var frase = $"Va a firmar {SeFirman} {(SeFirman == 1 ? "campo" : "campos")} de "
                      + $"{Documentos} {(Documentos == 1 ? "documento" : "documentos")} y "
                      + $"{Personas} {(Personas == 1 ? "persona" : "personas")}";
            return cola.Count == 0 ? frase + "." : frase + " · " + string.Join(" · ", cola) + ".";
        }
    }
}

/// <summary>Lo que quedo firmado de verdad, contado despues.</summary>
/// <param name="Firmados">Cuantos campos quedaron con verificado a nombre de quien firmo.</param>
/// <param name="NoSePudieron">Cuantos se intentaron y el almacen no admitio.</param>
/// <param name="QuienFirmo">El nombre que quedo escrito en <c>verificado_por</c>.</param>
/// <param name="Avisos">Lo que hay que dejar en la franja; puede estar vacio.</param>
public sealed record ResultadoDeLaFirma(
    int Firmados, int NoSePudieron, string QuienFirmo, IReadOnlyList<Aviso> Avisos)
{
    /// <summary>La linea que se pinta despues de firmar.</summary>
    public string Linea
    {
        get
        {
            var cola = NoSePudieron == 0
                ? string.Empty
                : $" · {NoSePudieron} no se {(NoSePudieron == 1 ? "pudo" : "pudieron")}";
            return $"Dados por buenos {Firmados} {(Firmados == 1 ? "campo" : "campos")} "
                 + $"a nombre de {QuienFirmo}{cola}.";
        }
    }
}

/// <summary>
/// Da por bueno de una vez todo lo que trajo un paquete, con el nombre de Miguel.
/// </summary>
/// <remarks>
/// <para>Lo pidio el dueno el 2026-09-06: <i>«debe haber botones donde aplique "todo
/// completo", igual en los paquetes, porque ir uno por uno si está bien pero no es
/// suficiente»</i>. Y dijo donde: <i>«lo que sí debo revisar sí o sí son los paquetes que
/// vienen de los agentes»</i>.</para>
///
/// <para>⛔ <b>Esto firma CAMPOS, y solo campos.</b> No toca
/// <c>casos.estado_recomendacion</c> ni ninguna de las columnas del companero: el estado de
/// la recomendacion lo escribio el Excel con el nombre del companero y sigue siendo suyo
/// (DECISIONES.md, 2026-09-03). Son dos cosas y no se mezclan. Si este boton acabara
/// escribiendo el estado, o firmando con el nombre del companero, estaria mal.</para>
///
/// <para>⛔ <b>Y sigue sin firmarse nada solo.</b> La regla permanente 5 no se relaja: el
/// unico camino a <c>verificado = 1</c> sigue siendo <see cref="IProcedencia.Firmar"/> con
/// quien y cuando, y aqui solo se llama porque el dueno pulso, y despues de que la pantalla
/// le haya dicho cuantos campos son.</para>
///
/// <para>Las tres reglas de que se puede firmar son las de la pantalla de Correccion y se
/// leen de donde viven: un campo vacio no se firma —firmar un hueco deja
/// <c>verificado = 1</c> sobre nada—, un valor que no cumple su forma tampoco, y un campo
/// sin fila de procedencia se anota primero, porque <c>Firmar</c> es un <c>UPDATE</c> y sin
/// fila no cambia nada.</para>
/// </remarks>
public sealed class FirmaEnBloque
{
    /// <summary>El único camino a <c>verificado = 1</c>; también por donde se lee si un campo ya lleva firma.</summary>
    private readonly IProcedencia _procedencia;
    /// <summary>De dónde sale la fecha de la firma; inyectado para que las pruebas la fijen.</summary>
    private readonly IReloj _reloj;

    /// <summary>Se ata a los dos puertos que necesita y a nada mas.</summary>
    /// <param name="procedencia">Repositorio de procedencia de cada campo.</param>
    /// <param name="reloj">De dónde sale la fecha de la firma.</param>
    public FirmaEnBloque(IProcedencia procedencia, IReloj reloj)
    {
        _procedencia = procedencia;
        _reloj = reloj;
    }

    /// <summary>Cuenta lo que se firmaria, sin escribir nada.</summary>
    /// <param name="loQueTrajo">Lo que casó del paquete; si no hay nada que revisar, la cuenta es <see cref="CuentaDeLaFirma.Nada"/>.</param>
    public CuentaDeLaFirma Contar(LoQueTrajoElPaquete loQueTrajo)
    {
        ArgumentNullException.ThrowIfNull(loQueTrajo);
        if (!loQueTrajo.HayQueRevisar) return CuentaDeLaFirma.Nada;

        var seFirman = 0;
        var yaFirmados = 0;
        var fuera = 0;
        foreach (var campo in CamposDelPaquete.De(loQueTrajo.Informaciones))
        {
            if (NoSePuedeFirmar(campo)) fuera++;
            else if (YaEstaFirmado(campo)) yaFirmados++;
            else seFirman++;
        }

        return new CuentaDeLaFirma(
            seFirman,
            yaFirmados,
            fuera,
            loQueTrajo.Documentos,
            loQueTrajo.Informaciones.Select(una => una.PersonaId).Distinct().Count());
    }

    /// <summary>Firma de verdad, a nombre de quien se diga, y devuelve la cuenta de lo escrito.</summary>
    /// <param name="loQueTrajo">Lo que caso; lo demas no llega hasta aqui.</param>
    /// <param name="quienFirma">El administrador. Nunca se inventa un nombre.</param>
    /// <returns>Cuántos quedaron firmados, cuántos no admitió el almacén y sus avisos.</returns>
    public ResultadoDeLaFirma Firmar(LoQueTrajoElPaquete loQueTrajo, Companero quienFirma)
    {
        ArgumentNullException.ThrowIfNull(loQueTrajo);
        ArgumentNullException.ThrowIfNull(quienFirma);

        var firmados = 0;
        var noSePudieron = 0;
        var avisos = new List<Aviso>();
        var cuando = _reloj.Ahora();

        foreach (var campo in CamposDelPaquete.De(loQueTrajo.Informaciones))
        {
            if (NoSePuedeFirmar(campo) || YaEstaFirmado(campo)) continue;

            AnotarSiNoHayFila(campo);
            var escritura = _procedencia.Firmar(
                campo.Tabla, campo.RegistroId, campo.Campo, quienFirma.Id, cuando);

            if (escritura.SeEscribio) firmados++;
            else
            {
                noSePudieron++;
                avisos.AddRange(escritura.Avisos);
            }
        }

        return new ResultadoDeLaFirma(firmados, noSePudieron, quienFirma.Nombre, avisos);
    }

    /// <summary>Un campo vacio o con la forma equivocada no se firma. Las reglas son las de Correccion.</summary>
    /// <param name="campo">El campo con el valor que hay guardado.</param>
    private static bool NoSePuedeFirmar(CampoDelPaquete campo)
        => string.IsNullOrWhiteSpace(campo.Valor)
        || ReglasDeCampo.MotivoDe(campo.Campo, campo.Valor) is not null;

    /// <summary>Si ese campo ya lleva firma; volver a firmarlo pisaria la fecha de quien lo hizo.</summary>
    /// <param name="campo">El campo que se pregunta.</param>
    private bool YaEstaFirmado(CampoDelPaquete campo)
        => _procedencia.DeRegistro(campo.Tabla, campo.RegistroId)
            .Any(fila => fila.Campo == campo.Campo && fila.Verificado);

    /// <summary>
    /// Deja la fila de procedencia cuando no hay ninguna, porque <c>Firmar</c> es un
    /// <c>UPDATE</c> y sin fila no cambia nada.
    /// </summary>
    /// <remarks>
    /// Origen «vacio» porque es lo unico que consta: de ese campo no hay lectura guardada.
    /// <c>Anotar</c> jamas pone verificado, asi que la regla permanente 5 sigue intacta.
    /// Es el caso medido de <c>templo_nombre</c>, que en la base del dueno no tenia fila en
    /// ninguno de los siete documentos.
    /// </remarks>
    /// <param name="campo">El campo que se va a firmar.</param>
    private void AnotarSiNoHayFila(CampoDelPaquete campo)
    {
        var hay = _procedencia.DeRegistro(campo.Tabla, campo.RegistroId)
            .Any(fila => fila.Campo == campo.Campo);
        if (hay) return;

        _procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = campo.Tabla,
            RegistroId = campo.RegistroId,
            Campo = campo.Campo,
            Origen = OrigenDeCampo.Vacio,
        });
    }
}
