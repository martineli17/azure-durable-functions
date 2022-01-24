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
- Os dados salvos não são excluídos automaticamente, sendo necessário um processo 'manual' para isso
- Há a opção de você retornar endpoints para que o status de execução da Durable Function seja consultado
- É permitido definir status para o processamento e retornar um output no final
- Em caso da aplicação 'cair', a própria Durable Function executa novamente dentro de um intervalo de aproximadamente 4 minutos
- Há uma política de retry própria e simples para evitar exceptions sem tratamento

## Tipos de Functions
> As functions são separadas em 4 diferentes tipos e cada uma com sua responsabilidade:
- Client: são functions que são executadas a partir de uma ação externa, como: requisição HTTP, queues trigger, topic trigger, timers, etc. São essas functions que irão chamar o Orchestrator e iniciar as Durable Functions.
- Orchestrator: são functions que tem como responsabilidade controlar o fluxo de execução das Durable Functions. Esse tipo não executa nenhuma ação 'externa'.
- Activity: são functions que irão executar o processamento de fato, executar a ação delega pelo Orchestrator.
- Entity: são functions que tem como objetivo armazenar e gerenciar o estado de uma entidade que poderá ser manipulado ao longo do processamento da Durable Function. 
