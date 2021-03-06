using Microsoft.ApplicationInsights;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using PureCloud.Utils.Domain.Models;
using PureCloud.Utils.Infra.Service.Client;
using PureCloudPlatform.Client.V2.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PureCloud.Utils.Function.ServiceBusTrigger
{
    public static class ConversationQueue
    {
        [FunctionName("ConversationQueue")]
        public static async Task RunAsync(
            [ServiceBusTrigger("pagequeue", Connection = "ServiceBusConnectionString")]string pageJson,
            [EventHub("conversationhub", Connection = "EventhubConnectionString")]IAsyncCollector<string> conversationhub,
            [ServiceBus("pagequeue", Connection = "ServiceBusConnectionString", EntityType = EntityType.Queue)]IAsyncCollector<string> pageQueue,
            [Blob("conversation", Connection = "StorageConnectionString")] CloudBlobContainer container,
            ILogger log)
        {
            try
            {
                //await container.CreateIfNotExistsAsync(); // create before execution, to avoid overhead proccess

                // TODO: read from "pageQueue"
                DatePage datePage = new DatePage(pageJson);

                // TODO: get conversations from PureCloudClient
                PureCloudClient pureCloudClient = new PureCloudClient();
                await pureCloudClient.GetAccessToken();
                List<AnalyticsConversation> conversations = await pureCloudClient.GetConversationsByInterval(
                    datePage.Date, datePage.Date, datePage.Page);

                log.LogInformation($"Processing date: {datePage.Date}, at page: {datePage.Page}, with {conversations.Count} conversations");

                // TODO: add on "conversationQueue"
                //List<Task> taskList = new List<Task>(); // to mutch performatic kkkk :P
                foreach (var item in conversations)
                {
                    string conversationJson = JsonConvert.SerializeObject(item);
                    await conversationhub.AddAsync(item.ConversationId);

                    // add conversationJson to blob storage
                    CloudBlockBlob convesrationBlob = container.GetBlockBlobReference($"{item.ConversationId}-conversation.json");
                    await convesrationBlob.UploadTextAsync(conversationJson);
                }
                //await Task.WhenAll(taskList.ToArray()); // to mutch performatic kkkk :P

                // TODO: add new page on same date
                if (conversations.Count.Equals(100))
                { 
                    await pageQueue.AddAsync(new DatePage()
                    {
                        Date = datePage.Date,
                        Page = datePage.Page + 1
                    }.ToJson());
                }
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
                        await pageQueue.AddAsync(pageJson);

                        log.LogInformation($"Readded pageJson: {pageJson} to pageQueue");

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