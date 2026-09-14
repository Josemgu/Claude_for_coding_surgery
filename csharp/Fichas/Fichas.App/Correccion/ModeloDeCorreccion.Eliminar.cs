using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>
/// Eliminar una persona del documento abierto: la pregunta que se arma y lo que pasa después
/// del «sí». Sin una sola línea de XAML.
/// </summary>
/// <remarks>
/// <para><b>El encargo del dueño, 2026-09-14:</b> <i>«Agrega un botón en la Corrección de
/// eliminar información también. No lo tengo y es importante tenerlo»</i>. Lo que aquí se decide
/// es a quién, con qué palabras se pregunta y qué queda; preguntar de verdad —el cuadro— es de
/// <c>OperacionDeEliminarUnaPersona</c>, y borrar el documento entero va por la misma puerta que
/// Revisar (<c>OperacionDeBorrar</c>), no por aquí.</para>
///
/// <para>⛔ <b>Aquí no se borra sin que la pantalla haya preguntado.</b> Este modelo no puede
/// saberlo, así que separa en dos lo que la pantalla tiene que hacer en orden:
/// <see cref="PlanearEliminarPersona"/> no escribe nada y devuelve la pregunta;
/// <see cref="EliminarPersona"/> es lo que se llama solo después de que él conteste que sí.</para>
///
/// <para>⛔ <b>Y no firma nada</b> (regla permanente 5): lo único que se escribe es el borrado, por
/// <see cref="Fichas.Contratos.Puertos.IPersonas.Borrar"/>, que se lleva la fila y su
/// procedencia y no toca ninguna otra.</para>
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>
    /// Arma la pregunta de eliminar a esa persona, sin escribir nada.
    /// </summary>
    /// <param name="personaId">A quién; una que no es de este documento devuelve nulo, y entonces no hay nada que preguntar.</param>
    /// <returns>La pregunta con el nombre delante y cuántas quedarían, o nulo si no hay documento abierto o la persona no es suya.</returns>
    public PreguntaDeEliminarPersona? PlanearEliminarPersona(long personaId)
    {
        if (_caso is null) return null;
        if (_personasDelCaso.FirstOrDefault(p => p.Id == personaId) is not Persona persona) return null;

        return new PreguntaDeEliminarPersona(
            personaId, TextoDeEliminar.ComoSeLlama(persona), _personasDelCaso.Count - 1);
    }

    /// <summary>
    /// Borra a esa persona de la base con su procedencia y vuelve a leer el documento. Solo se
    /// llama después de que él haya contestado que sí.
    /// </summary>
    /// <remarks>
    /// <para>Lo tecleado en los campos de la persona borrada se tira con ella —ya no hay dónde
    /// guardarlo—, y lo tecleado en los campos de las demás se conserva: eliminar a una no puede
    /// tirar lo que se estaba escribiendo al lado. Es el mismo cuidado que
    /// <see cref="VolverALeerLasPersonas"/> tiene al añadir.</para>
    ///
    /// <para>Si era la última, el resultado lo dice y el documento se queda sin nadie: es la
    /// pantalla la que entonces pregunta si borrar el documento entero, por la puerta de siempre.
    /// Aquí no se decide eso.</para>
    /// </remarks>
    /// <param name="personaId">A quién; una que no es de este documento no borra nada y lo dice.</param>
    /// <returns>Si salió, cuántas quedan, si era la última y la línea del pie; si no, el motivo. Nunca lanza.</returns>
    public ResultadoDeEliminarPersona EliminarPersona(long personaId)
    {
        if (_caso is null) return NoSeElimino(TextoDeEliminar.SinDocumentoAbierto);
        if (_personasDelCaso.FirstOrDefault(p => p.Id == personaId) is not Persona persona)
        {
            return NoSeElimino(TextoDeEliminar.NoEsDeEsteDocumento);
        }

        var comoSeLlama = TextoDeEliminar.ComoSeLlama(persona);
        var borrado = _personas.Borrar(personaId);
        if (!borrado.SeBorro)
        {
            return new ResultadoDeEliminarPersona(
                false, false, _personasDelCaso.Count, borrado.RutaDeLaCopia, borrado.Avisos,
                $"No se eliminó a {comoSeLlama}; mire el aviso de al lado.");
        }

        OlvidarLoTecleadoDe(personaId);
        VolverALeerLasPersonas();

        var quedan = _personasDelCaso.Count;
        return new ResultadoDeEliminarPersona(
            true, quedan == 0, quedan, borrado.RutaDeLaCopia, borrado.Avisos,
            TextoDeEliminar.AlEliminarUnaPersona(comoSeLlama, quedan));
    }

    /// <summary>
    /// Cierra el documento: deja el modelo como antes de abrir ninguno, sin avisos y sin campos.
    /// </summary>
    /// <remarks>
    /// Existe para cuando se elimina el documento abierto y no queda ningún otro que abrir:
    /// llamar a <see cref="Cargar"/> con el id borrado dejaría el aviso de «ese caso ya no está en
    /// la base», que aquí sería mentira —está donde él lo mandó—. No escribe nada.
    /// </remarks>
    public void Cerrar()
    {
        _tecleado.Clear();
        _campos.Clear();
        _avisosDeCarga.Clear();
        _respuestas = [];
        _personasDelCaso = [];
        _caso = null;
    }

    /// <summary>Tira lo tecleado en los campos de esa persona: ya no hay fila donde guardarlo.</summary>
    /// <param name="personaId">La persona que se acaba de borrar.</param>
    private void OlvidarLoTecleadoDe(long personaId)
    {
        var suyas = _campos
            .Where(c => c.Tabla == TablaDeProcedencia.Personas && c.RegistroId == personaId)
            .Select(c => c.Clave)
            .ToList();
        foreach (var clave in suyas) _tecleado.Remove(clave);
    }

    /// <summary>No se borró nada, y se dice por qué en una línea.</summary>
    /// <param name="linea">El motivo, que va a la vez en el aviso y en el pie.</param>
    private ResultadoDeEliminarPersona NoSeElimino(string linea)
        => new(false, false, _personasDelCaso.Count, null, [Aviso.Advierte(linea, string.Empty, null)], linea);
}
