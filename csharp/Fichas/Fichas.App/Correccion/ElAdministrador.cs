using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Quien es el administrador, y que se dice cuando no se sabe.
/// </summary>
/// <remarks>
/// El dueno describio su papel asi: <i>«Yo solo estoy para verificar que todo este correcto
/// con el sistema y los PDF. Yo puedo completarlos tambien, los paquetes, desde el sistema
/// sin pasar la verificacion, y cuando pase eso debe decir "el administrador lo hizo"»</i>.
/// <para>
/// ⛔ <b>El defecto que esta clase existe para no repetir, y esta MEDIDO.</b>
/// <c>AccionesDeRevisar.QuienFirmaAMano</c> busca el companero activo llamado «Miguel» y,
/// si no lo encuentra, coge <b>el primer activo</b>. La tabla <c>companeros</c> de la base
/// viva del dueno tiene UNA fila —<c>[(1, 'Sandy', 1)]</c>, ADR-0005 §6.3—, asi que con esa
/// regla el atajo del administrador quedaria firmado por Sandy, y el reporte diria que Sandy
/// completo algo que no toco. <b>Nunca se firma a nombre de quien no fue.</b>
/// </para>
/// <para>
/// La regla, que no adivina nada: el administrador es el <b>unico</b> companero activo con
/// <see cref="RolDeCompanero.Administrador"/>, un dato que la migracion 18 puso en la base.
/// Si no hay exactamente uno, no hay atajo y se dice por que en una linea.
/// </para>
/// <para>
/// Va sin ventana a proposito: la decision se mide con un comando, y la frase que ve el
/// dueno se lee en una prueba.
/// </para>
/// </remarks>
public static class ElAdministrador
{
    /// <summary>
    /// Lo que se guarda en <c>casos.estado_marcado_origen</c> cuando el usa el atajo.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Son las palabras del dueno, literales, y no se retocan.</b> Es texto que acaba
    /// en su base y que leen los reportes: cambiarlo deja sin reconocer todo lo ya marcado.
    /// <para>
    /// Es el TERCER valor de una columna que ya existe desde la migracion 14 y que ya
    /// guardaba dos —<c>«a mano en la pantalla Revisar»</c> y la ruta del Excel que trajo la
    /// marca—. Sin columna nueva, que es lo que decidio ADR-0005 §6.1.
    /// </para>
    /// </remarks>
    public const string Origen = "el administrador lo hizo";

    /// <summary>
    /// El administrador, o nulo si no hay exactamente uno entre los activos.
    /// </summary>
    /// <remarks>
    /// Devuelve nulo con cero y tambien con dos: elegir uno de dos seria adivinar, y de eso
    /// va toda esta clase. Quien llame no tiene que buscar mas: si esto es nulo, no hay
    /// atajo, y <see cref="PorQueNoSePuede"/> dice por que.
    /// </remarks>
    /// <param name="activos">Los companeros activos; los desactivados no entran aqui.</param>
    public static Companero? De(IReadOnlyList<Companero> activos)
    {
        ArgumentNullException.ThrowIfNull(activos);

        Companero? unico = null;
        foreach (var companero in activos)
        {
            if (companero.Rol != RolDeCompanero.Administrador || !companero.Activo) continue;
            if (unico is not null) return null;
            unico = companero;
        }

        return unico;
    }

    /// <summary>
    /// Por que no hay atajo, en una linea, sin nombrar a nadie que no sea administrador.
    /// </summary>
    /// <remarks>
    /// Las dos formas de no poder son distintas y se dicen distinto: con cero, lo que falta
    /// es que el se de de alta —el dueno ya lo dijo el 2026-09-04: «el se anade a si mismo y
    /// firma con su nombre»—; con dos o mas, lo que falta es saber cual es el.
    /// <para>
    /// ⛔ La linea NO nombra a Sandy ni a ningun otro companero. Ofrecer un nombre aqui es
    /// justo el paso previo a firmar con el.
    /// </para>
    /// </remarks>
    public static Aviso PorQueNoSePuede(IReadOnlyList<Companero> activos)
    {
        ArgumentNullException.ThrowIfNull(activos);

        var cuantos = activos.Count(companero => companero.Activo && companero.Rol == RolDeCompanero.Administrador);
        return cuantos == 0
            ? Aviso.Problema(
                "para completar sin verificar, dese de alta usted como administrador",
                string.Empty,
                "Este atajo firma con el nombre del administrador, y ahora mismo no hay ninguno en la "
                + "lista de compañeros. Dese de alta a usted mismo con el rol de administrador y vuelva "
                + "a pulsar. El programa NO firma a nombre de otra persona.")
            : Aviso.Problema(
                $"hay {cuantos} administradores activos: el programa no adivina cuál de ellos es usted",
                string.Empty,
                "Con más de un administrador en la lista, marcar el documento obligaría a elegir un "
                + "nombre, y el que se eligiera podría no ser el suyo. Deje activo solo al que firma y "
                + "vuelva a pulsar.");
    }
}
