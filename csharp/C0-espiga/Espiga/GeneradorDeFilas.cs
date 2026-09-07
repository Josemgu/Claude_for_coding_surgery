namespace Espiga;

/// <summary>
/// Fabrica las filas de prueba. Determinista: la misma semilla da la misma lista,
/// para que dos mediciones se puedan comparar.
/// </summary>
public static class GeneradorDeFilas
{
    private static readonly string[] NombresDePila =
    [
        "Maria", "Jose", "Ana", "Luis", "Carmen", "Pedro", "Rosa", "Juan",
        "Elizabeth", "Miguel", "Sandy", "Ramon", "Yulissa", "Rafael"
    ];

    private static readonly string[] Apellidos =
    [
        // 2026-09-07: eran doce apellidos sacados de los escaneos del dueno. Se cambian
        // por inventados con la misma forma —dos palabras, particula, acento— porque el
        // repositorio se sube a GitHub y esos apellidos son de personas de verdad.
        "Aledo", "Bermello", "Candal", "Duarte", "De Lama", "Espino",
        "Fontela", "Gadea", "Hinojal", "Illera", "Jauregui", "Lombao"
    ];

    private static readonly string[] Estados =
    [
        "Sin revisar", "En revision", "Completa", "No completa", "Archivada"
    ];

    /// <summary>Crea <paramref name="cuantas"/> filas con datos inventados.</summary>
    public static List<Fila> Generar(int cuantas)
    {
        var azar = new Random(20260904);
        var filas = new List<Fila>(cuantas);

        for (var i = 1; i <= cuantas; i++)
        {
            var nombre = NombresDePila[azar.Next(NombresDePila.Length)];
            var apellido = Apellidos[azar.Next(Apellidos.Length)];
            var dia = 1 + azar.Next(28);

            filas.Add(new Fila
            {
                Numero = i,
                NumeroDeCaso = $"BARC{2600 + (i % 100):D4}",
                Nombre = $"{nombre} {apellido}",
                FechaDeViaje = $"2026-09-{dia:D2}",
                Estado = Estados[azar.Next(Estados.Length)]
            });
        }

        return filas;
    }
}
