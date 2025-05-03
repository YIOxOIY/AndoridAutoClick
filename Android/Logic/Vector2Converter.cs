using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Android.Logic
{
	public class Vector2Converter : JsonConverter<Vector2>
	{
		// 反序列化逻辑
		public override Vector2 ReadJson(JsonReader reader, Type objectType,
			Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			JObject obj = JObject.Load(reader);
			return new Vector2
			{
				X = (int)obj["X"],
				Y = (int)obj["Y"]
			};
		}

		// 序列化逻辑
		public override void WriteJson(JsonWriter writer, Vector2 value,
			JsonSerializer serializer)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("X");
			writer.WriteValue(value.X);
			writer.WritePropertyName("Y");
			writer.WriteValue(value.Y);
			writer.WriteEndObject();
		}
	}
}
