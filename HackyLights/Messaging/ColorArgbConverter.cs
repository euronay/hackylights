using System;
using System.Drawing;
using System.Globalization;
using Newtonsoft.Json;

namespace HackyLights.Messaging
{
    public class ColorArgbConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var color = (Color)value;
            var hexString = color.IsEmpty ? string.Empty : string.Concat("#",  color.ToArgb().ToString("X8"));
            writer.WriteValue(hexString);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var hexString = reader.Value.ToString();
            if (hexString == null || !hexString.StartsWith("#")) 
                return Color.Empty;
            
            return Color.FromArgb(
                int.Parse(hexString.Substring(1, 2), NumberStyles.HexNumber),
                int.Parse(hexString.Substring(3, 2), NumberStyles.HexNumber),
                int.Parse(hexString.Substring(5, 2), NumberStyles.HexNumber),
                int.Parse(hexString.Substring(7, 2), NumberStyles.HexNumber)
            );
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Color);
        }
    }
}