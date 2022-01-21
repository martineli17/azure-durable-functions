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
        public static async Task<HttpResponseMessage> Starter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage reqMessage,
            [DurableClient] IDurableOrchestrationClient starter,
            HttpRequest req,
            ILogger log)
        {
            var salario = Convert.ToDecimal(req.Query["salario"].ToString());
            string instanceId = await starter.StartNewAsync("Orchestrator", null, salario);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(reqMessage, instanceId);
        }

        [FunctionName("Activity_Orchestrator")]
        public async Task Orchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var retryOptions = new RetryOptions(firstRetryInterval: TimeSpan.FromSeconds(5), maxNumberOfAttempts: 3);

            var valorSalarioBruto = context.GetInput<decimal>();
            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(20), default);
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR INSS");
            var valorINSS = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularINSS", retryOptions, valorSalarioBruto);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(20), default);
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR SALARIO BASE");
            var valorSalarioBase = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularSalarioBase", retryOptions, (valorINSS, valorSalarioBruto));

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(20), default);
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR IMPOSTO DE RENDA");
            var valorImpostoRenda = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularImpostoRenda", retryOptions, valorSalarioBase);

            await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(20), default);
            context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR SALARIO LIQUIDO");
            var valorSalarioLiquido = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularSalarioLiquido", retryOptions, (valorINSS, valorImpostoRenda, valorSalarioBruto));

            context.SetOutput($"VALOR SALARIO LIQUIDO: {valorSalarioLiquido}");
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
        public decimal CalcularSalarioLiquido([ActivityTrigger] (decimal INSS, decimal IR, decimal Salario) dados, ILogger logger)
        {
            logger.LogInformation("CALCULANDO SALARIO LIQUIDO");
            return dados.Salario - (dados.INSS + dados.IR);
        }
    }
}