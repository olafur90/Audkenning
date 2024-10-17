////----------------------------------------------------------------------------////
////
////                        Reiknistofa Bankanna - Auðkenning
////                               HomeController.cs
////
////----------------------------------------------------------------------------////

using Audkenning.Models;
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using Newtonsoft.Json;
using System.Diagnostics;
using Audkenning.Dtos;
using Newtonsoft.Json.Linq;
using Audkenning.Utils;
using System.Web;
using Azure.Core;
using Azure;
using Microsoft.EntityFrameworkCore;

namespace Audkenning.Controllers
{
    /// <summary>
    /// Home Controller
    /// </summary>
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        //private readonly AudkenniDbContext _context;

        private readonly string _basePath;
        private readonly string _clientId;
        private readonly string _outgoingMessage;
        private readonly string _appTitle;
        private readonly string _relatedParty;
        private readonly string _useVchoice;
        private readonly string _useConfirmMessage;
        private readonly string _generatedRandomString;
        private readonly string _hashValue;
        private readonly string _authenticationChoice;
        private readonly AudkenniDbContext _context;
        private readonly DbHelper _dbHelper;
        private string nameToReturn;

        // Fake data for testing - TODO: Remove after testing is done
        private static List<string> _recentAuthentications = new List<string>() { "1505902649", "0802932839", "0312232530", "3110192790" };

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="_context"></param>
        public HomeController(ILogger<HomeController> logger, AudkenniDbContext context, DbHelper dbHelper)
        {
            this._logger = logger;
            this._context = context;
            this._dbHelper = dbHelper;

            _generatedRandomString = HashUtil.GenerateRandomString(15);

            _basePath = Environment.GetEnvironmentVariable("BASE_PATH")!;
            _clientId = Environment.GetEnvironmentVariable("CLIENT_ID")!;
            _outgoingMessage = Environment.GetEnvironmentVariable("OUTGOING_MESSAGE")!;
            _appTitle = Environment.GetEnvironmentVariable("APP_TITLE")!;
            _relatedParty = Environment.GetEnvironmentVariable("RELATED_PARTY")!;
            _useVchoice = Environment.GetEnvironmentVariable("USE_VCHOICE")!;
            _useConfirmMessage = Environment.GetEnvironmentVariable("USE_CONFIRM_MESSAGE")!;
            _hashValue = HashUtil.GenerateSHA512Hash(_generatedRandomString);
            _authenticationChoice = Environment.GetEnvironmentVariable("AUTHENTICATION_CHOICE")!;
        }

        /// <summary>
        /// REST endpoint to Authenticate a user
        /// </summary>
        /// <param name="userId">The SSN/Phone number of the person that is being authenticated.</param>
        /// <returns>An Ok status if successful, and Unauthorized if not.</returns>
        [HttpPost]
        public async Task<IActionResult> AuthenticateUser([FromQuery] string userId)
        {
            // TODO: Sanitize the input and check if it is a valid SSN/Phone number
            // TODO: Continued: Allow dash and space (xxxxxx-xxxx, xxx-xxxx, xxx - xxxx, xxxxxx - xxxx)? Maybe do that on frontend?
            try
            {
                var apiResponse = await GetAuthIdAndCallbacksAsync();

                if (apiResponse == null) return NotFound();
            
                return await ReturnCallbacksAsync(apiResponse, userId);
            }
            catch
            {
                return BadRequest();
            }
        }

        [HttpGet]
        public async Task<List<Authentication>> GetRecentAuthentications()
        {
            return await _dbHelper.GetRecentAuthenticationsAsync();
        }

        private RestClient restClient()
        {
            var options = new RestClientOptions(_basePath)
            {
                FollowRedirects = false
            };
            var client = new RestClient(options);
            return client;
        }

        /// <summary>
        /// Starts the authenticating process by calling Auðkenni to get the authId and 
        /// </summary>
        /// <returns>A GetCallbacksDto object</returns>
        private async Task<CallbacksDto?> GetAuthIdAndCallbacksAsync()
        {
            var client = restClient();
            var request = new RestRequest("/sso/json/realms/root/realms/audkenni/authenticate?authIndexType=service&authIndexValue=api_v202");

            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept-API-Version", "resource=2.0,protocol=1.0");
            request.AddParameter("application/json", "{}", ParameterType.RequestBody);

            var response = await client.PostAsync(request);

            if (response.Content == null) return null;

            string jsonResponse = response.Content;

            var deserializedObject = JsonConvert.DeserializeObject<CallbacksDto>(jsonResponse);
            
            if (deserializedObject == null)
            {
                throw new Exception("Failed to deserialize the response.");
            }

            return deserializedObject;
        }

        /// <summary>
        /// Answers the callback from the previous call and fills in the required information such as our clientId
        /// </summary>
        /// <param name="apiResponse">The response we got from our previous call</param>
        /// <param name="userId">The social security number of the person being authenticated</param>
        /// <returns>A result of Ok if successful, and unauthorized if not.</returns>
        private async Task<IActionResult> ReturnCallbacksAsync(CallbacksDto apiResponse, string userId)
        {
            var options = new RestClientOptions(_basePath)
            {
                FollowRedirects = false
            };
            var client = new RestClient(options);
            var request = new RestRequest("/sso/json/realms/root/realms/audkenni/authenticate?authIndexType=service&authIndexValue=api_v202");

            try
            {
                apiResponse.Callbacks[0].Input[0].Value = _clientId;
                apiResponse.Callbacks[1].Input[0].Value = _relatedParty;
                apiResponse.Callbacks[2].Input[0].Value = _appTitle;
                apiResponse.Callbacks[3].Input[0].Value = userId;
                apiResponse.Callbacks[4].Input[0].Value = _outgoingMessage;
                apiResponse.Callbacks[5].Input[0].Value = _useVchoice;
                apiResponse.Callbacks[6].Input[0].Value = _useConfirmMessage;
                apiResponse.Callbacks[7].Input[0].Value = _hashValue;
                apiResponse.Callbacks[8].Input[0].Value = _authenticationChoice;

                string jsonSerialized = JsonConvert.SerializeObject(apiResponse);

                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Accept-API-Version", "resource=2.0,protocol=1.0");
                request.AddHeader("Cookie", "audssossolb=03");
                request.AddParameter("application/json", jsonSerialized, ParameterType.RequestBody);

                // This call should throw an error if it's a bad SSN/Phone
                var response = await client.PostAsync(request);

                if (response.Content == null)
                {
                    return NoContent();
                }

                string jsonResponse = response.Content;

                GetCallbackDto2 apiResponse2 = JsonConvert.DeserializeObject<GetCallbackDto2>(jsonResponse);

                string updatedJson = JsonConvert.SerializeObject(apiResponse2);

                return await RunPollCallAsync(updatedJson, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error when calling Auðkenni with user id {userId}\n", ex.ToString());
                return BadRequest();
            }
        }

        // Step 3 - Polling (waiting for user response)
        /// <summary>
        /// Polls the user on their app/sim and waits for them to successfully confirm on their device.
        /// </summary>
        /// <param name="updatedJson">The response from our previous call to answering the callbacks.</param>
        /// <param name="userId">The SSN/Phone of the user that is being identified.</param>
        /// <returns>A result of Ok if successful, and unauthorized if not.</returns>
        private async Task<IActionResult> RunPollCallAsync(string updatedJson, string userId)
        {
            var options = new RestClientOptions(_basePath)
            {
                FollowRedirects = false
            };
            var client = new RestClient(options);
            var request = new RestRequest("/sso/json/realms/root/realms/audkenni/authenticate");

            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept-API-Version", "resource=2.0,protocol=1.0");
            request.AddHeader("Cookie", "audssossolb=03; audsso=UgT8UelNnFKc-Wm0GvQzDpwu0Ag.*AAJTSQACMDIAAlNLABwxQ1M5QVVlTFFxaXVCZWFTMkxXajhHV2JMWTg9AAR0eXBlAANDVFMAAlMxAAIwMw..*");
            request.AddParameter("application/json", updatedJson, ParameterType.RequestBody);

            int attempt = 0; // Number of attempts so far
            int seconds = 40;
            int refreshRate = 2000; // 2 sec
            int maxAttempts = (int) seconds / (refreshRate / 1000);

            // FIXME: This is not working right atm
            int x = HashUtil.CalculateVerificationCode(_hashValue);
            _logger.LogCritical($"Verification code: {x}"); // Log critical

            while (attempt < maxAttempts)
            {
                try
                {
                    _logger.LogInformation($"Attempt nr: {attempt}");
                    var response = await client.PostAsync(request);
                    var content = response.Content;

                    if (content == null) return NoContent();

                    var jsonResponse = JObject.Parse(content);

                    // Check for 'successUrl' and 'tokenId' - These should be present if success
                    if (jsonResponse["successUrl"] != null && jsonResponse["tokenId"] != null)
                    {
                        var successString = JsonConvert.SerializeObject(jsonResponse);
                        string tokenId = jsonResponse["tokenId"]!.ToString();
                        
                        _logger.LogWarning($"User {userId} authenticated successfully.");
                        
                        return await GetAuthenticationCode(tokenId);
                    }

                    // Periodically check if user has confirmed on their end.
                    await Task.Delay(refreshRate);
                    attempt++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"User {userId} cancelled.\n {ex}");

                    // Add new record of a failed authentication to the database
                    await AddAuthRecordToDatabase(userId, false);
                    return Unauthorized();
                }
            }

            _logger.LogError("Poll timed out while waiting for user.");
            await _dbHelper.AddAuthenticationAsync(userId, false);
            return BadRequest();
        }

        private async Task AddAuthRecordToDatabase(string userId, bool authenticated)
        {
            await this._dbHelper.AddAuthenticationAsync(userId, authenticated);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenId"></param>
        /// <returns></returns>
        private async Task<IActionResult> GetAuthenticationCode(string tokenId)
        {
            var options = new RestClientOptions(_basePath)
            {
                FollowRedirects = false
            };
            var client = new RestClient(options);
            var request = new RestRequest(
                "/sso/oauth2/realms/root/realms/audkenni/authorize?service=api_v202&client_id=rbApiTest&response_type=code&scope=openid profile signature&code_challenge=5WnuXW4ALVNtX9G6MydkrPs-F2suz0TQkoaKBsk8Hzk&code_challenge_method=S256&state=abc123&redirect_uri=http://localhost:3000/callback"
                , Method.Get
            );
            request.AddHeader("Cookie", $"audsso={tokenId}; audssossolb=03");

            try
            {
                RestResponse response = await client.ExecuteAsync(request);

                var locationHeader = response.Headers?.FirstOrDefault(h => h.Name == "Location");
                if (locationHeader != null)
                {
                    _logger.LogInformation($"Location: {locationHeader.Value}");
                    string url = locationHeader.Value;

                    Uri uri = new Uri(url);
                    var queryParams = HttpUtility.ParseQueryString(uri.Query);
                    string code = queryParams["code"]!;
                    _logger.LogInformation(code);
                    return await GetAccessAndIdToken(code, tokenId);
                }
                else
                {
                    _logger.LogInformation("Location header not found.");
                }

                if (response.Content != null)
                {
                    Console.WriteLine("Here!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex}");
            }

            return Ok();
        }

        private async Task<IActionResult> GetAccessAndIdToken(string location, string tokenId)
        {
            _logger.LogWarning("Núna í skrefi 5");
            var client = new RestClient(_basePath);
            var request = new RestRequest("/sso/oauth2/realms/root/realms/audkenni/access_token");

            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Cookie", $"audsso={tokenId}; audssossolb=03");
            request.AddParameter("grant_type", "authorization_code");
            request.AddParameter("client_id", _clientId);
            request.AddParameter("redirect_uri", "http://localhost:3000/callback");
            request.AddParameter("code_verifier", "nO1rQDGH1QXNTTCMBb5rUFqwasA1LOEMBxJN9dtxWFDD0AFVPqMVDOoPyIrkLqPe7YGn2Q45o7ZG20L7zIJaOe8v8L51wy178ayQSk2zcNrT1ZjI2Kn3LxH2GGIbPqUK");
            request.AddParameter("code", location);
            request.AddParameter("client_secret", "eNJi1oo0wxA1");

            var response = await client.PostAsync(request);

            var jsonResponse = JObject.Parse(response.Content!);
            var bearerToken = jsonResponse["access_token"];
            var idToken = jsonResponse["id_token"];

            if (bearerToken != null && idToken != null & tokenId != null)
            {
                return await GetUserInfo(bearerToken.ToString(), idToken!.ToString(), tokenId!);
                
            }

            return BadRequest("Error in step 5");
        }

        // Step 6 - Get user info
        private async Task<IActionResult> GetUserInfo(string bearerToken, string accessToken, string tokenId)
        {
            var client = new RestClient(_basePath);
            var request = new RestRequest("/sso/oauth2/realms/root/realms/audkenni/userinfo");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Authorization", $"Bearer {bearerToken}");
            request.AddHeader("Cookie", $"audssossolb=03; audsso={tokenId}");

            var response = await client.PostAsync(request);
            var jsonResponse = JObject.Parse(response.Content!);
            var name = jsonResponse["name"]!;

            await _dbHelper.AddAuthenticationAsync(name.ToString(), true);

            _logger.LogCritical($"\nVelkomin/n {name}");
            return Ok(name.ToString());
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
