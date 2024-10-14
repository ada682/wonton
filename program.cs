using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly Random random = new Random();
    private static readonly Dictionary<string, string> headers = new Dictionary<string, string>
    {
        { "authority", "wonton.food" },
        { "accept", "*/*" },
        { "accept-language", "vi-VN,vi;q=0.9,fr-FR;q=0.8,fr;q=0.7,en-US;q=0.6,en;q=0.5" },
        { "content-type", "application/json" },
        { "origin", "https://www.wonton.restaurant" },
        { "referer", "https://www.wonton.restaurant/" },
        { "sec-ch-ua", "\"Not/A)Brand\";v=\"99\", \"Google Chrome\";v=\"115\", \"Chromium\";v=\"115\"" },
        { "sec-ch-ua-mobile", "?0" },
        { "sec-ch-ua-platform", "\"Windows\"" },
        { "sec-fetch-dest", "empty" },
        { "sec-fetch-mode", "cors" },
        { "sec-fetch-site", "cross-site" },
        { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36" }
    };

    static async Task Main(string[] args)
    {
        Logger.Log("Starting Wonton script...");
        Logger.Log("t.me/slyntherinnn", LogType.Watermark);

        while (true)
        {
            string[] urls = await File.ReadAllLinesAsync("data.txt");

            foreach (var url in urls)
            {
                await ProcessAccount(url);
                await Task.Delay(5000); 
            }

            Logger.Log("All accounts processed. Waiting 1 hour before restarting.", LogType.Info);
            await Task.Delay(TimeSpan.FromHours(1));
        }
    }

    static async Task ProcessAccount(string url)
    {
        try
        {
            Logger.Log($"Processing URL: {url}", LogType.Info); // Log the URL for debugging
            var uri = new Uri(url);
        
        // Check if URL contains a fragment
            if (string.IsNullOrEmpty(uri.Fragment))
            {
                Logger.Log("URL has no fragment. Skipping.", LogType.Error);
                return;
            }

            var fragment = uri.Fragment.TrimStart('#');
            var fragmentParams = HttpUtility.ParseQueryString(fragment);
            var tgWebAppData = fragmentParams["tgWebAppData"];

        // Check if tgWebAppData exists
            if (string.IsNullOrEmpty(tgWebAppData))
            {
                Logger.Log("'tgWebAppData' not found in URL fragment. Skipping.", LogType.Error);
                return;
            }

            Logger.Log($"tgWebAppData: {tgWebAppData}", LogType.Info); // Log tgWebAppData for better debugging

            // Decode and parse tgWebAppData
            var tgWebAppDataParams = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(tgWebAppData));
            var userString = tgWebAppDataParams["user"];

            if (string.IsNullOrEmpty(userString))
            {
                Logger.Log("User data not found in tgWebAppData. Skipping.", LogType.Error);
                return;
            }

            Logger.Log($"User data found: {userString}", LogType.Info); // Log the user data for debugging

            // Try to deserialize user data
            var userData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(HttpUtility.UrlDecode(userString));
            if (userData == null || !userData.TryGetValue("id", out JsonElement idElement))
            {
                Logger.Log("Invalid user data or missing 'id' field. Skipping.", LogType.Error);
                return;
            }

            var id = idElement.GetInt32().ToString();
            Logger.Log($"Processing account: {userData.GetValueOrDefault("first_name").GetString()} (ID: {id})", LogType.Info);

            // Get token and perform tasks
            var token = await GetToken(tgWebAppData);
            if (string.IsNullOrEmpty(token))
            {
                Logger.Log("Failed to get token. Skipping account.", LogType.Error);
                return;
            }

            Logger.Log("Token obtained. Performing tasks...", LogType.Success);
            await Checkin(token);
            await Task.Delay(2000); 

            await PerformTasks(token);
            await Task.Delay(2000); 

            await PlayGames(token);
            await Task.Delay(2000); 

            await CheckFarming(token);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error processing account: {ex.Message}", LogType.Error);
        }
    }

    static async Task<string?> GetToken(string query)
    {
        try
        {
            var loginData = new Dictionary<string, string> { { "initData", query }, { "inviteCode", "" } };
            client.DefaultRequestHeaders.Clear();
            foreach (var header in headers)
                if (header.Key != "content-type") client.DefaultRequestHeaders.Add(header.Key, header.Value);

            var content = new FormUrlEncodedContent(loginData);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

            var response = await client.PostAsync("https://wonton.food/api/v1/user/auth", content);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement?>(await response.Content.ReadAsStringAsync());
                if (jsonResponse.HasValue &&
                    jsonResponse.Value.TryGetProperty("tokens", out var tokensElement) &&
                    tokensElement.TryGetProperty("accessToken", out var accessTokenElement))
                    return accessTokenElement.GetString();
            }
            else
                Logger.Log($"Token request failed: {response.StatusCode}", LogType.Error);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error in GetToken: {ex.Message}", LogType.Error);
        }

        return null;
    }

    static async Task Checkin(string token)
    {
        Logger.Log("Performing check-in...", LogType.Info);
        var response = await MakeRequest("get", "https://wonton.food/api/v1/checkin", token);

        if (response.HasValue)
        {
            var checkinData = response.Value;
            Logger.Log("Check-in completed", LogType.Success);

            if (checkinData.TryGetProperty("lastCheckinDay", out JsonElement lastCheckinDayElement))
            {
                string lastCheckinDay = lastCheckinDayElement.ValueKind switch
                {
                    JsonValueKind.String => lastCheckinDayElement.GetString(),
                    JsonValueKind.Number => lastCheckinDayElement.GetInt32().ToString(),
                    _ => "Unknown"
                };
                Logger.Log($"Last check-in date: {lastCheckinDay}", LogType.Info);

                if (checkinData.TryGetProperty("newCheckin", out JsonElement newCheckinElement) && 
                    newCheckinElement.ValueKind == JsonValueKind.True)
                {
                    if (checkinData.TryGetProperty("configs", out JsonElement configsElement) && 
                        configsElement.ValueKind == JsonValueKind.Array)
                    {
                        var configs = configsElement.EnumerateArray();
                    
                        if (int.TryParse(lastCheckinDay, out var lastCheckinDayInt))
                        {
                            var reward = configs.FirstOrDefault(c => 
                                c.TryGetProperty("day", out JsonElement dayElement) && 
                                dayElement.GetInt32() == lastCheckinDayInt);
 
                            if (reward.ValueKind != JsonValueKind.Undefined)
                            {
                                Logger.Log($"Daily reward {lastCheckinDay}:", LogType.Success);

                                if (reward.TryGetProperty("tokenReward", out JsonElement tokenRewardElement))
                                    Logger.Log($"- {tokenRewardElement.GetDecimal()} WTON", LogType.Success);

                                if (reward.TryGetProperty("ticketReward", out JsonElement ticketRewardElement))
                                    Logger.Log($"- {ticketRewardElement.GetInt32()} ticket", LogType.Success);
                            }
                            else
                            {
                                Logger.Log($"Reward for day {lastCheckinDay} not found.", LogType.Error);
                            }
                        }
                        else
                        {
                            Logger.Log($"Invalid format for lastCheckinDay: {lastCheckinDay}", LogType.Error);
                        }
                    }
                }
                else
                {
                    Logger.Log("You've already checked in today.", LogType.Info);
                }
            }
            else
            {
                Logger.Log("Last check-in day information not found in response", LogType.Error);
            }
        }
        else
        {
            Logger.Log("Failed to perform check-in.", LogType.Error);
        }
    }

    static async Task PerformTasks(string token)
    {
        var taskListResponse = await MakeRequest("get", "https://wonton.food/api/v1/task/list", token);
        bool hasPendingTasks = false;

        if (taskListResponse.HasValue)
        {
            var tasks = taskListResponse.Value.GetProperty("tasks").EnumerateArray();
            Logger.Log($"Found {tasks.Count()} tasks.", LogType.Info);

            foreach (var task in tasks)
            {
                string taskId = task.GetProperty("id").GetString() ?? string.Empty;
                int status = task.GetProperty("status").GetInt32();

                if (status == 0)
                {
                    hasPendingTasks = true;
                    string taskName = task.GetProperty("name").GetString() ?? "Unknown Task";
                    Logger.Log($"Task: {taskName} (ID: {taskId}) is in progress. Verifying...", LogType.Info);

                    await VerifyAndClaimTask(token, taskId, taskName);
                    await Task.Delay(random.Next(1000, 3000));
                }
            }

            if (!hasPendingTasks)
            {
                Logger.Log("All tasks are already completed, and no new tasks are available for now.", LogType.Success);
            }

            if (taskListResponse.Value.TryGetProperty("taskProgress", out var taskProgress) && taskProgress.GetInt32() >= 3)
            {
                await GetTaskProgress(token);
            }
        }
        else
        {
            Logger.Log("Failed to fetch task list.", LogType.Error);
        } 
    }

    static async Task VerifyAndClaimTask(string token, string taskId, string taskName)
    {
        var payload = new Dictionary<string, string> { { "taskId", taskId } };
        var verifyResponse = await MakeRequest("post", "https://wonton.food/api/v1/task/verify", token, payload);

        if (verifyResponse.HasValue)
        {
            Logger.Log($"Verified {taskName}. Claiming...", LogType.Success);
            await Task.Delay(2000);
            var claimResponse = await MakeRequest("post", "https://wonton.food/api/v1/task/claim", token, payload);

            if (claimResponse.HasValue) Logger.Log($"Claimed {taskName}.", LogType.Success);
            else Logger.Log($"Failed to claim {taskName}.", LogType.Error);
        }
        else
            Logger.Log($"Verification failed for {taskName}.", LogType.Error);
    }

    static async Task GetTaskProgress(string token)
    {
        var response = await MakeRequest("get", "https://wonton.food/api/v1/task/claim-progress", token);
        if (response.HasValue)
        {
            var items = response.Value.GetProperty("items").EnumerateArray();
            Logger.Log("Claimed WONTON!", LogType.Success);

            foreach (var item in items)
            {
                Logger.Log($"Name: {item.GetProperty("name").GetString()} | Farming Power: {item.GetProperty("farmingPower").GetInt32()} | Token Value: {item.GetProperty("tokenValue").GetDecimal()} WTON", LogType.Info);
            }
        }
        else
            Logger.Log("Failed to get task progress.", LogType.Error);
    }

    static async Task<JsonElement?> MakeRequest(string method, string url, string token, Dictionary<string, string>? payload = null)
    {
        try
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
            HttpResponseMessage response = method.ToLower() == "get"
                ? await client.GetAsync(url)
                : await client.PostAsync(url, new FormUrlEncodedContent(payload ?? new Dictionary<string, string>()));

            if (response.IsSuccessStatusCode)
                return JsonSerializer.Deserialize<JsonElement?>(await response.Content.ReadAsStringAsync());

            Logger.Log($"Request failed: {response.StatusCode}", LogType.Error);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error: {ex.Message}", LogType.Error);
        }
        return null;
    }

    static async Task PlayGames(string token)
    {
        Logger.Log("Starting game play...", LogType.Info);

        while (true)
        {
            var startResponse = await MakeRequest("post", "https://wonton.food/api/v1/user/start-game", token, new Dictionary<string, string>());

            if (startResponse == null)
            {
                Logger.Log("No available game chances at the moment. Please try again in 1 hour.", LogType.Info);
                break;
            }

            if (startResponse.Value.TryGetProperty("bonusRound", out var bonusElement))
            {
                bool hasBonus = bonusElement.GetBoolean();
                await Task.Delay(TimeSpan.FromSeconds(random.Next(15, 21)));

                int points = random.Next(400, 601);
                var finishData = new Dictionary<string, object>
                {
                    { "points", points },
                    { "hasBonus", hasBonus }
                };

                var finishResponse = await client.PostAsync(
                    "https://wonton.food/api/v1/user/finish-game",
                    new StringContent(JsonSerializer.Serialize(finishData), Encoding.UTF8, "application/json")
                );

                if (finishResponse.IsSuccessStatusCode)
                {
                    Logger.Log($"Game completed with {points} points.", LogType.Success);
                    if (hasBonus)
                    {
                        Logger.Log("Bonus round completed!", LogType.Success);
                    }
                }
                else if (finishResponse.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    Logger.Log("No available game chances at the moment. Please try again in 1 hour.", LogType.Info);
                    break;
                }
                else
                {
                    Logger.Log($"Failed to finish the game. Status: {finishResponse.StatusCode}", LogType.Error);
                    break;
                }
            }
        }
    }

    static async Task CheckFarming(string token)
    {
        Logger.Log("Checking farming status...", LogType.Info);
        var response = await MakeRequest("get", "https://wonton.food/api/v1/user/farming-status", token);

        if (response.HasValue)
        {
            var responseElement = response.Value;

            if (responseElement.TryGetProperty("finishAt", out var finishAtElement))
            {
                string? finishAtString = null;

                if (finishAtElement.ValueKind == JsonValueKind.String)
                {
                    finishAtString = finishAtElement.GetString();
                }
                else if (finishAtElement.ValueKind == JsonValueKind.Number)
                {
                    finishAtString = finishAtElement.GetDecimal().ToString();
                }

                if (!string.IsNullOrEmpty(finishAtString))
                {
                    if (DateTime.TryParse(finishAtString, out var finishAt))
                    {
                        if (DateTime.UtcNow >= finishAt)
                        {
                            await ClaimFarming(token);
                            await StartFarming(token);
                        }
                        else
                        {
                            Logger.Log($"Farming in progress. Finishing at: {finishAt}", LogType.Info);
                        }
                    }
                    else
                    {
                        Logger.Log($"Failed to parse 'finishAt' value: {finishAtString}.", LogType.Error);
                    }
                }
                else
                {
                    Logger.Log("'finishAt' is null or empty.", LogType.Error);
                }
            }
            else
            {
                Logger.Log("'finishAt' property not found in the response.", LogType.Error);
            }
        }
        else
        {
            Logger.Log("Failed to check farming status.", LogType.Error);
        }
    }
    static async Task StartFarming(string token)
    {
        var response = await MakeRequest("post", "https://wonton.food/api/v1/user/start-farming", token);
        if (response.HasValue)
        {
            Logger.Log("Farming started", LogType.Success);
        }
    }

    static async Task ClaimFarming(string token)
    {
        var response = await MakeRequest("post", "https://wonton.food/api/v1/user/farming-claim", token);
        if (response.HasValue)
        {
            Logger.Log("Farming rewards claimed", LogType.Success);
        }
    }
}

public enum LogType
{
    Info,
    Success,
    Error,
    Watermark
}

public static class Logger
{
    public static void Log(string message, LogType type = LogType.Info)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var coloredMessage = type switch
        {
            LogType.Info => $"\u001b[36m[INFO]\u001b[0m {message}",
            LogType.Success => $"\u001b[32m[SUCCESS]\u001b[0m {message}",
            LogType.Error => $"\u001b[31m[ERROR]\u001b[0m {message}",
            LogType.Watermark => $"\u001b[35m[WATERMARK]\u001b[0m {message}",
            _ => message
        };
        Console.WriteLine($"[{timestamp}] {coloredMessage}");
    }
}
