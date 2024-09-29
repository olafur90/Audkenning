////----------------------------------------------------------------------------////
////
////                        Reiknistofa Bankanna - Auðkenning
////
////----------------------------------------------------------------------------////


using Audkenning.Models;
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using Newtonsoft.Json;
using System.Diagnostics;
using Audkenning.Dtos;
using Newtonsoft.Json.Linq;

namespace Audkenning.Controllers
{
    /// <summary>
    /// Home Controller
    /// </summary>
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _basePath = "https://tctgt.audkenni.is:443";
        private readonly string _clientId = "rbApiTest";
        private readonly string _outgoingMessage = "Hæhæ, hver ert þú?";
        private readonly string _appTitle = "RB Auðkenning";
        private readonly string _relatedParty = "Test";
        private readonly string _useVchoice = "false";
        private readonly string _useConfirmMessage = "false";
        private readonly string _hashValue = "n/kRNhXaZ2jFKv8KlQX7ydgedXUmVy8b2O4xNq2ZxHteG7wOvCa0Kg3rY1JLOrOBXYQm+z2FRVwIv47w8gUb5g==";
        private readonly string _authenticationChoice = "2"; // 0 = sim, 1 = card, 2 = app

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// REST endpoint to Authenticate a user
        /// </summary>
        /// <param name="socialSecurityNumber">The social security number of the person that is being authenticated.</param>
        /// <returns>An Ok status if successful, and Unauthorized if not.</returns>
        [HttpPost]
        public async Task<IActionResult> AuthenticateUser([FromQuery] string userIdentifier)
        {
            try
            {
                var apiResponse = await GetCallbacksAsync();

                if (apiResponse == null) return NotFound();
            
                return await ReturnCallbacksAsync(apiResponse, userIdentifier);
            }
            catch
            {
                return BadRequest();
            }
        }

        /// <summary>
        /// Starts the authenticating process by calling Auðkenni to get the callbacks needed
        /// </summary>
        /// <returns>A GetCallbacksDto object</returns>
        private async Task<GetCallbacksDto?> GetCallbacksAsync()
        {
            var client = new RestClient(_basePath);
            var request = new RestRequest("/sso/json/realms/root/realms/audkenni/authenticate?authIndexType=service&authIndexValue=api_v202");

            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept-API-Version", "resource=2.0,protocol=1.0");
            request.AddParameter("application/json", "{}", ParameterType.RequestBody);

            var response = await client.PostAsync(request);

            if (response.Content == null) return null;

            string jsonResponse = response.Content;
            return JsonConvert.DeserializeObject<GetCallbacksDto>(jsonResponse);
        }

        /// <summary>
        /// Answers the callback from the previous call and fills in the required information such as our clientId
        /// </summary>
        /// <param name="apiResponse">The response we got from our previous call</param>
        /// <param name="socialSecurityNumber">The social security number of the person being authenticated</param>
        /// <returns>A result of Ok if successful, and unauthorized if not.</returns>
        private async Task<IActionResult> ReturnCallbacksAsync(GetCallbacksDto apiResponse, string userIdentifier)
        {
            var client = new RestClient(_basePath);
            var request = new RestRequest("/sso/json/realms/root/realms/audkenni/authenticate?authIndexType=service&authIndexValue=api_v202");

            try
            {
                // TODO: Add env variables and get phone number/social security number from user
                apiResponse.Callbacks[0].Input[0].Value = _clientId;
                apiResponse.Callbacks[1].Input[0].Value = _relatedParty;
                apiResponse.Callbacks[2].Input[0].Value = _appTitle;
                apiResponse.Callbacks[3].Input[0].Value = userIdentifier;
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

                var response = await client.PostAsync(request);

                if (response.Content == null)
                {
                    return NoContent();
                }

                string jsonResponse = response.Content;

                // TODO: Rename variable and DTO object
                GetCallbackDto2 apiResponse2 = JsonConvert.DeserializeObject<GetCallbackDto2>(jsonResponse);

                string updatedJson = JsonConvert.SerializeObject(apiResponse2);

                return await RunPollCallAsync(updatedJson, userIdentifier);
            }
            catch
            {
                return BadRequest();
            }
        }

        // Step 3 - Polling. Er þetta ekki nóg? Þarf að fara eitthvað lengra þegar notandi hefur staðfest hér?
        /// <summary>
        /// Polls the user on their app/sim and waits for them to successfully confirm on their device
        /// </summary>
        /// <param name="updatedJson">The response from our previous call to answering the callbacks</param>
        /// <returns>A result of Ok if successful, and unauthorized if not.</returns>
        private async Task<IActionResult> RunPollCallAsync(string updatedJson, string userIdentifier)
        {
            var client = new RestClient(_basePath);
            var request = new RestRequest("/sso/json/realms/root/realms/audkenni/authenticate");

            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept-API-Version", "resource=2.0,protocol=1.0");
            request.AddHeader("Cookie", "audssossolb=03; audsso=UgT8UelNnFKc-Wm0GvQzDpwu0Ag.*AAJTSQACMDIAAlNLABwxQ1M5QVVlTFFxaXVCZWFTMkxXajhHV2JMWTg9AAR0eXBlAANDVFMAAlMxAAIwMw..*");
            request.AddParameter("application/json", updatedJson, ParameterType.RequestBody);

            int attempts = 0;
            int seconds = 40;
            int refreshRate = 2000;
            int maxAttempts = (int) seconds / (refreshRate / 1000);

            while (attempts < maxAttempts)
            {
                try
                {
                    var response = await client.PostAsync(request);
                    var content = response.Content;

                    if (content == null) return NoContent();

                    // Parse the JSON response
                    var jsonResponse = JObject.Parse(content);

                    // Check for 'successUrl' and 'tokenId'
                    if (jsonResponse["successUrl"] != null && jsonResponse["tokenId"] != null)
                    {
                        var successString = JsonConvert.SerializeObject(jsonResponse);
                        _logger.Log(LogLevel.Information, $"User {userIdentifier} authenticated successfully.");
                        // await GetAuthenticationCode(tokenId);
                        return Ok();
                    }

                    // Periodically check if user has confirmed on their end.
                    await Task.Delay(refreshRate);
                    attempts++;
                }
                catch
                {
                    _logger.Log(LogLevel.Warning, $"User {userIdentifier} cancelled.");
                    return Unauthorized();
                }
            }

            _logger.LogError("Poll timed out while waiting for user.");
            return BadRequest();
        }

        // Step 4
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenId"></param>
        /// <returns></returns>
        private async Task<IActionResult> GetAuthenticationCode(string tokenId)
        {
            Console.WriteLine(tokenId);
            
            var client = new RestClient(_basePath);
            var request = new RestRequest("/sso/oauth2/realms/root/realms/audkenni/authorize?service=api_v202&client_id=rbApiTest&response_type=code&scope=openid profile signature&code_challenge=5WnuXW4ALVNtX9G6MydkrPs-F2suz0TQkoaKBsk8Hzk&code_challenge_method=S256&state=abc123&redirect_uri=http://localhost:3000/callback");
            request.AddHeader("Cookie", $"audsso={tokenId}; audssossolb=01");
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("application/x-www-form-urlencoded", ParameterType.RequestBody);
            var response = await client.GetAsync(request);

            if (response.Content != null && response.Content.Equals("1"))
            {
                Console.WriteLine("Here!");
                await GetAccessAndIdToken();
            }

            if (response.Content == null) { return NoContent(); }

            return Ok();
        }

        // Step 5
        private async Task<IActionResult> GetAccessAndIdToken()
        {
            return Ok();
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
