using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Xml.XPath;
using Newtonsoft.Json;

namespace LauncherMoscowRp
{
    internal class ServerInfo
    {
        List<string> ServerIP = new List<string>
        {
            "185.71.66.80",
        };

        public (string, string, int, int) httpRequestss(string targetIp)
        {
            string apiUrl = "https://mtasa.com/api/";
            (string serverName, string serverIp, int serverPlayers, int serverMaxPlayers) = ("", "", 0, 0);

            Task.Run(async () =>
            {
                using (HttpClient client = new HttpClient())
                {
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string data = await response.Content.ReadAsStringAsync();
                            var serverList = JsonConvert.DeserializeObject<List<ServerInfos>>(data);
                            var targetServer = serverList.FirstOrDefault(server => server.ip == targetIp);

                            (serverName, serverIp, serverPlayers, serverMaxPlayers) = (targetServer?.name, targetServer?.ip, targetServer?.players ?? 0, targetServer?.maxplayers ?? 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при выполнении HTTP-запроса: {ex.Message}");
                    }
                }
            }).Wait(); // Ожидаем завершение задачи

            return (serverName, serverIp, serverPlayers, serverMaxPlayers);
        }

        public class ServerInfos
        {
            public string name { get; set; }
            public string ip { get; set; }
            public int maxplayers { get; set; }
            public int keep { get; set; }
            public int players { get; set; }
            public string version { get; set; }
            public int password { get; set; }
            public int port { get; set; }
        }

        public List<string> GetServerIPList()
        {
            return ServerIP;
        }
    }
}
