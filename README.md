# CRM CDL Uberlândia

CRM próprio da equipe comercial da CDL Uberlândia, começando do zero.
Mesma tecnologia do site: **C# / .NET 8, ASP.NET Core MVC, Entity Framework Core 8 e MySQL 8 (Pomelo)**.
Visual com as cores do novo site (`desing-novo-site---cdl`) e a logo da CDL.

## O que já faz

| Área | Funções |
|---|---|
| **Funil (quadro)** | Vários funis; colunas por etapa com quantidade e soma de valores; arrastar e soltar cartões entre etapas e dentro da coluna; "Adicionar" rápido no topo da coluna; busca por lead, contato, telefone ou e-mail; filtro por responsável; prazo da próxima tarefa no cartão (atrasada em vermelho, hoje em laranja). |
| **Lead** | Barra de etapas clicável; valor, responsável, tags; campos personalizados (lista, múltipla escolha, data, texto, UTM…); contatos vinculados (com link direto para o WhatsApp); empresa; notas; tarefas; histórico automático (criação, mudança de etapa, responsável, tarefas); motivo de perda. |
| **Contatos e empresas** | Lista com busca e paginação; ficha com campos fixos e personalizados; leads vinculados. |
| **Tarefas** | Em aberto, atrasadas, para hoje, concluídas; concluir com resultado. |
| **Painel (gestores)** | Vendas ganhas, novos leads, conversão, valor em negociação, leads por etapa, motivos de perda e desempenho por responsável, por período. |
| **Exportar para Excel** | Botão "Exportar" no quadro baixa os leads do funil com a mesma busca e os mesmos filtros da tela (inclui campos personalizados). Em Contatos, gestores exportam a busca atual. Vendedor(a) só exporta os próprios leads. |
| **Notificações** | Sino no menu com o número de não lidas (atualiza sozinho a cada minuto). Avisa quando alguém cria ou passa um lead ou uma tarefa para você, e lembra das tarefas que vencem hoje e das que ficaram atrasadas (verificação a cada 5 minutos). |
| **Lista de leads** | Todos os leads em tabela, de todos os funis ou de um só, com filtros (etapa, situação, responsável, tag, parados), ordenação e **ações em massa**: passar para outra pessoa, mover de etapa, aplicar tag, excluir. |
| **Produtos e serviços** | Catálogo da CDL (associação, cursos, certificado digital…). No lead, os produtos escolhidos (com quantidade e desconto) somam o valor da venda automaticamente. |
| **Previsão de vendas** | Cada etapa tem uma chance de fechamento (%) e cada lead uma data prevista. O painel mostra o valor previsto por mês e o valor ponderado pela chance. |
| **Leads parados** | Lead em aberto sem movimentação há 7 dias ganha o selo "parado" no quadro e na lista; dá para filtrar só os parados. |
| **Automações por etapa** | Ao entrar numa etapa, o CRM cria a tarefa sozinho para o responsável (ex.: "Retornar sobre a proposta em 2 dias às 9h"). |
| **Formulários do site** | Cada landing page ganha um endereço próprio (`/api/captura/{código}`). O envio cria o lead no funil, com responsável fixo ou em **rodízio** entre a equipe, avisa a pessoa, preenche campos com o mesmo nome (ex.: `utm_source`) e reaproveita o contato se o telefone ou e-mail já existir. Proteção anti-spam (`_gotcha`) e limite de 20 envios/minuto por IP. |
| **Duplicados** | Ao cadastrar contato ou lead, o CRM avisa se já existe alguém com o mesmo telefone (mesmo escrito diferente) ou e-mail, e permite usar o existente. |
| **Mover de funil** | Leva o lead para outro funil (ex.: do comercial para o pós-venda) com histórico, contatos e tarefas. |
| **Metas** | Meta mensal por pessoa (valor e quantidade de vendas), com o andamento no painel. |
| **Busca global** | `Ctrl+K` em qualquer tela: leads, contatos (inclusive por telefone) e empresas. |
| **Assistente de IA** | Chat no canto da tela (Claude, da Anthropic). Responde perguntas como "quais leads meus estão parados?", resume leads e escreve mensagens de WhatsApp. Consulta o CRM só para leitura e com as permissões de quem pergunta. Precisa de chave da API (ver Configuração). |
| **Configurações (administrador)** | Funis e etapas (criar, renomear, cor, % de chance, reordenar arrastando, arquivar) e **modelo de funil pronto**; automações; produtos; formulários do site; campos personalizados; usuários e perfis; motivos de perda. |

**Perfis:** Vendedor(a) vê só os próprios leads · Gestor vê todos os leads e o painel · Administrador também configura o sistema.

## Estrutura

```
src/
  CrmCDL.Domain          entidades (Lead, Funil, Etapa, Contato, Empresa, Tarefa, Atividade, Campos…)
  CrmCDL.Application     regras de negócio (LeadService, ConfiguracaoService, CapturaService, Automacoes, NotificacaoService)
  CrmCDL.Infrastructure  banco (AppDbContext, migrações)
  CrmCDL.Web             telas (MVC + Razor), CSS e JS sem bibliotecas
banco/criar-banco.sql    cria o banco CrmCDL e o usuário próprio
tests/
  CrmCDL.Tests           testes automáticos (xUnit + SQLite em memória)
```

## Testes automáticos

```bash
dotnet test
```

Cobrem as regras principais: quem vê o quê (vendedor x gestor), criação de lead, troca de etapa (ganho, perda com motivo, reabertura),
permissões de edição, tarefas e exclusão; as notificações e os lembretes; e os filtros e o arquivo Excel da exportação.
Cada teste usa um banco SQLite novo em memória, então não precisam de MySQL.

## Banco de dados

O CRM usa **o mesmo servidor MySQL do site, mas um banco separado (`CrmCDL`) com usuário próprio**.
Esse usuário só tem permissão no banco `CrmCDL`, então o CRM não consegue ler nem alterar nada do site.

1. Abra `banco/criar-banco.sql`, troque `TROQUE_ESTA_SENHA` por uma senha forte e rode no MySQL com um usuário administrador.
2. As tabelas são criadas sozinhas na primeira vez que o sistema sobe (migrações do Entity Framework).

## Configuração

Nada de senha no código: tudo vem de variáveis de ambiente (veja `.env.exemplo`).

| Variável | Para quê |
|---|---|
| `ConnectionStrings__CrmCDL` | Conexão com o banco `CrmCDL` (usuário `crmcdl`). |
| `Admin__Email`, `Admin__Senha`, `Admin__Nome` | Cria o primeiro administrador quando o banco está vazio. Depois pode remover. |
| `Assistente__ApiKey` | Chave da API da Anthropic para o assistente de IA (opcional). Sem ela, o resto do CRM funciona normalmente. |
| `Assistente__Modelo` | Modelo do assistente (padrão `claude-opus-5-5`). |

## Rodar localmente

```bash
cd src/CrmCDL.Web
export ConnectionStrings__CrmCDL="Server=127.0.0.1;Database=CrmCDL;User=crmcdl;Password=..."
export Admin__Email="seu@email" Admin__Senha="umaSenhaForte"
dotnet run
```

Abra http://localhost:5000 e entre com o administrador. Com o banco vazio, o CRM abre em **Configurações → Funis**: use **Usar modelo pronto** ou crie o primeiro funil.

## Nova migração (quando mudar as entidades)

```bash
dotnet tool install --global dotnet-ef --version 8.0.10
cd src
ConnectionStrings__CrmCDL="Server=localhost;Database=CrmCDL;User=x;Password=x" \
  dotnet ef migrations add NomeDaMudanca -p CrmCDL.Infrastructure -s CrmCDL.Web -o Migrations
```

## Assistente de IA

O chat fica no botão **Assistente**, no canto inferior direito de todas as telas.
Ele usa o modelo Claude, da Anthropic, pela API oficial (SDK `Anthropic` para C#).

- **Como ativar:** crie uma chave em https://platform.claude.com (conta da CDL, com cartão para cobrança por uso) e coloque em `Assistente__ApiKey` no servidor. Reinicie o sistema.
- **O que ele faz:** consulta funis, leads, detalhes do lead (notas, tarefas, produtos), tarefas, resumo do funil, contatos e produtos. **Só leitura**: não cria, não altera e não apaga nada.
- **Permissões:** cada consulta usa o usuário que perguntou. Vendedor(a) só vê os próprios leads também pelo assistente.
- **Custo:** cobrado por uso pela Anthropic. Há limite de 15 perguntas por minuto por pessoa e de 8 consultas ao banco por pergunta.
- **Privacidade:** os dados consultados para responder são enviados à API da Anthropic. Avalie com a CDL antes de ativar.

## Formulário do site (exemplo)

Em **Configurações → Formulários do site**, crie o formulário e copie o endereço. Na landing page:

```html
<form action="https://SEU-CRM/api/captura/CODIGO" method="post">
  <input name="nome" required>
  <input name="email" type="email">
  <input name="telefone">
  <input name="empresa">
  <textarea name="mensagem"></textarea>
  <input name="_gotcha" style="display:none">  <!-- anti-spam -->
  <input type="hidden" name="_redirect" value="https://cdludi.org.br/obrigado">
  <button>Enviar</button>
</form>
```

Também aceita JSON (`fetch` com `Content-Type: application/json`).

