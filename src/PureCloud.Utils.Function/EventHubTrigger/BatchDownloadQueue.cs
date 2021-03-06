using Microsoft.ApplicationInsights;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using PureCloud.Utils.Infra.Service.Client;
using PureCloudPlatform.Client.V2.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PureCloud.Utils.Function.EventHubTrigger
{
    public static class BatchDownloadQueue
    {
        [FunctionName("BatchDownloadQueue")]
        public static async void Run(
            [EventHubTrigger("conversationhub", Connection = "EventhubConnectionString")]EventData[] events,
            [EventHub("conversationhub", Connection = "EventhubConnectionString")]IAsyncCollector<string> conversationhub,
            [ServiceBus("jobqueue", Connection = "ServiceBusConnectionString", EntityType = EntityType.Queue)]IAsyncCollector<string> jobQueue,
            ILogger log)
        {
            try
            {
                List<string> conversations = new List<string>();

                // TODO: get list of conversations from "eventHub"
                foreach (EventData eventData in events)
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    conversations.Add(messageBody);
                    //await Task.Yield();
                }

                log.LogInformation($"Total conversations: {conversations.Count}");

                // TODO: batch job donwload
                PureCloudClient purecloudClient = new PureCloudClient();
                await purecloudClient.GetAccessToken();
                BatchDownloadJobSubmissionResult job = await purecloudClient.BatchRecordingDownloadByConversation(conversations);

                // TODO: add in "jobQueue"
                await jobQueue.AddAsync(job.Id);
            }
            catch (Exception ex)
            {
                TelemetryClient telemetry = new TelemetryClient();
                telemetry.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                telemetry.TrackException(ex);

                log.LogInformation($"Exception during execution: {ex.Message}");

                do
                {
                    try
                    {
                        await Task.Delay(Convert.ToInt32(Environment.GetEnvironmentVariable("deplaytime")));

                        //List<Task> taskList = new List<Task>(); // to mutch performatic kkkk :P
                        foreach (EventData eventData in events)
                        {
                            string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                            await conversationhub.AddAsync(messageBody);

                            log.LogInformation($"Readded conversation: {messageBody} to conversationhub");
                        }
                        //await Task.WhenAll(taskList); // to mutch performatic kkkk :P

                        break;
                    }
                    catch (Exception exEx)
                    {
                        telemetry.InstrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                        telemetry.TrackException(exEx);

                        log.LogInformation($"Exception during execution: {exEx.Message}");
                    }
                } while (true);               
            }
        }
    }
}