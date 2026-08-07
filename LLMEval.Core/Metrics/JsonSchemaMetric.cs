using System.Text.Json;
using System.Text.Json.Nodes;

namespace LLMEval;

/// <summary>Validates that Actual is parseable JSON (MatchingType = json).</summary>
public sealed class JsonValidityMetric : IEvaluationMetric
{
    public string Name => "json";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var _ = JsonDocument.Parse(context.Actual ?? string.Empty);
            return Task.FromResult(new MetricResult
            {
                Score = 1.0,
                IsPassed = 1.0 >= context.PassThreshold,
                Details = "Actual is valid JSON."
            });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new MetricResult
            {
                Score = 0.0,
                IsPassed = false,
                Details = $"Invalid JSON: {ex.Message}"
            });
        }
    }
}

/// <summary>
/// Validates Actual JSON against a JSON Schema subset (MatchingType = schema).
/// Schema is taken from <see cref="MetricContext.Schema"/>, else Expected.
/// Supports: type, required, properties (object), items (array), enum, const, minimum/maximum, minLength/maxLength, pattern (simple contains).
/// </summary>
public sealed class JsonSchemaMetric : IEvaluationMetric
{
    public string Name => "schema";

    public Task<MetricResult> EvaluateAsync(MetricContext context, CancellationToken cancellationToken = default)
    {
        var schemaText = !string.IsNullOrWhiteSpace(context.Schema)
            ? context.Schema!
            : context.Expected ?? string.Empty;

        if (string.IsNullOrWhiteSpace(schemaText))
        {
            return Task.FromResult(new MetricResult
            {
                Score = 0,
                IsPassed = false,
                Details = "No JSON Schema provided (set Schema or Expected)."
            });
        }

        JsonNode? instance;
        JsonNode? schema;
        try
        {
            instance = JsonNode.Parse(context.Actual ?? string.Empty);
            schema = JsonNode.Parse(schemaText);
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new MetricResult
            {
                Score = 0,
                IsPassed = false,
                Details = $"JSON parse error: {ex.Message}"
            });
        }

        if (schema == null)
        {
            return Task.FromResult(new MetricResult
            {
                Score = 0,
                IsPassed = false,
                Details = "Schema is empty."
            });
        }

        var errors = new List<string>();
        Validate(instance, schema, "$", errors);

        var ok = errors.Count == 0;
        return Task.FromResult(new MetricResult
        {
            Score = ok ? 1.0 : 0.0,
            IsPassed = ok && 1.0 >= context.PassThreshold,
            Details = ok
                ? "JSON validates against schema."
                : "Schema validation failed: " + string.Join("; ", errors.Take(8))
        });
    }

    private static void Validate(JsonNode? instance, JsonNode schema, string path, List<string> errors)
    {
        if (schema is not JsonObject schemaObj)
        {
            errors.Add($"{path}: schema must be an object.");
            return;
        }

        if (schemaObj.TryGetPropertyValue("const", out var constNode) && !JsonNodeEquals(instance, constNode))
            errors.Add($"{path}: expected const {constNode}.");

        if (schemaObj.TryGetPropertyValue("enum", out var enumNode) && enumNode is JsonArray enumArr)
        {
            var match = enumArr.Any(e => JsonNodeEquals(instance, e));
            if (!match) errors.Add($"{path}: value not in enum.");
        }

        if (schemaObj.TryGetPropertyValue("type", out var typeNode))
        {
            var typeName = typeNode?.GetValue<string>();
            if (!string.IsNullOrEmpty(typeName) && !MatchesType(instance, typeName!))
                errors.Add($"{path}: expected type {typeName}, got {DescribeType(instance)}.");
        }

        if (instance is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s))
            {
                if (schemaObj.TryGetPropertyValue("minLength", out var minLen) && s.Length < minLen!.GetValue<int>())
                    errors.Add($"{path}: string shorter than minLength.");
                if (schemaObj.TryGetPropertyValue("maxLength", out var maxLen) && s.Length > maxLen!.GetValue<int>())
                    errors.Add($"{path}: string longer than maxLength.");
                if (schemaObj.TryGetPropertyValue("pattern", out var patternNode))
                {
                    var pattern = patternNode?.GetValue<string>() ?? string.Empty;
                    if (!string.IsNullOrEmpty(pattern) &&
                        !System.Text.RegularExpressions.Regex.IsMatch(s, pattern))
                        errors.Add($"{path}: string does not match pattern.");
                }
            }

            if (TryGetNumber(jv, out var num))
            {
                if (schemaObj.TryGetPropertyValue("minimum", out var min) && num < min!.GetValue<double>())
                    errors.Add($"{path}: below minimum.");
                if (schemaObj.TryGetPropertyValue("maximum", out var max) && num > max!.GetValue<double>())
                    errors.Add($"{path}: above maximum.");
            }
        }

        if (instance is JsonObject obj && schemaObj.TryGetPropertyValue("required", out var reqNode) && reqNode is JsonArray req)
        {
            foreach (var r in req)
            {
                var name = r?.GetValue<string>();
                if (!string.IsNullOrEmpty(name) && !obj.ContainsKey(name!))
                    errors.Add($"{path}: missing required property '{name}'.");
            }
        }

        if (instance is JsonObject objProps && schemaObj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject props)
        {
            foreach (var prop in props)
            {
                if (objProps.TryGetPropertyValue(prop.Key, out var child) && prop.Value != null)
                    Validate(child, prop.Value, $"{path}.{prop.Key}", errors);
            }
        }

        if (instance is JsonArray arr && schemaObj.TryGetPropertyValue("items", out var itemsNode) && itemsNode != null)
        {
            for (var i = 0; i < arr.Count; i++)
                Validate(arr[i], itemsNode, $"{path}[{i}]", errors);
        }
    }

    private static bool MatchesType(JsonNode? node, string type) => type.ToLowerInvariant() switch
    {
        "object" => node is JsonObject,
        "array" => node is JsonArray,
        "string" => node is JsonValue jv && jv.TryGetValue<string>(out _),
        "number" => node is JsonValue n && TryGetNumber(n, out _),
        "integer" => node is JsonValue i && TryGetNumber(i, out var d) && Math.Abs(d % 1) < 1e-9,
        "boolean" => node is JsonValue b && b.TryGetValue<bool>(out _),
        "null" => node is null || (node is JsonValue nv && nv.GetValueKind() == JsonValueKind.Null),
        _ => true
    };

    private static string DescribeType(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject => "object",
        JsonArray => "array",
        JsonValue jv when jv.TryGetValue<string>(out _) => "string",
        JsonValue jv when jv.TryGetValue<bool>(out _) => "boolean",
        JsonValue jv when TryGetNumber(jv, out _) => "number",
        _ => "unknown"
    };

    private static bool TryGetNumber(JsonValue value, out double number)
    {
        if (value.TryGetValue<double>(out number)) return true;
        if (value.TryGetValue<long>(out var l)) { number = l; return true; }
        if (value.TryGetValue<int>(out var i)) { number = i; return true; }
        number = 0;
        return false;
    }

    private static bool JsonNodeEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return string.Equals(a.ToJsonString(), b.ToJsonString(), StringComparison.Ordinal);
    }
}
