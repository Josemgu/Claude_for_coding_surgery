using Fichas.App.Asignar;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Paquetes;

/// <summary>
/// Lo que se le quita a un companero cuando su paquete vuelve: los que devolvio COMPLETOS.
/// </summary>
/// <remarks>
/// <para><b>Lo pidio el dueno el 2026-09-07, literal:</b> <i>«No quiero que despues de subir el
/// paquete de los agentes al sistema y le asigne mas casos, se queden los casos que el ya ha
/// completado. Debe quedar limpio. Cuando el sube un paquete que completo, debe quitarle que ese
/// caso esta asignado a el. Lo unico que no se le quitan son los que no estan completos aun,
/// pero debe quedar la informacion que el coloco en el paquete.»</i></para>
///
/// <para>⚠️ <b>Esto cambia lo que se decidio unas horas antes el mismo dia.</b> Entonces se
/// eligio filtrar el paquete y NO cerrar la asignacion, con un motivo que resulto ser falso:
/// que cerrarla borraba del informe del agente el trabajo que acababa de hacer. No lo borra —
/// <c>ReportesEnPdf.DocumentoDeCompanero</c> pide sus asignaciones con <c>SoloActivas: false</c>
/// y arma sus casos con todas, vivas y retiradas—, y esta medido en
/// <c>PruebasDeQueElPaqueteCompletoLimpiaLaAsignacion</c>.</para>
///
/// <para>⛔ <b>Aqui NO se decide que documento esta completo.</b> Eso lo escribio la vuelta con
/// lo que dijo la hoja del companero, y esta clase solo LEE lo que ya quedo escrito en la base.
/// Es lo que hace que la regla permanente 5 siga intacta: quitar una asignacion no marca nada
/// ni firma nada.</para>
/// </remarks>
public sealed class LimpiezaAlVolver
{
    private readonly IAsignaciones _asignaciones;
    private readonly ICasos _casos;
    private readonly OperacionDeAsignar _reparto;

    /// <summary>Se ata a los dos puertos que hacen falta para leer, y a la puerta de retirar.</summary>
    /// <remarks>
    /// Retira por <see cref="OperacionDeAsignar"/> y no llamando al puerto: quitarle un caso a
    /// alguien al volver el paquete tiene que dejar la base igual que quitarselo a mano desde
    /// Asignar, y dos caminos distintos se separan solos.
    /// </remarks>
    public LimpiezaAlVolver(IAsignaciones asignaciones, ICasos casos, OperacionDeAsignar reparto)
    {
        _asignaciones = asignaciones;
        _casos = casos;
        _reparto = reparto;
    }

    /// <summary>
    /// Le quita a ese companero las asignaciones vivas de todo lo que el devolvio completo.
    /// </summary>
    /// <remarks>
    /// <para><b>Que casos son se lee de la base y no de la hoja que acaba de entrar</b>, con
    /// <see cref="CargaDeUnCompanero"/>, que es quien ya sabia distinguir «lo que EL devolvio
    /// completo» de «lo que completo otro». Leerlo de la hoja dejaria fuera lo que devolvio
    /// completo en una vuelta anterior, y el dueno pidio que quede limpio, no que quede limpio
    /// de hoy.</para>
    ///
    /// <para>Un companero sin nada completo devuelve un resumen de cero y no toca la base: es
    /// el caso normal y no tiene por que escribir nada.</para>
    /// </remarks>
    public ResumenDeRetirada QuitarleLoQueDevolvioCompleto(Companero companero)
    {
        ArgumentNullException.ThrowIfNull(companero);

        var carga = CargaDeUnCompanero.Leer(_asignaciones, _casos, companero.Id);
        return carga.YaLosDevolvioCompletos.Count == 0
            ? new ResumenDeRetirada(0, 0, companero.Nombre)
            : _reparto.RetirarleEstosCasos(companero.Id, carga.YaLosDevolvioCompletos, companero.Nombre);
    }
}
