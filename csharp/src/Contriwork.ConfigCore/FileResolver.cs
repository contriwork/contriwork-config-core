using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Contriwork.ConfigCore;

/// <summary>
/// Resolve <c>${file:path}</c> by reading the file's contents. Trailing
/// whitespace (including newlines) is stripped — this matches the common
/// "secret in a single-line file" pattern used by Docker secrets.
/// </summary>
public sealed class FileResolver : ISecretResolver
{
    private const string Scheme = "file";

    private readonly string? _baseDir;

    /// <summary>Initializes a new resolver.</summary>
    /// <param name="baseDir">
    /// Directory used to resolve relative paths. If <c>null</c>, relative paths
    /// resolve against the process working directory. Absolute paths are honored
    /// as-is either way.
    /// </param>
    public FileResolver(string? baseDir = null)
    {
        _baseDir = baseDir;
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string scheme, string value, CancellationToken cancellationToken = default)
    {
        if (scheme != Scheme)
        {
            throw new SecretSchemeUnsupportedException(
                $"FileResolver does not handle scheme '{scheme}'");
        }
        if (string.IsNullOrEmpty(value))
        {
            throw new SecretRefUnresolvedException("${file:} requires a path");
        }

        var path = value;
        if (!Path.IsPathRooted(path) && _baseDir is not null)
        {
            path = Path.Combine(_baseDir, path);
        }

        try
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return text.TrimEnd();
        }
        catch (FileNotFoundException)
        {
            throw new SecretRefUnresolvedException($"secret file not found: {path}");
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new SecretRefUnresolvedException($"secret file not found: {path}", ex);
        }
        catch (IOException ex)
        {
            throw new SecretRefUnresolvedException($"cannot read secret file {path}: {ex.Message}", ex);
        }
    }
}
