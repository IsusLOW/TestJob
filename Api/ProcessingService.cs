using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Dapper;
using Npgsql;
using AngleSharpConfiguration = AngleSharp.Configuration;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace TestJob.Api;

// Текстовые коды ошибок
public static class ErrorCodes
{
    public const string Ok = "OK";
    public const string MissingParam = "MISSING_PARAM";
    public const string EmptySelector = "EMPTY_SELECTOR";
    public const string EmptyAttribute = "EMPTY_ATTRIBUTE";
    public const string UrlBase64Error = "URL_BASE64_ERROR";
    public const string PageBase64Error = "PAGE_BASE64_ERROR";
    public const string KeyBase64Error = "KEY_BASE64_ERROR";
    public const string CipherBase64Error = "CIPHER_BASE64_ERROR";
    public const string DecryptError = "DECRYPT_ERROR";
    public const string HtmlParseError = "HTML_PARSE_ERROR";
    public const string SelectorError = "SELECTOR_ERROR";
    public const string DbError = "DB_ERROR";
    public const string UnknownError = "UNKNOWN_ERROR";
}

public interface IProcessingService
{
    Task<ProcessResponse> ProcessAsync(ProcessRequest request, CancellationToken ct = default);
}

public sealed partial class ProcessingService : IProcessingService
{
    public const string InitSql = """
        CREATE TABLE IF NOT EXISTS elements (
          id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
          attr_value TEXT NOT NULL,
          html_code TEXT NOT NULL
        );
        """;

    private const string InsertSql =
        "INSERT INTO elements (attr_value, html_code) VALUES (@AttrValue, @HtmlCode);";

    // Скомпилированное регулярное выражение
    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    private readonly string _connectionString;
    private readonly ILogger<ProcessingService> _logger;

    public ProcessingService(IConfiguration configuration, ILogger<ProcessingService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection.");
        _logger = logger;
    }

    public async Task<ProcessResponse> ProcessAsync(ProcessRequest request, CancellationToken ct = default)
    {
        try
        {
            string url = DecodeUtf8(request.UrlB64!, ErrorCodes.UrlBase64Error, "url_b64");
            string pageHtml = DecodeUtf8(request.PageB64!, ErrorCodes.PageBase64Error, "page_b64");
            byte[] key = DecodeBytes(request.KeyBytesB64!, ErrorCodes.KeyBase64Error, "key_bytes_b64");
            byte[] cipher = DecodeBytes(request.EncryptedTextBytesB64!, ErrorCodes.CipherBase64Error, "encrypted_text_bytes_b64");

            IDocument document;
            try
            {
                var context = BrowsingContext.New(AngleSharpConfiguration.Default);
                document = await context.OpenAsync(req => req.Content(pageHtml), ct);
            }
            catch (Exception ex)
            {
                throw new ProcessingException(ErrorCodes.HtmlParseError,
                    $"Ошибка парсинга HTML: {ex.Message}", ex);
            }

            List<(string AttrValue, string HtmlCode)> elements;
            try
            {
                var nodes = document.QuerySelectorAll(request.Selector!);
                elements = new List<(string, string)>(nodes.Length);
                foreach (var node in nodes)
                {
                    string attrValue = node.GetAttribute(request.Attribute!) ?? string.Empty;
                    elements.Add((attrValue, node.OuterHtml));
                }
            }
            catch (Exception ex)
            {
                throw new ProcessingException(ErrorCodes.SelectorError,
                    $"Ошибка выборки по селектору: {ex.Message}", ex);
            }

            // Запись в postgres через Dapper
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct);

                await using var transaction = await conn.BeginTransactionAsync(ct);

                await conn.ExecuteAsync(
                    InsertSql,
                    elements.Select(el => new
                    {
                        AttrValue = el.AttrValue,
                        HtmlCode = el.HtmlCode
                    }),
                    transaction);

                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                throw new ProcessingException(ErrorCodes.DbError,
                    $"Ошибка записи в БД: {ex.Message}", ex);
            }

            // Email регулярным выражением (синхронно, CPU-bound)
            MatchCollection matches = EmailRegex().Matches(pageHtml);
            var emails = new List<string>(matches.Count);
            foreach (Match m in matches)
            {
                emails.Add(m.Value);
            }

            // AES-256 расшифровка (синхронно, CPU-bound)
            string plainText = DecryptAesEcbNoPadding(cipher, key);

            return new ProcessResponse
            {
                IsError = 0,
                ErrorCode = ErrorCodes.Ok,
                ErrorMessage = string.Empty,
                ElementsCount = elements.Count,
                EmailsCount = emails.Count,
                Url = url,
                DecryptedPlainText = plainText,
                ElementsAttrList = elements.Select(e => e.AttrValue).ToList(),
                EmailsList = emails,
            };
        }
        catch (ProcessingException pex)
        {
            _logger.LogWarning(pex, "Ошибка обработки: {Code}", pex.Code);
            return new ProcessResponse
            {
                IsError = 1,
                ErrorCode = pex.Code,
                ErrorMessage = pex.Message,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неизвестная ошибка обработки");
            return new ProcessResponse
            {
                IsError = 1,
                ErrorCode = ErrorCodes.UnknownError,
                ErrorMessage = ex.Message,
            };
        }
    }

    private static string DecodeUtf8(string value, string errorCode, string paramName)
    {
        try
        {
            return Encoding.UTF8.GetString(DecodeBytes(value, errorCode, paramName));
        }
        catch (ProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProcessingException(errorCode,
                $"Ошибка декодинга base64 в {paramName}: {ex.Message}", ex);
        }
    }

    private static byte[] DecodeBytes(string value, string errorCode, string paramName)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            try
            {
                string padded = value + new string('=', (4 - value.Length % 4) % 4);
                return Convert.FromBase64String(padded);
            }
            catch (Exception ex)
            {
                throw new ProcessingException(errorCode,
                    $"Ошибка декодинга base64 в {paramName}: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            throw new ProcessingException(errorCode,
                $"Ошибка декодинга base64 в {paramName}: {ex.Message}", ex);
        }
    }

    private static string DecryptAesEcbNoPadding(byte[] cipher, byte[] key)
    {
        try
        {
            if (key.Length != 32)
            {
                throw new ProcessingException(ErrorCodes.DecryptError,
                    $"Некорректная длина ключа AES-256: {key.Length} байт, ожидается 32.");
            }
            if (cipher.Length == 0 || cipher.Length % 16 != 0)
            {
                throw new ProcessingException(ErrorCodes.DecryptError,
                    "Длина шифротекста должна быть кратна 16 байт (PaddingMode.None).");
            }

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key;

            using var decryptor = aes.CreateDecryptor();
            byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain).TrimEnd('\0');
        }
        catch (ProcessingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProcessingException(ErrorCodes.DecryptError,
                $"Ошибка расшифровки: {ex.Message}", ex);
        }
    }
}

// Фоновая инициализация БД
public sealed class DbInitHostedService : BackgroundService
{
    private readonly string? _connectionString;
    private readonly ILogger<DbInitHostedService> _logger;

    public DbInitHostedService(IConfiguration configuration, ILogger<DbInitHostedService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            _logger.LogWarning("Нет строки подключения — таблица elements не создана.");
            return;
        }

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.ExecuteAsync(
                    new CommandDefinition(ProcessingService.InitSql, cancellationToken: stoppingToken));
                _logger.LogInformation("Таблица elements готова.");
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (attempt < 30)
            {
                _logger.LogWarning(ex, "БД недоступна, повтор {Attempt}/30 через 2с...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}

public sealed class ProcessingException : Exception
{
    public string Code { get; }

    public ProcessingException(string code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }
}
