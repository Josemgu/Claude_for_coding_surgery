using Fichas.Contratos.Puertos;

namespace Fichas.Datos.Falso;

/// <summary>El reloj de verdad, el del sistema. Es el que usa la app cuando arranca.</summary>
public sealed class RelojDelSistema : IReloj
{
    /// <summary>La fecha de hoy en ISO-8601.</summary>
    public string Hoy() => DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>El instante de ahora en ISO-8601 con segundos.</summary>
    public string Ahora() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>La fecha de dentro de N dias en ISO-8601.</summary>
    /// <param name="dias">Cuántos días; negativo va hacia atrás.</param>
    public string HoyMasDias(int dias)
        => DateTime.Now.AddDays(dias).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Un reloj parado en un dia elegido, para que las pruebas no dependan del calendario.</summary>
/// <remarks>
/// Sin esto, una prueba de «viaja en los proximos 7 dias» pasaria hoy y fallaria el martes
/// que viene, y nadie sabria por que.
/// </remarks>
public sealed class RelojFijo : IReloj
{
    /// <summary>El día en el que está parado, a medianoche; <see cref="Ahora"/> le pone las 12:00.</summary>
    private readonly DateTime _dia;

    /// <summary>Para el reloj en el dia que se le diga, en ISO-8601.</summary>
    /// <param name="diaIso">El día, exactamente <c>AAAA-MM-DD</c>; otra forma lanza <see cref="FormatException"/>.</param>
    public RelojFijo(string diaIso)
        => _dia = DateTime.ParseExact(diaIso, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>La fecha en la que esta parado.</summary>
    public string Hoy() => _dia.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>El instante de ahora; siempre las 12:00 del dia en el que esta parado.</summary>
    public string Ahora() => _dia.ToString("yyyy-MM-dd 12:00:00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>La fecha de dentro de N dias contada desde el dia en el que esta parado.</summary>
    /// <param name="dias">Cuántos días; negativo va hacia atrás.</param>
    public string HoyMasDias(int dias)
        => _dia.AddDays(dias).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
