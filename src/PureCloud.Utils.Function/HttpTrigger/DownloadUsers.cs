using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PureCloud.Utils.Domain.Attribute;
using PureCloud.Utils.Infra.Service.Client;
using PureCloud.Utils.Infra.Service.Storage;
using PureCloudPlatform.Client.V2.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HttpTrigger
{
    public static class DownloadUsers
    {
        [FunctionName("DownloadUsers")]
        [ExceptionFilter(Name = "DownloadUsers")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/users")] HttpRequest req,
            ILogger log)
        {
            PureCloudClient purecloudClient = new PureCloudClient();
            await purecloudClient.GetAccessToken();

            List<User> users = await purecloudClient.GetAvailableUsers();

            if (!users.Count.Equals(0))
            {
                log.LogInformation($"Total users to sabe: {users.Count}");

                foreach (var user in users)
                {
                    var tableUser = await TableStorageService.GetUserAsync(user.Id);
                    if (tableUser == null)
                    {
                        await TableStorageService.AddToUserAsync(
                            new PureCloud.Utils.Domain.Models.User() {
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
    }
}
