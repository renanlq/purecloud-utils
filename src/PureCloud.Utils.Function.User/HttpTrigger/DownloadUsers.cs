using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PureCloud.Utils.Infra.Service.Client;
using PureCloud.Utils.Infra.Service.Storage;
using PureCloudPlatform.Client.V2.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HttpTrigger
{
    public static class DownloadUsers
    {
        [FunctionName("DownloadUsers")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/users")] HttpRequest req,
            ILogger log)
        {
            try
            {
                PureCloudClient purecloudClient = new PureCloudClient();
                await purecloudClient.GetAccessToken();

                List<User> users = await purecloudClient.GetAvailableUsers();

                if (!users.Count.Equals(0))
                {
                    log.LogInformation($"Total users to save: {users.Count}");

                    foreach (var user in users)
                    {
                        var tableUser = await TableStorageService.GetUserAsync(user.Id);
                        if (tableUser == null)
                        {
                            await TableStorageService.AddUserAsync(
                                new PureCloud.Utils.Domain.Models.User()
                                {
                                    Id = user.Id,
                                    Email = user.Email
                                });
                            await BlobStorageService.AddToUserAsync(
                                JsonConvert.SerializeObject(user), $"{user.Id}.json");
                        }
                    }
                }

                return (ActionResult)new OkResult();
            }
            catch (Exception ex)
            {
                log.LogInformation($"Exception: {ex.Message}");
                await BlobStorageService.AddToErrorAsync(
                    JsonConvert.SerializeObject(ex), "exception", $"{DateTime.Now}.json");

                return (ActionResult) new StatusCodeResult(500);
            }
        }
    }
}