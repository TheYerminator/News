using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    private DiscordSocketClient? _client;

    static void Main(string[] args)
    {
        var jsonString = File.ReadAllText("secrets.json");
        var jsonObject = JObject.Parse(jsonString);
        string? token = jsonObject["NewsBot"]?["TokenBot"]?.ToString();

        new Program().RunBotAsync(token).GetAwaiter().GetResult();
    }

    private async Task UpdatePresenceAsync(DiscordSocketClient _client)
    {
        if (_client != null)
        {
            try
            {
                await _client.SetActivityAsync(new Discord.Game("📰 - Monitor the news - 📰"));
            }
            catch (Exception ex)
            {
                await Log(new LogMessage(LogSeverity.Error, "UpdatePresenceAsync", $"Erreur lors de la mise à jour de la présence : {ex.Message}"));
            }
        }
    }

    public async Task RunBotAsync(string token)
    {
        _client = new DiscordSocketClient();
        _client.Log += Log;

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _client.Ready += OnReady;
        _client.SlashCommandExecuted += SlashCommandHandler;

        await Task.Delay(-1);
    }

    private Task Log(LogMessage arg)
    {
        string logEntry = $"{DateTime.Now} [{arg.Severity}] {arg.Source}: {arg.Message}";
        Console.WriteLine(logEntry);
        using (StreamWriter sw = new StreamWriter("logs.txt"))
        {
                sw.WriteLine(logEntry);
        }
        return Task.CompletedTask;
    }

    public class Article
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Content { get; set; }
        public string? Author { get; set; }
        public string? UrlToImage { get; set; }
    }

    public class NewsApiResponse
    {
        public string? status { get; set; }
        public List<Article>? articles { get; set; }
    }

    private List<string> sentArticles = new List<string>();

    private async Task OnReady()
    {
        await UpdatePresenceAsync(_client);

        var globalCommand = new SlashCommandBuilder()
            .WithName("news")
            .WithDescription("Send news about a particular subject")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("subject")
                .WithDescription("The subject that interests you")
                .WithRequired(true)
                .AddChoice("Buisness", "business")
                .AddChoice("Divertissement", "entertainment")
                .AddChoice("Général", "general")
                .AddChoice("Santé", "health")
                .AddChoice("Sciences", "science")
                .AddChoice("Sports", "sports")
                .AddChoice("Technologie", "technology")
                .WithType(ApplicationCommandOptionType.String))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("count")
                .WithDescription("Number of news articles to send")
                .WithRequired(false)
                .WithType(ApplicationCommandOptionType.Integer));

        try
        {
            await _client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
        }
        catch (HttpException exception)
        {
            Console.WriteLine(exception);
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        var countOption = command.Data.Options?.FirstOrDefault(opt => opt.Name == "count");
        int countValue;
        if (countOption?.Value != null && int.TryParse(countOption.Value.ToString(), out countValue))
        {
            await Log(new LogMessage(LogSeverity.Info, "SlashCommandHandler", $"Count Value: {countValue}"));
        }
        else
        {
            await Log(new LogMessage(LogSeverity.Info, "SlashCommandHandler", $"Using default count: 5"));
            countValue = 5;
        }

        string subject = command.Data.Options?.FirstOrDefault(opt => opt.Name == "subject")?.Value as string;

        var jsonString = File.ReadAllText("secrets.json");
        var jsonObject = JObject.Parse(jsonString);
        string? newsApiKey = jsonObject["NewsBot"]?["NewsAPIKey"]?.ToString();

        var newsApiClient = new NewsApiClient(newsApiKey);

        if (subject != null)
        {
            var articlesResponse = newsApiClient.GetTopHeadlines(new TopHeadlinesRequest
            {
                Category = Enum.Parse<Categories>(subject, true),
                Page = countValue,
                PageSize = countValue,
                Language = Languages.FR,
            });



            if (articlesResponse.Status == Statuses.Ok)
            {
                foreach (var article in articlesResponse.Articles)
                {
                    if (!sentArticles.Contains(article.Url))
                    {
                        await SendArticle(command.Channel, article);
                        sentArticles.Add(article.Url);
                        await Task.Delay(100);
                    }
                }
            }
        }
    }

    private async Task SendArticle(ISocketMessageChannel channel, NewsAPI.Models.Article article)
    {
        Random random = new Random();
        Discord.Color randomColor = new Discord.Color(random.Next(256), random.Next(256), random.Next(256));

        var embed = new EmbedBuilder()
            .WithTitle(article.Title)
            .WithDescription(article.Content)
            .WithColor(randomColor)
            .WithAuthor(article.Author)
            .WithImageUrl(article.UrlToImage)
            .AddField("Article", article.Url, true)
            .WithCurrentTimestamp()
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }
}
