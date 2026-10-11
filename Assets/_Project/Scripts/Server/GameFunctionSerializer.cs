using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.SharedModels;

namespace Scripts.Server
{
    /// <summary>Preserve integers and decimal amounts before FunctionResult becomes an object.</summary>
    public sealed class GameFunctionSerializer : ISerializerPlugin
    {
        private readonly ISerializerPlugin _fallback;
        internal static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            FloatParseHandling = FloatParseHandling.Decimal,
            DateParseHandling = DateParseHandling.None,
            TypeNameHandling = TypeNameHandling.None,
            Culture = System.Globalization.CultureInfo.InvariantCulture,
            MaxDepth = 64,
            Converters = { new StrictIntegerConverter() }
        };

        public GameFunctionSerializer(ISerializerPlugin fallback) =>
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

        public static void Install()
        {
            var previous = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
            if (!(previous is GameFunctionSerializer))
                PluginManager.SetPlugin(new GameFunctionSerializer(previous), PluginContract.PlayFab_Serializer);
        }

        public T DeserializeObject<T>(string serialized) => IsPreciseResponse(typeof(T))
            ? JsonConvert.DeserializeObject<T>(serialized, Settings) : _fallback.DeserializeObject<T>(serialized);
        public T DeserializeObject<T>(string serialized, object strategy) => IsPreciseResponse(typeof(T))
            ? DeserializeObject<T>(serialized) : _fallback.DeserializeObject<T>(serialized, strategy);
        public object DeserializeObject(string serialized) => _fallback.DeserializeObject(serialized);
        // The SDK parses its HTTP envelope, reserializes data, then parses the typed result.
        public string SerializeObject(object value) => value is JToken token ? token.ToString(Formatting.None) : _fallback.SerializeObject(value);
        public string SerializeObject(object value, object strategy) => value is JToken ? SerializeObject(value) : _fallback.SerializeObject(value, strategy);
        private static bool IsPreciseResponse(Type type) => type == typeof(HttpResponseObject) || type == typeof(ExecuteFunctionResult);
    }
}
