namespace Fichas.App.Cascara;

/// <summary>
/// El titulo que la cabecera ensena para cada entrada del menu.
/// </summary>
/// <remarks>
/// <para>Sale de <c>mockups/mockup-v2-inicio.html</c>, que dibuja el titulo de la pantalla en
/// la cabecera y lo dice en su tabla de controles: al pulsar una entrada, «la cabecera cambia
/// de titulo».</para>
///
/// <para>⚠️ Los titulos NO son los del mockup. El mockup dibuja cinco entradas —Panel,
/// Revisar, Tabla, Equipo, Historial— y el programa tiene nueve, cada una con el nombre que
/// le puso el dueno. Lo que se copia del mockup es la FORMA, no la lista: renombrar una
/// pantalla aqui seria cambiar una palabra suya sin preguntarle.</para>
///
/// <para>Va aparte de <c>VentanaPrincipal</c> a proposito: asi se puede comprobar sin abrir
/// una ventana, que es como se comprueba todo lo demas en este proyecto.</para>
/// </remarks>
public static class TituloDeLaPantalla
{
    /// <summary>
    /// Lo que se ensena cuando la etiqueta no se conoce.
    /// </summary>
    /// <remarks>
    /// El mismo trato que <c>VentanaPrincipal.PantallaDe</c>, que ante un nombre raro abre
    /// Inicio: una cabecera en blanco no dice nada y una excepcion cierra el programa.
    /// </remarks>
    private const string ElDeInicio = "Inicio";

    /// <summary>El titulo de la pantalla de esa etiqueta del menu.</summary>
    /// <param name="etiqueta">El <c>Tag</c> de la entrada, tal como lo escribe el XAML.</param>
    public static string De(string? etiqueta) => etiqueta switch
    {
        "Importar" => "Importar",
        "Flujo" => "Flujo de trabajo",
        "Correccion" => "Corrección",
        "Completar" => "Completar",
        "Revisar" => "Revisar",
        "Asignar" => "Asignar",
        "Paquetes" => "Paquetes",
        "Reportes" => "Reportes",
        _ => ElDeInicio,
    };
}
