using AngleSharp;
using Newtonsoft.Json;
using RestSharp;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Viole_Logger_Interactive;

namespace Rust
{
    public partial class Program
    {
        private readonly static Logger Logger = new();

        private static readonly string FixerFile = "! Fixer.json";
        private static readonly string SteamCommunityFile = "! Steam Community.json";
        private static readonly string SteamStoreFile = "! Steam Store.json";

        private static Dictionary<DateTime, Dictionary<string, decimal>> Fixer = new();
        private static List<ICommunityResponse> SteamCommunity = new();
        private static List<IStoreResponse> SteamStore = new();

        private const string ACQUIRED_BY = "Marketplace";
        private const string CURRENCY_NAME = "USD";
        private const long STEAM_ID = 76561199035536243;

        #region Cookie

        public enum ECookie : byte
        {
            SteamCommunity = 0,
            SteamStore = 1,
            Rust = 2
        }

        public partial class ICookie
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("value")]
            public string? Value { get; set; }

            [JsonProperty("path")]
            public string? Path { get; set; }

            [JsonProperty("domain")]
            public string? Domain { get; set; }
        }

        #endregion

        private static readonly Dictionary<ECookie, List<ICookie>> Cookie = new();

        public static void Main()
        {
            foreach (var T in new[] {
                GetCookie(ECookie.SteamCommunity),
                GetCookie(ECookie.SteamStore),
                GetCookie(ECookie.Rust)
            })
            {
                Cookie.Add(T.Key, T.Value);
            }

            Init();

            Console.ReadLine();
        }

        private static KeyValuePair<ECookie, List<ICookie>> GetCookie(ECookie Cookie)
        {
            List<ICookie> List;

            try
            {
                string[] Array = Regex.Split(Cookie.ToString(), @"(?<!^)(?=[A-Z])");

                Console.Title = $"$ {string.Join(" ", Array.Select(x => x.ToUpper()))} COOKIE:";

            Retry:

                Console.Clear();

                string? JSON = string.Empty, Line;

                while (!string.IsNullOrWhiteSpace(Line = Console.ReadLine()))
                {
                    JSON += Line;
                }

                if (Logger.Helper.IsValidJson(JSON))
                {
                    var X = JsonConvert.DeserializeObject<List<ICookie>>(JSON);

                    if (X == null || X.Count == 0)
                    {
                        goto Retry;
                    }

                    List = X;
                }
                else
                {
                    goto Retry;
                }
            }
            finally
            {
                Console.Title = "$ ";

                Console.Clear();
            }

            return KeyValuePair.Create(Cookie, List);
        }

        private static async void Init()
        {
            if (File.Exists(FixerFile))
            {
                string? TEXT = File.ReadAllText(FixerFile);

                if (string.IsNullOrEmpty(TEXT)) return;

                var JSON = JsonConvert.DeserializeObject<Dictionary<DateTime, Dictionary<string, decimal>>>(TEXT);

                if (JSON == null) return;

                Fixer = JSON;
            }
            else
            {
                await GetFixer();

                if (Fixer.Count > 0)
                {
                    File.WriteAllText(FixerFile, JsonConvert.SerializeObject(Fixer, Formatting.Indented));
                }
            }

            bool Compare = false;

            if (File.Exists(SteamCommunityFile))
            {
                string? TEXT = File.ReadAllText(SteamCommunityFile);

                if (string.IsNullOrEmpty(TEXT)) return;

                var JSON = JsonConvert.DeserializeObject<List<ICommunityResponse>>(TEXT);

                if (JSON == null) return;

                SteamCommunity = JSON;
            }
            else
            {
                await GetCommunity();

                Compare = true;
            }

            if (File.Exists(SteamStoreFile))
            {
                string? TEXT = File.ReadAllText(SteamStoreFile);

                if (string.IsNullOrEmpty(TEXT)) return;

                var JSON = JsonConvert.DeserializeObject<List<IStoreResponse>>(TEXT);

                if (JSON == null) return;

                SteamStore = JSON;
            }
            else
            {
                await GetStore();

                Compare = true;
            }

            if (Compare)
            {
                var Array = SteamCommunity
                    .Where(x => x.DateTime.Year == DateTime.MaxValue.Year)
                    .ToArray();

                foreach (var T in SteamStore)
                {
                    if (T.Count == 1)
                    {
                        for (int i = 0; i < Array.Length; i++)
                        {
                            if (Array[i].DateTime.Day == T.DateTime.Day &&
                                Array[i].DateTime.Month == T.DateTime.Month &&
                                Array[i].Price == T.Price)
                            {
                                Array[i].DateTime = T.DateTime;

                                break;
                            }
                        }
                    }
                    else
                    {

                        for (int i = 0; i < Array.Length; i++)
                        {
                            int Start = i;
                            int End = T.Count + Start;

                            if (Array.Length >= Start && Array.Length >= End)
                            {
                                var Range = Array[Start..End];

                                if (Range.Sum(x => x.Price) == T.Price)
                                {
                                    foreach (var X in Range)
                                    {
                                        X.DateTime = T.DateTime;
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }

                foreach (var T in SteamCommunity)
                {
                    if (T.Default.EndsWith("pуб."))
                    {
                        T.RUB = Helper.ToPrice(T.Default);

                        if (T.RUB.HasValue)
                        {
                            if (Fixer.TryGetValue(T.DateTime, out var X))
                            {
                                T.USD = Math.Ceiling(T.RUB.Value / X["RUB"]);
                            }
                        }
                    }
                    else if (T.Default.EndsWith("TL"))
                    {
                        T.TRY = Helper.ToPrice(T.Default);

                        if (T.TRY.HasValue)
                        {
                            if (Fixer.TryGetValue(T.DateTime, out var X))
                            {
                                T.USD = Math.Ceiling(T.TRY.Value / X["TRY"]);
                            }
                        }
                    }
                    else
                    {
                        T.USD = Helper.ToPrice(T.Default);
                    }
                }
            }

            Logger.LogGenericObject(SteamStore);

            if (SteamStore.Count > 0)
            {
                File.WriteAllText(SteamStoreFile, JsonConvert.SerializeObject(SteamStore, Formatting.Indented));
            }

            Logger.LogGenericObject(SteamCommunity);

            if (SteamCommunity.Count > 0)
            {
                File.WriteAllText(SteamCommunityFile, JsonConvert.SerializeObject(SteamCommunity, Formatting.Indented));
            }

            var Currency = await GetCurrency();

            if (Currency.HasValue)
            {
                var Inventory = await GetInventory();

                if (Inventory is not null)
                {
                    foreach ((ICommunityResponse Value, int Index) in SteamCommunity
                        .Select((x, i) => (Value: x, Index: i + 1))
                        .ToList())
                    {
                        Console.Title = $"~ UPDATE | {Index}/{SteamCommunity.Count}";

                        for (int i = 0; i < Value.Quantity; i++)
                        {
                            foreach (var T in Inventory
                                .Where(x => x.Price == null)
                                .ToList())
                            {
                                if (T.Name == Value.Name)
                                {
                                    if (Value.USD.HasValue)
                                    {
                                        T.Price = Math.Round(Value.USD.Value / Value.Quantity, 2);

                                        var Logger = new Logger(T.ID.ToString());

                                        string Message = $"{T.Name} ({Value.Quantity}) -> {T.Price.Value}";

                                        if (await Update(Logger, T.ID, ACQUIRED_BY, Currency.Value, T.Price.Value))
                                        {
                                            Logger.LogInfo(Message);
                                        }
                                        else
                                        {
                                            Logger.LogError(Message);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #region Fixer

        public class IFixer
        {
            [JsonProperty("rates")]
            public Dictionary<string, decimal>? List { get; set; }
        }

        private static async Task GetFixer()
        {
            var Start = new DateTime(2020, 3, 18);
            var End = new DateTime(2022, 10, 2);

            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                for (var N = Start; N <= End; N = N.AddDays(1))
                {
                    Console.Title = $"~ FIXER | {N}/{End} - {Fixer.Count}";

                    var Request = new RestRequest($"http://data.fixer.io/api/{N:yyyy-MM-dd}?access_key=200dbb2024c0511d4795a093e5eaf423");

                    for (byte i = 0; i < 3; i++)
                    {
                        try
                        {
                            var Execute = await Client.ExecuteGetAsync(Request);

                            if (string.IsNullOrEmpty(Execute.Content))
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    Logger.LogWarning("Ответ пуст!");
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                                }
                            }
                            else
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    if (Logger.Helper.IsValidJson(Execute.Content))
                                    {
                                        try
                                        {
                                            var JSON = JsonConvert.DeserializeObject<IFixer>(Execute.Content);

                                            if (JSON == null || JSON.List == null || JSON.List.Count == 0)
                                            {
                                                Logger.LogWarning($"Ошибка: {Execute.Content}.");
                                            }
                                            else
                                            {
                                                Fixer.Add(N, JSON.List
                                                    .Where(x => x.Key == "USD" || x.Key == "RUB" || x.Key == "TRY")
                                                    .ToDictionary(x => x.Key, x => x.Value));
                                            }

                                            break;
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogException(e);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Ошибка: {Execute.Content}");
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.Content}");
                                }
                            }

                            await Task.Delay(2500);
                        }
                        catch (Exception e)
                        {
                            Logger.LogException(e);
                        }
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        #endregion

        #region Inventory

        #region Currency

        public class ICurrency
        {
            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("guid")]
            public Guid Guid { get; set; }
        }

        private static async Task<Guid?> GetCurrency()
        {
            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                var Request = new RestRequest($"https://rust.scmm.app/api/currency?detailed=true");

                foreach (var X in Cookie.Where(x => x.Key == ECookie.Rust).SelectMany(x => x.Value))
                {
                    try
                    {
                        Client.AddCookie(X.Name!, X.Value!, X.Path!, X.Domain!);
                    }
                    catch { }
                }

                for (byte i = 0; i < 3; i++)
                {
                    try
                    {
                        var Execute = await Client.ExecuteGetAsync(Request);

                        if (string.IsNullOrEmpty(Execute.Content))
                        {
                            if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                            {
                                Logger.LogWarning("Ответ пуст!");
                            }
                            else
                            {
                                Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                            }
                        }
                        else
                        {
                            if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                            {
                                if (Logger.Helper.IsValidJson(Execute.Content))
                                {
                                    try
                                    {
                                        var JSON = JsonConvert.DeserializeObject<List<ICurrency>>(Execute.Content);

                                        if (JSON == null || JSON.Count == 0)
                                        {
                                            Logger.LogWarning($"Ошибка: {Execute.Content}.");
                                        }
                                        else
                                        {
                                            foreach (var T in JSON)
                                            {
                                                if (T.Name == CURRENCY_NAME)
                                                {
                                                    return T.Guid;
                                                }
                                            }
                                        }

                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        Logger.LogException(e);
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.Content}");
                                }
                            }
                            else
                            {
                                Logger.LogWarning($"Ошибка: {Execute.Content}");
                            }
                        }

                        await Task.Delay(2500);
                    }
                    catch (Exception e)
                    {
                        Logger.LogException(e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

            return null;
        }

        #endregion

        public class IInventory
        {
            [JsonProperty("items")]
            public List<IList>? List { get; set; }

            public class IList
            {
                [JsonProperty("itemId")]
                public long ID { get; set; }

                [JsonProperty("name")]
                public string? Name { get; set; }

                [JsonIgnore]
                public decimal? Price { get; set; }
            }

            [JsonProperty("start")]
            public int Start { get; set; }

            [JsonProperty("total")]
            public int Count { get; set; }
        }

        private static async Task<List<IInventory.IList>> GetInventory()
        {
            const int PAGE_SIZE = 100;

            int Start = 0;

            int Index = 0;
            int Count = 0;

            List<IInventory.IList> List = new();

            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                foreach (var X in Cookie.Where(x => x.Key == ECookie.Rust).SelectMany(x => x.Value))
                {
                    try
                    {
                        Client.AddCookie(X.Name!, X.Value!, X.Path!, X.Domain!);
                    }
                    catch { }
                }

                while (Start == 0 || ++Index <= Count)
                {
                    if (Start > 0)
                    {
                        Console.Title = $"~ INVESTMENT | {Index}/{Count} - {List.Count}";
                    }

                    var Request = new RestRequest($"https://rust.scmm.app/api/profile/{STEAM_ID}/inventory/investment?filter=&start={Start}&count={PAGE_SIZE}&sortBy=BuyPrice&sortDirection=Descending");

                    for (byte i = 0; i < 3; i++)
                    {
                        try
                        {
                            var Execute = await Client.ExecuteGetAsync(Request);

                            if (string.IsNullOrEmpty(Execute.Content))
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    Logger.LogWarning("Ответ пуст!");
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                                }
                            }
                            else
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    if (Logger.Helper.IsValidJson(Execute.Content))
                                    {
                                        try
                                        {
                                            var JSON = JsonConvert.DeserializeObject<IInventory>(Execute.Content);

                                            if (JSON == null || JSON.List == null)
                                            {
                                                Logger.LogWarning($"Ошибка: {Execute.Content}.");
                                            }
                                            else
                                            {
                                                List.AddRange(JSON.List);

                                                if (Count == 0)
                                                {
                                                    Count = (int)Math.Ceiling((double)JSON.Count / PAGE_SIZE);
                                                }

                                                Start += PAGE_SIZE;
                                            }

                                            break;
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogException(e);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Ошибка: {Execute.Content}");
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.Content}");
                                }
                            }

                            await Task.Delay(2500);
                        }
                        catch (Exception e)
                        {
                            Logger.LogException(e);
                        }
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

            return List;
        }

        #endregion

        private static async Task<bool> Update(Logger Logger, long ID, string AcquiredBy, Guid CurrencyGuid, decimal BuyPrice)
        {
            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                var Request = new RestRequest($"https://rust.scmm.app/api/profile/{STEAM_ID}/inventory/item/{ID}");

                foreach (var X in Cookie.Where(x => x.Key == ECookie.Rust).SelectMany(x => x.Value))
                {
                    try
                    {
                        Client.AddCookie(X.Name!, X.Value!, X.Path!, X.Domain!);
                    }
                    catch { }
                }

                Request.AddBody(new
                {
                    AcquiredBy,
                    CurrencyGuid,
                    BuyPrice
                });

                for (byte i = 0; i < 3; i++)
                {
                    try
                    {
                        var Execute = await Client.ExecutePutAsync(Request);

                        if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                        {
                            return true;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(Execute.Content))
                            {
                                Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                            }
                            else
                            {
                                Logger.LogWarning($"Ошибка: {Execute.Content}");
                            }
                        }

                        await Task.Delay(2500);
                    }
                    catch (Exception e)
                    {
                        Logger.LogException(e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

            return false;
        }

        #region Community

        public class ICommunityRequest
        {
            [JsonProperty("results_html")]
            public string? HTML { get; set; }

            [JsonProperty("start")]
            public int Start { get; set; }

            [JsonProperty("total_count")]
            public int Count { get; set; }
        }

        public class ICommunityResponse
        {
            [JsonProperty]
            public string Name { get; set; }

            [JsonProperty]
            public DateTime DateTime { get; set; }

            [JsonProperty]
            public string Default { get; set; }

            [JsonIgnore]
            public decimal? Price { get; set; }

            [JsonProperty]
            public decimal? USD { get; set; }

            [JsonProperty]
            public decimal? RUB { get; set; }

            [JsonProperty]
            public decimal? TRY { get; set; }

            [JsonProperty]
            public int Quantity { get; set; }

            public ICommunityResponse(string Name, DateTime DateTime, string Default, int Quantity = 1)
            {
                this.Name = Name;
                this.DateTime = DateTime;
                this.Default = Default;

                Price = Helper.ToPrice(Default);

                this.Quantity = Quantity;
            }
        }

        private static async Task GetCommunity()
        {
            const int PAGE_SIZE = 500;

            int Start = 0;

            int Index = 0;
            int Count = 0;

            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                foreach (var X in Cookie.Where(x => x.Key == ECookie.SteamCommunity).SelectMany(x => x.Value))
                {
                    try
                    {
                        Client.AddCookie(X.Name!, X.Value!, X.Path!, X.Domain!);
                    }
                    catch { }
                }

                while (Start == 0 || ++Index <= Count)
                {
                    Console.Title = $"~ STORE: {SteamStore.Count} | COMMUNITY: {SteamCommunity.Count} ({Index}/{Count})";

                    var Request = new RestRequest($"https://steamcommunity.com/market/myhistory?count={PAGE_SIZE}&start={Start}&l=english");

                    for (byte i = 0; i < 3; i++)
                    {
                        try
                        {
                            var Execute = await Client.ExecuteGetAsync(Request);

                            if (string.IsNullOrEmpty(Execute.Content))
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    Logger.LogWarning("Ответ пуст!");
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                                }
                            }
                            else
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    if (Logger.Helper.IsValidJson(Execute.Content))
                                    {
                                        try
                                        {
                                            var JSON = JsonConvert.DeserializeObject<ICommunityRequest>(Execute.Content);

                                            if (JSON == null || string.IsNullOrEmpty(JSON.HTML))
                                            {
                                                Logger.LogWarning($"Ошибка: {Execute.Content}.");
                                            }
                                            else
                                            {
                                                if (Count == 0)
                                                {
                                                    Count = (int)Math.Ceiling((double)JSON.Count / PAGE_SIZE);
                                                }

                                                Start += PAGE_SIZE;

                                                var Context = BrowsingContext.New();

                                                var Document = await Context.OpenAsync(request => request.Content(JSON.HTML)).ConfigureAwait(false);

                                                foreach (var Element in Document.QuerySelectorAll(".market_listing_row"))
                                                {
                                                    string? Either = Element.QuerySelector(".market_listing_gainorloss")?.TextContent.Trim();
                                                    string? App = Element.QuerySelector(".market_listing_game_name")?.TextContent.Trim();

                                                    if (Either == "+" && App == "Rust")
                                                    {
                                                        string? Default = Element.QuerySelector(".market_listing_price")?.TextContent.Trim();

                                                        if (string.IsNullOrEmpty(Default)) continue;

                                                        string? Name = Element.QuerySelector(".market_listing_item_name")?.TextContent.Trim();
                                                        string? N = Element.QuerySelector(".market_listing_listed_date")?.TextContent.Trim();

                                                        if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(N)) continue;

                                                        if (System.DateTime.TryParseExact($"{N} {System.DateTime.MaxValue.Year}", "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var DateTime))
                                                        {
                                                            SteamCommunity.Add(new ICommunityResponse(Name, DateTime, Default));
                                                        }
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogException(e);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Ошибка: {Execute.Content}");
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.Content}");
                                }
                            }

                            await Task.Delay(2500);
                        }
                        catch (Exception e)
                        {
                            Logger.LogException(e);
                        }
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        #endregion

        #region Store

        #region Cursor

        public class ICursor
        {
            [JsonProperty("wallet_txnid")]
            public string? ID { get; set; }

            [JsonProperty("timestamp_newest")]
            public long Unix { get; set; }

            [JsonProperty("balance")]
            public string? Balance { get; set; }

            [JsonProperty("currency")]
            public int Currency { get; set; }
        }

        private static async Task<ICursor?> GetCursor()
        {
            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                foreach (var X in Cookie.Where(x => x.Key == ECookie.SteamStore).SelectMany(x => x.Value))
                {
                    try
                    {
                        Client.AddCookie(X.Name!, X.Value!, X.Path!, X.Domain!);
                    }
                    catch { }
                }

                var Request = new RestRequest($"https://store.steampowered.com/account/history?l=english");

                for (byte i = 0; i < 3; i++)
                {
                    try
                    {
                        var Execute = await Client.ExecuteGetAsync(Request);

                        if (string.IsNullOrEmpty(Execute.Content))
                        {
                            if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                            {
                                Logger.LogWarning("Ответ пуст!");
                            }
                            else
                            {
                                Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                            }
                        }
                        else
                        {
                            if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                            {
                                await ParseTable(Execute.Content);

                                var Match = Regex.Match(Execute.Content, "g_historyCursor = ([^;]+)");

                                if (Match.Groups[1].Success)
                                {
                                    if (Logger.Helper.IsValidJson(Match.Groups[1].Value))
                                    {
                                        try
                                        {
                                            var JSON = JsonConvert.DeserializeObject<ICursor>(Match.Groups[1].Value);

                                            if (JSON == null)
                                            {
                                                Logger.LogWarning($"Ошибка: {Execute.Content}.");
                                            }
                                            else
                                            {
                                                return JSON;
                                            }

                                            break;
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogException(e);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Ошибка: {Execute.Content}");
                                    }
                                }
                            }
                            else
                            {
                                Logger.LogWarning($"Ошибка: {Execute.Content}");
                            }
                        }

                        await Task.Delay(2500);
                    }
                    catch (Exception e)
                    {
                        Logger.LogException(e);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }

            return null;
        }

        #endregion

        public class IStoreRequest
        {
            [JsonProperty("cursor")]
            public ICursor? Cursor { get; set; }

            [JsonProperty("html")]
            public string? HTML { get; set; }
        }

        public class IStoreResponse
        {
            [JsonProperty]
            public DateTime DateTime { get; set; }

            [JsonProperty]
            public string Default { get; set; }

            [JsonIgnore]
            public decimal? Price { get; set; }

            [JsonProperty]
            public int Count { get; set; }

            public IStoreResponse(DateTime DateTime, string Default, int Count = 1)
            {
                this.DateTime = DateTime;
                this.Default = Default;

                Price = Helper.ToPrice(Default);

                this.Count = Count;
            }
        }

        private static async Task GetStore()
        {
            try
            {
                var Client = new RestClient(
                    new RestClientOptions()
                    {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36",
                        MaxTimeout = 300000
                    });

                foreach (var X in Cookie.Where(x => x.Key == ECookie.SteamStore).SelectMany(x => x.Value))
                {
                    try
                    {
                        Client.AddCookie(X.Name!, X.Value!, X.Path!, X.Domain!);
                    }
                    catch { }
                }

                var Cursor = await GetCursor();

                while (Cursor is not null)
                {
                    Console.Title = $"~ STORE: {SteamStore.Count} ({Helper.ConvertFromUnixTime(Cursor.Unix):dd MMM, yyyy}) | COMMUNITY: {SteamCommunity.Count}";

                    var Request = new RestRequest($"https://store.steampowered.com/account/AjaxLoadMoreHistory/");

                    Request.AddParameter("cursor[wallet_txnid]", Cursor.ID);
                    Request.AddParameter("cursor[timestamp_newest]", Cursor.Unix);
                    Request.AddParameter("cursor[balance]", Cursor.Balance);
                    Request.AddParameter("cursor[currency]", Cursor.Currency);

                    foreach (var T in Client.CookieContainer.GetAllCookies().Where(x => x.Name.ToUpper() == "SESSIONID"))
                    {
                        Request.AddParameter(T.Name, T.Value);
                    }

                    for (byte i = 0; i < 3; i++)
                    {
                        try
                        {
                            var Execute = await Client.ExecutePostAsync(Request);

                            if (string.IsNullOrEmpty(Execute.Content))
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    Logger.LogWarning("Ответ пуст!");
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.StatusCode}.");
                                }
                            }
                            else
                            {
                                if (Execute.StatusCode == 0 || Execute.StatusCode == HttpStatusCode.OK)
                                {
                                    if (Logger.Helper.IsValidJson(Execute.Content))
                                    {
                                        try
                                        {
                                            var JSON = JsonConvert.DeserializeObject<IStoreRequest>(Execute.Content);

                                            if (JSON == null || string.IsNullOrEmpty(JSON.HTML))
                                            {
                                                Logger.LogWarning($"Ошибка: {Execute.Content}.");
                                            }
                                            else
                                            {
                                                Cursor = JSON.Cursor;

                                                await ParseTable($"<table>{JSON.HTML}</table>");
                                            }

                                            break;
                                        }
                                        catch (Exception e)
                                        {
                                            Logger.LogException(e);
                                        }
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"Ошибка: {Execute.Content}");
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"Ошибка: {Execute.Content}");
                                }
                            }

                            await Task.Delay(2500);
                        }
                        catch (Exception e)
                        {
                            Logger.LogException(e);
                        }
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private static async Task ParseTable(string HTML)
        {
            var Context = BrowsingContext.New();

            var Document = await Context.OpenAsync(request => request.Content(HTML)).ConfigureAwait(false);

            foreach (var Element in Document.QuerySelectorAll(".wallet_table_row"))
            {
                string? Default = Element.QuerySelector(".wht_wallet_change")?.TextContent.Trim();

                if (string.IsNullOrEmpty(Default)) continue;

                if (Default.StartsWith("-"))
                {
                    Default = Default[1..];

                    string? N = Element.QuerySelector(".wht_date")?.TextContent.Trim();

                    if (string.IsNullOrEmpty(N)) continue;

                    if (System.DateTime.TryParseExact(N, "dd MMM, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var DateTime))
                    {
                        var Value = Element.QuerySelector(".wht_items");

                        if (Value == null || Value.Children.Length == 0)
                        {
                            string? Transaction = Element.QuerySelector(".wht_type > div")?.TextContent.Trim();

                            if (string.IsNullOrEmpty(Transaction)) continue;

                            string[] Split = Transaction.Split(' ');

                            if (Split.Length > 0)
                            {
                                if (int.TryParse(Split[0], out int Count))
                                {
                                    SteamStore.Add(new IStoreResponse(DateTime, Default, Count));
                                }
                                else
                                {
                                    SteamStore.Add(new IStoreResponse(DateTime, Default));
                                }
                            }
                        }
                        else
                        {
                            string? App = Element.QuerySelector(".wht_items > div")?.TextContent.Trim();

                            if (App == "Rust")
                            {
                                string? Name = Element.QuerySelector(".wht_items > .wth_payment")?.TextContent.Trim();

                                if (string.IsNullOrEmpty(Name)) continue;

                                string[] Split = Name.Split(' ');

                                switch (Split.Length)
                                {
                                    case > 0 when int.TryParse(Split[0], out int Quantity):

                                        SteamCommunity.Add(new ICommunityResponse(string.Join(" ", Split[1..]), DateTime, Default, Quantity));

                                        break;

                                    default:

                                        SteamCommunity.Add(new ICommunityResponse(Name, DateTime, Default));

                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}