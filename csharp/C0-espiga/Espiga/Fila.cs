namespace Espiga;

/// <summary>
/// Una fila de la lista de prueba. Imita el ancho de un renglon real de la
/// pantalla Inicio (numero de caso, nombre, fecha de viaje, estado) para que la
/// medicion de dibujado no salga barata por usar filas de una sola palabra.
/// </summary>
public sealed class Fila
{
    public required int Numero { get; init; }

    public required string NumeroDeCaso { get; init; }

    public required string Nombre { get; init; }

    public required string FechaDeViaje { get; init; }

    public required string Estado { get; init; }

    public string Resumen => $"{Numero:D4}  ·  {NumeroDeCaso}  ·  {Nombre}";
}
