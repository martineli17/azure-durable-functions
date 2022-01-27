using DurableTask.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orquestracao.Functions
{
    public class ClearHistory
    {
        [FunctionName("ClearHistory_Timer")]
        public async Task ClearHistoryTimerAsync([TimerTrigger("*/30 * * * * *", RunOnStartup = true)] TimerInfo myTimer,
            [DurableClient(TaskHub = "MyTaskHubName")] IDurableOrchestrationClient durableClient,
            ILogger log)
        {
            var orchestrationStatusList = new List<OrchestrationStatus> { OrchestrationStatus.Completed, OrchestrationStatus.Canceled, OrchestrationStatus.Terminated };
            var purgeHistoryResult = await durableClient.PurgeInstanceHistoryAsync(DateTime.MinValue, DateTime.MaxValue, orchestrationStatusList);
            log.LogInformation($"Total de instâncias removidas: {purgeHistoryResult.InstancesDeleted}");
        }

        [FunctionName("ClearHistory_Queue")]
        public async Task ClearHistoryQueueAsync([QueueTrigger("remove-instance")] string instanceId,
            [DurableClient(TaskHub = "MyTaskHubName")] IDurableOrchestrationClient durableClient,
            ILogger log)
        {
            await durableClient.PurgeInstanceHistoryAsync(instanceId);
            log.LogInformation($"Instancia removida: {instanceId}");
        }

        [FunctionName("ClearHistory_Entidade")]
        public async Task ClearHistoryEntidadeAsync([ActivityTrigger] (string InstanceId, string Entidade) dados,
            [DurableClient(TaskHub = "MyTaskHubName")] IDurableClient client, ILogger logger)
        {
            await client.PurgeInstanceHistoryAsync($"@{dados.Entidade}@{dados.InstanceId}");
            logger.LogInformation($"Entidade removida: @{dados.Entidade}@{dados.InstanceId}");
        }
    }
}
