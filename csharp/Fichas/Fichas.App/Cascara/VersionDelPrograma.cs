using System.Reflection;

namespace Fichas.App.Cascara;

/// <summary>
/// El número de versión del programa, leído del ensamblado y no escrito a mano.
/// </summary>
/// <remarks>
/// <para>Nace el 2026-09-10 de lo que el dueño pidió el 2026-09-07 y repitió hoy: «control
/// de actualización». Va a recibir versiones nuevas y tiene que saber cuál tiene abierta.
/// Medido sobre <c>master</c> antes de tocar nada: el programa no llevaba número de versión
/// en ningún sitio.</para>
///
/// <para><b>El único sitio donde está escrito</b> es <c>&lt;Version&gt;</c> en
/// <c>Fichas.App.csproj</c>. De ahí MSBuild genera <c>AssemblyInformationalVersion</c> con
/// el texto tal cual, y eso es lo que se lee aquí: si esta clase escribiera «9» a mano,
/// habría dos sitios y un día dirían cosas distintas.</para>
///
/// <para>⛔ Aquí NO se busca ninguna actualización ni se toca la red: el repositorio es
/// privado, eso necesita una llave y es decisión del dueño, que todavía no la ha tomado.
/// Solo el número y que se vea.</para>
/// </remarks>
public static class VersionDelPrograma
{
    /// <summary>Lo que se enseña si el ensamblado no trae versión, que no debería pasar nunca.</summary>
    /// <remarks>
    /// Se dice en vez de inventar un «1.0»: un número inventado es justo lo que el control de
    /// actualización existe para evitar.
    /// </remarks>
    private const string CuandoNoHayVersion = "sin versión";

    /// <summary>El número tal como está escrito en el código: «9», «9.1», «10».</summary>
    public static string Numero { get; } = LeerElNumero();

    /// <summary>Como lo cuenta el dueño y como se etiqueta en git: «v9».</summary>
    public static string ComoSeLee => "v" + Numero;

    /// <summary>
    /// El <c>AssemblyInformationalVersion</c> del ensamblado de la app, sin la cola de commit.
    /// </summary>
    /// <remarks>
    /// El SDK puede pegarle «+abc123» con el commit; se corta para que la cabecera diga lo
    /// que dice <c>Fichas.App.csproj</c> y nada más. Y se mira el ensamblado de ESTA clase,
    /// no el de entrada: en las pruebas el de entrada es el corredor de pruebas.
    /// </remarks>
    private static string LeerElNumero()
    {
        var declarada = typeof(VersionDelPrograma).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(declarada)) return CuandoNoHayVersion;

        var mas = declarada.IndexOf('+', StringComparison.Ordinal);
        return mas < 0 ? declarada.Trim() : declarada[..mas].Trim();
    }
}
