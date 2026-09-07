using Fichas.App.Cascara;

namespace Fichas.App.Grupo;

/// <summary>
/// Lo que se le pasa a la pantalla del grupo al navegar: los servicios y QUE dia.
/// </summary>
/// <remarks>
/// ⚠️ <b>Existe porque la navegacion de la cascara solo sabe pasar los servicios.</b>
/// <c>Cascara/VentanaPrincipal.xaml.cs:91</c> hace <c>Navigate(PantallaDe(nombre),
/// _servicios, …)</c> y nada mas, y ese archivo esta CONGELADO y es de otro terreno. El
/// criterio C12-5 pide llevar «que grupo», asi que la pantalla del grupo se navega desde
/// el marco de la pagina que la abre —Inicio o la ventana de incompletos— con este
/// paquete, y recoge el parametro ella misma en vez de heredarlo de
/// <see cref="PaginaDeFichas"/>.
/// <para>Consecuencia que hay que saber: la entrada del menu de la izquierda NO se marca
/// al llegar aqui, y pulsar «Inicio» estando ya seleccionado no dispara nada. Por eso la
/// pantalla del grupo trae su propio boton de volver, bien visible.</para>
/// </remarks>
/// <param name="Servicios">Los servicios de la aplicacion.</param>
/// <param name="Fecha">El dia cuyo grupo se abre.</param>
public sealed record LlegadaAlGrupo(Servicios Servicios, DateOnly Fecha);
