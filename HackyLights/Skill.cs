using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Alexa.NET.Response;
using Alexa.NET.Request;
using Alexa.NET;
using HackyLights.Interfaces;
using Microsoft.Azure.ServiceBus;
using HackyLights.Messaging;
using System.Drawing;
using System.Text;
using Alexa.NET.Request.Type;

namespace HackyLights
{
    public class Skill
    {
        private readonly IAuth _security;
        private readonly IQueueClient _queueClient;

        public Skill(IAuth security, IQueueClient queueClient)
        {
            _security = security;
            _queueClient = queueClient;
        }

        [FunctionName("HackyLights")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]
            HttpRequest httpRequest,
            ILogger log)
        {
            var json = await httpRequest.ReadAsStringAsync();
            var skillRequest = JsonConvert.DeserializeObject<SkillRequest>(json);

            // TODO authorisation!
            //var principal = await _security.ValidateTokenAsync(skillRequest.Session.User.AccessToken);
            //if (principal == null)
            //{
            //    var message = new PlainTextOutputSpeech { Text = $"Not authorised!" };
            //    response = ResponseBuilder.Tell(message);
            //}

            SkillResponse response = null;
            var request = skillRequest.Request;

            if (request is LaunchRequest launchRequest)
            {
                log.LogInformation("Session started");

                await SendCommand(LightCommand.Color, Color.Turquoise, 3);

                var welcomeMessage = new PlainTextOutputSpeech() { Text = "Welcome to Hacky Lights! What color would you like to set your lights?" };
                var welcomeRepromptMessage = new PlainTextOutputSpeech() { Text = "You can ask help if you need instructions on how to interact with the skill" };
                response = ResponseBuilder.Ask(welcomeMessage, new Reprompt() { OutputSpeech = welcomeRepromptMessage });
            }
            else if (request is IntentRequest intentRequest)
            {
                try
                {
                    var systemIntentResponse = await HandleSystemIntents(intentRequest);
                    if (systemIntentResponse.IsHandled)
                    {
                        response = systemIntentResponse.Response;
                    }
                    else
                    {
                        IOutputSpeech message;
                        switch (intentRequest.Intent.Name)
                        {
                            case "ColorIntent":
                            case "BlinkIntent":
                            case "ChaseIntent":
                                intentRequest.Intent.Slots.TryGetValue("color", out var colorSlot);
                                var color = Color.FromName(colorSlot.Value);
                                if (color == null)
                                {
                                    message = new PlainTextOutputSpeech { Text = $"Sorry I couldn't figure out the color name '{colorSlot.Value}'. Try again!" };
                                    response = ResponseBuilder.Ask(message, new Reprompt() { OutputSpeech = message });
                                }
                                else
                                {
                                    // TODO fix this hack to get Command Type from Intent name
                                    var command = (LightCommand)Enum.Parse(typeof(LightCommand), intentRequest.Intent.Name.Replace("Intent", ""));
  
                                    await SendCommand(command, color);
                                    message = new PlainTextOutputSpeech { Text = $"Set light to {colorSlot.Value}!" };
                                    response = ResponseBuilder.Tell(message);
                                }
                                break;
                            case "PatternIntent":
                                intentRequest.Intent.Slots.TryGetValue("pattern", out var patternSlot);
                                var patternCommand = (LightCommand)Enum.Parse(typeof(LightCommand), patternSlot.Value, ignoreCase: true);

                                await SendCommand(patternCommand, Color.White);
                                message = new PlainTextOutputSpeech { Text = $"Playing {patternSlot.Value} pattern!" };
                                response = ResponseBuilder.Tell(message);
                                break;
                            case "TurnOffIntent":
                                await SendCommand(LightCommand.Off, Color.Black);
                                message = new PlainTextOutputSpeech { Text = $"Turning light off" };
                                response = ResponseBuilder.Tell(message);
                                break;
                            case "TurnOnIntent":
                                await SendCommand(LightCommand.On, Color.White);
                                message = new PlainTextOutputSpeech { Text = $"Turning light on!" };
                                response = ResponseBuilder.Tell(message);
                                break;
                            default:
                                await SendCommand(LightCommand.Off, Color.Black);
                                message = new PlainTextOutputSpeech { Text = $"Unhandled intent {intentRequest.Intent.Name}" };
                                response = ResponseBuilder.Tell(message);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error occured");
                    var message = new PlainTextOutputSpeech { Text = $"Sorry, an error occurred" };
                    response = ResponseBuilder.Tell(message);
                }
            }
            else if (request is SessionEndedRequest sessionEndedRequest)
            {
                log.LogInformation("Session ended");
                response = ResponseBuilder.Empty();
            }
                
            
            return new OkObjectResult(response);
        }

        private async Task<(bool IsHandled, SkillResponse Response)> HandleSystemIntents(IntentRequest request)
        {
            SkillResponse response = null;
            IOutputSpeech message;
            switch (request.Intent.Name)
            {
                case BuiltInIntent.Cancel:
                    message = new PlainTextOutputSpeech() { Text = "Cancelling..." };
                    response = ResponseBuilder.Tell(message);
                    break;

                case BuiltInIntent.Help:
                case BuiltInIntent.Fallback:
                    message = new PlainTextOutputSpeech() { Text = "Help goes here" };
                    response = ResponseBuilder.Ask(message, new Reprompt() { OutputSpeech = message });
                    break;

                case BuiltInIntent.Stop:
                    await SendCommand(LightCommand.Off, Color.Black);
                    message = new PlainTextOutputSpeech() { Text = "Bye!" };
                    response = ResponseBuilder.Tell(message);
                    break;
            }

            return (response != null, response);
        }

        private async Task SendCommand(LightCommand command, Color color, int durationSecs = 0)
        {
            // Create a new message to send to the queue
            var message = new LightCommandMessage { Command = command, Color = color };
            if (durationSecs > 0)
                message.Duration = TimeSpan.FromSeconds(durationSecs);
            string messageBody = JsonConvert.SerializeObject(message);
            var serviceBusMessage = new Message(Encoding.UTF8.GetBytes(messageBody));
            await _queueClient.SendAsync(serviceBusMessage);
        }
    }
}
