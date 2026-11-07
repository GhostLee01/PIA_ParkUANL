using System.Reflection;

namespace PIA_ParkUANL;

// =====================================================================================
//  DATOSCONFIABLES.CS
//  Nada que venga de un archivo es confiable: alguien pudo editarlo a mano. Aquí se limpia
//  lo que el compilador da por seguro y el JSON no garantiza, como un texto en null.
// =====================================================================================
public static class DatosConfiables
{
    /// <summary>Quita los elementos nulos y cambia los textos y listas nulos por valores vacíos.</summary>
    public static List<T> Normalizar<T>(List<T> lista) where T : class
    {
        ArgumentNullException.ThrowIfNull(lista);

        lista.RemoveAll(elemento => elemento == null);
        foreach (T elemento in lista)
        {
            foreach (PropertyInfo propiedad in Propiedades<T>.Editables)
            {
                if (propiedad.GetValue(elemento) != null)
                {
                    continue;
                }

                if (propiedad.PropertyType == typeof(string))
                {
                    propiedad.SetValue(elemento, "");
                }
                else if (propiedad.PropertyType == typeof(List<string>))
                {
                    propiedad.SetValue(elemento, new List<string>());
                }
            }
        }
        return lista;
    }

    private static class Propiedades<T>
    {
        public static readonly PropertyInfo[] Editables = typeof(T).GetProperties().Where(propiedad => propiedad.CanWrite).ToArray();
    }
}
