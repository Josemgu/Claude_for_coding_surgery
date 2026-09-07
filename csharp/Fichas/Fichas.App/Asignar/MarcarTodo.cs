namespace Fichas.App.Asignar;

/// <summary>
/// La regla de Ctrl+A, sola y sin ventana, porque las dos pantallas la comparten.
/// </summary>
/// <remarks>
/// El marcado en si lo lleva el <c>ItemsView</c> de WinUI, que ya virtualiza y ya sabe
/// marcar varios. Lo unico que WinUI NO decide es que hace Ctrl+A cuando ya estaba todo
/// marcado, y eso es lo que se prueba aqui: la segunda pulsacion desmarca, que es lo que
/// espera quien lo pulso por error.
///
/// Y marca lo que se VE, no la base entera: marcar con Ctrl+A algo que no esta en pantalla
/// y archivarlo despues seria archivar a ciegas.
/// </remarks>
public static class MarcarTodo
{
    /// <summary>Si la pulsacion tiene que marcar (verdadero) o desmarcar (falso).</summary>
    /// <param name="yaMarcados">Cuantos hay marcados ahora mismo.</param>
    /// <param name="aLaVista">Cuantos hay en la lista que se esta viendo.</param>
    public static bool HayQueMarcar(int yaMarcados, int aLaVista) => yaMarcados < aLaVista;
}
