namespace TurbulentDisplace
{
    internal static class ShaderResourceUri
    {
        public static Uri Get(string shaderName)
        {
            var assemblyName = typeof(ShaderResourceUri).Assembly.GetName().Name;
            return new Uri($"pack://application:,,,/{assemblyName};component/Shaders/{shaderName}.cso");
        }
    }
}
