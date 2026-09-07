using System.Diagnostics;
using Fichas.App.Asignar;
using Fichas.App.Correccion;
using Fichas.App.Inicio;
using Fichas.App.Paquetes;
using Fichas.App.Reportes;
using Fichas.App.Revisar;
using Fichas.Contratos.Modelos;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace Fichas.App.Cascara;

/// <summary>
/// La ventana principal: la barra de titulo, las seis entradas, el marco de las
/// pantallas, la franja de avisos arriba y el acuse en el pie.
/// </summary>
/// <remarks>
/// ⛔ Esta clase es de la cascara y queda CONGELADA al cerrar el pase de esqueleto.
/// Quien construya una pantalla trabaja en su carpeta (<c>Inicio/</c>, <c>Correccion/</c>…)
/// y no toca este archivo.
/// </remarks>
public sealed partial class VentanaPrincipal : Window
{
    private readonly Servicios _servicios;

    /// <summary>Monta la ventana, la pone del tamano pedido y abre por Inicio.</summary>
    public VentanaPrincipal(Servicios servicios)
    {
        _servicios = servicios;
        InitializeComponent();

        _franjaDeAvisos.AtarA(servicios.Avisos);
        AppWindow.Resize(new SizeInt32(servicios.Argumentos.Ancho, servicios.Argumentos.Alto));

        // El tema que eligio el dueno, ANTES de que se vea nada: ponerlo despues de activar
        // la ventana ensena un parpadeo del tema equivocado. Vive en VentanaPrincipal.Tema.cs.
        MontarElTema();

        // El icono de la barra de titulo y de la barra de tareas. Vive en
        // VentanaPrincipal.Icono.cs, y el comentario de alli dice por que no basta con
        // declararlo en el .csproj.
        PonerElIcono();

        // Lo que no se entendio de la linea de ordenes se dice y no detiene nada.
        foreach (var sobra in servicios.Argumentos.NoSeEntendio)
        {
            servicios.Avisos.Dejar(Aviso.Advierte(sobra, string.Empty,
                "El programa arrancó igual con los valores por defecto. Ningún argumento raro impide abrir."));
        }

        DecirSiLosDatosSonInventados(servicios);
        _navegacion.SelectedItem = _navegacion.MenuItems[0];
    }

    /// <summary>
    /// Si el programa abrio con datos inventados, lo dice en el pie y no lo quita.
    /// </summary>
    /// <remarks>
    /// ⚠️ Un programa que ensena datos que no son y NO lo dice es peor que uno que no abre:
    /// cualquier cifra que se lea es de mentira y no hay forma de saberlo. Por eso va en el
    /// pie —que es donde el acuse ya vive— y ademas en la franja, que es donde Miguel mira
    /// los avisos.
    /// </remarks>
    private void DecirSiLosDatosSonInventados(Servicios servicios)
    {
        if (!servicios.SonDatosInventados) return;

        var cuantos = servicios.Argumentos.CasosInventados ?? 0;
        _acuse.DecirSiempre($"DATOS DE EJEMPLO ({cuantos} casos inventados): nada de lo que se ve es real.");

        // ⚠️ Si NADIE pidio «--falso», estos datos inventados son los de emergencia: la
        // base no se pudo abrir. Ese aviso ya lo dejo Servicios.Montar, con el motivo y
        // —cuando hubo migracion— donde quedo la copia previa. Escribir aqui encima lo
        // taparia (la franja ensena el ultimo que entro) y ademas culparia a un argumento
        // que no se uso. Medido el 2026-09-04 al forzar una migracion rota: en pantalla se
        // leia «Se pidio con el argumento --falso» y «( casos)», las dos cosas falsas.
        if (!servicios.Argumentos.SeUsanDatosInventados) return;

        // ⛔ Y se dice DÓNDE escribe. Desde el 2026-09-05, con «--falso» y sin decir carpeta el
        // programa usa una de usar y tirar y no la del dueño: era la única puerta por la que
        // una prueba alcanzaba sus datos sin querer, y le pasó a tres agentes el mismo día.
        var dondeEscribe = servicios.Argumentos.LaCarpetaEsDeUsarYTirar
            ? $" No se tocó su carpeta de datos: todo lo de esta sesión va a «{servicios.Argumentos.CarpetaDeDatos}», " +
              "que es de usar y tirar y se puede borrar."
            : string.Empty;

        servicios.Avisos.Dejar(Aviso.Advierte(
            $"El programa abrió con DATOS INVENTADOS ({cuantos} casos), no con los suyos.",
            string.Empty,
            "Se pidió con el argumento «--falso», que existe para medir una pantalla con miles de " +
            "documentos sin tenerlos. Nada de lo que se vea son datos reales y nada de lo que se " +
            "haga aquí se guardará en la base. Para abrir la base de verdad, arranque sin ese argumento."
            + dondeEscribe));
    }

    /// <summary>El acuse del pie, para que las pantallas puedan decir «guardado».</summary>
    public Acuse AcuseDelPie => _acuse;

    /// <summary>Navega a la pantalla de la entrada elegida y anota cuanto tardo.</summary>
    private void AlElegirUnaEntrada(NavigationView quien, NavigationViewSelectionChangedEventArgs cuando)
    {
        if (cuando.SelectedItem is not NavigationViewItem entrada) return;
        var nombre = entrada.Tag as string ?? "Inicio";

        var cronometro = Stopwatch.StartNew();
        _marco.Navigate(PantallaDe(nombre), _servicios, new SuppressNavigationTransitionInfo());
        cronometro.Stop();

        _servicios.Registro.AnotarNavegacion(nombre, cronometro.Elapsed.TotalMilliseconds);
    }

    /// <summary>Traduce el nombre de la entrada a la clase de su pantalla.</summary>
    private static Type PantallaDe(string nombre) => nombre switch
    {
        "Importar" => typeof(Importar.PaginaDeImportar),
        "Correccion" => typeof(PaginaDeCorreccion),
        "Completar" => typeof(Completar.PaginaDeCompletar),
        "Revisar" => typeof(PaginaDeRevisar),
        "Asignar" => typeof(PaginaDeAsignar),
        "Paquetes" => typeof(PaginaDePaquetes),
        "Reportes" => typeof(PaginaDeReportes),
        _ => typeof(PaginaDeInicio),
    };
}
