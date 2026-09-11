using Fichas.App.Asignar;
using Fichas.App.Cascara;
using Fichas.App.Revisar;
using Fichas.Contratos.Puertos;

namespace Fichas.Pruebas.App.Revisar;

/// <summary>
/// Monta <see cref="AccionesDeRevisar"/> con la tubería entera, igual que la pantalla.
/// </summary>
/// <remarks>
/// Desde el 2026-09-11 archivar quita la asignación, y eso pasa por
/// <see cref="RetiradaAlArchivar"/> y por la única puerta de retirar,
/// <see cref="OperacionDeAsignar"/>. Una prueba que montara las acciones sin la retirada
/// mediría una tubería más corta que la del programa, y por eso el montaje está en un solo
/// sitio y no repetido en cada prueba.
/// </remarks>
internal static class MontajeDeRevisar
{
    /// <summary>Las acciones de Revisar con la retirada al archivar puesta, sobre esos puertos.</summary>
    public static AccionesDeRevisar Acciones(ICasos casos, IAsignaciones asignaciones, IReloj reloj, BuzonDeAvisos avisos)
        => new(
            casos, reloj, avisos,
            new RetiradaAlArchivar(asignaciones, casos, new OperacionDeAsignar(asignaciones, reloj, avisos)));
}
