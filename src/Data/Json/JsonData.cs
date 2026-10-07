using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace GodotXOPS
{
    /// <summary>
    /// 외부 게임 데이터 JSON 의 읽기/쓰기 규칙을 한 곳에 모은 유틸리티.
    /// 데이터 클래스의 public 필드를 JSON 키와 같은 이름으로 대응시키고, 파일에 있는 필드만 덮어쓰는 병합을 제공한다.
    /// </summary>
    public static class JsonData
    {
        public static readonly JsonSerializerOptions Options = CreateOptions();

        /// <summary>
        /// JSON 텍스트의 최상위 키들을 대상 객체의 같은 이름 필드에 덮어쓴다. 파일에 없는 필드는 건드리지 않는다.
        /// 값 하나가 잘못돼도 그 필드만 건너뛰고 나머지는 계속 적용하며, 예외를 밖으로 내보내지 않는다.
        /// </summary>
        /// <param name="json">JSON 텍스트. 루트는 오브젝트여야 한다.</param>
        /// <param name="target">덮어쓸 대상 객체.</param>
        /// <param name="sourceName">로그에 표시할 출처(파일 경로).</param>
        /// <returns>루트를 읽는 데 성공했으면 true. JSON 형식이 깨졌거나 루트가 오브젝트가 아니면 false.</returns>
        public static bool Overwrite(string json, object target, string sourceName)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            }
            catch (JsonException e)
            {
                Debugger.LogError($"Data file is not valid JSON (using defaults): {sourceName}\n{e.Message}", nameof(JsonData));
                return false;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    Debugger.LogError($"Data file root is not an object (using defaults): {sourceName}", nameof(JsonData));
                    return false;
                }

                Type type = target.GetType();
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    FieldInfo field = type.GetField(property.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (field == null || field.IsDefined(typeof(JsonIgnoreAttribute)))
                    {
                        Debugger.LogWarning($"Unknown key \"{property.Name}\" ignored: {sourceName}", nameof(JsonData));
                        continue;
                    }

                    // null 은 "값 없음"으로 보고 기본값을 유지한다(소비자의 null 역참조 방지).
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }

                    try
                    {
                        field.SetValue(target, property.Value.Deserialize(field.FieldType, Options));
                    }
                    catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or NotSupportedException)
                    {
                        Debugger.LogError($"Value of \"{property.Name}\" could not be read (using default): {sourceName}\n{e.Message}", nameof(JsonData));
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 객체를 들여쓰기된 JSON 텍스트로 직렬화한다.
        /// </summary>
        /// <param name="value">직렬화할 객체.</param>
        /// <returns>JSON 텍스트.</returns>
        public static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value, value.GetType(), Options);
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                WriteIndented = true
            };
            options.Converters.Add(new LenientIntConverter());
            options.Converters.Add(new Vector2Converter());
            options.Converters.Add(new Vector3Converter());
            options.Converters.Add(new ColorConverter());
            return options;
        }

        /// <summary>
        /// 오브젝트 하나를 읽으며 숫자 프로퍼티마다 콜백을 부른다. {x, y, z} 같은 고정 형태 값의 공용 판독기.
        /// </summary>
        /// <param name="reader">오브젝트 시작 토큰에 위치한 리더.</param>
        /// <param name="assign">프로퍼티 이름과 값을 받는 콜백.</param>
        private static void ReadNumberObject(ref Utf8JsonReader reader, Action<string, float> assign)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("오브젝트가 와야 합니다.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string name = reader.GetString();
                reader.Read();
                if (reader.TokenType == JsonTokenType.Number)
                {
                    assign(name, reader.GetSingle());
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        /// <summary>
        /// 정수 필드에 실수 표기(예: 3.0)가 와도 소수부를 버리고 받아들이는 변환기. 손으로 고친 JSON 에 관대하게 대응한다.
        /// </summary>
        private class LenientIntConverter : JsonConverter<int>
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.TryGetInt32(out int value) ? value : (int)reader.GetDouble();
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value);
            }
        }

        /// <summary>
        /// Vector2 를 {x, y} 로 읽고 쓰는 변환기.
        /// </summary>
        private class Vector2Converter : JsonConverter<Vector2>
        {
            public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Vector2 result = Vector2.Zero;
                ReadNumberObject(ref reader, (name, value) =>
                {
                    if (name == "x") result.X = value;
                    else if (name == "y") result.Y = value;
                });
                return result;
            }

            public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.X);
                writer.WriteNumber("y", value.Y);
                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// Vector3 를 {x, y, z} 로 읽고 쓰는 변환기. 좌표 변환은 하지 않는다(값은 파일에 적힌 그대로).
        /// </summary>
        private class Vector3Converter : JsonConverter<Vector3>
        {
            public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Vector3 result = Vector3.Zero;
                ReadNumberObject(ref reader, (name, value) =>
                {
                    if (name == "x") result.X = value;
                    else if (name == "y") result.Y = value;
                    else if (name == "z") result.Z = value;
                });
                return result;
            }

            public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", value.X);
                writer.WriteNumber("y", value.Y);
                writer.WriteNumber("z", value.Z);
                writer.WriteEndObject();
            }
        }

        /// <summary>
        /// Color 를 채널당 0~1 실수 {r, g, b, a} 로 읽고 쓰는 변환기.
        /// </summary>
        private class ColorConverter : JsonConverter<Color>
        {
            public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var result = new Color(0f, 0f, 0f, 0f);
                ReadNumberObject(ref reader, (name, value) =>
                {
                    if (name == "r") result.R = value;
                    else if (name == "g") result.G = value;
                    else if (name == "b") result.B = value;
                    else if (name == "a") result.A = value;
                });
                return result;
            }

            public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("r", value.R);
                writer.WriteNumber("g", value.G);
                writer.WriteNumber("b", value.B);
                writer.WriteNumber("a", value.A);
                writer.WriteEndObject();
            }
        }
    }
}
