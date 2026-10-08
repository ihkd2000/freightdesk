using System.Text;

namespace FreightDesk.Infra;

public class MailGunService : IMailSender
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _domain;

    public string? FromEmail { get; set; }
    public string? ToEmail { get; set; }

    public MailGunService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["EmailSettings:apikey"];
        _domain = configuration["EmailSettings:domain"];
        FromEmail = configuration["EmailSettings:FromAddress"];
        ToEmail = configuration["EmailSettings:ToAddress"];
    }

    public async Task SendEmailAsync(string subject, string textBody, string htmlBody,
        string? to = null, string? from = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_domain))
        {
            throw new InvalidOperationException(
                "Mailgun is not configured. Set EmailSettings:apikey and EmailSettings:domain.");
        }

        from ??= FromEmail;
        to ??= ToEmail;

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("Both a sender and a recipient address are required.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.mailgun.net/v3/{_domain}/messages")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("from", from),
                new KeyValuePair<string, string>("to", to),
                new KeyValuePair<string, string>("subject", subject),
                new KeyValuePair<string, string>("text", textBody),
                new KeyValuePair<string, string>("html", htmlBody),
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{_apiKey}")));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Mailgun returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
    }
}
