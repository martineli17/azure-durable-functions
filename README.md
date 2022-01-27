# Azure Durable Functions

## O que é
> Durable Functions é mais uma maneira de utilizar o contexto de serveless com functions dentro do Azure. As Durable Functions tem como objetivo processar mais de uma function e
o processamente pode levar diferentes tempos, como: segundos, minutos, horas, etc. Ou seja, para utilizar este recurso, você não precisa de um tempo específico.

## Quando usar
> As Durable Functions são recomendadas quando existe um fluxo de processamento que contém, ou pode conter, várias etapas e que esse fluxo precise ser gerenciado automaticamente.

## Características
> As Durables Functions contém características específicas que as distinguem de functions simples. São algumas:
- Utiliza um Storage Account para gerenciar os seus dados
- Armazena cada etapa processada, suas informações e seu respectivo retorno (caso tenha algum). Isso evita que ocorra reprocessamento de algo que já foi executado, assim a Durable Function consegue identificar de onde ela parou e o que falta processar.
- Armezena os inputs recebidos e os que foram passados para cada etapa
- Armazena informações sobre configurações do host
- Gerencia um fila de Works a serem executados
- Gerencia o estado de entidades
- Não são executadas diretamente por eventos externos
- Há a opção de você retornar endpoints para que o status de execução da Durable Function seja consultado
- É permitido definir status para o processamento e retornar um output no final
- Em caso da aplicação 'cair', a própria Durable Function executa novamente dentro de um intervalo de aproximadamente 4 minutos
- Há uma política de retry própria e simples para evitar exceptions sem tratamento

## Tipos de Functions
> As functions são separadas em 4 diferentes tipos e cada uma com sua responsabilidade:
- Client
- Orchestrator
- Activity
- Entity

### Client
> São functions que são executadas a partir de uma ação externa, como: requisição HTTP, queues trigger, topic trigger, timers, etc. São essas functions que irão chamar o Orchestrator e iniciar as Durable Functions.
```c#
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
```

### Orchestrator
> São functions que tem como responsabilidade controlar o fluxo de execução das Durable Functions. Esse tipo não executa nenhuma ação 'externa'.

Recuperar o parâmetro informado ao iniciar a orquestração
```c#
var valorSalarioBruto = context.GetInput<decimal>();
```

Informar um tempo de espera antes de executar a próxima etapa
```c#
await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(30), default);
```


Definir o status da orquestração
```c#
context.SetCustomStatus("ULTIMA ETAPA CONCLUIDA: CALCULAR SALARIO LIQUIDO");
```

Definir o output da orquestração
```c#
context.SetOutput($"VALOR SALARIO LIQUIDO: {valorSalarioLiquido} | VALOR TOTAL DESCONTOS: {totalDescontos}");
```

Criar política de resiliência
```c#
var retryOptions = new RetryOptions(firstRetryInterval: TimeSpan.FromSeconds(5), maxNumberOfAttempts: 3);
```

### Activity
> São functions que irão executar o processamento de fato, executar a ação delega pelo Orchestrator.
```c#
[FunctionName("Activity_CalcularINSS")]
public decimal CalcularINSS([ActivityTrigger] decimal salario, ILogger logger)
{
  logger.LogInformation("CALCULANDO VALOR INSS");
  return salario * 0.15M;
}
```

Chamar uma Activity Function
```c#
var valorINSS = await context.CallActivityWithRetryAsync<decimal>("Activity_CalcularINSS", retryOptions, valorSalarioBruto);
```

### Entity
> São functions que tem como objetivo armazenar e gerenciar o estado de uma entidade que poderá ser manipulado ao longo do processamento da Durable Function.

Criar uma entidade
```c#
var entity = new EntityId("Entity_Descontos", context.InstanceId);
```

Criar uma Entity Function
```c#
[FunctionName("Entity_Descontos")]
public void Descontos([EntityTrigger] IDurableEntityContext context)
{
 context.SetState(context.GetState<decimal>() + context.GetInput<decimal>());
 context.Return(context.GetState<decimal>());
}
```

Chamar a Entity Function
```c#
totalDescontos = await context.CallEntityAsync<decimal>(entity, "add", valorINSS);
```

## Observações
> ### Task Hub
Para utilizar Durables Functions, é necessário informar o Task Hub. Esse componente funciona como um container lógico que será utilizado dentro do seu Storage Account para armazenar as informações de maneira mais separada e melhor identificáveis. É necessário informar um Task Hub para cada aplicativo de função que você hospedar.

Um Task Hub não consegue se comunicar com Durable Functions de outro Task Hub. Mas você consegue acessar um Task Hub especificando o seu nome através do Attribute *DurableClient*. Exemplo:
```c#
[DurableClient(TaskHub = "MyTaskHubName")]
```

Para inserir este componente, basta você informá-lo no seu arquivo 'host.json'. Segue abaixo um exemplo:
```json
{
 "extensions": {
    "durableTask": {
      "hubName": "MyTaskHubName"
    }
  }
}
```

E no seu Storage Account, irá ser gerado uma separação parecida a esta:

![image](https://user-images.githubusercontent.com/50757499/151272894-c4eb6a7f-bf14-470c-857f-292dfe5bced8.png)



> ### Dados salvos no Storage Account
Os dados salvos não são excluídos automaticamente, sendo necessário um processo 'manual' para isso.

Para excluir as instâncias salvas, é necessário chamar o método *PurgeInstanceHistoryAsync* , que está disponível na interface *IDurableOrchestrationClient*

![image](https://user-images.githubusercontent.com/50757499/151274173-f80a93d1-f4d1-4e60-834c-80ca144d067c.png)

Sendo assim, algumas opções são:
- Você pode executar um TimerTrigger para limpar periodicamente os dados já processados.
- Ao finalizar o processamento da Durable Function, inserir os ID's das instâncias em um Queue e criar uma QueueTrigger para executar o processo de exclusão.
- Fazer com que o Orchestrator chame uma Activity responsável pela exclusão dos dados (não recomendável, pois se torna um processo síncrono e compromete sua Durable Function).

Exemplo:
```c#
[FunctionName("ClearHistory_Queue")]
public async Task ClearHistoryQueueAsync([QueueTrigger("remove-instance")] string instanceId,
       [DurableClient(TaskHub = "MyTaskHubName")] IDurableOrchestrationClient durableClient,
       ILogger log)
{
  await durableClient.PurgeInstanceHistoryAsync(instanceId);
  log.LogInformation($"Instancia removida: {instanceId}");
}
```
