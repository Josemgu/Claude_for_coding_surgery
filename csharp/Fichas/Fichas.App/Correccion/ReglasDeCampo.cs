using System.Globalization;
using System.Text.RegularExpressions;
using Fichas.Lectura;

namespace Fichas.App.Correccion;

/// <summary>
/// Las reglas de formato de los campos que se corrigen. Portadas de <c>datos/validacion.py</c>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Devuelven el motivo, no lanzan.</b> Es el requisito 9 del dueno («avisar, nunca
/// impedir») convertido en firma: un valor raro no puede tumbar nada porque no hay nada
/// que tumbar. Quien llama decide que hace con el motivo; aqui solo se dice cual es.
/// <para>
/// ⚠️ <b>Esto vive aqui porque <c>Fichas.Contratos</c> esta congelado.</b> Cuando exista
/// <c>Fichas.Datos</c> (fase C2) las mismas cuatro reglas estaran tambien en la capa de
/// datos, y entonces hay que decidir cual manda. Va dicho en la entrega, no resuelto a
/// mitad de fase.
/// </para>
/// <para>
/// Regla permanente 1: aqui no se corrige nada. Una cedula en minuscula queda en
/// minuscula; los espacios de los bordes se recortan y nada mas.
/// </para>
/// </remarks>
public static partial class ReglasDeCampo
{
    /// <summary>Tope del nombre de unidad; el mas largo de los formularios mide 17.</summary>
    public const int LargoMaximoDelNombreDeUnidad = 120;

    /// <summary>Los dos ejemplos que trae el papel; van los dos en el mensaje a proposito.</summary>
    private const string EjemploDeMrnConDigitos = "055-1111-3853";

    /// <summary>El segundo ejemplo: el ultimo caracter puede ser letra.</summary>
    private const string EjemploDeMrnConLetra = "066-2222-133A";

    /// <summary>Cuatro letras mayusculas y cuatro digitos, como «CASP2609».</summary>
    [GeneratedRegex("^[A-Z]{4}[0-9]{4}$")]
    private static partial Regex PatronDelNumeroDeCaso();

    /// <summary>3 digitos, 4 digitos y 4 caracteres cuyo ultimo puede ser letra.</summary>
    [GeneratedRegex("^[0-9]{3}-[0-9]{4}-[0-9]{3}[0-9A-Za-z]$")]
    private static partial Regex PatronDelMrn();

    /// <summary>6 o 7 digitos: manda el papel, no la suposicion de que siempre son 6.</summary>
    [GeneratedRegex("^[0-9]{6,7}$")]
    private static partial Regex PatronDeLaUnidad();

    /// <summary>La forma AAAA-MM-DD; que la fecha exista se comprueba aparte.</summary>
    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$")]
    private static partial Regex PatronDeLaFecha();

    /// <summary>Por que no vale la cedula de miembro, o nulo si vale. El vacio vale.</summary>
    public static string? MotivoDelMrn(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null || PatronDelMrn().IsMatch(limpio)) return null;
        return $"La cédula va {EjemploDeMrnConDigitos} o {EjemploDeMrnConLetra}: el último puede ser letra.";
    }

    /// <summary>Por que no vale el numero de unidad, o nulo si vale. El vacio vale.</summary>
    public static string? MotivoDeLaUnidadNumero(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null || PatronDeLaUnidad().IsMatch(limpio)) return null;
        return "El número de unidad son 6 o 7 dígitos, como 123456 o 7000011.";
    }

    /// <summary>Por que no vale la fecha de viaje, o nulo si vale. El vacio vale.</summary>
    public static string? MotivoDeLaFechaDeViaje(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null) return null;
        if (!PatronDeLaFecha().IsMatch(limpio))
            return "La fecha de viaje va en formato AAAA-MM-DD, como 2026-09-08.";
        // La forma no basta: «2026-02-31» la cumple y no existe.
        return DateTime.TryParseExact(limpio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : $"«{limpio}» tiene la forma correcta pero no es una fecha que exista.";
    }

    /// <summary>Por que no vale el numero de caso, o nulo si vale. El vacio vale: puede no leerse.</summary>
    public static string? MotivoDelNumeroDeCaso(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null || PatronDelNumeroDeCaso().IsMatch(limpio)) return null;
        return "El número de caso son 4 letras mayúsculas y 4 dígitos, como CASP2609.";
    }

    /// <summary>Por que no vale el nombre de unidad, o nulo si vale.</summary>
    public static string? MotivoDelNombreDeUnidad(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null || limpio.Length <= LargoMaximoDelNombreDeUnidad) return null;
        return $"El nombre de unidad tiene {limpio.Length} caracteres y el máximo son {LargoMaximoDelNombreDeUnidad}.";
    }

    /// <summary>
    /// Lleva cada campo a su regla. Un campo sin regla —el nombre— siempre pasa.
    /// </summary>
    /// <remarks>
    /// Que el nombre no tenga regla no es un olvido: no hay forma de decidir si
    /// «ANONIMO, J0SE M1GUEL» esta bien o mal, asi que no se pinta nunca de rojo y aun asi
    /// hay que mirarlo (<c>interfaz/campo.py</c>).
    /// <para>
    /// ⚠️ El <c>campo</c> que entra aqui es el nombre de la COLUMNA, en espanol y con guion
    /// bajo. Hasta el 2026-09-04 eran los de C# («Mrn», «UnidadNumero»), y como no eran los
    /// que la importacion escribia, un valor mal leido por el OCR no se pintaba de rojo.
    /// </para>
    /// </remarks>
    public static string? MotivoDe(string campo, string? valor) => campo switch
    {
        Extraccion.CampoCedula => MotivoDelMrn(valor),
        Extraccion.CampoUnidadNumero => MotivoDeLaUnidadNumero(valor),
        Extraccion.CampoFechaDeViaje => MotivoDeLaFechaDeViaje(valor),
        Extraccion.CampoNumeroDeCaso => MotivoDelNumeroDeCaso(valor),
        Extraccion.CampoUnidadNombre => MotivoDelNombreDeUnidad(valor),
        _ => null,
    };

    /// <summary>
    /// El aviso del mes cruzado: el dato SI se guarda y alguien tiene que mirarlo.
    /// </summary>
    /// <remarks>
    /// Decision del dueno (DECISIONES.md 2026-09-02, P-2): como pared haria imposible
    /// guardar un viaje reprogramado a otro mes. Es la regla permanente 5 —el sistema
    /// propone, Miguel confirma— aplicada a una fecha.
    /// </remarks>
    public static string? AvisoDelMesCruzado(string? numeroCaso, string? fechaViaje)
    {
        var caso = Limpiar(numeroCaso);
        var fecha = Limpiar(fechaViaje);
        if (caso is null || fecha is null) return null;
        if (!PatronDelNumeroDeCaso().IsMatch(caso) || !PatronDeLaFecha().IsMatch(fecha)) return null;

        var esperado = caso[4..8];
        var encontrado = fecha[2..4] + fecha[5..7];
        if (esperado == encontrado) return null;
        return $"La fecha {fecha} cae en el periodo {encontrado} y el caso {caso} dice {esperado}.";
    }

    /// <summary>Recorta los bordes; lo que queda vacio es nulo, no cadena vacia.</summary>
    /// <remarks>
    /// En esta base <c>NULL</c> significa «no hay dato» y <c>''</c> no significa nada. Dos
    /// formas de decir lo mismo son dos formas de que una consulta se olvide de una.
    /// </remarks>
    public static string? Limpiar(string? valor)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }
}
