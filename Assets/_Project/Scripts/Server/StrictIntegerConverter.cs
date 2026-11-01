using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Scripts.Server
{
    /// <summary>Json.NET normally coerces fractional/string values into integral DTO properties.</summary>
    internal sealed class StrictIntegerConverter : JsonConverter
    {
        public override bool CanWrite => false;
        public override bool CanConvert(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum) type = Enum.GetUnderlyingType(type);
            return type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
                type == typeof(short) || type == typeof(ushort) || type == typeof(byte) || type == typeof(sbyte);
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type type = Nullable.GetUnderlyingType(objectType) ?? objectType;
            if (reader.TokenType == JsonToken.Null && type != objectType) return null;
            if (reader.TokenType != JsonToken.Integer) throw new JsonSerializationException("Expected an integer at " + reader.Path);
            Type storage = type.IsEnum ? Enum.GetUnderlyingType(type) : type;
            // All 64-bit integers fit exactly in decimal; no double conversion is involved.
            decimal exact = decimal.Parse(Convert.ToString(reader.Value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            object value = Convert.ChangeType(exact, storage, CultureInfo.InvariantCulture);
            return type.IsEnum ? Enum.ToObject(type, value) : value;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
    }
}
