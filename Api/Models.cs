using System.Text.Json.Serialization;

namespace TestJob.Api;

public sealed class ProcessRequest
{
    [JsonPropertyName("selector")]
    public string? Selector { get; set; }

    [JsonPropertyName("attribute")]
    public string? Attribute { get; set; }

    [JsonPropertyName("url_b64")]
    public string? UrlB64 { get; set; }

    [JsonPropertyName("encrypted_text_bytes_b64")]
    public string? EncryptedTextBytesB64 { get; set; }

    [JsonPropertyName("key_bytes_b64")]
    public string? KeyBytesB64 { get; set; }

    [JsonPropertyName("page_b64")]
    public string? PageB64 { get; set; }
}

public sealed class ProcessResponse
{
    [JsonPropertyName("is_error")]
    public int IsError { get; set; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonPropertyName("elements_count")]
    public int ElementsCount { get; set; }

    [JsonPropertyName("emails_count")]
    public int EmailsCount { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("decrypted_plain_text")]
    public string DecryptedPlainText { get; set; } = string.Empty;

    [JsonPropertyName("elements_attr_list")]
    public List<string> ElementsAttrList { get; set; } = new();

    [JsonPropertyName("emails_list")]
    public List<string> EmailsList { get; set; } = new();
}
