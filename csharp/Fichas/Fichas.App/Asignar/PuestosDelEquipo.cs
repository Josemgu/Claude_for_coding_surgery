using Fichas.App.Cascara;
using Fichas.App.Correccion;
using Fichas.Contratos.Consultas;
using Fichas.Contratos.Modelos;
using Fichas.Contratos.Puertos;

namespace Fichas.App.Asignar;

/// <summary>
/// Quien es cada uno del equipo y en que peldano esta. Sin ventana.
/// </summary>
/// <remarks>
/// <para>⛔ <b>El hueco que cierra, medido el 2026-09-05.</b> El alta creaba siempre con el
/// rol por defecto —<c>new Companero { Nombre = nombre }</c>— y no habia selector de rol en
/// ninguna pantalla. La migracion 18 dejo <c>companeros.rol</c> con sus tres valores y
/// <c>companeros.categoria</c> con el peldano, y <see cref="ElAdministrador"/> ya sabia
/// leerlos: el dueno veia la linea «para completar sin verificar, dese de alta usted como
/// administrador» y <b>no tenia donde hacerlo</b>. El atajo existia y era inalcanzable.</para>
///
/// <para><b>Los dos cuidados del dueno, que no cambian.</b> Nadie se crea ni se asciende
/// solo: el puesto solo se pone desde aqui, con un gesto suyo, y ninguna importacion ni
/// ninguna hoja devuelta lo toca. Y nunca se pierde el rastro de quien hizo que: cambiar el
/// puesto reescribe la fila del companero y <b>nada mas</b>; las asignaciones, las firmas de
/// <c>procedencia_campo</c> y el <c>estado_marcado_por</c> de los casos viven en otras tablas
/// y no se rozan. Hay una prueba que lo mide con el conteo antes y despues.</para>
///
/// <para>Va sin ventana por lo mismo que <see cref="ListaParaAsignar"/>: la regla se prueba
/// con un comando y <see cref="PanelDelEquipo"/> solo coloca controles.</para>
/// </remarks>
public sealed class PuestosDelEquipo
{
    /// <summary>El equipo; el alta y el cambio de puesto escriben por su <c>Guardar</c>.</summary>
    private readonly ICompaneros _companeros;
    /// <summary>La franja de la cáscara, donde se deja el aviso de cada escritura y de cada rechazo.</summary>
    private readonly BuzonDeAvisos _avisos;

    /// <summary>Ata los puestos al repositorio de companeros y al buzon de la franja.</summary>
    /// <param name="companeros">El equipo.</param>
    /// <param name="avisos">El buzón de la franja.</param>
    public PuestosDelEquipo(ICompaneros companeros, BuzonDeAvisos avisos)
    {
        _companeros = companeros;
        _avisos = avisos;
    }

    /// <summary>
    /// El primer peldano de la escalera. No hay tope arriba: los pone el dueno.
    /// </summary>
    /// <remarks>
    /// Sus palabras, 2026-09-05: «los agentes categoria 1 no pudieron comunicarse con los
    /// lideres, debo pasarlo a los agentes de categoria 2… Asi puedes agregarle a los gerentes
    /// categoria y a los agentes categorias».
    /// </remarks>
    public const int CategoriaMinima = 1;

    /// <summary>Los tres roles del esquema, en el orden de la escalera.</summary>
    public static IReadOnlyList<RolDeCompanero> LosTresRoles { get; } =
    [
        RolDeCompanero.Companero,
        RolDeCompanero.Gerente,
        RolDeCompanero.Administrador,
    ];

    /// <summary>Como se lee cada rol en el desplegable.</summary>
    /// <remarks>
    /// La base guarda claves en minuscula sin tilde —<c>companero</c>, <c>gerente</c>,
    /// <c>administrador</c>— y aqui estan las palabras que se leen. Cambiar la redaccion no
    /// obliga a migrar datos, que es para lo que se separan.
    /// </remarks>
    /// <param name="rol">El rol tal como está en la base.</param>
    public static string DecirElRol(RolDeCompanero rol) => rol switch
    {
        RolDeCompanero.Gerente => "Gerente",
        RolDeCompanero.Administrador => "Administrador",
        _ => "Compañero",
    };

    /// <summary>Da de alta a alguien con su nombre, su rol y su peldano, en un gesto.</summary>
    /// <param name="nombre">Su nombre; sin el no se guarda, porque una firma sin nombre no dice quien firmo.</param>
    /// <param name="rol">Que es. El dueno lo elige; nunca se deduce.</param>
    /// <param name="categoria">En que peldano entra, desde <see cref="CategoriaMinima"/>.</param>
    public ResultadoDeEscritura DarDeAlta(string nombre, RolDeCompanero rol, int categoria)
    {
        if (RechazarPeldano(categoria) is ResultadoDeEscritura malo) return malo;

        var resultado = _companeros.Guardar(new Companero
        {
            Nombre = (nombre ?? string.Empty).Trim(),
            Rol = rol,
            Categoria = categoria,
        });
        _avisos.Dejar(resultado.Avisos);
        return resultado;
    }

    /// <summary>
    /// Cambia el puesto de alguien que ya esta, sin tocar nada mas de el.
    /// </summary>
    /// <remarks>
    /// Se relee el companero y se reescribe con <c>with</c>, asi que <c>Activo</c>,
    /// <c>DesactivadoEn</c>, <c>CreadoEn</c> y el nombre salen tal cual entraron. Poner un
    /// puesto <b>no</b> es una puerta trasera para reactivar a quien esta de baja.
    /// </remarks>
    /// <param name="companeroId">A quién se le cambia; si ya no está, se dice y no se escribe.</param>
    /// <param name="rol">El rol nuevo.</param>
    /// <param name="categoria">El peldaño nuevo, desde <see cref="CategoriaMinima"/>.</param>
    public ResultadoDeEscritura Cambiar(long companeroId, RolDeCompanero rol, int categoria)
    {
        if (RechazarPeldano(categoria) is ResultadoDeEscritura malo) return malo;

        if (_companeros.Obtener(companeroId) is not Companero companero)
        {
            return Decir(Aviso.Problema(
                "No se cambió el puesto: ese compañero ya no está en el equipo.",
                nameof(Companero.Id),
                $"Ninguna fila de compañeros con el número interno {companeroId}. "
                + "Vuelve a abrir el panel del equipo para verlo como está ahora."));
        }

        var resultado = _companeros.Guardar(companero with { Rol = rol, Categoria = categoria });
        _avisos.Dejar(resultado.Avisos);
        return resultado;
    }

    /// <summary>
    /// Edita a alguien que ya esta: nombre, rol y peldano en un gesto, sin tocar nada mas.
    /// </summary>
    /// <remarks>
    /// <para>Del dueño, 2026-09-16: <i>«En Asignar debe dar la opción de eliminar, editar o
    /// desactivar agentes»</i>. Desactivar y quitar ya existían; editar no.</para>
    ///
    /// <para><b>Editar el nombre no toca lo que ya firmó.</b> Medido antes de escribir esto:
    /// todas las firmas de la base guardan el <b>id</b> del compañero, nunca su nombre
    /// —<c>verificado_por</c>, <c>estado_marcado_por</c>, <c>estado_del_companero_por</c>,
    /// <c>propuesto_por</c>, <c>contactado_por</c>, <c>pasos_por</c>, todas <c>INTEGER</c>—.
    /// Así que las filas firmadas quedan intactas; lo que cambia es cómo se LEE esa firma
    /// desde ahora: con el nombre nuevo, porque el nombre se resuelve por id al pintar. Hay
    /// una prueba que cuenta firmas y asignaciones antes y después.</para>
    ///
    /// <para>Igual que <see cref="Cambiar"/>, se relee y se reescribe con <c>with</c>:
    /// <c>Activo</c>, <c>DesactivadoEn</c> y <c>CreadoEn</c> salen tal cual. Editar no es una
    /// puerta trasera para reactivar. Y si no cambia nada, no se escribe ni se avisa.</para>
    /// </remarks>
    /// <param name="companeroId">A quién se edita; si ya no está, se dice y no se escribe.</param>
    /// <param name="nombre">El nombre nuevo; en blanco no se escribe, porque una firma sin nombre no dice quién firmó.</param>
    /// <param name="rol">El rol nuevo.</param>
    /// <param name="categoria">El peldaño nuevo, desde <see cref="CategoriaMinima"/>.</param>
    public ResultadoDeEscritura Editar(long companeroId, string nombre, RolDeCompanero rol, int categoria)
    {
        if (RechazarPeldano(categoria) is ResultadoDeEscritura malo) return malo;

        var nombreLimpio = (nombre ?? string.Empty).Trim();
        if (nombreLimpio.Length == 0)
        {
            return Decir(Aviso.Problema(
                "No se guardó: el compañero necesita un nombre.",
                nameof(Companero.Nombre),
                "El nombre es lo único que identifica a quien firma, y una firma sin nombre no dice quién firmó."));
        }

        if (_companeros.Obtener(companeroId) is not Companero companero)
        {
            return Decir(Aviso.Problema(
                "No se editó: ese compañero ya no está en el equipo.",
                nameof(Companero.Id),
                $"Ninguna fila de compañeros con el número interno {companeroId}. "
                + "Vuelve a abrir el panel del equipo para verlo como está ahora."));
        }

        var editado = companero with { Nombre = nombreLimpio, Rol = rol, Categoria = categoria };
        if (editado == companero) return ResultadoDeEscritura.NoSeEscribio();

        var resultado = _companeros.Guardar(editado);
        _avisos.Dejar(resultado.Avisos);
        return resultado;
    }

    /// <summary>
    /// Lo que hay que decir sobre el atajo del administrador, o nulo si no hay nada.
    /// </summary>
    /// <remarks>
    /// Solo habla cuando hay <b>dos o mas</b> administradores activos, que es cuando el boton
    /// de Corrección desaparece sin que nada en pantalla lo relacione con lo que se acaba de
    /// hacer aqui. Con cero no dice nada: un equipo sin administrador es lo normal, y el aviso
    /// de «dese de alta usted» es de la pantalla que ofrece el atajo, no de esta.
    ///
    /// La frase sale de <see cref="ElAdministrador.PorQueNoSePuede"/> y no se copia: dos
    /// redacciones de la misma regla acaban contestando cosas distintas.
    /// </remarks>
    public Aviso? LoQueLePasaAlAtajo()
    {
        var activos = _companeros.Activos();
        var cuantos = activos.Count(c => c.Activo && c.Rol == RolDeCompanero.Administrador);
        return cuantos >= 2 ? ElAdministrador.PorQueNoSePuede(activos) : null;
    }

    /// <summary>El peldano imposible, dicho con palabras del programa y no con el del motor.</summary>
    /// <remarks>
    /// En la base de verdad lo rechaza el <c>CHECK</c> de la columna; el doble no lo tiene, asi
    /// que sin esta guarda un cero pasaria en las pruebas y reventaria en la maquina del dueno.
    /// No se corrige en silencio: un valor asi solo puede venir de un defecto del programa.
    /// </remarks>
    /// <param name="categoria">El peldaño que se pidió.</param>
    /// <returns>El «no se escribió» con su aviso, o nulo si el peldaño vale.</returns>
    private ResultadoDeEscritura? RechazarPeldano(int categoria)
        => categoria >= CategoriaMinima
            ? null
            : Decir(Aviso.Problema(
                $"No se guardó: la categoría más baja es {CategoriaMinima}.",
                nameof(Companero.Categoria),
                $"Se pidió la categoría {categoria}, y la escalera empieza en {CategoriaMinima}. "
                + "Los peldaños de arriba no tienen tope: pon el número que quieras a partir de ahí."));

    /// <summary>Deja el aviso en la franja y devuelve el «no se escribió» que lo lleva dentro.</summary>
    /// <param name="aviso">Lo que hay que decir.</param>
    private ResultadoDeEscritura Decir(Aviso aviso)
    {
        var resultado = ResultadoDeEscritura.NoSeEscribio(aviso);
        _avisos.Dejar(resultado.Avisos);
        return resultado;
    }
}
