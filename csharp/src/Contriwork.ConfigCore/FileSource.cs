using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tomlyn;
using YamlDotNet.Serialization;

namespace Contriwork.ConfigCore;

/// <summary>The file format a <see cref="FileSource"/> parses.</summary>
public enum FileFormat
{
    /// <summary>YAML (.yaml / .yml).</summary>
    Yaml,

    /// <summary>JSON (.json).</summary>
    Json,

    /// <summary>TOML (.toml).</summary>
    Toml,
}

/// <summary>
/// Load a YAML / JSON / TOML file from disk. Format is inferred from the file
/// extension unless passed explicitly.
/// </summary>
public sealed class FileSource : ISource
{
    private readonly string _path;
    private readonly FileFormat _format;
    private readonly bool _required;

    /// <summary>Initializes a new source.</summary>
    /// <param name="path">Path to the config file.</param>
    /// <param name="format">Explicit format. If omitted, inferred from the extension.</param>
    /// <param name="required">If <c>true</c> (default), a missing file raises <see cref="SourceUnavailableException"/>. If <c>false</c>, an empty dict is returned.</param>
    public FileSource(string path, FileFormat? format = null, bool required = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        _path = path;
        _format = format ?? InferFormat(path);
        _required = required;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, object?>> SnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        string content;
        try
        {
            content = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException) when (!_required)
        {
            return new Dictionary<string, object?>();
        }
        catch (FileNotFoundException)
        {
            throw new SourceUnavailableException($"config file not found: {_path}");
        }
        catch (DirectoryNotFoundException) when (!_required)
        {
            return new Dictionary<string, object?>();
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new SourceUnavailableException($"config file not found: {_path}", ex);
        }
        catch (IOException ex)
        {
            throw new SourceUnavailableException(
                $"cannot read config file {_path}: {ex.Message}", ex);
        }

        try
        {
            var parsed = Parse(content, _format);
            if (parsed is null)
            {
                return new Dictionary<string, object?>();
            }
            if (parsed is Dictionary<string, object?> dict)
            {
                return dict;
            }
            throw new SourceParseFailedException(
                $"{_path} ({_format}) must contain a top-level mapping; got {parsed.GetType().Name}");
        }
        catch (ConfigException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SourceParseFailedException(
                $"failed to parse {_path} as {_format}: {ex.Message}", ex);
        }
    }

    private static FileFormat InferFormat(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".yaml" or ".yml" => FileFormat.Yaml,
            ".json" => FileFormat.Json,
            ".toml" => FileFormat.Toml,
            _ => throw new SourceParseFailedException(
                $"cannot infer format from {Path.GetFileName(path)}; pass format explicitly (Yaml / Json / Toml)"),
        };
    }

    private static object? Parse(string content, FileFormat format)
    {
        return format switch
        {
            FileFormat.Json => ParseJson(content),
            FileFormat.Yaml => ParseYaml(content),
            FileFormat.Toml => ParseToml(content),
            _ => throw new InvalidOperationException($"unsupported format: {format}"),
        };
    }

    private static object? ParseJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }
        using var doc = JsonDocument.Parse(content);
        return Normalize(doc.RootElement);
    }

    private static object? ParseYaml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }
        var deserializer = new DeserializerBuilder().Build();
        var parsed = deserializer.Deserialize<object?>(content);
        return Normalize(parsed);
    }

    private static object? ParseToml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }
        var model = Toml.ToModel(content);
        return Normalize(model);
    }

    private static object? Normalize(object? value)
    {
        switch (value)
        {
            case null:
                return null;

            case JsonElement je:
                return je.ValueKind switch
                {
                    JsonValueKind.Object => je.EnumerateObject().Aggregate(
                        new Dictionary<string, object?>(),
                        (acc, p) => { acc[p.Name] = Normalize(p.Value); return acc; }),
                    JsonValueKind.Array => je.EnumerateArray().Select(e => Normalize(e)).ToList(),
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.Number => je.TryGetInt64(out var l) ? (object)l : je.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => null,
                };

            case string s:
                return s;

            case IDictionary<string, object?> generic:
                {
                    var result = new Dictionary<string, object?>(generic.Count);
                    foreach (var kv in generic)
                    {
                        result[kv.Key] = Normalize(kv.Value);
                    }
                    return result;
                }

            case IDictionary untyped:
                {
                    var result = new Dictionary<string, object?>();
                    foreach (DictionaryEntry e in untyped)
                    {
                        var key = e.Key.ToString()
                            ?? throw new SourceParseFailedException("dictionary key cannot be null");
                        result[key] = Normalize(e.Value);
                    }
                    return result;
                }

            case IEnumerable<object?> seq:
                return seq.Select(Normalize).ToList();

            case IEnumerable untypedSeq:
                {
                    var list = new List<object?>();
                    foreach (var item in untypedSeq)
                    {
                        list.Add(Normalize(item));
                    }
                    return list;
                }

            case IConvertible conv:
                // Preserve numeric / bool types from YAML / TOML.
                return conv;

            default:
                return value;
        }
    }
}
