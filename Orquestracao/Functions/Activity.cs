using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Orquestracao
{
    public class Activity
    {
        [FunctionName("Activity_Starter")]
        public async Task<HttpResponseMessage> Starter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage reqMessage,
            [DurableClient] IDurableOrchestrationClient starter,
            HttpRequest req,
            ILogger log)
        {
            var salario = Convert.ToDecimal(req.Query["salario"].ToString());
            string instanceId = await starter.StartNewAsync("Activity_Orchestrator", null, salario);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(reqMessage, instanceId);
        }

        [FunctionName("Activity_Orchestrator")]
        public async Task Orchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, 
            [Queue("remove-instance")] ICollector<string> outputQueueItem)
        {
            var retryOptions = new RetryOptions(firstRetryInterval: TimeSpan.FromSeconds(5), maxNumberOfAttempts: 3);
            var entity = new EntityId("Entity_Descontos", context.InstanceId);

            var totalDescontos = 0M;
            var valorSalarioBruto = context.GetInput<decimal>();

            // INSS
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(0), default);
            var valorINSS = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularINSS", retryOptions, valorSalarioBruto);
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR INSS");
            totalDescontos = await context.CallEntityAsync<decimal>(entity, "add", valorINSS);

            // SALARIO BASE
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(0), default);
            var valorSalarioBase = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularSalarioBase", retryOptions, (valorINSS, valorSalarioBruto));
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR SALARIO BASE");

            // IMPOSTO DE RENDA
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(0), default);
            var valorImpostoRenda = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularImpostoRenda", retryOptions, valorSalarioBase);
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR IMPOSTO DE RENDA");
            totalDescontos = await context.CallEntityAsync<decimal>(entity, "add", valorImpostoRenda);

            // SALARIO LIQUIDO
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(0), default);
            var valorSalarioLiquido = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularSalarioLiquido", retryOptions, (valorINSS, valorImpostoRenda, valorSalarioBruto));
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR SALARIO LIQUIDO");

            await context.CallEntityAsync<decimal>(entity, "completed", valorINSS);
            context.SetOutput($"VALOR SALARIO LIQUIDO: {valorSalarioLiquido} | VALOR TOTAL DESCONTOS: {totalDescontos}");

            // EXCLUIR DADOS DO HISTORICO DE INSTANCIAS POR QUEUE
            outputQueueItem.Add(context.InstanceId);
            outputQueueItem.Add($"@entity_descontos@{context.InstanceId}");

            // EXCLUIR DADOS DO HISTORICO DE INSTANCIAS DA ENTIDADE POR ACTIVITY
            //await context.CallActivityWithRetryAsync("ClearHistory_Entidade", retryOptions, (context.InstanceId, "entity_descontos"));
        }

        [FunctionName("Activity_CalcularINSS")]
        public decimal CalcularINSS([ActivityTrigger] decimal salario, ILogger logger)
        {
            logger.LogInformation("CALCULANDO VALOR INSS");
            return salario * 0.15M;
        }

        [FunctionName("Activity_CalcularSalarioBase")]
        public decimal CalcularSalarioBase([ActivityTrigger] (decimal INSS, decimal SalarioBruto) dados, ILogger logger)
        {
            logger.LogInformation("CALCULANDO SALARIO BASE");
            return dados.SalarioBruto - dados.INSS;
        }

        [FunctionName("Activity_CalcularImpostoRenda")]
        public decimal CalcularImpostoRenda([ActivityTrigger] decimal salario, ILogger logger)
        {
            logger.LogInformation("CALCULANDO IMPOSTO DE RENDA");
            return salario * 0.15M;
        }

        [FunctionName("Activity_CalcularSalarioLiquido")]
        public decimal CalcularSalarioLiquido([ActivityTrigger] (decimal INSS, decimal IR, decimal Salario) dados, [DurableClient] IDurableEntityClient client, ILogger logger)
        {
            logger.LogInformation("CALCULANDO SALARIO LIQUIDO");
            return dados.Salario - (dados.INSS + dados.IR);
        }

        [FunctionName("Entity_Descontos")]
        public void Descontos([EntityTrigger] IDurableEntityContext context)
        {
            context.SetState(context.GetState<decimal>() + context.GetInput<decimal>());
            context.Return(context.GetState<decimal>());
        }
    }
}