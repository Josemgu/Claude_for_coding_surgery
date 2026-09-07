using System.Globalization;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Cascara;

/// <summary>El reloj de verdad: el de la maquina donde corre el programa.</summary>
/// <remarks>
/// Hora LOCAL y no UTC, igual que el resto del proyecto (<c>AplicadorDeEsquema.MarcaDeTiempo</c>):
/// quien lee esta base mira su reloj de pared, y una marca en UTC le saldria corrida cuatro
/// horas sin decirselo. Y el formato es el mismo que escribe el esquema, para que dos filas
/// escritas por caminos distintos se puedan comparar.
/// </remarks>
public sealed class RelojDelSistema : IReloj
{
    /// <summary>Hoy, en AAAA-MM-DD.</summary>
    public string Hoy() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Ahora, en AAAA-MM-DD HH:MM:SS.</summary>
    public string Ahora() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>La fecha de dentro de N dias; N negativo va hacia atras.</summary>
    public string HoyMasDias(int dias)
        => DateTime.Now.AddDays(dias).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
