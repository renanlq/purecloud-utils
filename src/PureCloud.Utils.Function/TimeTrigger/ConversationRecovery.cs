using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PureCloud.Utils.Domain.Attribute;
using PureCloud.Utils.Domain.Models;
using PureCloud.Utils.Infra.Service.Client;
using PureCloud.Utils.Infra.Service.Storage;
using PureCloudPlatform.Client.V2.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PureCloud.Utils.Function.TimeTrigger
{
    public static class ConversationRecovery
    {
        [FunctionName("ConversationRecovery")]
        [ExceptionFilter(Name = "ConversationRecovery")]
        public async static Task Run(
            [TimerTrigger("* */1 * * * *", RunOnStartup = true)]TimerInfo myTimer,
            ILogger log)
        {
            log.LogInformation($"Started 'ConversationRecovery' function");

            // TODO 1. get last processed date on "table.processeddates"
            DateTime limitDate = (new DateTime(2016, 06, 10));//DateTime.Now;
            ProcessedDate processedDate = await TableStorageService.GetLastProcessedDateTableAsync();
            processedDate = ProcessedDate.ReturnDateToProcess(processedDate);

            if (processedDate.Date < limitDate.Date)
            {
                // TODO 2. /api/v2/analytics/conversations/details/query
                PureCloudClient purecloudClient = new PureCloudClient();
                await purecloudClient.GetAccessToken();

                // TODO 3. add to "table.conversations"
                List<AnalyticsConversation> conversations = await purecloudClient.GetConversationsByInterval(
                    processedDate.Date, processedDate.Date);

                if (!conversations.Count.Equals(0))
                {
                    log.LogInformation($"Processing date: {processedDate.Date}, with {conversations.Count} conversations");

                    foreach (var item in conversations)
                    {
                        Domain.Models.Conversation conversation = new Domain.Models.Conversation() {
                            ConversationId = item.ConversationId,
                            ConversationJson = JsonConvert.SerializeObject(item),
                            Processed = false
                        };
                        await TableStorageService.AddToConversationTableAsync(conversation);
                    }
                }

                // TODO 4. add processed date to "table.processeddates"
                await TableStorageService.AddToProcessedDatesTableAsync(processedDate);
            }
            else
            {
                log.LogInformation($"Pass limit date");
            }

            log.LogInformation($"Ended 'ConversationRecovery' function");
        }
    }
}