using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contriwork.ConfigCore;

/// <summary>
/// Adapt a POCO class with <see cref="ValidationAttribute"/>s (e.g.
/// <c>[Required]</c>, <c>[Range]</c>) to the <see cref="ISchemaAdapter{T}"/>
/// protocol.
///
/// The adapter serialises the input dict to JSON and deserialises it into
/// <typeparamref name="T"/>, then runs <c>Validator.TryValidateObject</c>.
/// Light coercion is performed via JSON options so that environment-variable
/// strings ("true" / "42") hydrate into bool / int properties.
/// </summary>
/// <typeparam name="T">
/// The target POCO type. Must have a parameterless constructor.
/// </typeparam>
public sealed class DataAnnotationsAdapter<T> : ISchemaAdapter<T>
    where T : class, new()
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

    /// <inheritdoc />
    public T Validate(IReadOnlyDictionary<string, object?> data)
    {
        T instance;
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            instance = JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new ValidationFailedException(
                    $"config deserialized to null for type {typeof(T).Name}");
        }
        catch (JsonException ex)
        {
            throw new ValidationFailedException(
                $"config failed to hydrate into {typeof(T).Name}: {ex.Message}", ex);
        }

        var errors = new List<ValidationResult>();
        var ctx = new ValidationContext(instance);
        if (!Validator.TryValidateObject(instance, ctx, errors, validateAllProperties: true))
        {
            throw new ValidationFailedException(
                $"config failed validation against {typeof(T).Name}: {errors.Count} error(s)")
            {
                Details = errors.Select(e => new
                {
                    Message = e.ErrorMessage,
                    Members = e.MemberNames.ToArray(),
                }).ToArray(),
            };
        }

        return instance;
    }

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new LenientBoolConverter());
        return options;
    }
}

/// <summary>
/// Accept common string forms for booleans ("true", "yes", "1", etc.) in
/// addition to JSON <c>true</c> / <c>false</c>. Matches the pragma of
/// pydantic on the Python side — env vars are always strings.
/// </summary>
internal sealed class LenientBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.String:
                {
                    var s = reader.GetString();
                    return s?.ToLowerInvariant() switch
                    {
                        "true" or "yes" or "on" or "1" => true,
                        "false" or "no" or "off" or "0" or "" => false,
                        _ => throw new JsonException($"cannot convert {s} to bool"),
                    };
                }
            case JsonTokenType.Number:
                {
                    if (reader.TryGetInt64(out var i))
                    {
                        return i != 0;
                    }
                    throw new JsonException("non-integral number cannot convert to bool");
                }
            default:
                throw new JsonException($"unexpected token {reader.TokenType} for bool");
        }
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteBooleanValue(value);
    }
}
