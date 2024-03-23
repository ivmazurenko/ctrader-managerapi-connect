using System.Security.Cryptography;
using System.Text;
using CtraderApi;
using CtraderApi.Helpers;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var appSettings = configuration.GetSection("TradePlatform:Credentials:45");
var proxyHost = appSettings["Host"].ThrowIfNull();
var proxyPort = int.Parse(appSettings["Port"].ThrowIfNull());
var plantId = appSettings["PlantId"].ThrowIfNull();
var environmentName = appSettings["EnvironmentName"].ThrowIfNull();
var login = long.Parse(appSettings["Login"].ThrowIfNull());
var password = appSettings["Password"].ThrowIfNull();

var client = new CtraderManagerApiClient(proxyHost, proxyPort);
client.MessageWithoutIdReceived += OnMessageWithoutIdReceived;
client.Error += OnException;

await client.Connect();

var applicationAuthReq = new ProtoManagerAuthReq
{
    PlantId = plantId,
    EnvironmentName = environmentName,
    Login = login,
    PasswordHash = Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(password))).ToLower()
};

await client.SendMessage<ProtoManagerAuthReq, ProtoManagerAuthRes>(applicationAuthReq);

var response = await client.SendMessage<ProtoManagerSymbolListReq, ProtoManagerSymbolListRes>(
    new ProtoManagerSymbolListReq());
Console.WriteLine(response.PayloadType);


static void OnMessageWithoutIdReceived(IMessage message) =>
    Console.WriteLine($"Message received -> {message.GetPayloadType()}");

static void OnException(Exception e) => Console.WriteLine($"\n{DateTime.Now}: Exception\n: {e}");