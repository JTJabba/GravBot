using Microsoft.Playwright;
using System.Text.Json;

public class Program
{
    public static GameState CurrentGameState = new([],[]);

    public static async Task Main(string[] args)
    {
        // Initialize Playwright
        using var playwright = await Playwright.CreateAsync();

        // Launch browser with headless = false
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
        {
            Headless = false,
            IgnoreDefaultArgs = new[] { "--enable-automation" },
            Args = new[] 
            {
                "--use-fake-ui-for-media-stream",
                "--use-fake-device-for-media-stream",
                "--autoplay-policy=no-user-gesture-required",
                "--no-sandbox",
                "--enable-usermedia-screen-capturing",
                "--allow-http-screen-capture",
                "--enable-experimental-web-platform-features",
                "--auto-select-desktop-capture-source=Entire screen",
                "--disable-web-security",
                "--allow-running-insecure-content",
                "--disable-site-isolation-trials"
            }
        });

        // Create a new context and page
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            HasTouch = true,
            ViewportSize = null,  // Use default window size instead of fixed viewport
            JavaScriptEnabled = true,
            BypassCSP = false,
            IgnoreHTTPSErrors = false
        });
        var page = await context.NewPageAsync();

        // Add HTTP request monitoring
        page.Request += async (_, request) =>
{
    if (request.Url.Contains("logger.platform.beter.live/beter.client.prod"))
    {
        var method = request.Method;
        var url = request.Url;
        var postData = request.PostData;  // This contains the request body for POST requests
        
        // SafeLog($"HTTP {method} {url}");
        if (!string.IsNullOrEmpty(postData))
        {
            var preview = postData.Length > 50 ? postData.Substring(0, 50) + "..." : postData;
            // SafeLog($"Request Body preview: {preview}");
            
            try 
            {
                // Try to parse the array of log entries
                using var doc = JsonDocument.Parse(postData);
                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    if (entry.TryGetProperty("message", out var message))
                    {
                        var messageStr = message.GetString();
                        if (messageStr != null)
                        {
                            WatchTable(messageStr);
                            WatchHandUpdate(messageStr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SafeLog($"Error parsing request body: {ex.Message}");
            }
        }
    }
};

        // Subscribe to WebSocket events before navigating
        // page.WebSocket += async (_, webSocket) =>
        // {
        //     SafeLog($"WebSocket connection attempt: {webSocket.Url}");
            
        //     webSocket.FrameSent += (_, data) =>
        //     {
        //         // SafeLog($">> Sent: {data.Text}");
        //     };
            
        //     webSocket.FrameReceived += (_, data) =>
        //     {
        //         var preview = data.Text.Length > 50 ? data.Text.Substring(0, 50) + "..." : data.Text;
        //         SafeLog($"WebSocket message preview: {preview}");
        //         WatchTable(data.Text);
        //     };

        //     webSocket.Close += (_, ws) =>
        //     {
        //         SafeLog($"WebSocket closed: {webSocket.Url}");
        //     };

        //     webSocket.SocketError += (_, error) =>
        //     {
        //         SafeLog($"WebSocket error: {webSocket.Url}, Error: {error}");
        //     };
        // };

        // Navigate to a website that uses the WebSocket
        await page.GotoAsync("https://www.google.com");  // Replace with the website that connects to the WebSocket

        // Keep the browser open
        await Task.Delay(-1);

        // Clean up
        await browser.CloseAsync();
    }


    static void WatchTable(string message)
    {
        return;
        try 
        {
            if (!message.Contains("[GameService.WatchTable]"))
            {
                return;
            }

            SafeLog($"Found WatchTable message: {message.Substring(0, 50)}");
            
            // Find JSON content between possible RPC markers
            var jsonStart = message.IndexOf('{');
            var jsonEnd = message.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = message.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonContent);
                
                // Try to navigate to cardsList in the expected structure
                if (doc.RootElement.TryGetProperty("tableState", out var tableState) &&
                    tableState.TryGetProperty("gameRound", out var gameRound) &&
                    gameRound.TryGetProperty("gameState", out var gameState) &&
                    gameState.TryGetProperty("cardGame", out var cardGame) &&
                    cardGame.TryGetProperty("handsList", out var handsList))
                {
                    foreach (var hand in handsList.EnumerateArray())
                    {
                        if (hand.TryGetProperty("handId", out var handId) && 
                            handId.TryGetProperty("seat", out var seat) &&
                            hand.TryGetProperty("cardsList", out var cards))
                        {
                            var role = seat.GetInt32() == 0 ? "Dealer" : "Player";
                            SafeLog($"{role} cards: " + string.Join(", ", cards.EnumerateArray()));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SafeLog($"Error processing WatchTable message: {ex.Message}");
        }
    }

    static void WatchHandUpdate(string message)
    {
        try 
        {
            if (!message.Contains("[engine][hands-store]"))
            {
                return;
            }

            // Split message into parts
            var parts = message.Split("--");
            if (parts.Length < 3) 
            {
                SafeLog("Not enough parts in message");
                return;
            }

            var messageType = parts[1].Trim().ToLower();
            var jsonContent = parts[2].Trim();

            List<string> dealerCards = new();
            List<string> playerCards = new();

            // Handle dealer hand updates
            if (messageType.Contains("dealer hand"))
            {
                using var doc = JsonDocument.Parse(jsonContent);
                dealerCards = doc.RootElement.EnumerateArray()
                                .Select(c => c.GetString())
                                .Where(c => c != null)
                                .ToList()!;
                // SafeLog($"Dealer cards: {string.Join(", ", dealerCards)}");
                var newState = new GameState(dealerCards, CurrentGameState.PlayerHand.Select(c => c.Value.ToString().ToLower()));
                DoAction(newState);
                return;
            }

            // Handle player hand updates
            if (messageType.Contains("my hands"))
            {
                using var doc = JsonDocument.Parse(jsonContent);
                foreach (var handElement in doc.RootElement.EnumerateArray())
                {
                    if (handElement.TryGetProperty("cards", out var cards))
                    {
                        playerCards = cards.EnumerateArray()
                            .Select(c => c.GetString())
                            .Where(c => c != null)
                            .ToList()!;
                        var newState = new GameState(CurrentGameState.DealerHand.Select(c => c.Value.ToString().ToLower()), playerCards);
                        DoAction(newState);
                    }
                }
                return;
            }
        }
        catch (Exception ex)
        {
            SafeLog($"Error processing HandUpdate message: {ex.Message}");
        }
    }

    static void DoAction(GameState newState)
    {
        if (CurrentGameState.Equals(newState))
        {
            SafeLog("GameState unchanged, skipping action");
            return;
        }

        CurrentGameState = newState;
        SafeLog($"New game state: {newState}");
        // TODO: Implement additional action logic here
    }

    // Add this helper method at the top level
    static void SafeLog(string message)
    {
        // Remove session keys and other sensitive parameters
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            message.ToLower(),
            @"sessionkey=[^&\s]+",
            "sessionkey=REDACTED"
        );
        Console.WriteLine(sanitized);
    }
}
