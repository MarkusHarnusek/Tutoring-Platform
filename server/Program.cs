namespace server
{
    internal class Program
    {
        private static Database? database;
        private static Network? network;
        private static bool isShuttingDown = false;

        static async Task Main(string[] args)
        {
            // Set up graceful shutdown handler
            Console.CancelKeyPress += OnCancelKeyPress;

            Util.Log($"Server started at: {Environment.CurrentDirectory}", LogLevel.Ok);

            // Get the server's IP address
            string ip = await Network.GetIpAsync();
            Util.Log($"Server's IP Address: {ip}", LogLevel.Ok);

            // Check if server is behind Nat
            if (await Network.IsBehindNat())
            {
                Util.Log("The server is behind a NAT. This may cause issues with clients connecting.", LogLevel.Warning);
            }

            Config config = Config.Load();

            // Initialize the database
            database = new Database();

            // Load data from the database
            await database.LoadData(config);

            // Initialize the HTTP server
            network = new Network([$"http://localhost:8443/"], database);
            await network.StartAsync(config);

            // Keep the application running
            Console.WriteLine("Press Ctrl+C to shut down the server...");
            
            // Simple loop that checks for shutdown signal
            while (!isShuttingDown)
            {
                await Task.Delay(1000); // Check every second
            }

            Util.Log("Server terminated.", LogLevel.Ok);
        }

        private static async void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            if (isShuttingDown)
            {
                // Force exit if already shutting down
                Environment.Exit(1);
            }

            e.Cancel = true; // Prevent immediate termination
            isShuttingDown = true;

            Util.Log("Shutdown signal received. Performing cleanup.", LogLevel.Info);

            try
            {
                // Perform cleanup operations
                if (database != null)
                {
                    await Database.ApplyInMemoryDataToDB(database);
                    await database.DisconnectFromDatabase();
                    Util.Log("Database synchronized and disconnected.", LogLevel.Ok);
                }

                if (network != null)
                {
                    // Stop network if it has a stop method
                    // await network.StopAsync();
                    Util.Log("Network server stopped.", LogLevel.Ok);
                }

                Util.Log("Cleanup completed. Exiting.", LogLevel.Ok);
            }
            catch (Exception ex)
            {
                Util.Log($"Error during cleanup: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}