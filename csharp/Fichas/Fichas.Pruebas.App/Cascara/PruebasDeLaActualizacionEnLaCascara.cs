using System.Xml.Linq;
using Fichas.App.Cascara;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.App.Cascara;

/// <summary>
/// Lo que la cáscara pone para la actualización: el argumento <c>--sin-actualizacion</c>, el
/// aviso con botón en el buzón, y el botón «Buscar actualización» en la cabecera.
/// </summary>
/// <remarks>
/// <para><b>De dónde sale el criterio.</b> El pase del 2026-09-11: «Con <c>--falso</c> no se
/// consulta. Un argumento nuevo <c>--sin-actualizacion</c> tampoco»; «franja "Hay una versión
/// nueva: v12" con botón "Actualizar ahora"»; «Botón "Buscar actualización" en la cabecera».</para>
///
/// <para>El aviso con botón no toca <c>Aviso</c> —<c>Fichas.Contratos</c> está congelado—: el
/// buzón guarda la acción al lado del aviso y la franja la pregunta al pintar.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLaActualizacionEnLaCascara
{
    // ---- el argumento -------------------------------------------------------------

    /// <summary>Sin argumentos, el programa busca actualización al arrancar.</summary>
    [TestMethod]
    public void SinArgumentosSeBuscaAlArrancar()
    {
        var argumentos = ArgumentosDeArranque.Leer([]);

        Assert.IsFalse(argumentos.SinActualizacion);
        Assert.IsTrue(argumentos.SeBuscaActualizacionAlArrancar);
    }

    /// <summary>Con <c>--sin-actualizacion</c> no se busca, y no es un argumento desconocido.</summary>
    [TestMethod]
    public void ConSinActualizacionNoSeBusca()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--sin-actualizacion"]);

        Assert.IsTrue(argumentos.SinActualizacion);
        Assert.IsFalse(argumentos.SeBuscaActualizacionAlArrancar);
        Assert.IsEmpty(argumentos.NoSeEntendio, "Es un argumento que el programa conoce.");
    }

    /// <summary>Con <c>--falso</c> tampoco se busca, aunque nadie diga <c>--sin-actualizacion</c>.</summary>
    [TestMethod]
    public void ConFalsoNoSeBusca()
    {
        var argumentos = ArgumentosDeArranque.Leer(["--falso", "3"]);

        Assert.IsFalse(argumentos.SinActualizacion);
        Assert.IsFalse(argumentos.SeBuscaActualizacionAlArrancar);
    }

    // ---- el buzón con acción --------------------------------------------------------

    /// <summary>Dado un aviso dejado con acción, el buzón la devuelve para ese aviso y no para otro.</summary>
    [TestMethod]
    public void ElBuzonGuardaLaAccionAlLadoDelAviso()
    {
        var buzon = new BuzonDeAvisos();
        var conBoton = Aviso.Informa("Hay una versión nueva: v12");
        var sinBoton = Aviso.Informa("Otro aviso cualquiera");
        var pulsado = 0;

        buzon.Dejar(conBoton, new AccionDelAviso("Actualizar ahora", () => pulsado++));
        buzon.Dejar(sinBoton);

        var accion = buzon.AccionDe(conBoton);
        Assert.IsNotNull(accion);
        Assert.AreEqual("Actualizar ahora", accion.Rotulo);
        Assert.IsNull(buzon.AccionDe(sinBoton));

        accion.Hacer();
        Assert.AreEqual(1, pulsado);
    }

    /// <summary>Dos avisos iguales letra a letra no comparten acción: la acción va con el objeto, no con el texto.</summary>
    [TestMethod]
    public void DosAvisosIgualesNoCompartenAccion()
    {
        var buzon = new BuzonDeAvisos();
        var uno = Aviso.Informa("Hay una versión nueva: v12");
        var otro = Aviso.Informa("Hay una versión nueva: v12");

        buzon.Dejar(uno, new AccionDelAviso("Actualizar ahora", () => { }));
        buzon.Dejar(otro);

        Assert.IsNotNull(buzon.AccionDe(uno));
        Assert.IsNull(buzon.AccionDe(otro));
    }

    /// <summary>Al cerrar el aviso, su acción se va con él.</summary>
    [TestMethod]
    public void AlCerrarElAvisoSeVaSuAccion()
    {
        var buzon = new BuzonDeAvisos();
        var aviso = Aviso.Informa("Hay una versión nueva: v12");
        buzon.Dejar(aviso, new AccionDelAviso("Actualizar ahora", () => { }));

        buzon.CerrarElPrimero();

        Assert.IsNull(buzon.AccionDe(aviso));
        Assert.IsEmpty(buzon.Pendientes);
    }

    /// <summary>Cerrar todos también se lleva todas las acciones.</summary>
    [TestMethod]
    public void CerrarTodosSeLlevaLasAcciones()
    {
        var buzon = new BuzonDeAvisos();
        var aviso = Aviso.Informa("Hay una versión nueva: v12");
        buzon.Dejar(aviso, new AccionDelAviso("Actualizar ahora", () => { }));
        buzon.Dejar(Aviso.Informa("Otro"));

        buzon.CerrarTodos();

        Assert.IsNull(buzon.AccionDe(aviso));
    }

    // ---- la cabecera y la franja -----------------------------------------------------

    /// <summary>La cabecera tiene el botón «Buscar actualización», con nombre y con identificador para leerlo por accesibilidad.</summary>
    [TestMethod]
    public void LaCabeceraTieneElBotonDeBuscarActualizacion()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var cabecera = ElXaml("VentanaPrincipal.xaml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "NavigationView.Header");
        Assert.IsNotNull(cabecera, "La ventana no declara «NavigationView.Header».");

        var boton = cabecera.Descendants()
            .FirstOrDefault(e => e.Attribute(x + "Name")?.Value == "_botonDeBuscarActualizacion");

        Assert.IsNotNull(boton, "La cabecera no tiene «_botonDeBuscarActualizacion».");
        Assert.AreEqual("buscarActualizacion", Atributo(boton, "AutomationProperties.AutomationId"));
        Assert.AreEqual("Buscar actualización", Atributo(boton, "AutomationProperties.Name"));
    }

    /// <summary>La franja tiene un botón de acción, oculto hasta que un aviso traiga acción.</summary>
    [TestMethod]
    public void LaFranjaTieneElBotonDeAccionOculto()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var boton = ElXaml("FranjaDeAvisos.xaml").Descendants()
            .FirstOrDefault(e => e.Attribute(x + "Name")?.Value == "_botonDeAccion");

        Assert.IsNotNull(boton, "La franja no tiene «_botonDeAccion».");
        Assert.AreEqual("Collapsed", Atributo(boton, "Visibility"));
        Assert.AreEqual("botonDeAccionDelAviso", Atributo(boton, "AutomationProperties.AutomationId"));
    }

    /// <summary>El valor de un atributo por su nombre local, o vacío.</summary>
    /// <param name="elemento">El elemento del XAML.</param>
    /// <param name="nombre">El nombre local del atributo, como «AutomationProperties.Name».</param>
    private static string Atributo(XElement elemento, string nombre)
        => elemento.Attributes().FirstOrDefault(a => a.Name.LocalName == nombre)?.Value ?? string.Empty;

    /// <summary>Un <c>.xaml</c> de la cáscara, cargado desde el árbol de código.</summary>
    /// <param name="archivo">El nombre del archivo dentro de <c>Fichas.App/Cascara</c>.</param>
    private static XDocument ElXaml(string archivo)
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var cascara = Path.Combine(actual.FullName, "csharp", "Fichas", "Fichas.App", "Cascara");
            if (Directory.Exists(cascara)) return XDocument.Load(Path.Combine(cascara, archivo));
            actual = actual.Parent;
        }

        Assert.Inconclusive($"No se encontró «csharp/Fichas/Fichas.App/Cascara» desde «{AppContext.BaseDirectory}».");
        return new XDocument();
    }
}
