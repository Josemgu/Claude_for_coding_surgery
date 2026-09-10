using Fichas.Contratos.Lectura;

namespace Fichas.Lectura;

/// <summary>
/// Las personas de un formulario rellenable rellenado en el ordenador: salen de sus campos.
/// </summary>
/// <remarks>
/// <para>Es la segunda clase de documento que recibe el dueño, medida el 2026-09-10 sobre su
/// PDF: una hoja, 93 <c>/Widget</c>, y el nombre y la cedula tecleados en <c>Full NamesRow1</c>
/// y <c>Membership Record NumberRow1</c>. Los siete escaneos no traen ni un campo de estos, y
/// por eso este camino no los toca: <see cref="Personas.Extraer"/> decide cual de los dos
/// seguir y sigue siendo quien fija donde empieza y acaba el bloque.</para>
///
/// <para>⛔ Regla permanente 1: lo tecleado es texto exacto y se le pide la misma forma que a
/// lo leido. No se corrige, no se completa, no se adivina de quien es una cedula: la fila la
/// decide el rectangulo del campo.</para>
/// </remarks>
public static class PersonasDelFormulario
{
    /// <summary>
    /// Las filas del bloque de personas tal como las dibujan los campos del formulario.
    /// </summary>
    /// <remarks>
    /// <para>Es la segunda clase de documento, medida el 2026-09-10: un formulario rellenable
    /// rellenado en el ordenador, donde cada fila del bloque tiene un campo de texto para el
    /// nombre y otro para la cedula, y lo tecleado no esta en la imagen sino dentro del
    /// campo. Las filas se arman por el RECTANGULO de cada campo, nunca por el orden en que
    /// el PDF los guarde: dos campos son de la misma fila si comparten mas de la mitad del
    /// alto. El orden de los campos en el archivo no dice nada del papel.</para>
    /// <para>Entran los campos vacios tambien: son los que dicen que fila del formulario es
    /// cada una, y por eso <c>FilaFormulario</c> aqui es la fila FISICA del papel, base 1.</para>
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<AnotacionDelPdf>> FilasDeCampos(
        IReadOnlyList<AnotacionDelPdf> anotaciones, double arriba, double abajo)
    {
        var filas = new List<List<AnotacionDelPdf>>();
        var campos = anotaciones
            .Where(a => a.Subtipo == Anotaciones.SubtipoDeCampoDeTexto && a.Banda.Y0 >= arriba && a.Banda.Y0 < abajo)
            .OrderBy(a => a.Banda.Y0)
            .ThenBy(a => a.Banda.X0);

        foreach (var campo in campos)
        {
            var ultima = filas.Count == 0 ? null : filas[^1];
            if (ultima is not null && Geometria.FraccionDeTraslapeVertical(campo.Banda, ultima[0].Banda) > 0.5)
            {
                ultima.Add(campo);
            }
            else
            {
                filas.Add([campo]);
            }
        }
        return filas;
    }

    /// <summary>El campo de esa fila que cae en la columna pedida, o nulo.</summary>
    /// <remarks>
    /// El nombre esta a la izquierda del limite entre columnas y la cedula a su derecha, sin
    /// pasar del borde del rotulo de las cedulas: mas alla estan las casillas de ordenanza,
    /// que no son de texto y no llegan aqui, pero el corte se conserva por si un formulario
    /// distinto pusiera un campo de texto en esa zona.
    /// </remarks>
    private static AnotacionDelPdf? CampoDeLaColumna(
        IReadOnlyList<AnotacionDelPdf> fila, double desde, double hasta)
        => fila.Where(campo => campo.Banda.X0 >= desde && campo.Banda.X0 < hasta)
               .OrderBy(campo => campo.Banda.X0)
               .FirstOrDefault();

    /// <summary>
    /// Un campo tecleado pasa por la precedencia como cualquier correccion escrita.
    /// </summary>
    /// <remarks>
    /// Sin lineas de OCR: lo que hay en la fila es el campo, y su texto es exacto. Lo unico
    /// que se le pide es la forma —<see cref="Normalizacion.NormalizarCedula"/> para la cedula;
    /// para el nombre, solo juntar los espacios— y si no la tiene va a revision con lo
    /// tecleado a la vista. El tachon se pregunta sobre la columna, como en los escaneos.
    /// </remarks>
    private static CampoExtraido CampoTecleadoResuelto(
        AnotacionDelPdf? campo,
        IReadOnlyList<AnotacionDelPdf> anotaciones,
        BandaDeLaPagina columna,
        Func<string?, string?> normalizar)
        => Campos.ResolverCampo(
            [],
            campo is null ? [] : [campo],
            Bandas.HayTachonEnLaBanda(anotaciones, columna),
            normalizar);

    /// <summary>Un nombre tecleado se guarda tal cual, sin mas que recortarle los bordes.</summary>
    /// <remarks>
    /// Es la misma regla que <c>Extraccion.NormalizadorDe</c> aplica a un nombre que teclea
    /// Miguel, y no <see cref="Normalizacion.NombreSinLaCedula"/>, que existe para una caja
    /// del OCR que se trago dos columnas: un campo del formulario es una sola columna.
    /// </remarks>
    private static string? NombreTecleado(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>
    /// Las personas de un formulario rellenado a maquina, una por fila con algo tecleado.
    /// </summary>
    /// <remarks>
    /// Una fila del formulario sin nada tecleado ni en el nombre ni en la cedula no es una
    /// persona, y tampoco se cuenta como descartada: no se leyo nada que tirar. La banda de
    /// la persona es la fila entera del formulario, del campo del nombre al de la cedula.
    /// </remarks>
    public static IReadOnlyList<PersonaExtraida> Extraer(
        IReadOnlyList<IReadOnlyList<AnotacionDelPdf>> filas,
        IReadOnlyList<AnotacionDelPdf> anotaciones,
        BandaDeLaPagina anclaCedula,
        double limiteDeLosNombres)
    {
        var personas = new List<PersonaExtraida>();
        for (int indice = 0; indice < filas.Count; indice++)
        {
            var fila = filas[indice];
            var campoDelNombre = CampoDeLaColumna(fila, 0.0, limiteDeLosNombres);
            var campoDeLaCedula = CampoDeLaColumna(fila, limiteDeLosNombres, anclaCedula.X1);
            bool hayNombre = campoDelNombre is not null && Anotaciones.EsCampoTecleado(campoDelNombre);
            bool hayCedula = campoDeLaCedula is not null && Anotaciones.EsCampoTecleado(campoDeLaCedula);
            if (!hayNombre && !hayCedula) continue;

            var renglon = new BandaDeLaPagina(
                fila.Min(c => c.Banda.X0), fila.Min(c => c.Banda.Y0), fila.Max(c => c.Banda.X1), fila.Max(c => c.Banda.Y1));
            var nombre = CampoTecleadoResuelto(
                campoDelNombre, anotaciones, Personas.FranjaDeTrazos(renglon, renglon.X0, limiteDeLosNombres), NombreTecleado);
            var cedula = CampoTecleadoResuelto(
                campoDeLaCedula, anotaciones, Personas.FranjaDeTrazos(renglon, limiteDeLosNombres, anclaCedula.X1), Normalizacion.NormalizarCedula);

            personas.Add(new PersonaExtraida(
                FilaFormulario: indice + 1,
                Nombre: nombre,
                Cedula: cedula,
                Banda: new BandaDeLaPagina(
                    (campoDelNombre ?? campoDeLaCedula)!.Banda.X0, renglon.Y0,
                    (campoDeLaCedula ?? campoDelNombre)!.Banda.X1, renglon.Y1)));
        }
        return personas;
    }
}
