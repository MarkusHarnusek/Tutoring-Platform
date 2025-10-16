using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using SQLitePCL;

namespace server
{
    /// <summary>
    /// Handles network related tasks
    /// </summary>
    public class Network
    {
        /// <summary>
        /// The HTTP listener for incoming requests
        /// </summary>
        private readonly HttpListener _httpListener;
        /// <summary>
        /// The database instance for data access
        /// </summary>
        private readonly Database _database;
        /// <summary>
        /// The list of rate limits per IP address
        /// </summary>
        private Dictionary<string, (int count, DateTime reset)> _rateLimits = new Dictionary<string, (int count, DateTime reset)>();

        /// <summary>
        /// Initializes a new instance of the Network class
        /// </summary>
        /// <param name="prefixes">The prefixes for the HTTP l2istener</param>
        /// <param name="database">The database instance for data access</param>
        /// <exception cref="ArgumentException"></exception>
        public Network(string[] prefixes, Database database)
        {
            // Check if HttpListener is supported
            if (!HttpListener.IsSupported)
            {
                Util.Log("HttpListener is not supported on this platform.", LogLevel.Fatal);
                Environment.Exit(1);
            }

            // Validate prefixes
            if (prefixes == null || prefixes.Length == 0)
            {
                throw new ArgumentException("prefixes");
            }

            // Create HttpListener and add prefixes
            _httpListener = new HttpListener();
            foreach (string prefix in prefixes)
            {
                _httpListener.Prefixes.Add(prefix);
            }

            // Assign database
            _database = database;
            Util.Log("HTTPS server initialized.", LogLevel.Ok);
        }

        /// <summary>
        /// Starts the HTTP server and begins listening for incoming requests
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync(Config config)
        {
            // Start the listener
            _httpListener.Start();
            Util.Log("HTTPS server started.", LogLevel.Ok);

            // Listen for incoming requests
            while (true)
            {
                // Wait for a client request
                var context = await _httpListener.GetContextAsync();
                //_ = Task.Run(() => HandleRequest(context));
                await HandleRequest(context, config);
            }
        }

        /// <summary>
        /// Handles an incoming HTTP request
        /// </summary>
        /// <param name="context">The HTTP listener context</param>
        /// <returns></returns>
        private async Task HandleRequest(HttpListenerContext context, Config config)
        {
            // Method wide student object
            Student student;
            ContactRequest contactRequest = null!;
            LessonRequest lessonRequest = null!;

            // Rate limiting check
            string ip = context.Request.RemoteEndPoint?.Address.ToString() ?? "unknown";
            DateTime now = DateTime.UtcNow;
            int limit = 5; // requests per minute
            TimeSpan window = TimeSpan.FromMinutes(1);


            Util.Log($"Received {context.Request.HttpMethod} request for {context.Request.Url} from {context.Request.RemoteEndPoint}", LogLevel.Ok);
            string path = context.Request.Url != null ? context.Request.Url.AbsolutePath.ToLower() : string.Empty;
            string method = context.Request.HttpMethod;

            if (method == "POST")
            {
                if (_rateLimits.TryGetValue(ip, out var entry))
                {
                    if (now > entry.reset)
                    {
                        // Window expired, reset count and window
                        _rateLimits[ip] = (1, now.Add(window));
                    }
                    else
                    {
                        if (entry.count >= limit)
                        {
                            context.Response.StatusCode = 429;
                            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Too many requests. Please wait before sending another message.\"}"));
                            Util.Log($"Rate limit exceeded for {ip}", LogLevel.Warning);
                            return;
                        }
                        else
                        {
                            _rateLimits[ip] = (entry.count + 1, entry.reset);
                        }
                    }
                }
                else
                {
                    // First request from this IP
                    _rateLimits[ip] = (1, now.Add(window));
                }
            }

            // Add CORS headers to every response
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            // Handle OPTIONS request for CORS preflight
            if (method == "OPTIONS")
            {
                Util.Log($"Handled CORS preflight request for {path} from {context.Request.RemoteEndPoint}", LogLevel.Ok);
                context.Response.StatusCode = 204;
                context.Response.OutputStream.Close();
                return;
            }

            // Set common response headers
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;

            try
            {
                // Handle GET requests
                if (method == "GET")
                {
                    Util.Log($"Received GET request for {path} from {context.Request.RemoteEndPoint}", LogLevel.Ok);

                    if (path == "/api/statuses")
                    {
                        await JsonSerializer.SerializeAsync(context.Response.OutputStream, _database.statuses);
                        Util.Log($"Sent statuses data to {context.Request.RemoteEndPoint}", LogLevel.Ok);
                    }
                    else if (path == "/api/subjects")
                    {
                        await JsonSerializer.SerializeAsync(context.Response.OutputStream, _database.subjects);
                        Util.Log($"Sent subjects data to {context.Request.RemoteEndPoint}", LogLevel.Ok);
                    }
                    else if (path == "/api/start_times")
                    {
                        await JsonSerializer.SerializeAsync(context.Response.OutputStream, _database.start_times);
                        Util.Log($"Sent start times data to {context.Request.RemoteEndPoint}", LogLevel.Ok);
                    }
                    else if (path == "/api/lessons")
                    {
                        var weekParam = context.Request.QueryString["WEEK"];
                        if (int.TryParse(weekParam, out int week))
                        {
                            var lessons = _database.lessons
                                .Where(l => System.Globalization.ISOWeek.GetWeekOfYear(l.date) == week)
                                .ToList();

                            // Filter out sensitive data
                            var filteredLessons = lessons.Select(lesson => new Lesson(
                                    lesson.id,
                                    lesson.start_time,
                                    lesson.date,
                                    new Subject(-1, "filtered", "filtered", "filtered", "filtered"), // Remove sensitive subject info
                                    new Student(-1, "filtered", "filtered", "filtered", "filtered"), // Remove sensitive student data
                                    lesson.status
                                )).ToList();

                            await JsonSerializer.SerializeAsync(context.Response.OutputStream, filteredLessons);
                        }
                        else
                        {
                            await JsonSerializer.SerializeAsync(context.Response.OutputStream, _database.lessons);
                        }
                        Util.Log($"Sent lessons data to {context.Request.RemoteEndPoint}", LogLevel.Ok);
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Not found\"}"));
                        Util.Log($"Unknown endpoint {path} requested by {context.Request.RemoteEndPoint}", LogLevel.Warning);
                    }
                }
                // Handle POST requests
                else if (method == "POST")
                {
                    Util.Log($"Received POST request for {path} from {context.Request.RemoteEndPoint}", LogLevel.Ok);

                    string smtpDomain = config.smtp.domain; ;
                    string smtpUser = config.smtp.user;
                    string smtpPassword = config.smtp.password;
                    string adminEmailAddress = config.notification.admin_email;

                    if (path == "/contact")
                    {
                        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                        string body = await reader.ReadToEndAsync();
                        contactRequest = JsonSerializer.Deserialize<ContactRequest>(body) ?? new ContactRequest("unknown", "unknown", "unknown", "unknown", "unknown", "unknown", "unknown");

                        if (contactRequest != null)
                        {
                            context.Response.StatusCode = 200;

                            if (config.notification.enable_notifications)
                            {
                                var smtpClient = new SmtpClient(smtpDomain)
                                {
                                    Port = 587,
                                    Credentials = new NetworkCredential(smtpUser, smtpPassword),
                                    EnableSsl = true,
                                };

                                var userEmail = new MailMessage
                                {
                                    From = new MailAddress(smtpUser),
                                    Subject = ResolveConfigReferences(config.notification.contact_response.user_subject, new RequestData(contactRequest, null!, _database)),
                                    Body = ResolveConfigReferences(config.notification.contact_response.user_body, new RequestData(contactRequest, null!, _database)),
                                };
                                userEmail.To.Add(contactRequest.email);

                                var adminEmail = new MailMessage
                                {
                                    From = new MailAddress(smtpUser),
                                    Subject = ResolveConfigReferences(config.notification.contact_response.admin_subject, new RequestData(contactRequest, null!, _database)),
                                    Body = ResolveConfigReferences(config.notification.contact_response.admin_body, new RequestData(contactRequest, null!, _database)),
                                };
                                adminEmail.To.Add(adminEmailAddress);

                                try
                                {
                                    await smtpClient.SendMailAsync(userEmail);
                                    Util.Log("Confirmation email sent.", LogLevel.Ok);
                                }
                                catch (Exception ex)
                                {
                                    Util.Log($"Error sending email: {ex}", LogLevel.Error);
                                    context.Response.StatusCode = 500;
                                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Email sending failed: " + ex.Message + "\"}"));
                                    return;
                                }

                                if (config.notification.enable_admin_notifications)
                                {
                                    try
                                    {
                                        await smtpClient.SendMailAsync(adminEmail);
                                        Util.Log("Admin notification email sent.", LogLevel.Ok);
                                    }
                                    catch (Exception ex)
                                    {
                                        Util.Log($"Error sending email: {ex}", LogLevel.Error);
                                        return;
                                    }
                                }
                            }

                            context.Response.StatusCode = 200;
                            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"status\":\"Message received\"}"));
                            Util.Log($"Contact request from {context.Request.RemoteEndPoint}", LogLevel.Ok);
                            return;

                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Invalid request body\"}"));
                            Util.Log($"Invalid contact request from {context.Request.RemoteEndPoint}", LogLevel.Warning);
                        }
                    }
                    else if (path == "/request-lesson")
                    {
                        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                        string body = await reader.ReadToEndAsync();
                        lessonRequest = JsonSerializer.Deserialize<LessonRequest>(body) ?? new LessonRequest("unknown", "unknown", "unknown", "unknown", DateTime.Now.Date, _database.start_times[0], "unknown", "unknown");

                        if (lessonRequest != null)
                        {
                            context.Response.StatusCode = 200;

                            if (config.notification.enable_notifications)
                            {

                                var smtpClient = new SmtpClient(smtpDomain)
                                {
                                    Port = 587,
                                    Credentials = new NetworkCredential(smtpUser, smtpPassword),
                                    EnableSsl = true,
                                };

                                var userEmail = new MailMessage
                                {
                                    From = new MailAddress(smtpUser),
                                    Subject = ResolveConfigReferences(config.notification.lesson_request_response.user_subject, new RequestData(null!, lessonRequest, _database)),
                                    Body = ResolveConfigReferences(config.notification.lesson_request_response.user_body, new RequestData(null!, lessonRequest, _database)),
                                };
                                userEmail.To.Add(lessonRequest.email);


                                var adminEmail = new MailMessage
                                {
                                    From = new MailAddress(smtpUser),
                                    Subject = ResolveConfigReferences(config.notification.lesson_request_response.admin_subject, new RequestData(null!, lessonRequest, _database)),
                                    Body = ResolveConfigReferences(config.notification.lesson_request_response.admin_body, new RequestData(null!, lessonRequest, _database)),
                                };
                                adminEmail.To.Add(adminEmailAddress);

                                try
                                {
                                    await smtpClient.SendMailAsync(userEmail);
                                    Util.Log("Confirmation email sent.", LogLevel.Ok);
                                }
                                catch (Exception ex)
                                {
                                    Util.Log($"Error sending email: {ex}", LogLevel.Error);
                                    context.Response.StatusCode = 500;
                                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Email sending failed: " + ex.Message + "\"}"));
                                    return;
                                }

                                if (config.notification.enable_admin_notifications)
                                {
                                    try
                                    {
                                        await smtpClient.SendMailAsync(adminEmail);
                                        Util.Log("Admin notification email sent.", LogLevel.Ok);
                                    }
                                    catch (Exception ex)
                                    {
                                        Util.Log($"Error sending email: {ex}", LogLevel.Error);
                                        return;
                                    }
                                }
                            }


                            context.Response.StatusCode = 200;
                            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"status\":\"Message received\"}"));
                            Util.Log($"Lesson request from {context.Request.RemoteEndPoint}", LogLevel.Ok);
                            return;
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Invalid request body\"}"));
                            Util.Log($"Invalid lesson request from {context.Request.RemoteEndPoint}", LogLevel.Warning);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Not found\"}"));
                        Util.Log($"Unknown endpoint {path} requested by {context.Request.RemoteEndPoint}", LogLevel.Warning);
                    }
                }
                else
                {
                    context.Response.StatusCode = 405;
                    await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Method not allowed\"}"));
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("{\"error\":\"Internal server error\"}"));
                Util.Log($"Error handling request: {ex}", LogLevel.Error);
                return;
            }
            finally
            {
                if (method == "POST")
                {
                    await _database.LoadData(config);

                    // Log the contact request to the database
                    if (path == "/contact" && contactRequest != null)
                    {
                        try
                        {
                            if (!_database.students.Any(s => s.email_address == contactRequest.email))
                            {
                                student = new Student(-1, contactRequest.first_name, contactRequest.last_name, contactRequest.student_class, contactRequest.email);
                                await _database.InsertStudent(student)  ;
                                await _database.InsertMessage(new Message(-1, student, null, contactRequest.title, contactRequest.body));
                            }
                            else
                            {
                                student = _database.students.Find(s => s.email_address == contactRequest.email) ?? null!;
                                await _database.InsertMessage(new Message(-1, student!, null, contactRequest.title, contactRequest.body));
                            }
                        }
                        catch (Exception ex)
                        {
                            Util.Log($"Error when processing student contact data: {ex}", LogLevel.Error);
                        }
                    }
                    else
                    {
                        // Log the lesson request to the database
                        if (path == "/lesson-request" && lessonRequest != null)
                        {
                            try
                            {
                                if (!_database.students.Any(s => s.email_address == lessonRequest.email))
                                {
                                    student = new Student(-1, lessonRequest.first_name, lessonRequest.last_name, lessonRequest.student_class, lessonRequest.email);
                                    await _database.InsertStudent(student);
                                    await _database.InsertLesson(new Lesson(-1, lessonRequest.start_time, lessonRequest.date, _database.subjects.Find(sub => sub.name == lessonRequest.subject) ?? _database.subjects[0], student, _database.statuses[0]));
                                }
                                else
                                {
                                    student = _database.students.Find(s => s.email_address == lessonRequest.email) ?? null!;
                                    await _database.InsertLesson(new Lesson(-1, lessonRequest.start_time, lessonRequest.date, _database.subjects.Find(sub => sub.name == lessonRequest.subject) ?? _database.subjects[0], student!, _database.statuses[0]));
                                }
                            }
                            catch (Exception ex)
                            {
                                Util.Log($"Error when processing student lesson request data: {ex}", LogLevel.Error);
                            }
                        }
                    }
                }

                try
                {
                    context.Response.OutputStream.Close();
                }
                catch (Exception ex)
                {
                    Util.Log($"Error closing response stream: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// Gets the public IPv6 address and falls back to returning the public IPv4 address
        /// </summary>
        /// <returns>An IPv6 or IPv4 address</returns>
        public static async Task<string> GetIpAsync()
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3);

            // Try to obtain the address
            try
            {
                return await httpClient.GetStringAsync("https://api.ipify.org");
            }
            catch (Exception e)
            {
                Util.Log($"An error occured while obtaining IP address: {e.Message}", LogLevel.Fatal);
                Environment.Exit(1);
            }

            return string.Empty;
        }

        public static async Task<bool> IsBehindNat()
        {
            // Get local ip address
            string localIp = string.Empty;

            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    localIp = ip.ToString();
                    break;
                }
            }

            // Get public ip address
            string publicIp = await GetIpAsync();

            bool isPrivate = localIp.StartsWith("10.") || localIp.StartsWith("192.168.") || (localIp.StartsWith("172.") && int.Parse(localIp.Split('.')[1]) is >= 16 and <= 31);
            return isPrivate && localIp != publicIp;
        }

        /// <summary>
        /// Resolves configuration references in the input string
        /// </summary>
        /// <param name="input">The string to resolve</param>
        /// <returns></returns>
        private static string ResolveConfigReferences(string input, RequestData requestData)
        {
            string resolved = "";

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '$')
                {
                    i++;

                    switch (input[i])
                    {
                        case '0':
                            resolved += requestData.first_name;
                            break;

                        case '1':
                            resolved += requestData.last_name;
                            break;

                        case '2':
                            resolved += requestData.email;
                            break;

                        case '3':
                            resolved += requestData.student_class;
                            break;

                        case '4':
                            resolved += requestData.body;
                            break;

                        case '5':
                            resolved += requestData.date.ToString("dd.MM.yyyy");
                            break;

                        case '6':
                            resolved += requestData.start_time.time;
                            break;

                        case '7':
                            resolved += requestData.subject;
                            break;

                        default:
                            Util.Log($"Unknown configuration reference ${input[i]}.", LogLevel.Warning);
                            break;
                    }
                }
                else
                {
                    resolved += input[i];
                }
            }

            return resolved;
        }
    }
}